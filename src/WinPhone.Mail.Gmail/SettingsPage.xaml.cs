using Microsoft.Phone.Controls;
using System;
using System.Windows;
using System.Windows.Navigation;
using WinPhone.Mail.Gmail.Shared.Accounts;
using WinPhone.Mail.Gmail.Shared.Storage;

namespace WinPhone.Mail.Gmail
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        private void AccountsClick(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Uri("/AccountsPage.xaml", UriKind.Relative));
        }

        // Burn it all
        private void ClearAllDataClick(object sender, RoutedEventArgs e)
        {
            // TODO: Prompt for confirmation
            // TODO: Error handling/reporting?

            // Clear usernames and passwords.
            // AppSettings.ClearAll();

            foreach (Account account in App.AccountManager.Accounts)
            {
                // TODO: Clear in-memory caches for accounts, labels, etc.
                account.DeleteAccountData();
            }

            // Clear any leftover garbage.
            MailStorage.ClearAll();

            // TODO: Prompt for success
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Uri("/AboutPage.xaml", UriKind.Relative));
        }
    }
}