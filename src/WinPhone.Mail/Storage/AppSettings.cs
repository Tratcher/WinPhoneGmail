using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Storage
{
    public class AppSettings
    {
        private const string AccountsKey = "Accounts";

        public static AccountInfo[] GetAccounts()
        {
            try
            {
                AccountInfo[] accounts;
                if (IsolatedStorageSettings.ApplicationSettings.TryGetValue(AccountsKey, out accounts))
                {
                    return accounts;
                }
            }
            catch (IsolatedStorageException)
            {
            }
            return new AccountInfo[0];
        }

        public static void SaveAccounts(AccountInfo[] accounts)
        {
            IsolatedStorageSettings.ApplicationSettings[AccountsKey] = accounts;
            IsolatedStorageSettings.ApplicationSettings.Save();
        }
    }
}
