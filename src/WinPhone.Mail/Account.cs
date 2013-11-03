using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Protocols.Imap;
using WinPhone.Mail.Storage;

namespace WinPhone.Mail
{
    public class Account
    {
        protected Account() { }

        public Account(AccountInfo info)
        {
            Info = info;
            GmailImap = new GmailImapClient(Info.Address, Info.Password);
        }

        public AccountInfo Info { get; private set; }

        public GmailImapClient GmailImap { get; private set; }

        public virtual async Task<MailMessage[]> GetMessagesAsync()
        {
            // TODO: Get from storage instead
            MailMessage[] messages = await GmailImap.GetMessagesAsync();
            return messages.Reverse().ToArray();
        }

        public virtual Task<Mailbox[]> GetLabelsAsync()
        {
            // TODO: Get from storage instead
            return GmailImap.GetLabelsAsync();
        }

        internal virtual Task SelectLabelAsync(Mailbox mailbox)
        {
            // TODO: Get from storage instead
            return GmailImap.SelectLabelAsync(mailbox.Name);
        }
    }
}
