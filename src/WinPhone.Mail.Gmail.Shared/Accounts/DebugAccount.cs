﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Protocols.Imap;
using WinPhone.Mail.Gmail.Shared.Storage;

namespace WinPhone.Mail.Gmail.Shared.Accounts
{
    // Used to test UI elements
    public class DebugAccount : Account
    {
        public static DebugAccount Current = new DebugAccount();

        private LabelInfo activeLabel = new LabelInfo() { Name = GConstants.Inbox };

        private DebugAccount()
        {
            Info = new AccountInfo() { Address = "example@example.com", Password = "stuff" };
        }

        public override Task<List<LabelInfo>> GetLabelsAsync(bool forceSync)
        {
            List<LabelInfo> labels = new List<LabelInfo>();
            labels.Add(new LabelInfo() { Name = GConstants.Inbox });
            labels.Add(new LabelInfo() { Name = "Custom" });
            labels.Add(new LabelInfo() { Name = "Geeky" });
            labels.Add(new LabelInfo() { Name = "Family" });
            labels.Add(new LabelInfo() { Name = "Label with spaces" });
            return Task.FromResult(labels);
        }

        public override Task<Label> GetLabelAsync(bool forceSync)
        {
            Label label = new Label();
            label.Info = activeLabel;

            List<ConversationThread> conversations = new List<ConversationThread>();
            label.Conversations = conversations;

            List<MailMessage> messages = new List<MailMessage>();
            messages.Add(new MailMessage()
            {
                Date = DateTime.Now,
                Subject = "A medium length subject",
                From = new MailAddress("user1@domain.com", "From1 User"),
                Headers = new HeaderDictionary()
                        {
                            { GConstants.LabelsHeader, new HeaderValue("\"\\\\Sent\" Family \"\\\\Important\" Geeky \"\\\\Starred\"") },
                        },
                Body = "Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, Hello World, "
            });
            messages.Add(new MailMessage()
            {
                Date = DateTime.Now - TimeSpan.FromDays(3),
                Subject = "RE: A medium length subject",
                From = new MailAddress("user2@domain.com", "From2 User"),
                Seen = true,
                Headers = new HeaderDictionary()
                        {
                            { GConstants.LabelsHeader, new HeaderValue("Geeky") },
                        },
                Body = "Hello World \r\n Hello World \r\nHello World \r\nHello World \r\nHello World \r\nHello World \r\nHello World \r\nHello World \r\nHello World \r\n"
            });
            messages.Add(new MailMessage()
            {
                Date = DateTime.Now - TimeSpan.FromDays(10),
                Subject = "RE: RE: A medium length subject",
                From = new MailAddress("user3@domain.com", "From3 User"),
                Seen = true,
                Body = "Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World "
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
                            { GConstants.LabelsHeader, new HeaderValue("Geeky") },
                        },
                Body = "Hello World"
            });
            
            conversations.Add(new ConversationThread(messages));
            
            messages = new List<MailMessage>();
            messages.Add(new MailMessage()
            {
                Date = DateTime.Now - TimeSpan.FromDays(10),
                Subject = "a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a a",
                From = new MailAddress("user@domain.com", "From User"),
                Seen = true,
                Body = "Hello World"
            });

            conversations.Add(new ConversationThread(messages));

            return Task.FromResult(label);
        }

        public override Task SelectLabelAsync(LabelInfo label)
        {
            activeLabel = label;
            return Task.FromResult(0);
        }

        public override Task SetReadStatusAsync(List<MailMessage> messages, bool read)
        {
            foreach (var message in messages)
            {
                message.Seen = read;
            }
            return Task.FromResult(0);
        }
    }
}
