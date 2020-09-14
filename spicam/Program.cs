using MMALSharp.Processors.Motion;
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
        private static CancellationTokenSource ctsSwitchPipe;
        private static CancellationTokenSource ctsCameraProcessing;

        public static CameraManager Camera;

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
                var switchServerTask = Task.Run(() => CommandLineSwitchPipe.StartServer(ProcessSwitches, ctsSwitchPipe.Token));

                // The localpath should be clear of files at startup
                // TODO HandleAbandonedFiles();

                // Prep the camera
                Camera = new CameraManager();
                Camera.Initialize();

                // Start processing
                ctsCameraProcessing = new CancellationTokenSource();



                // If we got any command-line args to this instance, process them now
                if (args.Length > 0)
                    ProcessSwitches(args, false);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nException of type {ex.GetType().Name}\n{ex.Message}");
                if (ex.InnerException != null) Console.Write(ex.InnerException.Message);
                Console.WriteLine($"\n{ex.StackTrace}");
            }
            finally
            {
                ctsSwitchPipe?.Cancel();
                Camera?.Dispose();
            }
        }

        private static void ValidateConfiguration()
        {
            Console.WriteLine("Loading and validating configuration...");
            AppConfig.LoadConfiguration();

            // Did the programmer accidentally hit F5? (That NEVER happens...)
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

        }

        private static void ProcessSwitches(string[] args)
        {
            Console.WriteLine($"Running instance received command line switch {args[0]}");
            ProcessSwitches(args, true);
        }

        private static void ProcessSwitches(string[] args, bool receivedFromPipe)
        {
            if (args.Length == 0) return;

            bool showHelp = true;
            var command = args[0].Trim().ToLower();

            switch(command)
            {
                case "-stop":
                    {
                        if (!receivedFromPipe)
                            throw new Exception("Nothing to stop, no running instance of spicam found");

                        showHelp = false;
                    }
                    break;

                case "-quiet":
                    {
                        if (args.Length != 2)
                        {
                            Console.WriteLine("The -quiet switch requires a 'minutes' parameter");
                            return;
                        }

                        showHelp = false;
                    }
                    break;

                case "-snapshot":
                    {
                        showHelp = false;
                    }
                    break;

                case "-video":
                    {
                        if (args.Length != 2)
                        {
                            Console.WriteLine("The -video switch requires a 'seconds' parameter");
                            return;
                        }

                        showHelp = false;
                    }
                    break;

                case "-stream":
                    {
                        if (args.Length != 2)
                        {
                            Console.WriteLine("The -stream switch requires an 'on' or 'off' parameter");
                            return;
                        }

                        showHelp = false;
                    }
                    break;

                case "-analysis":
                    {
                        if (args.Length != 2)
                        {
                            Console.WriteLine("The -analysis switch requires an 'on' or 'off' parameter");
                            return;
                        }

                        showHelp = false;
                    }
                    break;

                case "-getmask":
                    {
                        if (args.Length != 2)
                        {
                            Console.WriteLine("The -getmask switch requires a 'directory' parameter");
                            return;
                        }

                        showHelp = false;
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
                Console.WriteLine("\nspicam command-line switches:\n");
                Console.WriteLine("-stop                   terminates an already - running instance of spicam");
                Console.WriteLine("-quiet[minutes]         disables motion detection for the specified time");
                Console.WriteLine("-snapshot               write a full - sized timestamped snapshot JPG to the storagepath");
                Console.WriteLine("-video[seconds]         write full - sized timestamped MP4 video(s) to the storagepath");
                Console.WriteLine("-stream [on | off]      MJPEG stream of the camera's view");
                Console.WriteLine("-analysis [on | off]    MJPEG stream of the spicam motion detection algorithm");
                Console.WriteLine("-getmask [directory]    write a 640 x 480 x 24bpp snapshot.bmp to the directory");
                Console.WriteLine("-?                      help (this list)");
                Console.WriteLine();
            }
        }
    }
}
