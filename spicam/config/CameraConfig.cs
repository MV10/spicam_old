
using MMALSharp.Components;

namespace spicam
{
    /// <summary>
    /// Settings relating to the camera.
    /// </summary>
    public class CameraConfig
    {
        /// <summary>
        /// Full-sized horizontal resolution. Not validated, but must conform to one of the
        /// recognized modes for the installed camera version. Default is 1296 (v1 camera
        /// mode 4). Internally, motion detection always runs against 640 x 480.
        /// </summary>
        public int Width { get; set; } = 1296;

        /// <summary>
        /// Full-sized vertical resolution. Not validated, but must conform to one of the
        /// recognized modes for the installed camera version. Default is 972 (v1 camera
        /// mode 4). Internally, motion detection always runs against 640 x 480.
        /// </summary>
        public int Height { get; set; } = 972;

        /// <summary>
        /// Camera mode matching the configured horizontal and vertical resolutions. Ideally,
        /// select a native aspect-ratio 2x2 binning mode for higher performance. Not validated
        /// against the resolution settings but a mismatch may have adverse effects. Default is
        /// mode 4 (v1 camera 1296 x 972).
        /// </summary>
        public MMALSensorMode Mode { get; set; } = MMALSensorMode.Mode4;

        /// <summary>
        /// Full-sized frame rate. Default is 24.
        /// </summary>
        public int FPS { get; set; } = 24;
    }
}
