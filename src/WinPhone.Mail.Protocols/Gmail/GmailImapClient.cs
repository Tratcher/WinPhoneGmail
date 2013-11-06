﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols.Imap;

namespace WinPhone.Mail.Protocols.Gmail
{
    public class GmailImapClient : IDisposable
    {
        private const string Host = "imap.gmail.com";
        private const int Port = 993;

        public GmailImapClient(string username, string password)
        {
            Client = new ImapClient();
            UserName = username;
            Password = password;
        }

        public ImapClient Client { get; private set; }

        private string UserName { get; set; }
        private string Password { get; set; }
        
        public async Task ConnectAsync()
        {
            if (!Client.IsConnected)
            {
                await Client.ConnectAsync(Host, UserName, Password, Port, secure: true, validateCertificate: true);
            }
        }

        public async Task<List<ConversationThread>> GetConversationsAsync()
        {
            if (!Client.IsConnected)
            {
                await ConnectAsync();
            }

            // TODO: currently limited to 15 messages, headers only
            MailMessage[] messages = await Client.GetMessagesAsync(0, 30, headersonly: true, setseen: false);

            List<ConversationThread> conversations = new List<ConversationThread>();
            // Group by thread ID
            foreach (IGrouping<string, MailMessage> group in messages.GroupBy(message => message.GetThreadId()))
            {
                conversations.Add(new ConversationThread(group.OrderByDescending(message => message.Date).ToList()));
            }
            return conversations;
        }

        public async Task<Mailbox[]> GetLabelsAsync()
        {
            if (!Client.IsConnected)
            {
                await ConnectAsync();
            }

            Mailbox[] mailboxes = await Client.ListMailboxesAsync(string.Empty, "*");
            // Filter out the special [Gmail] dir that can't directly contain messages.
            return mailboxes.Where(box => !box.Flags.Contains("\\Noselect")).ToArray();
        }

        public async Task SelectLabelAsync(string mailboxName)
        {
            if (!Client.IsConnected)
            {
                await ConnectAsync();
            }

            await Client.SelectMailboxAsync(mailboxName);
        }
        
        public void Dispose()
        {
            Client.Dispose();
        }
    }
}