using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinPhone.Mail.Gmail.Shared.Storage;

namespace WinPhone.Mail.Gmail.Shared.Accounts
{
    public class AccountManager
    {
        public AccountManager()
        {
            AccountInfo[] accounts = AppSettings.GetAccounts();
            Accounts = new List<Account>();

            for (int i = 0; i < accounts.Length; i++)
            {
                Accounts.Add(new Account(accounts[i]));
            }
        }

        public IList<Account> Accounts { get; private set; }
        private int AccountIndex { get; set; }

        public Account GetCurrentAccount()
        {
            if (Accounts.Count == 0)
            {
#if DEBUG
                // TODO: Make sure to test with null;
                return DebugAccount.Current;
#else
                return null;
#endif
            }
            return Accounts[AccountIndex];
        }

        public void SetCurrentAccount(Account account)
        {
            if (account == null)
            {
                AccountIndex = 0;
            }
            else
            {
                AccountIndex = Accounts.IndexOf(account);
            }
        }

        public async Task LogoutAllAsync()
        {
            // TODO: Flush any pending IMAP/SMTP traffic.
            foreach (Account account in Accounts)
            {
                // This is a workaround to avoid hitting a connection closed error
                // next time we come back to the app.
                await account.LogoutAsync();
            }
        }

        public Task RemoveAccountAsync(Account account)
        {
            Accounts.Remove(account);
            AccountIndex = 0;

            SaveAccounts();

            account.DeleteAccountData();
            return account.LogoutAsync();
        }

        public void SaveAccounts()
        {
            AppSettings.SaveAccounts(Accounts.Select(ac => ac.Info).ToArray());
        }

        public void ResetMailCounts()
        {
            foreach (Account account in Accounts)
            {
                account.Info.NewMailCount = 0;
            }

            SaveAccounts();
        }

        // Called from the background to sync all mail accounts and labels.
        //
        // Algorithm: Favors new mail detection over local disk cleanup
        // For each account:
        //  For each Label:
        //   Determine if this label has completely sync'd within it's desired frequency.
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
        // For each account:
        //  Download & save queued text bodies, then html, then attachments
        // For each account:
        //  Remove anything from the local list that does not appear in the remote list. GC.
        public async Task<Tuple<int, bool>> SyncAllMailAsync(CancellationToken cancellationToken)
        {
            int newMailCount = 0;
            bool notify = false;
            foreach (Account account in Accounts)
            {
                // TODO: Per label frequency?
                if (account.Info.Frequency == Constants.Sync.Manual)
                {
                    continue;
                }

                int accountNewMail = 0;
                // TODO: Messages may be double counted across labels. Deduplicate by GUID?
                foreach (LabelInfo labelInfo in await account.GetLabelsAsync(forceSync: false))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (labelInfo.StoreMessages)
                    {
                        // Check the sync schedule to see if it's time to perform a sync
                        bool sync = account.Info.Frequency < DateTime.Now - labelInfo.LastSync;

                        if (sync)
                        {
                            await account.SelectLabelAsync(labelInfo);
                            accountNewMail += await account.SyncMessageHeadersAsync(cancellationToken);
                            await account.SyncMessageBodiesAsync(cancellationToken);
                            // TODO: Attachments are slow, do them in their own loop at the end.
                            await account.SyncAttachmentsAsync(cancellationToken);
                        }
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // TODO: Only do this if sync is called from the background?
                int accountPriorNewMail = account.Info.NewMailCount;
                account.Info.NewMailCount = newMailCount;
                SaveAccounts();

                if (account.Info.Notifications == NotificationOptions.FirstOnly
                    && accountPriorNewMail == 0 && accountNewMail > 0)
                {
                    notify = true;
                }
                // TODO: This check is inaccurate if we go on another system and read some messages and then receive more. (e.g. -2, +2)
                else if (account.Info.Notifications == NotificationOptions.Always
                    && accountNewMail > accountPriorNewMail)
                {
                    notify = true;
                }

                newMailCount += accountNewMail;

                await account.LogoutAsync();
            }

            return new Tuple<int, bool>(newMailCount, notify);
        }
    }
}
