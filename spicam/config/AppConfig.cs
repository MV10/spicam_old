
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace spicam
{
    /// <summary>
    /// The root object for all settings read from appsettings.json and appsettings.secrets.json.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Provides access to all application settings.
        /// </summary>
        public static AppConfig Get { get; private set; }

        /// <summary>
        /// Reads all application settings from the current working directory.
        /// </summary>
        public static void LoadConfiguration()
        {
            var dir = Directory.GetCurrentDirectory();
            Console.WriteLine($"Reading configuration from cwd: {dir}");

            Get = new ConfigurationBuilder()
                .SetBasePath(dir)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.secrets.json", optional: true)
                .Build()
                .Get<AppConfig>();
        }

        /// <summary>
        /// The camera's name (typically describing the location). This is shown in
        /// an overlay with a timestamp on any video or snapshots. Default is the machine name.
        /// </summary>
        public string Name { get; set; } = Environment.MachineName;

        /// <summary>
        /// The working directory for recordings and snapshots. If you have sufficient memory
        /// a ramdisk is ideal. Required, an exception will be thrown if this is left blank
        /// or the path is inaccessible.
        /// </summary>
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>
        /// The directory to store processed video, snapshots, and motion detection event logs.
        /// Typically this is a network folder. Required, an exception will be thrown if this is
        /// left blank or the path is inaccessible.
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>
        /// Settings relating to the camera.
        /// </summary>
        public CameraConfig Camera { get; set; }

        /// <summary>
        /// Settings relating to email notifications.
        /// </summary>
        public EmailConfig Email { get; set; }

        /// <summary>
        /// Settings relating to motion detection.
        /// </summary>
        public MotionDetectionConfig Motion { get; set; }

        /// <summary>
        /// Settings relating to video recordings.
        /// </summary>
        public RecordingConfig Recording { get; set; }
    }
}
