
namespace spicam
{
    /// <summary>
    /// Settings relating to video recording.
    /// </summary>
    public class RecordingConfig
    {
        /// <summary>
        /// Minimum number of seconds to record video when motion is detected. Default
        /// is 10 seconds.
        /// </summary>
        public int MinimumRecordTimeSecs { get; set; } = 10;

        /// <summary>
        /// Seconds to continue recording after no motion is detected; during this time
        /// any new motion is considered part of the same event. Default is 5 seconds.
        /// </summary>
        public int MotionEndedThresholdSecs { get; set; } = 5;

        /// <summary>
        /// Number of seconds after which an ongiong recording is segmented into a new h.264
        /// file; existing file is sent to the storagepath; prevents running out of space on
        /// localpath. Default is 30 seconds.
        /// </summary>
        public int SegmentRecordingSecs { get; set; } = 30;

        /// <summary>
        /// Number of minutes after which a continuous recording will always end; prevents
        /// spurious motion from triggering never-ending recordings. Default is 10 minutes.
        /// </summary>
        public int MaximumRecordTimeMin { get; set; } = 10;

        /// <summary>
        /// Number of minutes to suppress all motion detection when the maximum recording time
        /// threshold is triggered. Default is 30 minutes.
        /// </summary>
        public int MaximumReachedCooldownMin { get; set; } = 30;

        /// <summary>
        /// Number of full-resolution still-frame snapshot JPEGs to capture when motion is
        /// detected. Snapshots are taken once per second. Snapshots are attached to any
        /// email notifications. Set to 0 to disable, maximum value is 3. Default is 3.
        /// </summary>
        public int SnapshoutCount { get; set; } = 3;
    }
}
