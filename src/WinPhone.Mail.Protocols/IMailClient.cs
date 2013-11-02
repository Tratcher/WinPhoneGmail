using System;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols
{
    public interface IMailClient : IDisposable
    {
        Task<int> GetMessageCountAsync();
        Task<MailMessage> GetMessageAsync(int index, bool headersonly = false);
        Task<MailMessage> GetMessageAsync(string uid, bool headersonly = false);
        Task DeleteMessageAsync(string uid);
        Task DeleteMessageAsync(WinPhone.Mail.Protocols.MailMessage msg);
        Task DisconnectAsync();

        event EventHandler<WarningEventArgs> Warning;
    }
}
