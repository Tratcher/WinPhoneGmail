
using System;

namespace WinPhone.Mail.Protocols.Gmail
{
    public class GmailMessageInfo
    {
        public string Uid { get; set; }
        public string ThreadId { get; set; }
        public string MessageId { get; set; }
        public string Labels { get; set; }
        public string Flags { get; set; }
        public DateTime Date { get; set; }
    }
}
