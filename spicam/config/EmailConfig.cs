
namespace spicam
{
    /// <summary>
    /// Settings relating to email notifications.
    /// </summary>
    public class EmailConfig
    {
        /// <summary>
        /// Optional. IP or network name of an open SMTP server for sending
        /// email notification of motion detection events.
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Optional. Port number of an open SMTP server. Default is port 25.
        /// </summary>
        public int Port { get; set; } = 25;

        /// <summary>
        /// Optional (though required by some mail servers such as GMail).
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// One or more email addresses to notify when motion is detected.
        /// </summary>
        public string To { get; set; } = string.Empty;

        /// <summary>
        /// Minimum number of seconds that must pass before another motion detection
        /// email notification can be generated in response to a new motion event. Default
        /// is 90 seconds.
        /// </summary>
        public int CooldownSeconds { get; set; } = 90;

        /// <summary>
        /// Maximum number of emails the server will allow in a 24 hour period. Zero will
        /// disable this limit. Default is 100 which corresponds to GMail's limit.
        /// </summary>
        public int Max24Hrs { get; set; } = 100;
    }
}
