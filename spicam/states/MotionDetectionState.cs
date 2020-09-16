using MMALSharp.Common.Utility;
using MMALSharp.Processors.Motion;
using System;
using System.Collections.Generic;
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

            // TODO clean up properly if we're in the middle of a motion detection event

        }

        /// <summary>
        /// Ignores motion for the specified duration. If motion is being detected when
        /// this is called, the event will be ended. Any unprocessed files from that event
        /// will still be stored and logged. This can be set before processing starts.
        /// </summary>
        public void SetQuietTime(int minutes)
        {

        }

        public override void Dispose()
        {
            base.Dispose();
            Console.WriteLine("Exiting state: motion detection");
        }

        protected override void Initialize()
        {
            base.Initialize();
            ConfigureMotion();
        }

        private async void OnMotionDetected()
        {

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
