using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Storage;

namespace WinPhone.Mail
{
    public class Account
    {
        public Account(AccountInfo info)
        {
            Info = info;
            Imap = new GmailImapClient();
        }

        public AccountInfo Info { get; private set; }

        public GmailImapClient Imap { get; private set; }

        public Task ConnectAsync()
        {
            return Imap.ConnectAsync(Info.Address, Info.Password);
        }
    }
}
