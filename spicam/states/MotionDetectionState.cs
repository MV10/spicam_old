using MMALSharp.Processors.Motion;
using spicam.utils;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace spicam
{
    /// <summary>
    /// Manages the primary functionality of the spicam application.
    /// </summary>
    public class MotionDetectionState : CameraStateManager
    {
        /// <summary>
        /// The active motion detection algorithm and frame buffering settings.
        /// </summary>
        public MotionConfig motionConfig;

        /// <summary>
        /// Indicates whether spicam is actively recording a motion detection clip.
        /// </summary>
        public bool RecordingActive { get; private set; } = false;
        
        // Used to rename video clips when sending to permanent storage
        private string recordingStartTimeFilename;
        private int recordingSegment;

        // These capture the optional 2- and 3-second snapshots
        private CancellationTokenSource snapshotTwoSec;
        private CancellationTokenSource snapshotThreeSec;

        // Tests the following stopping targets every 500ms
        private CancellationTokenSource recordingStatusChecker;

        // Once recording starts, do not stop until this point
        private DateTime minimumStopTarget;

        // When motion detection events have ended, keep recording until this point
        private DateTime motionEndedStopTarget;

        // Segment the recording and send to storage at this point
        private DateTime nextSegmentTarget;

        // Never record longer than this point
        private DateTime maximumStopTarget;

        // If we reached the maximumStopTarget, ignore new motion events until this point
        private DateTime maxRecordTimeCooldown = DateTime.MinValue;

        private bool quietTime = false;
        private DateTime endQuietTime;

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public MotionDetectionState()
            : base()
        {
            Console.WriteLine("Requesting state: motion detection");
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            await Initialize();

            Console.WriteLine("Motion detection running.");

            await Cam
                .WithMotionDetection(
                    MotionCaptureHandler, 
                    motionConfig, 
                    OnMotionDetected)
                .ProcessAsync(Cam.Camera.VideoPort, cancellationToken)
                .ConfigureAwait(false);

            AbortRecording();
        }

        /// <summary>
        /// Ignores motion for the specified duration. If motion is being detected when
        /// this is called, the event will be ended. Any unprocessed files from that event
        /// will still be stored and logged. This can be set before processing starts.
        /// </summary>
        public void SetQuietTime(int minutes)
        {
            AbortRecording();
            quietTime = true;
            endQuietTime = DateTime.Now.AddMinutes(minutes);
        }

        /// <summary>
        /// Immediately aborts any in-progress recording and processes the files.
        /// No effect if recording is not active.
        /// </summary>
        public void AbortRecording()
        {
            if (!RecordingActive) return;

            Console.WriteLine($"Ended recording at {DateTime.Now:o}");

            RecordingActive = false;
            snapshotTwoSec?.Cancel();
            snapshotThreeSec?.Cancel();
            recordingStatusChecker?.Cancel();

            VideoCaptureHandler.StopRecording();
            ProcessVideoClip();
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public override void Dispose()
        {
            AbortRecording();
            base.Dispose();
            Console.WriteLine("Exiting state: motion detection");
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        protected override async Task Initialize()
        {
            await base.Initialize();
            ConfigureMotionDetection();
        }

        /// <summary>
        /// The event handler that is invoked when the library detects motion.
        /// </summary>
        private void OnMotionDetected()
        {
            try
            {
                if (quietTime)
                {
                    if (DateTime.Now < endQuietTime) return;
                    quietTime = false;
                }

                if (DateTime.Now < maxRecordTimeCooldown) return;
                maxRecordTimeCooldown = DateTime.MinValue;

                if(RecordingActive)
                {
                    motionEndedStopTarget = DateTime.Now.AddSeconds(AppConfig.Get.Recording.MotionEndedThresholdSecs);
                }

                if (!RecordingActive)
                {
                    Console.WriteLine($"Started recording at {DateTime.Now:o}");
                    BeginRecording();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"\nException of type {ex.GetType().Name}\n{ex.Message}");
                if (ex.InnerException != null) Console.Write(ex.InnerException.Message);
                Console.WriteLine($"\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// A new motion detection event has been triggered.
        /// </summary>
        private void BeginRecording()
        {
            // Set some flags used to manage the recording process
            RecordingActive = true;

            recordingStartTimeFilename = DateTime.Now.ToString(Program.FILENAME_DATE_FORMAT);
            recordingSegment = 1;
            
            minimumStopTarget = DateTime.Now.AddSeconds(AppConfig.Get.Recording.MinimumRecordTimeSecs);
            motionEndedStopTarget = DateTime.Now.AddSeconds(AppConfig.Get.Recording.MotionEndedThresholdSecs);
            nextSegmentTarget = DateTime.Now.AddSeconds(AppConfig.Get.Recording.SegmentRecordingSecs);
            maximumStopTarget = DateTime.Now.AddMinutes(AppConfig.Get.Recording.MaximumRecordTimeMin);

            // Set up the snapshots and logging/notification
            LogMotionEvent();
            PrepareSnapshots();

            // Start the recording and the stop-recording timing-checks
            VideoCaptureHandler.StartRecording(VideoEncoder.RequestIFrame);
            PrepareStatusCheck();
        }

        /// <summary>
        /// Writes a new event to a log file on the storage path.
        /// </summary>
        private void LogMotionEvent()
        {
            _ = Task.Run(() =>
            {
                try
                {
                    File.AppendAllText(Path.Combine(AppConfig.Get.StoragePath, "motion_events.log"), recordingStartTimeFilename);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"\nException of type {ex.GetType().Name} logging motion event\n{ex.Message}");
                    if (ex.InnerException != null) Console.Write(ex.InnerException.Message);
                    Console.WriteLine($"\n{ex.StackTrace}");
                }
            });
        }

        /// <summary>
        /// Takes optional immediate, 2- and 3-second snapshots and invokes
        /// motion event logging and notification after all snapshots are taken
        /// </summary>
        private void PrepareSnapshots()
        {
            // MailClient.ProcessNotifications also moves snapshots to storage
            switch (AppConfig.Get.Recording.SnapshoutCount)
            {
                case 0:
                    MailClient.ProcessNotifications(recordingStartTimeFilename);
                    break;

                case 1:
                    SnapshotCaptureHandler.WriteFrame();
                    MailClient.ProcessNotifications(recordingStartTimeFilename);
                    break;

                case 2:
                    SnapshotCaptureHandler.WriteFrame();
                    snapshotTwoSec = new CancellationTokenSource();
                    snapshotTwoSec.Token.Register(() =>
                    {
                        SnapshotCaptureHandler.WriteFrame();
                        MailClient.ProcessNotifications(recordingStartTimeFilename);
                    });
                    snapshotTwoSec.CancelAfter(1000);
                    break;

                case 3:
                    SnapshotCaptureHandler.WriteFrame();
                    snapshotTwoSec = new CancellationTokenSource();
                    snapshotThreeSec = new CancellationTokenSource();
                    snapshotTwoSec.Token.Register(SnapshotCaptureHandler.WriteFrame);
                    snapshotThreeSec.Token.Register(() =>
                    {
                        SnapshotCaptureHandler.WriteFrame();
                        MailClient.ProcessNotifications(recordingStartTimeFilename);
                    });
                    snapshotTwoSec.CancelAfter(1000);
                    snapshotThreeSec.CancelAfter(2000);
                    break;
            }
        }

        /// <summary>
        /// Creates a 500ms cancellation token timeout which invokes <see cref="RecordingStatusCheck"/>.
        /// </summary>
        private void PrepareStatusCheck()
        {
            if (!RecordingActive) return;
            recordingStatusChecker?.Dispose();
            recordingStatusChecker = new CancellationTokenSource();
            recordingStatusChecker.Token.Register(RecordingStatusCheck);
            recordingStatusChecker.CancelAfter(500);
        }

        /// <summary>
        /// When recording is active, every 500ms this method tests the current time against various
        /// target times to decide whether to end the recording and process the files. Also manages
        /// splitting the video for longer recordings.
        /// </summary>
        private void RecordingStatusCheck()
        {
            if (!RecordingActive) return;

            var now = DateTime.Now;

            if(now >= minimumStopTarget)
            {
                if(now >= motionEndedStopTarget || now >= maximumStopTarget)
                {
                    if (now >= maximumStopTarget)
                    {
                        Console.WriteLine("Maximum recording duration reached.");
                        maxRecordTimeCooldown = now.AddMinutes(AppConfig.Get.Recording.MaximumReachedCooldownMin);
                    }

                    AbortRecording();
                }
            }

            if (RecordingActive)
            {
                if(now >= nextSegmentTarget)
                {
                    Console.WriteLine("Segmenting the recording.");
                    ProcessVideoClip();
                    nextSegmentTarget = now.AddSeconds(AppConfig.Get.Recording.SegmentRecordingSecs);
                }

                PrepareStatusCheck();
            }
        }

        /// <summary>
        /// Splits longer recordings and processes the file so that the recording does not exceed the
        /// allocated local storage space. If recording is over, call StopRecording before calling this.
        /// </summary>
        private void ProcessVideoClip()
        {
            // This will change after the call to Split
            var localFilename = VideoCaptureHandler.CurrentFilename + ".h264";


            VideoCaptureHandler.Split();

            var storageFilename = $"{recordingStartTimeFilename}_{recordingSegment:000}.h264";
            FileProcessing.MoveVideoToStorage(localFilename, storageFilename);

            recordingSegment++;
        }

        /// <summary>
        /// Uses configuration settings to prepare the motion detection algorithm and frame
        /// buffering behaviors.
        /// </summary>
        private void ConfigureMotionDetection()
        {
            Console.WriteLine("Configuring motion detection.");

            var motionAlgorithm = new MotionAlgorithmRGBDiff(
                    rgbThreshold: AppConfig.Get.Motion.RgbThreshold,
                    cellPixelPercentage: AppConfig.Get.Motion.CellPercentage,
                    cellCountThreshold: AppConfig.Get.Motion.CellCount
                );

            motionConfig = new MotionConfig(
                    algorithm: motionAlgorithm,
                    testFrameInterval: TimeSpan.FromSeconds(AppConfig.Get.Motion.TestFrameInterval),
                    testFrameCooldown: TimeSpan.FromSeconds(AppConfig.Get.Motion.TestFrameCooldown)
                );
        }
    }
}
