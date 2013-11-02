using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols.Gmail
{
    public class GmailImapClient : IDisposable
    {
        private const string Host = "imap.gmail.com";
        private const int Port = 993;

        public GmailImapClient()
        {
            Client = new ImapClient();
        }

        public ImapClient Client { get; private set; }
        
        public async Task ConnectAsync(string username, string password)
        {
            await Client.ConnectAsync(Host, username, password, Port, secure: true, validateCertificate: true);
        }
        
        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
