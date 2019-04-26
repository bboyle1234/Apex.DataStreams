using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    internal static class SocketExtensions {

        public static async Task SendAllBytes(this Socket socket, byte[] bytes) {
            var totalBytesSent = 0;
            while (totalBytesSent < bytes.Length) {
                var segment = new ArraySegment<byte>(bytes, totalBytesSent, bytes.Length - totalBytesSent);
                var numBytesSent = await socket.SendAsync(segment, SocketFlags.None).ConfigureAwait(false);
                if (numBytesSent == 0) throw new Exception("No bytes were sent.");
                totalBytesSent += numBytesSent;
            }
        }

        public static async Task<byte[]> ReadAllBytes(this Socket socket, int numBytes) {
            var bytes = new byte[numBytes];
            var totalBytesRead = 0;
            while (totalBytesRead < numBytes) {
                var segment = new ArraySegment<byte>(bytes, totalBytesRead, numBytes - totalBytesRead);
                var numBytesRead = await socket.ReceiveAsync(segment, SocketFlags.None).ConfigureAwait(false);
                if (numBytesRead == 0) throw new Exception("No bytes were read.");
                totalBytesRead += numBytesRead;
            }
            return bytes;
        }

    }
}
