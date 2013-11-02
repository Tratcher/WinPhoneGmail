using System;

namespace WinPhone.Mail.Protocols
{
    public interface IMailClient : IDisposable
    {
        int GetMessageCount();
        MailMessage GetMessage(int index, bool headersonly = false);
        MailMessage GetMessage(string uid, bool headersonly = false);
        void DeleteMessage(string uid);
        void DeleteMessage(WinPhone.Mail.Protocols.MailMessage msg);
        void Disconnect();

        event EventHandler<WarningEventArgs> Warning;
    }
}
