using MMALSharp.Processors.Motion;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace spicam
{
    /// <summary>
    /// Manages the primary functionality of the spicam application.
    /// </summary>
    public class MotionDetectionState : CameraStateManager
    {
        public MotionConfig motionConfig;

        private bool recordingActive = false;
        private DateTime recordingStartTime;
        private CancellationTokenSource recordingStateChecker;

        // Once recording starts, do not stop until this point
        private DateTime minimumStopTarget;

        // When motion detection events end, keep recording until this point
        private DateTime motionEndedStopTarget;

        // Segment the recording and send to storage at this point
        private DateTime nextSegmentTarget;

        // Never record longer than this point
        private DateTime maximumStopTarget;

        // If we reached the maximumStopTarget, ignore new motion events until this point
        private DateTime maxRecordTimeCooldown = DateTime.MinValue;

        private bool quietTime = false;
        private DateTime endQuietTime;

        public MotionDetectionState()
            : base()
        {
            Console.WriteLine("Entering state: motion detection");
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            Initialize();

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
            if (!recordingActive) return;

            Console.WriteLine($"Ended recording at {DateTime.Now:o}");

            recordingActive = false;
            recordingStateChecker?.Cancel();

            // TODO Process the recorded files
        }

        public override void Dispose()
        {
            Console.WriteLine("Exiting state: motion detection");
            AbortRecording();
            base.Dispose();
        }

        protected override void Initialize()
        {
            base.Initialize();
            ConfigureMotion();
        }

        private async void OnMotionDetected()
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

                if(recordingActive)
                {
                    motionEndedStopTarget = DateTime.Now.AddSeconds(AppConfig.Get.Recording.MotionEndedThresholdSecs);
                }

                if (!recordingActive)
                {
                    Console.WriteLine($"Started recording at {DateTime.Now:o}");
                    recordingActive = true;
                    recordingStartTime = DateTime.Now;
                    minimumStopTarget = DateTime.Now.AddSeconds(AppConfig.Get.Recording.MinimumRecordTimeSecs);
                    motionEndedStopTarget = DateTime.Now.AddSeconds(AppConfig.Get.Recording.MotionEndedThresholdSecs);
                    nextSegmentTarget = DateTime.Now.AddSeconds(AppConfig.Get.Recording.SegmentRecordingSecs);
                    maximumStopTarget = DateTime.Now.AddMinutes(AppConfig.Get.Recording.MaximumRecordTimeMin);
                    PrepareStateCheck();

                    // TODO Split the buffer, take snapshots, start recording, send alerts, update log
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"\nException of type {ex.GetType().Name}\n{ex.Message}");
                if (ex.InnerException != null) Console.Write(ex.InnerException.Message);
                Console.WriteLine($"\n{ex.StackTrace}");
            }
        }

        private void PrepareStateCheck()
        {
            if (!recordingActive) return;
            recordingStateChecker?.Dispose();
            recordingStateChecker = new CancellationTokenSource();
            recordingStateChecker.Token.Register(StateCheck);
            recordingStateChecker.CancelAfter(500);
        }

        private void StateCheck()
        {
            if (!recordingActive) return;

            var now = DateTime.Now;

            if(now > minimumStopTarget)
            {
                if(now > motionEndedStopTarget || now > maximumStopTarget)
                {
                    AbortRecording();

                    if(now > maximumStopTarget)
                    {
                        maxRecordTimeCooldown = now.AddMinutes(AppConfig.Get.Recording.MaximumReachedCooldownMin);
                    }
                }
            }

            if (recordingActive) PrepareStateCheck();
        }

        private void ConfigureMotion()
        {
            Console.WriteLine("Configuring motion detection...");

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
