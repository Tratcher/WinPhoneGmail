using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols.Transport;

namespace WinPhone.Mail.Protocols
{
    public abstract class TextClient : IDisposable
    {
        protected ITransport _Connection;
        protected BufferedReadStream _Stream;

        public virtual string Host { get; private set; }
        public virtual int Port { get; set; }
        public virtual bool Ssl { get; set; }
        public virtual bool IsConnected { get; private set; }
        public virtual bool IsAuthenticated { get; private set; }
        public virtual bool IsDisposed { get; private set; }
        public virtual Encoding Encoding { get; set; }

        public event EventHandler<WarningEventArgs> Warning;

        public TextClient()
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); // System.Text.Encoding.GetEncoding("Windows-1252");
        }

        internal abstract Task OnLoginAsync(string username, string password);
        internal abstract Task OnLogoutAsync();
        internal abstract void CheckResultOK(string result);

        protected virtual void RaiseWarning(MailMessage mailMessage, string message)
        {
            var warning = Warning;
            if (warning != null)
            {
                warning(this, new WarningEventArgs { MailMessage = mailMessage, Message = message });
            }
        }

        protected virtual void OnConnected(string result)
        {
            CheckResultOK(result);
        }

        public virtual async Task LoginAsync(string username, string password)
        {
            if (!IsConnected)
            {
                throw new Exception("You must connect first!");
            }
            IsAuthenticated = false;
            await OnLoginAsync(username, password);
            IsAuthenticated = true;
        }

        public virtual Task LogoutAsync()
        {
            IsAuthenticated = false;
            return OnLogoutAsync();
        }
        
        public virtual async Task ConnectAsync(string hostname, int port, bool ssl, bool validateCertificate)
        {
            try
            {
                Host = hostname;
                Port = port;
                Ssl = ssl;

#if WINDOWS_PHONE
                _Connection = new WindowsNetSockets(hostname, port, ssl);
#else
                _Connection = new SystemNetTransport(hostname, port, ssl, validateCertificate);
#endif
                _Stream = new BufferedReadStream(await _Connection.ConnectAsync());

                OnConnected(await GetResponseAsync());

                IsConnected = true;
            }
            catch (Exception)
            {
                IsConnected = false;
                Utilities.TryDispose(_Stream);
                throw;
            }
        }

        protected virtual void CheckConnectionStatus()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(this.GetType().Name);
            if (!IsConnected)
                throw new Exception("You must connect first!");
            if (!IsAuthenticated)
                throw new Exception("You must authenticate first!");
        }

        protected virtual Task SendCommandAsync(string command)
        {
            var bytes = Utilities.ASCII.GetBytes(command + "\r\n");
            return _Stream.WriteAsync(bytes, 0, bytes.Length);
        }

        protected virtual async Task<string> SendCommandGetResponseAsync(string command)
        {
            await SendCommandAsync(command);
            return await GetResponseAsync();
        }

        protected virtual Task<string> GetResponseAsync()
        {
            return _Stream.ReadLineAsync(0, Encoding, null);
        }

        protected virtual async Task SendCommandCheckOKAsync(string command)
        {
            CheckResultOK(await SendCommandGetResponseAsync(command));
        }

        protected virtual async Task SendCommandCheckResponseAsync(string command, string expectedResponse)
        {
            string response = await SendCommandGetResponseAsync(command);
            if (!response.Equals(expectedResponse))
            {
                throw new InvalidOperationException("Unexpected response '" + response + "' for command '" + command);
            }
        }

        public virtual async Task DisconnectAsync()
        {
            if (IsAuthenticated)
            {
                await LogoutAsync();
            }

            Utilities.TryDispose(_Stream);
            Utilities.TryDispose(_Connection);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed && disposing)
            {
                lock (this)
                {
                    if (!IsDisposed && disposing)
                    {
                        IsDisposed = true;
                        DisconnectAsync();
                        if (_Stream != null) _Stream.Dispose();
                        if (_Connection != null) _Connection.Dispose();
                    }
                }
            }
        }
    }
}
