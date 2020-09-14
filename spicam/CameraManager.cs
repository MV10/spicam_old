using MMALSharp;
using MMALSharp.Common;
using MMALSharp.Common.Utility;
using MMALSharp.Components;
using MMALSharp.Config;
using MMALSharp.Handlers;
using MMALSharp.Native;
using MMALSharp.Ports;
using MMALSharp.Ports.Outputs;
using MMALSharp.Processors.Motion;
using System;
using System.Drawing;

namespace spicam
{
    public class CameraManager : IDisposable
    {
        public MMALCamera Cam;

        public CircularBufferCaptureHandler VideoCaptureHandler;
        public FrameBufferCaptureHandler MotionCaptureHandler;

        private MMALSplitterComponent splitter;
        private MMALIspComponent resizer;
        private MMALVideoEncoder videoEncoder;

        private MotionAlgorithmRGBDiff motionAlgorithm;
        private MotionConfig motionConfig;

        public void Initialize()
        {
            ConfigureCamera();
            ConfigurePipeline();
            ConfigureMotion();
        }

        public void Dispose()
        {
            VideoCaptureHandler?.Dispose();
            MotionCaptureHandler?.Dispose();
            videoEncoder?.Dispose();
            resizer?.Dispose();
            splitter?.Dispose();
            Cam?.Cleanup();
        }

        private void ConfigureCamera()
        {
            Console.WriteLine("Configuring camera...");
            Cam = MMALCamera.Instance;

            MMALCameraConfig.Resolution = new Resolution(1296, 972);
            MMALCameraConfig.SensorMode = MMALSensorMode.Mode4;
            MMALCameraConfig.Framerate = new MMAL_RATIONAL_T(24, 1); // numerator & denominator

            var overlay = new AnnotateImage(AppConfig.Get.Name, 30, Color.White)
            {
                ShowDateText = true,
                ShowTimeText = true,
                DateFormat = "yyyy-MM-dd",
                TimeFormat = "HH:mm:ss",
                RefreshRate = DateTimeTextRefreshRate.Seconds
            };
            MMALCameraConfig.Annotate = overlay;

            // image quality tweaks to play with later
            MMALCameraConfig.Sharpness = 0;             // 0 = auto, default; -100 to 100
            MMALCameraConfig.Contrast = 0;              // 0 = auto, default; -100 to 100
            MMALCameraConfig.Brightness = 50;           // 50 = default; 0 = black, 100 = white
            MMALCameraConfig.Saturation = 0;            // 0 = default; -100 to 100
            MMALCameraConfig.ExposureCompensation = 0;  // 0 = none, default; -10 to 10, lightens/darkens the image

            // low-light tweaks which don't seem to degrade full-light recording
            MMALCameraConfig.ExposureMode = MMAL_PARAM_EXPOSUREMODE_T.MMAL_PARAM_EXPOSUREMODE_NIGHT;
            MMALCameraConfig.ExposureMeterMode = MMAL_PARAM_EXPOSUREMETERINGMODE_T.MMAL_PARAM_EXPOSUREMETERINGMODE_MATRIX;
            MMALCameraConfig.DrcLevel = MMAL_PARAMETER_DRC_STRENGTH_T.MMAL_PARAMETER_DRC_STRENGTH_HIGH;

            // h.264 requires key frames for the circular buffer capture handler
            MMALCameraConfig.InlineHeaders = true; 
            
            Cam.ConfigureCameraSettings();
            Cam.EnableAnnotation();
        }

        private void ConfigurePipeline()
        {
            Console.WriteLine("Preparing pipeline...");

            VideoCaptureHandler = new CircularBufferCaptureHandler(4000000, AppConfig.Get.LocalPath, "h264");
            MotionCaptureHandler = new FrameBufferCaptureHandler();

            splitter = new MMALSplitterComponent();
            resizer = new MMALIspComponent();
            videoEncoder = new MMALVideoEncoder();

            resizer.ConfigureOutputPort<VideoPort>(0, new MMALPortConfig(MMALEncoding.RGB24, MMALEncoding.RGB24, width: 640, height: 480), MotionCaptureHandler);
            videoEncoder.ConfigureOutputPort(new MMALPortConfig(MMALEncoding.H264, MMALEncoding.I420, quality: 10, MMALVideoEncoder.MaxBitrateLevel4), VideoCaptureHandler);

            Cam.Camera.VideoPort.ConnectTo(splitter);

            splitter.Outputs[0].ConnectTo(resizer);
            splitter.Outputs[1].ConnectTo(videoEncoder);
        }

        private void ConfigureMotion()
        {
            Console.WriteLine("Configuring motion detection...");

            motionAlgorithm = new MotionAlgorithmRGBDiff(
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
