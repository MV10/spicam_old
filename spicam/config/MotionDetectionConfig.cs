
namespace spicam
{
    /// <summary>
    /// Settings relating to motion detection.
    /// </summary>
    public class MotionDetectionConfig
    {
        /// <summary>
        /// Full pathname to a 640x480 24-bit motion detection mask. Fully-black (RGB 0,0,0) pixles
        /// will be ignored for motion detection purposes. Must be a file format supported by the
        /// .NET Bitmap class (commonly BMP, JPEG, PNG). Not required, but an exception will be thrown
        /// if this is specified but inaccessible.
        /// </summary>
        public string MaskPathname { get; set; } = string.Empty;

        /// <summary>
        /// Frequency in seconds to update the test frame. The motion detection algorithm compares the
        /// test frame to each new frame. This helps suppress false motion events due to slow changes
        /// in the scene over time, such as shadows outdoors. Zero disables this feature. Defaults to
        /// 3 seconds.
        /// </summary>
        public int TestFrameInterval { get; set; } = 3;

        /// <summary>
        /// Minimum number of seconds without motion before the test frame can be updated. This prevents
        /// updating the test frame to an image with some moving object in view. Zero disables this
        /// feature. Defaults to 3 seconds.
        /// </summary>
        public int TestFrameCooldown { get; set; } = 3;

        /// <summary>
        /// Minimum value each pixel in the test frame must differ from the new frame in order to be
        /// considered changed. The algorithm uses "summed RGB differencing" -- the R, G, and B values
        /// are added together and compared. The range is 1 to 765, where 765 would only trigger if a
        /// full-white (RGB 255,255,255 = 765) changed to full-black (RGB 0,0,0, so a difference of 765).
        /// </summary>
        public int RgbThreshold { get; set; } = 200;

        /// <summary>
        /// Percentage of the pixels within a cell that must have changed (based on the RGB threshold) for
        /// the entire cell to be considered changed. Frames are divided into a large grid of approximately
        /// 700 to 1000 rectangles (depending on resolution) for parallel processing. This helps reject small
        /// changes like image sensor noise. Default is 50 percent.
        /// </summary>
        public int CellPercentage { get; set; } = 50;

        /// <summary>
        /// Number of cells in the frame that must have changed (based on the cell percentage setting) to
        /// trigger a motion detection event for the frame. This helps reject random differences that are
        /// not true motion (for example, a person moving close to the camera should tringer a relatively
        /// large number of cells), and can also help reject uninteresting motion (such as a small pet). 
        /// Default is 20 cells.
        /// </summary>
        public int CellCount { get; set; } = 20;
    }
}
