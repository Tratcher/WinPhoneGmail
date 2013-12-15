using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using WinPhone.Mail.Gmail.Shared.Storage;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Protocols.Imap;

namespace WinPhone.Mail.Gmail.Shared.Accounts
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
            Labels = await MailStorage.GetLabelInfoAsync() ?? new List<LabelInfo>();

            if (!forceSync && Labels.Count != 0)
            {
                return Labels;
            }

            if (Labels.Count == 0)
            {
                // Default label settings for a new account
                Labels.Add(new LabelInfo() { Name = GConstants.Inbox, StoreMessages = true });
            }

            // Sync with server
            List<LabelInfo> final = new List<LabelInfo>();
            Mailbox[] serverMailboxes = await GmailImap.GetLabelsAsync();
            IEnumerable<LabelInfo> serverLabels = serverMailboxes.Select(box => new LabelInfo() { Name = box.Name });

            await SyncUtilities.CompareListsAsync<LabelInfo>(serverLabels, Labels,
                label => label.Name, // Select key
                async (serverLabel, clientLabel) => final.Add(clientLabel), // Match, client wins, it has client side settings.
                async (serverLabel) => final.Add(serverLabel), // Server only, add.
                (clientLabel) => MailStorage.DeleteLabelMessageListAsync(clientLabel.Name)); // Client side only, garbage collect.

            Labels = final;

            // Save back to storage
            await MailStorage.SaveLabelInfoAsync(Labels);

            return Labels;
        }

        // Force save after settings changes.
        public async Task SaveLabelSettingsAsync()
        {
            List<LabelInfo> labels = await GetLabelsAsync();
            await MailStorage.SaveLabelInfoAsync(labels);
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

            if (ActiveLabel.Conversations == null && ActiveLabel.Info.StoreMessages)
            {
                // From disk
                ActiveLabel.Conversations = await MailStorage.GetConversationsAsync(ActiveLabel.Info.Name, Scope.HeadersAndMime);
            }

            if (!forceSync && ActiveLabel.Conversations != null)
            {
                return ActiveLabel;
            }

            if (!forceSync && !ActiveLabel.Info.StoreMessages)
            {
                // Don't sync non-stored labels by default. Require force sync.
                return ActiveLabel;
            }

            if (ActiveLabel.Info.StoreMessages)
            {
                await SyncMessageHeadersAsync(CancellationToken.None);
                await SyncMessageBodiesAsync(CancellationToken.None);

                if (ActiveLabel.Info.StoreAttachments)
                {
                    await SyncAttachmentsAsync(CancellationToken.None);
                }

                ActiveLabel.Conversations = await MailStorage.GetConversationsAsync(ActiveLabel.Info.Name, Scope.HeadersAndMime);
            }
            else
            {
                // Allow us to view mail without storing it to disk.
                // TODO: Consider downloading only headers and then downloading the body & attachments if we open it.
                List<ConversationThread> serverConversations = await GmailImap.GetConversationsAsync(Scope.HeadersAndMime,
                    range: Info.Range, cancellationToken: CancellationToken.None);
                ActiveLabel.Conversations = serverConversations;
            }

            return ActiveLabel;
        }

        //   Determine oldest date: Now - Range
        //   Query server for ids of messages in folder since oldest date (async while loading local list?)
        //   Load stored message list (Ids only)
        //   For each new item in the remote list:
        //   - Download & save headers, flags, & labels, add to local list, save (TODO: What if another label already had this message?)
        //   - Anything that is unread counts as new mail. It wasn't downloaded last time we opened the app, so the message date is irrelevant.
        //   - Queue body and attachments for download later.
        //   For each item that was already in the local list
        //   - query for updated flags and labels
        //   - Anything that is unread since the last time we opened the app counts as new mail.
        //   - Queue body and attachments for download later (if they weren't downloaded on a previous sync)
        public async Task<int> SyncMessageHeadersAsync(CancellationToken cancellationToken)
        {
            DateTime syncMailSince = DateTime.Now - Info.Range;
            Task<IList<GmailMessageInfo>> remoteMessageInfoTask = GmailImap.GetCurrentMessageIdsAsync(syncMailSince);
            List<MessageIdInfo> localIds = await MailStorage.GetLabelMessageListAsync(ActiveLabel.Info.Name) ?? new List<MessageIdInfo>();
            IList<GmailMessageInfo> remoteMessageInfos = await remoteMessageInfoTask;
            IEnumerable<MessageIdInfo> remoteIds = remoteMessageInfos.Select(info => new MessageIdInfo()
            {
                Uid = info.Uid,
                MessageId = info.MessageId,
                ThreadId = info.ThreadId,
            });

            DateTime lastAppActivation = AppSettings.LastAppActivationTime;
            int newMessages = remoteMessageInfos
                .Where(info => info.Date > lastAppActivation && !info.Flags.Contains(@"\Seen")).Count();

            IList<MessageIdInfo> messagesInBothPlaces = new List<MessageIdInfo>();
            IList<MessageIdInfo> messagesOnlyRemote = new List<MessageIdInfo>();
            IList<MessageIdInfo> messagesOnlyLocal = new List<MessageIdInfo>();
            IList<MessageIdInfo> messageHeadersToDownload = new List<MessageIdInfo>();

            if (cancellationToken.IsCancellationRequested) return newMessages;

            SyncUtilities.CompareLists(remoteIds, localIds, info => info.Uid,
                (remote, local) => messagesInBothPlaces.Add(local),
                remoteOnly => messagesOnlyRemote.Add(remoteOnly),
                localOnly => messagesOnlyLocal.Add(localOnly));

            if (cancellationToken.IsCancellationRequested) return newMessages;

            bool localMessageListModified = false;
            foreach (MessageIdInfo idInfo in messagesOnlyRemote)
            {
                // Check if the item is already on disk (from another label)
                if (MailStorage.HasMessageHeaders(idInfo.ThreadId, idInfo.MessageId))
                {
                    // If so, update the labels and flags.
                    messagesInBothPlaces.Add(idInfo);
                }
                // If not, download the headers / message structure.
                else
                {
                    messageHeadersToDownload.Add(idInfo);
                }

                // Add to labe's list.
                localIds.Add(idInfo);
                localMessageListModified = true;
            }

            if (messageHeadersToDownload.Count > 0)
            {
                // Bulk download headers
                await GmailImap.GetEnvelopeAndStructureAsync(messageHeadersToDownload.Select(ids => ids.Uid),
                    async data =>
                    {
                        // Find the matching Ids
                        string messageId = data.GetMessageId();
                        GmailMessageInfo info = remoteMessageInfos.First(infos => infos.MessageId.Equals(messageId));

                        if (cancellationToken.IsCancellationRequested) return;

                        // Save headers, labels, and flags to disk
                        await MailStorage.StoreMessageFlagsAsync(info.ThreadId, info.MessageId, info.Flags);
                        await MailStorage.StoreMessageLabelsAsync(info.ThreadId, info.MessageId, info.Labels);
                        await MailStorage.StoreMessageHeadersAsync(data);
                    },
                    cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) return newMessages;

            if (localMessageListModified)
            {
                await MailStorage.StoreLabelMessageListAsync(ActiveLabel.Info.Name, localIds);
            }

            foreach (MessageIdInfo idInfo in messagesInBothPlaces)
            {
                if (cancellationToken.IsCancellationRequested) return newMessages;
                // Find the matching Ids
                GmailMessageInfo info = remoteMessageInfos.First(infos => infos.MessageId.Equals(idInfo.MessageId));
                // Update the labels and flags.
                await MailStorage.StoreMessageFlagsAsync(info.ThreadId, info.MessageId, info.Flags);
                await MailStorage.StoreMessageLabelsAsync(info.ThreadId, info.MessageId, info.Labels);
            }

            if (cancellationToken.IsCancellationRequested) return newMessages;

            // Only remember our last sync time if we (mostly) finished.
            ActiveLabel.Info.LastSync = DateTime.Now;
            await SaveLabelSettingsAsync();

            if (messagesOnlyLocal.Any())
            {
                // Remove deleted mails from label list
                localIds = localIds.Except(messagesOnlyLocal).ToList();
                await MailStorage.StoreLabelMessageListAsync(ActiveLabel.Info.Name, localIds);
                // TODO: GC message data.
            }

            return newMessages;
        }

        // Examine the local data store to see if there are any message bodies that still need to be downloaded.
        public async Task SyncMessageBodiesAsync(CancellationToken cancellationToken)
        {
            List<MessageIdInfo> localIds = await MailStorage.GetLabelMessageListAsync(ActiveLabel.Info.Name) ?? new List<MessageIdInfo>();
            List<KeyValuePair<MessageIdInfo, ObjectWHeaders>> bodiesToDownload = new List<KeyValuePair<MessageIdInfo, ObjectWHeaders>>();

            foreach (MessageIdInfo ids in localIds)
            {
                if (cancellationToken.IsCancellationRequested) return;

                MailMessage headers = await MailStorage.GetMessageHeadersAsync(ids.ThreadId, ids.MessageId);
                if (headers == null)
                {
                    // Downloading headers should have happened elsewhere.
                    continue;
                }

                if (headers.HasMutipartBody)
                {
                    foreach (Attachment view in headers.AlternateViews)
                    {
                        if (!MailStorage.HasMessagePart(ids.ThreadId, ids.MessageId, view.BodyId))
                        {
                            bodiesToDownload.Add(new KeyValuePair<MessageIdInfo, ObjectWHeaders>(ids, view));
                        }
                    }
                    // Attachments will be downloaded seperately as well.
                    // TODO: but maybe we could build the list here while we're looking?
                }
                else
                {
                    // Primary body, no attachments or alternate views
                    if (!MailStorage.HasMessagePart(ids.ThreadId, ids.MessageId, headers.BodyId))
                    {
                        bodiesToDownload.Add(new KeyValuePair<MessageIdInfo, ObjectWHeaders>(ids, headers));
                    }
                }
            }

            if (cancellationToken.IsCancellationRequested) return;

            // TODO: Batch
            foreach (var pair in bodiesToDownload)
            {
                if (cancellationToken.IsCancellationRequested) return;

                // TODO: Consider only downloading the first X bytes of each message, and loading more only on demand.
                // TODO: Consider streaming the body directly to disk.  Even more so for attachments.
                await GmailImap.GetBodyPartAsync(pair.Key.Uid, pair.Value,
                    async () =>
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        await MailStorage.StoreMessagePartAsync(pair.Key.ThreadId, pair.Key.MessageId, pair.Value.BodyId, pair.Value.Body);

                        // Release the body for GC. Otherwise the bodiesToDownload list will keep everything im memory.
                        pair.Value.Body = null;
                    }, cancellationToken);
            }
        }

        // Examine the local data store to see if there are any attachment bodies that still need to be downloaded.
        public async Task SyncAttachmentsAsync(CancellationToken cancellationToken)
        {
            List<MessageIdInfo> localIds = await MailStorage.GetLabelMessageListAsync(ActiveLabel.Info.Name) ?? new List<MessageIdInfo>();
            List<KeyValuePair<MessageIdInfo, ObjectWHeaders>> attachmentsToDownload = new List<KeyValuePair<MessageIdInfo, ObjectWHeaders>>();

            foreach (MessageIdInfo ids in localIds)
            {
                if (cancellationToken.IsCancellationRequested) return;

                MailMessage headers = await MailStorage.GetMessageHeadersAsync(ids.ThreadId, ids.MessageId);
                if (headers == null)
                {
                    // Downloading headers should have happened elsewhere.
                    continue;
                }

                if (headers.HasMutipartBody)
                {
                    foreach (Attachment attachment in headers.Attachments)
                    {
                        if (!MailStorage.HasMessagePart(ids.ThreadId, ids.MessageId, attachment.BodyId))
                        {
                            attachmentsToDownload.Add(new KeyValuePair<MessageIdInfo, ObjectWHeaders>(ids, attachment));
                        }
                    }
                }
            }

            if (cancellationToken.IsCancellationRequested) return;

            // TODO: Batch by bodyId?
            foreach (var pair in attachmentsToDownload)
            {
                if (cancellationToken.IsCancellationRequested) return;

                // TODO: Consider streaming the body directly to disk.  Even more so for attachments.
                await GmailImap.GetBodyPartAsync(pair.Key.Uid, pair.Value,
                    async () =>
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        await MailStorage.StoreMessagePartAsync(pair.Key.ThreadId, pair.Key.MessageId, pair.Value.BodyId, pair.Value.Body);

                        // Release the body for GC. Otherwise the bodiesToDownload list will keep everything im memory.
                        pair.Value.Body = null;
                    }, cancellationToken);
            }
        }

        public virtual Task SelectLabelAsync(LabelInfo label)
        {
            if (ActiveLabel == null || !label.Name.Equals(ActiveLabel.Info.Name))
            {
                ActiveLabel = new Label() { Info = label };
                // TODO: Put in command queue and run later.
                return GmailImap.SelectLabelAsync(label.Name);
            }
            return Task.FromResult(0);
        }

        public virtual async Task SelectConversationAsync(ConversationThread conversation)
        {
            ActiveConversation = conversation;
            await SetReadStatusAsync(conversation.Messages, true);

            // Sync full conversation body, from disk or network.
            foreach (MailMessage message in conversation.Messages)
            {
                ObjectWHeaders view = message.GetHtmlView() ?? message.GetTextView() ?? message;
                if (view.Scope < Scope.HeadersAndBody)
                {
                    if (MailStorage.HasMessagePart(conversation.ID, message.GetMessageId(), view.BodyId))
                    {
                        view.Body = await MailStorage.GetMessagePartAsync(conversation.ID, message.GetMessageId(), view.BodyId);
                    }
                    else
                    {
                        await GmailImap.GetBodyPartAsync(message.Uid, view, async () =>
                        {
                            if (ActiveLabel.Info.StoreMessages)
                            {
                                await MailStorage.StoreMessagePartAsync(conversation.ID, message.GetMessageId(), view.BodyId, view.Body);
                            }
                        }, CancellationToken.None);
                    }
                }
            }
        }

        public virtual async Task SetReadStatusAsync(List<MailMessage> messages, bool read)
        {
            List<MailMessage> changedMessages = new List<MailMessage>(messages.Count);
            foreach (var message in messages)
            {
                if (message.Seen != read)
                {
                    // Update in memory
                    message.Seen = read;

                    if (MailStorage.HasMessageFlags(message.GetThreadId(), message.GetMessageId()))
                    {
                        // Update on disk
                        await MailStorage.StoreMessageFlagsAsync(message);
                    }

                    changedMessages.Add(message);
                }
            }

            // TODO: Queue command to send change to the server
            if (changedMessages.Count > 0)
            {
                await GmailImap.SetReadStatusAsync(changedMessages, read);
            }
        }

        public Task SetStarAsync(MailMessage message, bool starred)
        {
            return SetStarAsync(new MailMessage[] { message }, starred);
        }

        public async Task SetStarAsync(IEnumerable<MailMessage> messages, bool starred)
        {
            messages = messages.Where(message => message.Flagged != starred).ToList();
            // Set or remove the Flagged flag.
            foreach (MailMessage message in messages)
            {
                message.Flagged = starred;
                if (MailStorage.HasMessageFlags(message.GetThreadId(), message.GetMessageId()))
                {
                    // Update on disk
                    await MailStorage.StoreMessageFlagsAsync(message);
                }
            }
            if (messages.Any())
            {
                // TODO: Queue command to send change to the server
                await GmailImap.SetFlaggedStatusAsync(messages, starred);
            }
        }

        // It's assumed that labelName is never the active label.
        public virtual async Task AddLabelAsync(List<MailMessage> messages, string labelName)
        {
            foreach (var message in messages)
            {
                // Store in memory
                message.AddLabel(labelName);
                if (MailStorage.HasMessageLables(message.GetThreadId(), message.GetMessageId()))
                {
                    // Update on disk
                    await MailStorage.StoreMessageLabelsAsync(message);
                }

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
                if (message.RemoveLabel(label) && MailStorage.HasMessageLables(message.GetThreadId(), message.GetMessageId()))
                {
                    // Store changes
                    await MailStorage.StoreMessageLabelsAsync(message);
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
                    if (MailStorage.HasMessageLables(message.GetThreadId(), message.GetMessageId()))
                    {
                        // Update on disk
                        await MailStorage.StoreMessageLabelsAsync(message);
                    }
                }
            }

            LabelInfo labelInfo = Labels.Where(info => info.Name.Equals(labelName)).FirstOrDefault() ?? new LabelInfo() { Name = labelName };

            // Look up UIDs. If they're not here, we may need to check online.
            List<string> localMessageIds = new List<string>(); // Ids for messages we have referenced from a locally sync'd label.
            List<string> nonlocalMessageIds = new List<string>(); // Ids for messages we'll have to lookup online.
            List<MessageIdInfo> labelMessageIds = (labelInfo.StoreMessages ? await MailStorage.GetLabelMessageListAsync(labelName) : null)
                ?? new List<MessageIdInfo>();

            SyncUtilities.CompareLists(changedMessages.Select(message => message.GetMessageId()), labelMessageIds.Select(ids => ids.MessageId), id => id,
                (searchId, localId) => localMessageIds.Add(localId),
                (searchId) => nonlocalMessageIds.Add(searchId),
                (localId) => { } // Only in storage, ignore.
                );

            // Remove from that labelList
            List<MessageIdInfo> updatedLabelMessageIds = labelMessageIds.Where(messageIds => !localMessageIds.Contains(messageIds.MessageId)).ToList();
            if (labelInfo.StoreMessages)
            {
                await MailStorage.StoreLabelMessageListAsync(labelName, updatedLabelMessageIds);
            }

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
                if (MailStorage.HasMessageLables(message.GetThreadId(), message.GetMessageId()))
                {
                    // Update on disk
                    await MailStorage.StoreMessageLabelsAsync(message);
                }
            }

            // TODO: Queue command to send change to the server
            await GmailImap.AddLabelAsync(messages, labelName);
        }

        public async Task SendMessageAsync(MailMessage message)
        {
            // We don't send mail very often, so we don't need to keep the SmtpClient instance around.
            using (GmailSmtpClient client = new GmailSmtpClient(Info.Address, Info.Password))
            {
                await client.SendAsync(message);
            }
        }

        public void DeleteAccountData()
        {
            MailStorage.DeleteAccount();
            ActiveLabel = null;
            Labels = null;
            ActiveConversation = null;
        }

        public async Task LogoutAsync()
        {
            await GmailImap.Client.LogoutAsync();
            GmailImap.Dispose();
            GmailImap = new GmailImapClient(Info.Address, Info.Password);
            ActiveLabel = null;
        }

        public async Task OpenAttachmentAsync(MailMessage message, Attachment attachment)
        {
            // Lazy load
            if (attachment.Scope < Scope.HeadersAndBody)
            {
                // Check local storage
                if (MailStorage.HasMessagePart(message.GetThreadId(), message.GetMessageId(), attachment.BodyId))
                {
                    // TODO: Can we open the attachment directly from isolated storage?
                    attachment.Body = await MailStorage.GetMessagePartAsync(message.GetThreadId(), message.GetMessageId(), attachment.BodyId);
                }
                else
                {
                    // Download from the network
                    await GmailImap.GetBodyPartAsync(message.Uid, attachment, async () =>
                    {
                        if (ActiveLabel.Info.StoreMessages && ActiveLabel.Info.StoreAttachments)
                        {
                            await MailStorage.StoreMessagePartAsync(message.GetThreadId(), message.GetMessageId(), attachment.BodyId, attachment.Body);
                        }
                    }, CancellationToken.None);
                }
            }

            StorageFile file = await MailStorage.SaveAttachmentToTempAsync(attachment);
            // http://architects.dzone.com/articles/lap-around-windows-phone-8-sdk
            await Launcher.LaunchFileAsync(file);

            // TODO: Delete temp files on app close?
        }
    }
}
