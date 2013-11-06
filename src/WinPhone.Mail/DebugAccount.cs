using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;
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

        public override Task<List<ConversationThread>> GetConversationsAsync(bool forceSync)
        {
            List<ConversationThread> conversations = new List<ConversationThread>();

            List<MailMessage> messages = new List<MailMessage>();
            messages.Add(new MailMessage()
            {
                Date = DateTime.Now,
                Subject = "A medium length subject",
                From = new MailAddress("user1@domain.com", "From1 User"),
                Headers = new HeaderDictionary()
                        {
                            { "X-GM-LABELS", new HeaderValue("\"\\\\Sent\" Family \"\\\\Important\" Geeky \"\\\\Starred\"") },
                        }
            });
            messages.Add(new MailMessage()
            {
                Date = DateTime.Now - TimeSpan.FromDays(3),
                Subject = "RE: A medium length subject",
                From = new MailAddress("user2@domain.com", "From2 User"),
                Flags = Flags.Seen,
                Headers = new HeaderDictionary()
                        {
                            { "X-GM-LABELS", new HeaderValue("Geeky") },
                        }
            });
            messages.Add(new MailMessage()
            {
                Date = DateTime.Now - TimeSpan.FromDays(10),
                Subject = "RE: RE: A medium length subject",
                From = new MailAddress("user3@domain.com", "From3 User"),
                Flags = Flags.Seen,
            });

            conversations.Add(new ConversationThread(messages));
            
            messages = new List<MailMessage>();
            messages.Add(new MailMessage()
            {
                Date = DateTime.Now - TimeSpan.FromDays(3),
                Subject = "A very long subject with lots of random short words that just keeps going and going and going and going and going",
                From = new MailAddress("user@domain.com", "From User"),
                Headers = new HeaderDictionary()
                        {
                            { "X-GM-LABELS", new HeaderValue("Geeky") },
                        }
            });
            
            conversations.Add(new ConversationThread(messages));
            
            messages = new List<MailMessage>();
            messages.Add(new MailMessage()
            {
                Date = DateTime.Now - TimeSpan.FromDays(10),
                Subject = "a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a",
                From = new MailAddress("user@domain.com", "From User"),
                Flags = Flags.Seen,
            });

            conversations.Add(new ConversationThread(messages));

            return Task.FromResult(conversations);
        }

        public override Task SelectLabelAsync(string label)
        {
            return Task.FromResult(0);
        }
    }
}
