using Microsoft.Phone.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Navigation;
using WinPhone.Mail.Storage;

namespace WinPhone.Mail
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

            foreach (Account account in App.GetAccounts())
            {
                // TODO: Clear in-memory caches for accounts, labels, etc.
                account.DeleteAccount();
            }

            // Clear any leftover garbage.
            MailStorage.ClearAll();

            // TODO: Prompt for success
        }
    }
}