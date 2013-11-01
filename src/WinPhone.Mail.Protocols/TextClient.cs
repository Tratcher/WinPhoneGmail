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
        protected Stream _Stream;

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
            Encoding = System.Text.Encoding.GetEncoding(1252);
        }

        internal abstract void OnLogin(string username, string password);
        internal abstract void OnLogout();
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
            OnLogin(username, password); // TODO: Async
            IsAuthenticated = true;
        }

        public virtual void Logout()
        {
            IsAuthenticated = false;
            OnLogout();
        }
        
        public virtual async Task ConnectAsync(string hostname, int port, bool ssl, bool validateCertificate)
        {
            try
            {
                Host = hostname;
                Port = port;
                Ssl = ssl;

                _Connection = new SystemNetTransport(hostname, port, ssl, validateCertificate);
                _Stream = await _Connection.ConnectAsync(); // TODO: Async required for phone.

                OnConnected(GetResponse());

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

        protected virtual void SendCommand(string command)
        {
            var bytes = System.Text.Encoding.Default.GetBytes(command + "\r\n");
            _Stream.Write(bytes, 0, bytes.Length);
        }

        protected virtual string SendCommandGetResponse(string command)
        {
            SendCommand(command);
            return GetResponse();
        }

        protected virtual string GetResponse()
        {
            int max = 0;
            return _Stream.ReadLine(ref max, Encoding, null);
        }
        /* TODO:
        protected virtual Task<string> GetResponseAsync()
        {
            int max = 0;
            return _Stream.ReadLineAsync(ref max, Encoding, null);
        }
        */
        protected virtual void SendCommandCheckOK(string command)
        {
            CheckResultOK(SendCommandGetResponse(command));
        }

        public virtual void Disconnect()
        {
            if (IsAuthenticated)
            {
                Logout();
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
                        Disconnect();
                        if (_Stream != null) _Stream.Dispose();
                        if (_Connection != null) _Connection.Dispose();
                    }
                }
            }
        }
    }
}
