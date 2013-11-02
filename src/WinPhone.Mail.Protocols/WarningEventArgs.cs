using System;

namespace WinPhone.Mail.Protocols
{
    public class WarningEventArgs : EventArgs
    {
        public string Message { get; set; }
        public MailMessage MailMessage { get; set; }
    }
}
