using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Gmail.Shared.Storage
{
    public class AppSettings
    {
        private const string AccountsKey = "Accounts";
        private const string ActivationTimeKey = "LastAppActivationTime";

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

        public static DateTime LastAppActivationTime
        {
            get
            {
                try
                {
                    DateTime value;
                    if (IsolatedStorageSettings.ApplicationSettings.TryGetValue(ActivationTimeKey, out value))
                    {
                        return value;
                    }
                }
                catch (IsolatedStorageException)
                {
                }
                return DateTime.MinValue; // Never?
            }
            set
            {
                IsolatedStorageSettings.ApplicationSettings[ActivationTimeKey] = value;
                IsolatedStorageSettings.ApplicationSettings.Save();
            }
        }

        public static void SaveAccounts(AccountInfo[] accounts)
        {
            IsolatedStorageSettings.ApplicationSettings[AccountsKey] = accounts;
            IsolatedStorageSettings.ApplicationSettings.Save();
        }

        public static void ClearAll()
        {
            IsolatedStorageSettings.ApplicationSettings.Clear();
            IsolatedStorageSettings.ApplicationSettings.Save();
        }
    }
}
