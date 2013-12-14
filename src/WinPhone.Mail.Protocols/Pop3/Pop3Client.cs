using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols
{
    public class Pop3Client : TextClient, IMailClient
    {
        public Pop3Client()
        {
        }

        public async Task ConnectAsync(string host, string username, string password, int port = 110, bool secure = false, bool validateCertificate = true)
        {
            await ConnectAsync(host, port, secure, validateCertificate);
            await LoginAsync(username, password);
        }

        internal override async Task OnLoginAsync(string username, string password)
        {
            await SendCommandCheckOKAsync("USER " + username);
            await SendCommandCheckOKAsync("PASS " + password);
        }

        internal override Task OnLogoutAsync()
        {
            if (_Stream != null)
            {
                return SendCommandAsync("QUIT");
            }
            return Task.FromResult(0);
        }

        internal override void CheckResultOK(string result)
        {
            if (!result.StartsWith("+OK", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(result.Substring(result.IndexOf(' ') + 1).Trim());
            }
        }

        public virtual async Task<int> GetMessageCountAsync()
        {
            CheckConnectionStatus();
            var result = await SendCommandGetResponseAsync("STAT");
            CheckResultOK(result);
            return int.Parse(result.Split(' ')[1]);
        }

        public virtual Task<MailMessage> GetMessageAsync(int index, Scope scope = Scope.HeadersAndBody)
        {
            return GetMessageAsync((index + 1).ToString(), scope);
        }

        private static Regex rxOctets = new Regex(@"(\d+)\s+octets", RegexOptions.IgnoreCase);

        public virtual async Task<MailMessage> GetMessageAsync(string uid, Scope scope = Scope.HeadersAndBody)
        {
            CheckConnectionStatus();
            var line = await SendCommandGetResponseAsync(string.Format(scope == Scope.Headers ? "TOP {0} 0" : "RETR {0}", uid));
            var size = rxOctets.Match(line).Groups[1].Value.ToInt();
            CheckResultOK(line);
            var msg = new MailMessage();
            msg.Load(_Stream, scope, size, '.');

            msg.Uid = uid;
            var last = await GetResponseAsync();
            if (string.IsNullOrEmpty(last))
                last = await GetResponseAsync();

            if (last != ".")
            {
                System.Diagnostics.Debugger.Break();
                RaiseWarning(msg, "Expected \".\" in stream, but received \"" + last + "\"");
            }

            return msg;
        }

        public virtual Task DeleteMessageAsync(string uid)
        {
            return SendCommandCheckOKAsync("DELE " + uid);
        }

        public virtual Task DeleteMessageAsync(int index)
        {
            return DeleteMessageAsync((index + 1).ToString());
        }

        public virtual Task DeleteMessageAsync(MailMessage msg)
        {
            return DeleteMessageAsync(msg.Uid);
        }
    }
}