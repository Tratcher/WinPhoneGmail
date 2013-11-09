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
        }

        public AccountInfo Info { get; private set; }

        public GmailImapClient GmailImap { get; private set; }

        public List<LabelInfo> Labels { get; private set; }

        public Label ActiveLabel { get; private set; }

        public ConversationThread ActiveConversation { get; private set; }

        public virtual async Task<List<LabelInfo>> GetLabelsAsync(bool forceSync = false)
        {
            // From memory
            if (!forceSync && Labels != null && Labels.Count != 0)
            {
                return Labels;
            }

            // From disk
            Labels = await MailStorage.GetLabelsAsync(Info.Address) ?? new List<LabelInfo>();

            if (!forceSync && Labels.Count != 0)
            {
                return Labels;
            }

            // Sync with server
            List<LabelInfo> final = new List<LabelInfo>();
            Mailbox[] serverMailboxes = await GmailImap.GetLabelsAsync();
            IEnumerable<LabelInfo> serverLabels = serverMailboxes.Select(box => new LabelInfo() { Name = box.Name });

            await CompareListsAsync<LabelInfo>(serverLabels, Labels,
                label => label.Name, // Select key
                async (serverLabel, clientLabel) => final.Add(clientLabel), // Match, client wins, it has client side settings.
                async (serverLabel) => final.Add(serverLabel), // Server only, add.
                (clientLabel) => MailStorage.DeleteLabelAsync(clientLabel.Name)); // Client side only, garbage collect.

            Labels = final;

            // Save back to storage
            await MailStorage.SaveLabelsAsync(Info.Address, Labels);

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
                    Info = labels.Where(info => info.Name.Equals("INBOX")).First()
                };
            }

            if (ActiveLabel.Conversations == null)
            {
                // From disk
                ActiveLabel.Conversations = await MailStorage.GetConversationsAsync(Info.Address, ActiveLabel.Info.Name);
            }

            if (!forceSync && ActiveLabel.Conversations != null)
            {
                return ActiveLabel;
            }

            bool headersOnly = (ActiveLabel.Conversations != null && ActiveLabel.Conversations.Count != 0); // Optimize for the first download.
            List<ConversationThread> serverConversations = await GmailImap.GetConversationsAsync(headersOnly);

            ActiveLabel.Conversations = await ReconcileConversationsAsync(serverConversations, ActiveLabel.Conversations ?? new List<ConversationThread>());

            // Write back to storage.
            await MailStorage.StoreLabelConversationListAsync(Info.Address, ActiveLabel.Info.Name, ActiveLabel.Conversations);
            // TODO: Only store conversations that have changed.
            await MailStorage.StoreConverationsAsync(Info.Address, ActiveLabel.Conversations);

            return ActiveLabel;
        }

        private async Task<List<ConversationThread>> ReconcileConversationsAsync(List<ConversationThread> serverConversations, List<ConversationThread> clientConversations)
        {
            // TODO: Reconcile vs stored, remove no longer referenced conversations.
            List<ConversationThread> reconciledConversations = new List<ConversationThread>();
            await CompareListsAsync(serverConversations, clientConversations,
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
            await CompareListsAsync(serverThread.Messages, clientThread.Messages,
                message => message.GetMessageId(), // Selector
                async (serverMessage, clientMessage) => // Match
                {
                    if (serverMessage.HeadersOnly)
                    {
                        // Overwrite the local headers that may have been update on the server.
                        // Flags and labels are the primary things we expect to change.
                        clientMessage.Flags = serverMessage.Flags;
                        clientMessage.Headers["X-GM-LABELS"] = serverMessage.Headers["X-GM-LABELS"];
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

        public static async Task CompareListsAsync<T>(IEnumerable<T> list1, IEnumerable<T> list2,
            Func<T, string> selector, Func<T, T, Task> match, Func<T, Task> firstOnly, Func<T, Task> secondOnly)
        {
            IOrderedEnumerable<T> orderedList1 = list1.OrderBy(selector);
            IOrderedEnumerable<T> orderedList2 = list2.OrderBy(selector);

            IEnumerator<T> list1Enumerator = orderedList1.GetEnumerator();
            IEnumerator<T> list2Enumerator = orderedList2.GetEnumerator();

            bool moreInList1 = list1Enumerator.MoveNext();
            bool moreInList2 = list2Enumerator.MoveNext();
            T item1 = moreInList1 ? list1Enumerator.Current : default(T);
            T item2 = moreInList2 ? list2Enumerator.Current : default(T);

            while (moreInList1 && moreInList2)
            {
                int rank = selector(item1).CompareTo(selector(item2));
                if (rank == 0)
                {
                    await match(item1, item2);
                    moreInList1 = list1Enumerator.MoveNext();
                    moreInList2 = list2Enumerator.MoveNext();
                    item1 = moreInList1 ? list1Enumerator.Current : default(T);
                    item2 = moreInList2 ? list2Enumerator.Current : default(T);
                }
                else if (rank < 0)
                {
                    // Found in the first but not in the second.
                    await firstOnly(item1);
                    moreInList1 = list1Enumerator.MoveNext();
                    item1 = moreInList1 ? list1Enumerator.Current : default(T);
                }
                else
                {
                    // Found in the second list but not the first.
                    await secondOnly(item2);
                    moreInList2 = list2Enumerator.MoveNext();
                    item2 = moreInList2 ? list2Enumerator.Current : default(T);
                }
            }

            while (moreInList1)
            {
                await firstOnly(item1);
                moreInList1 = list1Enumerator.MoveNext();
                item1 = moreInList1 ? list1Enumerator.Current : default(T);
            }

            while (moreInList2)
            {
                await secondOnly(item2);
                moreInList2 = list2Enumerator.MoveNext();
                item2 = moreInList2 ? list2Enumerator.Current : default(T);
            }
        }

        public virtual Task SelectLabelAsync(LabelInfo label)
        {
            ActiveLabel = new Label() { Info = label };

            return GmailImap.SelectLabelAsync(label.Name);
        }

        public virtual Task SelectConversationAsync(ConversationThread conversation)
        {
            // TODO: Sync full conversation body, from disk or network.
            ActiveConversation = conversation;
            return Task.FromResult(0);
        }
    }
}
