using System;
using System.IO;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols.Transport
{
    public interface ITransport : IDisposable
    {
        Task<Stream> ConnectAsync();

        Stream Connect(); // TODO: Remove, not supported on phone
    }
}
