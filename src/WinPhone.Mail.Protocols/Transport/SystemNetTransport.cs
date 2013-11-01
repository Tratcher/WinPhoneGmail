using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols.Transport
{
    public class SystemNetTransport : ITransport
    {

        public SystemNetTransport(string host, int port, bool ssl, bool validateRemoteCert)
        {
            Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            Host = host;
            Port = port;
            Ssl = ssl;
            ValidateCert = validateRemoteCert;
        }

        private Socket Socket { get; set; }
        private string Host { get; set; }
        private int Port { get; set; }
        private bool Ssl { get; set; }
        private bool ValidateCert { get; set; }
        private Stream Stream { get; set; }

        public Stream Connect()
        {
            Socket.Connect(Host, Port);
            Stream = new NetworkStream(Socket);

            if (Ssl)
            {
                SslStream sslStream;
                if (ValidateCert)
                {
                    sslStream = new SslStream(Stream, false);
                }
                else
                {
                    sslStream = new SslStream(Stream, false, (_, __, ___, ____) => true);
                }
                Stream = sslStream;
                sslStream.AuthenticateAsClient(Host);
            }

            return Stream;
        }

        public async Task<Stream> ConnectAsync()
        {
            await Task.Factory.FromAsync(Socket.BeginConnect, Socket.EndConnect, Host, Port, state: null);
            Stream = new NetworkStream(Socket);

            if (Ssl)
            {
                SslStream sslStream;
                if (ValidateCert)
                {
                    sslStream = new SslStream(Stream, false);
                }
                else
                {
                    sslStream = new SslStream(Stream, false, (_, __, ___, ____) => true);
                }
                Stream = sslStream;
                await sslStream.AuthenticateAsClientAsync(Host);
            }

            return Stream;
        }

        public void Dispose()
        {
            Socket.Close();
            if (Stream != null)
            {
                Stream.Dispose();
            }
        }
    }
}
