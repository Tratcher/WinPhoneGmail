using System;
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

        public async Task CheckConnectedAsync()
        {
            if (!Client.IsConnected)
            {
                await ConnectAsync();
            }

            // TODO: Do a test send to verify connectivity? Auto-disconnect and reconnect?
        }

        public async Task<List<ConversationThread>> GetConversationsAsync(bool headersOnly, TimeSpan range)
        {
            await CheckConnectedAsync();

            // TODO: Configurable range
            SearchCondition condition = SearchCondition.Since(DateTime.Now - range);
            string[] uids = await Client.SearchAsync(condition, uid: true);

            List<ConversationThread> conversations = new List<ConversationThread>();
            MailMessage[] messages;
            if (uids.Length == 0)
            {
                messages = new MailMessage[0];
            }
            else if (uids.Length == 1)
            {
                MailMessage message = await Client.GetMessageAsync(uids[0], headersOnly);
                messages = new MailMessage[] { message };
            }
            else
            {
                // TODO: Consider comma joined list of UIDs
                messages = await Client.GetMessagesAsync(uids[0], uids[uids.Length - 1], headersOnly);
            }

            // Group by thread ID
            foreach (IGrouping<string, MailMessage> group in messages.GroupBy(message => message.GetThreadId()))
            {
                ConversationThread conversation = new ConversationThread(group.OrderByDescending(message => message.Date).ToList());
                conversation.Messages.ForEach(message => FixUpLabels(message));
                conversations.Add(conversation);
            }
            return conversations;
        }

        // IMAP messages for a given label do not actually list that label in the label header.  Append it
        // so there's less confusion correlating across different mailboxes.
        private void FixUpLabels(MailMessage message)
        {
            // Convert the special inbox label to match the inbox mailbox name.
            string newLabelHeader = message.Headers[GConstants.LabelsHeader].RawValue.Replace(GConstants.InboxLabel, GConstants.Inbox);

            message.Headers[GConstants.LabelsHeader] = new HeaderValue(newLabelHeader);

            if (Client.SelectedMailbox.Equals(GConstants.AllMailMailbox, StringComparison.Ordinal)
                || Client.SelectedMailbox.Equals(GConstants.StarredMailbox, StringComparison.Ordinal))
            {
                // These are special mailboxes that don't appear as labels.
                // TODO: Others?
            }
            else
            {
                message.AddLabel(Client.SelectedMailbox);
            }
        }

        // TODO: Starting with a message that's headers only, download and populate the body
        public async Task<MailMessage> DownloadMessageAsync(string uid)
        {
            MailMessage message = await Client.GetMessageAsync(uid, headersonly: false);
            FixUpLabels(message);
            return message;
        }

        public async Task<Mailbox[]> GetLabelsAsync()
        {
            await CheckConnectedAsync();

            Mailbox[] mailboxes = await Client.ListMailboxesAsync(string.Empty, "*");
            // Filter out the special [Gmail] dir that can't directly contain messages.
            return mailboxes.Where(box => !box.Flags.Contains("\\Noselect")).ToArray();
        }

        public async Task SetReadStatusAsync(List<MailMessage> messages, bool read)
        {
            await CheckConnectedAsync();

            if (read)
            {
                await Client.AddFlagsAsync(Flags.Seen, messages);
            }
            else
            {
                await Client.RemoveFlagsAsync(Flags.Seen, messages);
            }
        }

        public async Task SetFlaggedStatusAsync(IEnumerable<MailMessage> messages, bool flagged)
        {
            await CheckConnectedAsync();

            if (flagged)
            {
                await Client.AddFlagsAsync(Flags.Flagged, messages);
            }
            else
            {
                await Client.RemoveFlagsAsync(Flags.Flagged, messages);
            }
        }

        public async Task SelectLabelAsync(string mailboxName)
        {
            await CheckConnectedAsync();

            await Client.SelectMailboxAsync(mailboxName);
        }

        public async Task AddLabelAsync(List<MailMessage> messages, string labelName)
        {
            await CheckConnectedAsync();

            await Client.CopyAsync(messages, labelName);
        }

        public async Task RemoveCurrentLabelAsync(List<MailMessage> messages)
        {
            await CheckConnectedAsync();

            await Client.DeleteMessagesAsync(messages);
        }

        // Given a list of unique message ids, find the Uids for the corresponding mailbox/label.
        public async Task<List<string>> GetUidsFromMessageIds(string labelName, List<string> messageIds)
        {
            await CheckConnectedAsync();

            // Presume this is not the current mailbox.
            string priorMailbox = Client.SelectedMailbox;
            await Client.SelectMailboxAsync(labelName);

            List<string> uids = new List<string>();
            foreach (string id in messageIds)
            {
                // We won't normally have to look up very many items, so it should be ok do search for them individually.
                string[] results = await Client.SearchAsync(GConstants.MessageIdHeader + " " + id, uid: true);
                string result = results.FirstOrDefault();
                if (result != null)
                {
                    uids.Add(result);
                }
            }

            await Client.SelectMailboxAsync(priorMailbox);
            return uids;
        }

        public async Task RemoveOtherLabelAsync(string labelName, List<string> ids)
        {
            await CheckConnectedAsync();

            string priorMailbox = Client.SelectedMailbox;
            await Client.SelectMailboxAsync(labelName);

            await Client.DeleteMessagesAsync(ids);

            await Client.SelectMailboxAsync(priorMailbox);
        }
        
        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
