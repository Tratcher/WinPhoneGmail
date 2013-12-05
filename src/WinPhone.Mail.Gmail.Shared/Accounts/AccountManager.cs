using System.Linq;
using System.Collections.Generic;
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
    }
}
