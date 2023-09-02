using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MoreVoiceLines
{
    internal class NamedPipesUtils
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetNamedPipeClientProcessId(IntPtr Pipe, out int ClientProcessId);

        public static int GetNamedPipeClientProcessId(NamedPipeServerStream pipeServer)
        {
            var hPipe = pipeServer.SafePipeHandle.DangerousGetHandle();

            if (GetNamedPipeClientProcessId(hPipe, out var clientProcessId))
            {
                return clientProcessId;
            }
            else
            {
                // TODO: throw better error?
                throw new Exception("Failed to get named pipe client process ID");
            }
        }



        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetNamedPipeServerProcessId(IntPtr Pipe, out int ServerProcessId);

        public static int GetNamedPipeServerProcessId(NamedPipeClientStream pipeClient)
        {
            var hPipe = pipeClient.SafePipeHandle.DangerousGetHandle();

            if (GetNamedPipeServerProcessId(hPipe, out var serverProcessId))
            {
                return serverProcessId;
            }
            else
            {
                // TODO: throw better error?
                throw new Exception("Failed to get named pipe server process ID");
            }
        }
    }
}
