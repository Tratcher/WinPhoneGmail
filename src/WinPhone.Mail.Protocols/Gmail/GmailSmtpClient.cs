using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols.Smtp;

namespace WinPhone.Mail.Protocols.Gmail
{
    public class GmailSmtpClient : IDisposable
    {
        private const string Host = "smtp.gmail.com";
        private const int Port = 465;

        public GmailSmtpClient(string username, string password)
        {
            Client = new SmtpClient();
            UserName = username;
            Password = password;
        }

        public SmtpClient Client { get; private set; }

        private string UserName { get; set; }
        private string Password { get; set; }

        public async Task ConnectAsync()
        {
            if (!Client.IsConnected)
            {
                await Client.ConnectAsync(Host, UserName, Password, Port, secure: true, validateCertificate: true);
            }
        }

        public async Task CheckConnectedAsync()
        {
            if (!Client.IsConnected)
            {
                await ConnectAsync();
            }

            // TODO: Do a test send to verify connectivity? Auto-disconnect and reconnect?
        }

        public async Task SendAsync(MailMessage message)
        {
            await CheckConnectedAsync();
            await Client.SendAsync(message);
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
