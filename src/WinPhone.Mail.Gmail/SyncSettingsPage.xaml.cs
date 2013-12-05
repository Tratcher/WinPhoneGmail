using Microsoft.Phone.Controls;
using System;
using System.Windows.Controls;
using System.Windows.Navigation;
using WinPhone.Mail.Gmail.Shared;
using WinPhone.Mail.Gmail.Shared.Accounts;
using WinPhone.Mail.Gmail.Shared.Storage;

namespace WinPhone.Mail.Gmail
{
    public partial class SyncSettingsPage : PhoneApplicationPage
    {
        private TimeSpan[] Frequencies = new TimeSpan[]
        {
            Constants.Sync.AsItemsArrive, // Zero
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(12),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(7),
            Constants.Sync.Manual, // MaxValue
        };

        private static readonly int[] DayRanges = new int[]
        {
            1, 2, 3, 4, 5, 6, 7, 14, 21, 31, 61, 182, 365,
        };

        private bool _suppressChangeNotifications = true;

        public SyncSettingsPage()
        {
            InitializeComponent();

            freqNumbers.ItemsSource = Frequencies;
            rangeNumbers.ItemsSource = DayRanges;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            GetAccounts();
            _suppressChangeNotifications = false;
        }

        private void GetAccounts()
        {
            var accounts = App.AccountManager.Accounts;
            Account currentAccount = App.AccountManager.GetCurrentAccount();

            if (accounts.Count == 0)
            {
                return;
            }

            if (accounts.Count < 2)
            {
                AccountList.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                AccountList.ItemsSource = accounts;
                AccountList.SelectedItem = currentAccount;
            }

            ShowFrequency(currentAccount);
            ShowRange(currentAccount);
            ShowNotifications(currentAccount);
        }

        private void AccountList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Account currentAccount = (Account)AccountList.SelectedItem;
            App.AccountManager.SetCurrentAccount(currentAccount);
            _suppressChangeNotifications = true;
            ShowFrequency(currentAccount);
            ShowRange(currentAccount);
            ShowNotifications(currentAccount);
            _suppressChangeNotifications = false;
        }

        private void ShowFrequency(Account currentAccount)
        {
            freqNumbers.SelectedItem = currentAccount.Info.Frequency;
        }

        private void ShowRange(Account currentAccount)
        {
            rangeNumbers.SelectedItem = (int)currentAccount.Info.Range.TotalDays;
        }

        private void ShowNotifications(Account currentAccount)
        {
            switch (currentAccount.Info.Notifications)
            {
                case NotificationOptions.Always:
                    OptionNotifyEveryTime.IsChecked = true;
                    break;
                case NotificationOptions.FirstOnly:
                    OptionNotifyFirstTime.IsChecked = true;
                    break;
                case NotificationOptions.Never:
                    OptionNotifyNever.IsChecked = true;
                    break;
                default:
                    throw new NotImplementedException(currentAccount.Info.Notifications.ToString());
            }
        }

        private void freqNumbers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressChangeNotifications)
            {
                return;
            }

            Account currentAccount = App.AccountManager.GetCurrentAccount();
            currentAccount.Info.Frequency = (TimeSpan)freqNumbers.SelectedItem;
            App.AccountManager.SaveAccounts();
        }

        private void rangeNumbers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressChangeNotifications)
            {
                return;
            }

            Account currentAccount = App.AccountManager.GetCurrentAccount();
            currentAccount.Info.Range = TimeSpan.FromDays((int)rangeNumbers.SelectedItem);
            App.AccountManager.SaveAccounts();
        }

        private void OptionNotify_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_suppressChangeNotifications)
            {
                return;
            }

            Account currentAccount = App.AccountManager.GetCurrentAccount();
            if (OptionNotifyEveryTime.IsChecked == true)
            {
                currentAccount.Info.Notifications = NotificationOptions.Always;
            }
            else if (OptionNotifyFirstTime.IsChecked == true)
            {
                currentAccount.Info.Notifications = NotificationOptions.FirstOnly;
            }
            else if (OptionNotifyNever.IsChecked == true)
            {
                currentAccount.Info.Notifications = NotificationOptions.Never;
            }
            App.AccountManager.SaveAccounts();
        }
    }
}