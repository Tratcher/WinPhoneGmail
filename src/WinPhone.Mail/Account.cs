using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Protocols.Imap;
using WinPhone.Mail.Storage;

namespace WinPhone.Mail
{
    public class Account
    {
        protected Account() { }

        public Account(AccountInfo info)
        {
            Info = info;
            ActiveLabel = null;
            GmailImap = new GmailImapClient(Info.Address, Info.Password);
            MailStorage = new MailStorage(Info.Address);
        }

        public AccountInfo Info { get; protected set; }

        public GmailImapClient GmailImap { get; private set; }

        public List<LabelInfo> Labels { get; private set; }

        public Label ActiveLabel { get; private set; }

        public ConversationThread ActiveConversation { get; private set; }

        public MailStorage MailStorage { get; private set; }

        public virtual async Task<List<LabelInfo>> GetLabelsAsync(bool forceSync = false)
        {
            // From memory
            if (!forceSync && Labels != null && Labels.Count != 0)
            {
                return Labels;
            }

            // From disk
            Labels = await MailStorage.GetLabelsAsync() ?? new List<LabelInfo>();

            if (!forceSync && Labels.Count != 0)
            {
                return Labels;
            }

            // Sync with server
            List<LabelInfo> final = new List<LabelInfo>();
            Mailbox[] serverMailboxes = await GmailImap.GetLabelsAsync();
            IEnumerable<LabelInfo> serverLabels = serverMailboxes.Select(box => new LabelInfo() { Name = box.Name });

            await SyncUtilities.CompareListsAsync<LabelInfo>(serverLabels, Labels,
                label => label.Name, // Select key
                async (serverLabel, clientLabel) => final.Add(clientLabel), // Match, client wins, it has client side settings.
                async (serverLabel) => final.Add(serverLabel), // Server only, add.
                (clientLabel) => MailStorage.DeleteLabelAsync(clientLabel.Name)); // Client side only, garbage collect.

            Labels = final;

            // Save back to storage
            await MailStorage.SaveLabelsAsync(Labels);

            return Labels;
        }

        public virtual async Task<Label> GetLabelAsync(bool forceSync = false)
        {
            // From memory
            if (!forceSync && ActiveLabel != null && ActiveLabel.Conversations != null)
            {
                return ActiveLabel;
            }

            if (ActiveLabel == null)
            {
                List<LabelInfo> labels = await GetLabelsAsync();
                ActiveLabel = new Label()
                {
                    Info = labels.Where(info => info.Name.Equals(GConstants.Inbox)).First()
                };
            }

            if (ActiveLabel.Conversations == null)
            {
                // From disk
                ActiveLabel.Conversations = await MailStorage.GetConversationsAsync(ActiveLabel.Info.Name);
            }

            if (!forceSync && ActiveLabel.Conversations != null)
            {
                return ActiveLabel;
            }

            bool headersOnly = (ActiveLabel.Conversations != null && ActiveLabel.Conversations.Count != 0); // Optimize for the first download.
            List<ConversationThread> serverConversations = await GmailImap.GetConversationsAsync(headersOnly);

            ActiveLabel.Conversations = await ReconcileConversationsAsync(serverConversations, ActiveLabel.Conversations ?? new List<ConversationThread>());

            // Write back to storage.
            await MailStorage.StoreLabelMessageListAsync(ActiveLabel.Info.Name, ActiveLabel.Conversations);
            // TODO: Only store conversations that have changed.
            await MailStorage.StoreConverationsAsync(ActiveLabel.Conversations);

            return ActiveLabel;
        }

        private async Task<List<ConversationThread>> ReconcileConversationsAsync(List<ConversationThread> serverConversations, List<ConversationThread> clientConversations)
        {
            // TODO: Reconcile vs stored, remove no longer referenced conversations.
            List<ConversationThread> reconciledConversations = new List<ConversationThread>();
            await SyncUtilities.CompareListsAsync(serverConversations, clientConversations,
                thread => thread.ID, // Selector
                async (serverThread, clientThread) => // Match
                {
                    ConversationThread newThread = await ReconcileMessagesAsync(serverThread, clientThread);
                    reconciledConversations.Add(newThread);
                },
                async (serverThread) => // Server side only
                {
                    List<MailMessage> messages = new List<MailMessage>();

                    // Download the body
                    foreach (var message in serverThread.Messages)
                    {
                        if (message.HeadersOnly)
                        {
                            // TODO: PERF: Figure out how to just download the body, we already have the headers.
                            messages.Add(await GmailImap.DownloadMessageAsync(message.Uid));
                        }
                        else
                        {
                            messages.Add(message);
                        }
                    }

                    serverThread = new ConversationThread(messages.OrderByDescending(message => message.Date).ToList());
                    reconciledConversations.Add(serverThread);
                },
                async (clientThread) => // Client only, purge
                {
                    // TODO: Garbage collection. What if this thread is still referenced from another label?
                });

            return reconciledConversations.OrderByDescending(thread => thread.LatestDate).ToList();
        }

        // TODO: What about new messages in an old conversation?  We might have to reconcile individual messages at this stage.
        private async Task<ConversationThread> ReconcileMessagesAsync(ConversationThread serverThread, ConversationThread clientThread)
        {
            List<MailMessage> reconciledMessages = new List<MailMessage>();
            await SyncUtilities.CompareListsAsync(serverThread.Messages, clientThread.Messages,
                message => message.GetMessageId(), // Selector
                async (serverMessage, clientMessage) => // Match
                {
                    if (serverMessage.HeadersOnly)
                    {
                        // Overwrite the local headers that may have been update on the server.
                        // Flags and labels are the primary things we expect to change.
                        clientMessage.Flags = serverMessage.Flags;
                        clientMessage.Headers[GConstants.LabelsHeader] = serverMessage.Headers[GConstants.LabelsHeader];
                        reconciledMessages.Add(clientMessage);
                    }
                    else
                    {
                        reconciledMessages.Add(serverMessage);
                    }
                },
                async (serverMessage) => // Server side only
                {
                    if (serverMessage.HeadersOnly)
                    {
                        // TODO: Figure out how to just download the body, we already have the headers.
                        reconciledMessages.Add(await GmailImap.DownloadMessageAsync(serverMessage.Uid));
                    }
                    else
                    {
                        reconciledMessages.Add(serverMessage);
                    }
                },
                async (clientMessage) => // Client only, purge
                {
                    // TODO: Garbage collection. What if this thread is still referenced from another label?
                });

            return new ConversationThread(reconciledMessages.OrderByDescending(message => message.Date).ToList());
        }

        public virtual Task SelectLabelAsync(LabelInfo label)
        {
            ActiveLabel = new Label() { Info = label };
            // TODO: Put in command queue and run later.
            return GmailImap.SelectLabelAsync(label.Name);
        }

        public virtual async Task SelectConversationAsync(ConversationThread conversation)
        {
            // TODO: Sync full conversation body, from disk or network.
            ActiveConversation = conversation;
            await SetReadStatusAsync(conversation.Messages, true);
        }

        public virtual async Task SetReadStatusAsync(List<MailMessage> messages, bool read)
        {
            List<MailMessage> changedMessages = new List<MailMessage>(messages.Count);
            foreach (var message in messages)
            {
                if (message.Seen != read)
                {
                    message.Seen = read;
                    // TODO: Store in memory
                    await MailStorage.StoreMessageAsync(message);

                    changedMessages.Add(message);
                }
            }

            // TODO: Queue command to send change to the server
            if (changedMessages.Count > 0)
            {
                await GmailImap.SetReadStatusAsync(changedMessages, read);
            }
        }

        public async Task SetStarAsync(MailMessage message, bool starred)
        {
            // Set or remove the Flagged flag.
            message.Flagged = starred;
            await MailStorage.StoreMessageAsync(message);
            // TODO: Queue command to send change to the server
            await GmailImap.SetFlaggedStatusAsync(message, starred);
        }

        // It's assumed that labelName is never the active label.
        public virtual async Task AddLabelAsync(List<MailMessage> messages, string labelName)
        {
            foreach (var message in messages)
            {
                message.AddLabel(labelName);
                // TODO: Store in memory
                await MailStorage.StoreMessageAsync(message);

                // TODO: Store in the message list for that label
            }

            // TODO: Queue command to send change to the server
            await GmailImap.AddLabelAsync(messages, labelName);
        }

        public virtual Task RemoveLabelAsync(List<MailMessage> messages, string labelName)
        {
            if (ActiveLabel.Info.Name.Equals(labelName))
            {
                return RemoveCurrentLabelAsync(messages);
            }

            return RemoveOtherLabelAsync(messages, labelName);
        }

        // Removing the label for the currently active mailbox is easy, we just flag
        // the message as deleted in the active mailbox.
        private async Task RemoveCurrentLabelAsync(List<MailMessage> messages)
        {
            string label = ActiveLabel.Info.Name;
            // Remove from label message list.
            IEnumerable<string> removedThreadIds = messages.Select(message => message.GetThreadId()).Distinct();
            ActiveLabel.Conversations = ActiveLabel.Conversations.Where(conversation => !removedThreadIds.Contains(conversation.ID)).ToList();
            await MailStorage.StoreLabelMessageListAsync(label, ActiveLabel.Conversations);

            // TODO: If this was the last sync'd label, remove from storage.

            // Update the messages to remove the label.
            foreach (MailMessage message in messages)
            {
                if (message.RemoveLabel(label))
                {
                    // Store changes
                    await MailStorage.StoreMessageAsync(message);
                }
            }

            // Labels are deleted by deleting the message from the associated mailbox.
            await GmailImap.RemoveCurrentLabelAsync(messages);
        }

        // Removing labels for the non-active mailboxes is a bit more work.  We must:
        // - Switch to that mailbox
        // - Find the message mailbox UID for that message
        // -- Search storage first, then online
        // - Delete the message from the mailbox & storage
        // - Switch back to the original mailbox
        private async Task RemoveOtherLabelAsync(List<MailMessage> messages, string labelName)
        {
            List<MailMessage> changedMessages = new List<MailMessage>();
            // Remove the label from the messages and store the changes.
            // TODO: Remove message from disk if the message is no longer referenced by any sync'd labels.
            foreach (var message in messages)
            {
                if (message.RemoveLabel(labelName))
                {
                    changedMessages.Add(message);
                    await MailStorage.StoreMessageAsync(message);
                }
            }

            // Look up UIDs. If they're not here, we may need to check online.
            List<string> localMessageIds = new List<string>(); // Ids for messages we have referenced from a locally sync'd label.
            List<string> nonlocalMessageIds = new List<string>(); // Ids for messages we'll have to lookup online.
            List<MessageIdInfo> labelMessageIds = await MailStorage.GetLabelMessageListAsync(labelName) ?? new List<MessageIdInfo>();
            SyncUtilities.CompareLists(changedMessages.Select(message => message.GetMessageId()), labelMessageIds.Select(ids => ids.MessageId), id => id,
                (searchId, localId) => localMessageIds.Add(localId),
                (searchId) => nonlocalMessageIds.Add(searchId),
                (localId) => { } // Only in storage, ignore.
                );

            // Remove from that labelList
            List<MessageIdInfo> updatedLabelMessageIds = labelMessageIds.Where(messageIds => !localMessageIds.Contains(messageIds.MessageId)).ToList();
            await MailStorage.StoreLabelMessageListAsync(labelName, updatedLabelMessageIds);

            List<string> uidsToRemove = labelMessageIds.Where(messageIds => localMessageIds.Contains(messageIds.MessageId)).Select(ids => ids.Uid).ToList();

            // TODO: Queue up this action for later
            if (nonlocalMessageIds.Count > 0)
            {
                List<string> remoteUids = await GmailImap.GetUidsFromMessageIds(labelName, nonlocalMessageIds);
                uidsToRemove.AddRange(remoteUids);
            }
            if (uidsToRemove.Count > 0)
            {
                await GmailImap.RemoveOtherLabelAsync(labelName, uidsToRemove);
            }
        }

        // TODO: Full delete items already in Trash or Spam?
        public virtual async Task TrashAsync(List<MailMessage> messages, bool isSpam)
        {
            string labelName = isSpam ? "[Gmail]/Spam" : "[Gmail]/Trash";
            foreach (var message in messages)
            {
                // TODO: Remove from all label lists?  Add to trash label list if sync'd?
                message.AddLabel(labelName);
                // TODO: Store in memory
                await MailStorage.StoreMessageAsync(message);
            }

            // TODO: Queue command to send change to the server
            await GmailImap.AddLabelAsync(messages, labelName);
        }

        public void DeleteAccount()
        {
            MailStorage.DeleteAccount();
            ActiveLabel = null;
            GmailImap.Dispose();
            GmailImap = new GmailImapClient(Info.Address, Info.Password);
        }
    }
}
