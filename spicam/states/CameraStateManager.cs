using MMALSharp;
using MMALSharp.Common;
using MMALSharp.Common.Utility;
using MMALSharp.Components;
using MMALSharp.Config;
using MMALSharp.Handlers;
using MMALSharp.Native;
using MMALSharp.Ports;
using MMALSharp.Ports.Outputs;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace spicam
{
    /// <summary>
    /// Base class for state managers that interact with the camera module and MMAL library.
    /// </summary>
    public abstract class CameraStateManager : IDisposable
    {
        /// <summary>
        /// The active camera.
        /// </summary>
        public MMALCamera Cam;

        /// <summary>
        /// A circular buffer that continuously stores a video loop until a
        /// motion detection event triggers storage of a new video clip.
        /// </summary>
        public CircularBufferCaptureHandler VideoCaptureHandler;
        
        /// <summary>
        /// A single-frame buffer which stores the most recent full-size
        /// image, which can be  used to wriite a high-resolution snapshot.
        /// </summary>
        public FrameBufferCaptureHandler SnapshotCaptureHandler;
        
        /// <summary>
        /// A buffer which feeds data into the frame-buffer-based motion
        /// detection algorithm.
        /// </summary>
        public FrameBufferCaptureHandler MotionCaptureHandler;
        
        /// <summary>
        /// Directs full-sized image data to multiple processing endpoints.
        /// </summary>
        public MMALSplitterComponent Splitter;
        
        /// <summary>
        /// Resizes the full-resolution images to the 640 x 480 format used
        /// by the motion detection algorithm.
        /// </summary>
        public MMALIspComponent Resizer;
        
        /// <summary>
        /// Encodes raw frame data to an h.264 stream to feed into the
        /// circular video buffer.
        /// </summary>
        public MMALVideoEncoder VideoEncoder;
        
        /// <summary>
        /// Encodes each raw frame data to a JPEG image to feed into the
        /// snapshot frame buffer.
        /// </summary>
        public MMALImageEncoder SnapshotEncoder;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CameraStateManager()
        {
            Cam = MMALCamera.Instance;
        }

        /// <summary>
        /// Configures the camera and the pipeline. Derived classes should
        /// invoke this first from their <see cref="RunAsync"/> implementation.
        /// </summary>
        protected virtual async Task Initialize()
        {
            ConfigureCamera();
            ConfigurePipeline();

            Console.Write("Camera warmup");
            int countdown = 8;
            while(countdown-- > 0)
            {
                Console.Write(".");
                await Task.Delay(250);
            }
            Console.WriteLine();
        }

        /// <summary>
        /// The main processing loop, which derived classes must implement.
        /// </summary>
        public abstract Task RunAsync(CancellationToken cancellationToken);

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public virtual void Dispose()
        {
            Console.WriteLine("Shutting down pipeline.");
            VideoCaptureHandler?.Dispose();
            SnapshotCaptureHandler?.Dispose();
            MotionCaptureHandler?.Dispose();
            VideoEncoder?.Dispose();
            SnapshotEncoder?.Dispose();
            Resizer?.Dispose();
            Splitter?.Dispose();

            Console.WriteLine("Shutting down camera.");
            Cam?.Cleanup();
        }

        /// <summary>
        /// Camera settings (applies both config and hard-coded defaults).
        /// </summary>
        protected virtual void ConfigureCamera()
        {
            Console.WriteLine("Configuring camera.");

            MMALCameraConfig.Resolution = new Resolution(AppConfig.Get.Camera.Width, AppConfig.Get.Camera.Height);
            MMALCameraConfig.SensorMode = AppConfig.Get.Camera.Mode;
            MMALCameraConfig.Framerate = AppConfig.Get.Camera.FPS;

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

        /// <summary>
        /// Wires up the various image processing components.
        /// </summary>
        protected virtual void ConfigurePipeline()
        {
            Console.WriteLine("Preparing pipeline.");

            VideoCaptureHandler = new CircularBufferCaptureHandler(4000000, AppConfig.Get.LocalPath, "h264");
            MotionCaptureHandler = new FrameBufferCaptureHandler();
            SnapshotCaptureHandler = new FrameBufferCaptureHandler(directory: AppConfig.Get.LocalPath, extension: "jpg", fileDateTimeFormat: Program.FILENAME_DATE_FORMAT);

            Splitter = new MMALSplitterComponent();
            Resizer = new MMALIspComponent();
            VideoEncoder = new MMALVideoEncoder();
            SnapshotEncoder = new MMALImageEncoder(continuousCapture: true);

            Resizer.ConfigureOutputPort<VideoPort>(0, new MMALPortConfig(MMALEncoding.RGB24, MMALEncoding.RGB24, width: 640, height: 480), MotionCaptureHandler);
            VideoEncoder.ConfigureOutputPort(new MMALPortConfig(MMALEncoding.H264, MMALEncoding.I420, quality: 10, MMALVideoEncoder.MaxBitrateLevel4), VideoCaptureHandler);
            SnapshotEncoder.ConfigureOutputPort(new MMALPortConfig(MMALEncoding.JPEG, MMALEncoding.I420, quality: 90), SnapshotCaptureHandler);

            Cam.Camera.VideoPort.ConnectTo(Splitter);

            Splitter.Outputs[0].ConnectTo(Resizer);
            Splitter.Outputs[1].ConnectTo(VideoEncoder);
            Splitter.Outputs[2].ConnectTo(SnapshotEncoder);
        }
    }
}
