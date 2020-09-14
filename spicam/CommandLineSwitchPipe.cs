using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace spicam
{
    /// <summary>
    /// Manages a named-pipe connection to send command-line switches to a running instance, or
    /// if this is the only instance, to receive command-line switches after processing starts.
    /// </summary>
    public static class CommandLineSwitchPipe
    {
        /// <summary>
        /// Attempt to send any command-line switches to an already-running instance. If another
        /// instance is found but this instance was started without switches, an exception is
        /// thrown.
        /// </summary>
        public static async Task<bool> TrySendArgs(string[] args)
        {
            Console.WriteLine("Checking for a running instance.");

            // Is another instance already running?
            using (var client = new NamedPipeClientStream("spicam.pipe"))
            {
                try
                {
                    client.Connect(100);
                }
                catch (TimeoutException)
                {
                    return false;
                }

                // Connected, throw if we have no args to pass.
                if (args.Length == 0)
                    throw new Exception("Another instance of spicam is already running");

                Console.WriteLine("Sending switches to running instance.");

                // Send argument list with * separator (which will never be used in a filename or path)
                var message = string.Empty;
                foreach (var arg in args) message += arg + "*";
                var messageBuffer = Encoding.ASCII.GetBytes(message);
                var sizeBuffer = BitConverter.GetBytes(messageBuffer.Length);
                await client.WriteAsync(sizeBuffer, 0, sizeBuffer.Length);
                await client.WriteAsync(messageBuffer, 0, messageBuffer.Length);
            }

            // Switches sent, this instance can shut down.
            return true;
        }

        /// <summary>
        /// Creates a named-pipe server that waits to receive command-line switches from
        /// another running instance. These are handed off as they're received.
        /// </summary>
        public static async Task StartServer(Action<string[]> switchHandler, CancellationToken cancellationToken)
        {
            Console.WriteLine("The running instance is listening for switch commands.");

            try
            {
                using (var server = new NamedPipeServerStream("spicam.pipe"))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // Wait for another instance to send us switches
                        await server.WaitForConnectionAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        using (var reader = new BinaryReader(server))
                        {
                            // Read the length of the message, then the message itself
                            var size = reader.ReadInt32();
                            var buffer = reader.ReadBytes(size);
                            
                            // Goodbye, client
                            server.Disconnect();

                            // Split into original arg array and send for processing
                            var message = Encoding.ASCII.GetString(buffer);
                            var args = message.Split("*", StringSplitOptions.RemoveEmptyEntries);
                            switchHandler.Invoke(args);
                        }
                    }
                }
            }
            catch(OperationCanceledException)
            { } // normal, disregard

            Console.WriteLine("The running instance has stopped listening for switch commands.");
        }
    }
}
