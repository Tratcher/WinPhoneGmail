using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;

namespace WinPhone.Mail.Protocols.Transport
{
    public class WindowsNetSockets : ITransport
    {
        public WindowsNetSockets(string host, int port, bool ssl)
        {
            Socket = new StreamSocket();
            Host = host;
            Port = port;
            Ssl = ssl;
        }

        private StreamSocket Socket { get; set; }
        private string Host { get; set; }
        private int Port { get; set; }
        private bool Ssl { get; set; }
        private Stream Stream { get; set; }

        public async Task<Stream> ConnectAsync()
        {
            await Socket.ConnectAsync(new HostName(Host), Port.ToString(CultureInfo.InvariantCulture), 
                Ssl ? SocketProtectionLevel.Ssl : SocketProtectionLevel.PlainSocket);

            Stream readStream = Socket.InputStream.AsStreamForRead(0);
            Stream writeStream = Socket.OutputStream.AsStreamForWrite(0);
            Stream = new DuplexStream(readStream, writeStream);
            return Stream;
        }

        public void Dispose()
        {
            Socket.Dispose();
            if (Stream != null)
            {
                Stream.Dispose();
            }
        }
    }
}
