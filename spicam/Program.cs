using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/*

Switches:

-stop                           terminates an already-running instance of spicam
-quiet [minutes]                disables motion detection for the specified time
-snapshot                       write a full-sized timestamped snapshot JPG to the storagepath
-video [seconds]                write full-sized timestamped MP4 video(s) to the storagepath
-stream [on|off]                MJPEG stream of the camera's view
-analysis [on|off]              MJPEG stream of the spicam motion detection algorithm
-getmask [directory]            write a 640 x 480 x 24bpp snapshot.bmp to the directory
-?                              help (this list)

*/

namespace spicam
{
    public class Program
    {
        /// <summary>
        /// Standardized format for stored video and snapshot filenames.
        /// </summary>
        public static readonly string FILENAME_DATE_FORMAT = "yyyy-MM-dd-HH-mm-ss-ffff";

        /// <summary>
        /// Defines and controls the current spicam activity.
        /// </summary>
        public static CameraStateManager RunningState;
        
        /// <summary>
        /// A new target state after command-line switches are processed.
        /// </summary>
        public static CameraStateManager RequestedState;

        /// <summary>
        /// Used to terminate the camera's RunningState when RequestedState has been changed or
        /// application shutdown is requested (in which case RequestedState should be null).
        /// </summary>
        public static CancellationTokenSource ctsRunningState;

        // Terminates the thread monitoring a named pipe for receiving new command-line switches.
        private static CancellationTokenSource ctsSwitchPipe;

        public static async Task Main(string[] args)
        {
            try
            {
                // TODO PrepareLogger();

                // If this returns true, args were sent to an already-running instance, we can exit.
                if (await CommandLineSwitchPipe.TrySendArgs(args))
                    return;

                // Read appsettings.json and do some basic checks like path validity
                ValidateConfiguration();

                // Set up a pipe to receive switches once we're running
                ctsSwitchPipe = new CancellationTokenSource();
                _ = Task.Run(() => CommandLineSwitchPipe.StartServer(ProcessSwitches, ctsSwitchPipe.Token));

                // The localpath should be clear of files at startup
                FileProcessing.MoveSnapshotsToStorage();
                FileProcessing.MoveAbandonedVideoToStorage();
                FileProcessing.ClearLocalStorage();

                // The default state, although some command-line switches may change this
                RequestedState = new MotionDetectionState();

                // If command-line args were passed to this instance, process them now
                if (args.Length > 0)
                {
                    ProcessSwitches(args, false);
                    
                    if(RequestedState == null)
                    {
                        Console.WriteLine("No new execution state requested, exiting.");
                        return;
                    }
                }

                // If a command-line switch arrives in the SwitchPipe thread, it can
                // cancel the state-change token to end the current processing state
                // and request a different processing state. We loop until no new state
                // is requested, at which point the app exits.
                while(RequestedState != null)
                {
                    RunningState = RequestedState;
                    RequestedState = null;
                    ctsRunningState = new CancellationTokenSource();
                    await RunningState.RunAsync(ctsRunningState.Token).ConfigureAwait(false);
                    RunningState.Dispose();
                    RunningState = null;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nException of type {ex.GetType().Name}\n{ex.Message}");
                if (ex.InnerException != null) Console.Write(ex.InnerException.Message);
                Console.WriteLine($"\n{ex.StackTrace}");
            }
            finally
            {
                // Stephen Cleary says CTS disposal is unnecessary as long as you cancel
                ctsSwitchPipe?.Cancel();
                ctsRunningState?.Cancel();
                RequestedState?.Dispose();
                RunningState?.Dispose();

            }

            Console.WriteLine("Exiting spicam.");
        }

        private static void ValidateConfiguration()
        {
            Console.WriteLine("Loading and validating configuration...");
            AppConfig.LoadConfiguration();

            // Did the programmer accidentally hit F5 in VS on Windows? (That NEVER happens...)
            if (Environment.OSVersion.Platform != PlatformID.Unix)
                throw new Exception("This application only runs on a camera-equipped Raspberry Pi");

            // Is the local path reachable?
            if (!Directory.Exists(AppConfig.Get.LocalPath))
                throw new Exception($"Unable to find or access localpath: {AppConfig.Get.LocalPath}");

            // Is the storage path reachable?
            if (!Directory.Exists(AppConfig.Get.StoragePath))
                throw new Exception($"Unable to find or access storagepath: {AppConfig.Get.StoragePath}");

            // Is the motion mask available?
            var mask = AppConfig.Get.Motion.MaskPathname;
            if (!string.IsNullOrEmpty(mask) && !File.Exists(mask))
                throw new Exception($"Unable to find or access motion mask: {mask}");

            // Is the snapshot count valid?
            var snaps = AppConfig.Get.Recording.SnapshoutCount;
            if (snaps < 0 || snaps > 3)
                throw new Exception("SnapshotCount must be in the range of 0 to 3.");

            // Do we have email addresses?
            var mail = AppConfig.Get.Email;
            if (!string.IsNullOrWhiteSpace(mail.Server))
            {
                if (string.IsNullOrWhiteSpace(mail.From) || string.IsNullOrWhiteSpace(mail.To))
                    throw new Exception("When an email server is specified, the from and to addresses are mandatory.");
            }
        }

        private static void ProcessSwitches(string[] args)
        {
            Console.WriteLine($"Running instance received command line switch {args[0]}");
            ProcessSwitches(args, true);
        }

        private static void ProcessSwitches(string[] args, bool argsReceivedFromPipe)
        {
            if (args.Length == 0) return;

            bool showHelp = true;
            var command = args[0].Trim().ToLower();

            switch(command)
            {

                case "-stop":
                    {
                        showHelp = false;

                        if (!argsReceivedFromPipe)
                            throw new Exception("Nothing to stop, no running instance of spicam found.");
                        
                        // Cancel the current processing state. Setting RequestedState
                        // to null will cause the application to exit.
                        RequestedState = null;
                        ctsRunningState.Cancel();
                    }
                    break;


                case "-quiet":
                    {
                        showHelp = false;

                        if (args.Length != 2 || !int.TryParse(args[1], out var minutes))
                        {
                            Console.WriteLine("The -quiet switch requires a 'minutes' parameter.");
                            return;
                        }

                        // Ensure the target of this switch is doing motion detection (or will be)
                        var target = (argsReceivedFromPipe) 
                            ? RunningState as MotionDetectionState 
                            : RequestedState as MotionDetectionState;

                        if(target == null)
                        {
                            Console.WriteLine("The -quiet switch only applies to motion detection.");
                            return;
                        }

                        Console.WriteLine($"Motion detection suppressed for {minutes} minutes.");
                        target.SetQuietTime(minutes);
                    }
                    break;

                case "-snapshot":
                    {
                        showHelp = false;
                    }
                    break;

                case "-video":
                    {
                        showHelp = false;

                        if (args.Length != 2)
                        {
                            Console.WriteLine("The -video switch requires a 'seconds' parameter.");
                            return;
                        }

                    }
                    break;

                case "-stream":
                    {
                        showHelp = false;

                        if (args.Length != 2)
                        {
                            Console.WriteLine("The -stream switch requires an 'on' or 'off' parameter.");
                            return;
                        }

                    }
                    break;

                case "-analysis":
                    {
                        showHelp = false;

                        if (args.Length != 2)
                        {
                            Console.WriteLine("The -analysis switch requires an 'on' or 'off' parameter.");
                            return;
                        }

                    }
                    break;

                case "-getmask":
                    {
                        showHelp = false;
 
                        if (args.Length != 2)
                        {
                            Console.WriteLine("The -getmask switch requires a 'directory' parameter.");
                            return;
                        }

                    }
                    break;

                case "-?":
                    break;

                default:
                    Console.WriteLine($"\nUnrecognized switch: {command}");
                    break;
            }

            if(showHelp)
            {
                Console.WriteLine("\nspicam - Simple Pi Camera");
                Console.WriteLine("\nA basic motion-detecting Raspberry Pi surveillence camera utility.");
                Console.WriteLine("See https://github.com/MV10/spicam for more information");
                Console.WriteLine("\nspicam command-line switches:\n");
                Console.WriteLine("-?                      help (this list)");
                Console.WriteLine("-stop                   terminates an already-running instance of spicam");
                Console.WriteLine("-quiet [minutes]        disables motion detection for the specified time");
                Console.WriteLine("-snapshot               write a full-sized timestamped snapshot JPG to the storagepath");
                Console.WriteLine("-video [seconds]        write full-sized timestamped MP4 video(s) to the storagepath");
                Console.WriteLine("-stream [on | off]      MJPEG stream of the camera's view");
                Console.WriteLine("-analysis [on | off]    MJPEG stream of the spicam motion detection algorithm");
                Console.WriteLine("-getmask [directory]    write a 640 x 480 x 24bpp snapshot.bmp to the directory");
                Console.WriteLine("\nSwitches may not be combined. The -quiet, -snapshot, and -video switches are only accepted");
                Console.WriteLine("when spicam is already running in normal motion detection mode. The -stream and -analysis");
                Console.WriteLine("switches are intended for short-term use. The -stream, -analysis, and -getmask switches will");
                Console.WriteLine("interrupt motion detection, if running. Set streaming to 'off' to resume motion detection.");
                Console.WriteLine();
            }
        }
    }
}
