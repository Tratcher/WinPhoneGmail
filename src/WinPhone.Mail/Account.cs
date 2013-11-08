﻿using System;
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

            List<ConversationThread> conversations = await GmailImap.GetConversationsAsync();
            conversations.Reverse();

            // TODO: Reconcile vs stored, remove no longer referenced conversations.
            ActiveLabel.Conversations = conversations;

            // Write back to storage.
            await MailStorage.StoreLabelConversationListAsync(Info.Address, ActiveLabel.Info.Name, ActiveLabel.Conversations);
            // TODO: Only store conversations that have changed.
            await MailStorage.StoreConverationsAsync(Info.Address, ActiveLabel.Conversations);

            return ActiveLabel;
        }

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
            IOrderedEnumerable<LabelInfo> serverEnumerator = serverLabels.OrderBy(label => label.Name);
            IOrderedEnumerable<LabelInfo> clientEnumerator = Labels.OrderBy(label => label.Name);

            await CompareListsAsync<LabelInfo>(serverEnumerator, clientEnumerator,
                (serverLabel, clientLabel) => serverLabel.Name.CompareTo(clientLabel.Name),
                (serverLabel, clientLabel) => final.Add(clientLabel), // Match, client wins, it has client side settings.
                (serverLabel) => final.Add(serverLabel), // Server only, add.
                (clientLabel) => MailStorage.DeleteLabelAsync(clientLabel.Name)); // Client side only, garbage collect.

            Labels = final;

            // Save back to storage
            await MailStorage.SaveLabelsAsync(Info.Address, Labels);

            return Labels;
        }

        public static async Task CompareListsAsync<T>(IOrderedEnumerable<T> list1, IOrderedEnumerable<T> list2,
            Func<T, T, int> comparison, Action<T, T> match, Action<T> firstOnly, Func<T, Task> secondOnly)
        {
            IEnumerator<T> list1Enumerator = list1.GetEnumerator();
            IEnumerator<T> list2Enumerator = list2.GetEnumerator();

            bool moreInList1 = list1Enumerator.MoveNext();
            bool moreInList2 = list2Enumerator.MoveNext();
            T item1 = moreInList1 ? list1Enumerator.Current : default(T);
            T item2 = moreInList2 ? list2Enumerator.Current : default(T);

            while (moreInList1 && moreInList2)
            {
                int rank = comparison(item1, item2);
                if (rank == 0)
                {
                    match(item1, item2);
                    moreInList1 = list1Enumerator.MoveNext();
                    moreInList2 = list2Enumerator.MoveNext();
                    item1 = moreInList1 ? list1Enumerator.Current : default(T);
                    item2 = moreInList2 ? list2Enumerator.Current : default(T);
                }
                else if (rank < 0)
                {
                    // Found in the first but not in the second.
                    firstOnly(item1);
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
                firstOnly(item1);
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
