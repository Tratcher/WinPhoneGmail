using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Imap;
using WinPhone.Mail.Storage;

namespace WinPhone.Mail
{
    // Used to test UI elements
    public class DebugAccount : Account
    {
        public static DebugAccount Current = new DebugAccount();

        private DebugAccount()
        {
        }

        public override Task<List<LabelInfo>> GetLabelsAsync(bool forceSync)
        {
            List<LabelInfo> labels = new List<LabelInfo>();
            labels.Add(new LabelInfo() { Name = "Inbox" });
            labels.Add(new LabelInfo() { Name = "Custom" });
            labels.Add(new LabelInfo() { Name = "Label with spaces" });
            return Task.FromResult(labels);
        }

        public override Task<MailMessage[]> GetMessagesAsync()
        {
            MailMessage[] messages = new MailMessage[3];
            messages[0] = new MailMessage()
            {
                Date = DateTime.Now,
                Subject = "A medium length subject",
                From = new MailAddress("user@domain.com", "From User"),
                Headers = new HeaderDictionary()
                        {
                            { "X-GM-LABELS", new HeaderValue("\"\\\\Sent\" Family \"\\\\Important\" Geeky \"\\\\Starred\"") },
                        }
            };
            messages[1] = new MailMessage()
            {
                Date = DateTime.Now - TimeSpan.FromDays(3),
                Subject = "A very long subject with lots of random short words that just keeps going and going and going and going and going",
                From = new MailAddress("user@domain.com", "From User"),
                Flags = Flags.Seen,
                Headers = new HeaderDictionary()
                        {
                            { "X-GM-LABELS", new HeaderValue("Geeky") },
                        }
            };
            messages[2] = new MailMessage()
            {
                Date = DateTime.Now - TimeSpan.FromDays(10),
                Subject = "a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a",
                From = new MailAddress("user@domain.com", "From User"),
                Flags = Flags.Seen,
            };

            return Task.FromResult(messages);
        }

        public override Task SelectLabelAsync(string label)
        {
            return Task.FromResult(0);
        }
    }
}
