using System;

namespace WinPhone.Mail.Protocols.Imap
{
    public class MessageEventArgs : EventArgs
    {
        public virtual int MessageCount { get; set; }
        internal ImapClient Client { get; set; }
    }
}
