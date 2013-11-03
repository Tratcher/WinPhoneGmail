using System;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Imap;

namespace WinPhone.Mail
{
    // Used to test UI elements
    public class DebugAccount : Account
    {
        public static DebugAccount Current = new DebugAccount();

        private DebugAccount()
        {
        }

        public override Task<Mailbox[]> GetLabelsAsync()
        {
            Mailbox[] labels = new Mailbox[3];
            labels[0] = new Mailbox("Inbox");
            labels[1] = new Mailbox("Custom");
            labels[2] = new Mailbox("Label with spaces");
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

        internal override Task SelectLabelAsync(Mailbox mailbox)
        {
            return Task.FromResult(0);
        }
    }
}
