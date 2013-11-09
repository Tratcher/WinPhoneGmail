using Microsoft.Phone.Controls;
using System;
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
            // AppSettings.ClearAll();
            MailStorage.ClearAll();
            // TODO: Prompt for success

            // TODO: Clear in-memory caches for accounts, labels, etc.
        }
    }
}