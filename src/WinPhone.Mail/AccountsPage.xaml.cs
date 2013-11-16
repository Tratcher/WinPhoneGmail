using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using WinPhone.Mail.Resources;
using WinPhone.Mail.Storage;

namespace WinPhone.Mail
{
    public partial class AccountsPage : PhoneApplicationPage
    {
        public AccountsPage()
        {
            InitializeComponent();

            BuildLocalizedApplicationBar();
        }

        private void BuildLocalizedApplicationBar()
        {
            ApplicationBar = new ApplicationBar();

            ApplicationBarIconButton newButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/new.png", UriKind.Relative));
            newButton.Text = AppResources.NewButtonText;
            ApplicationBar.Buttons.Add(newButton);
            newButton.Click += NewClick;

            ApplicationBarIconButton removeButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/delete.png", UriKind.Relative));
            removeButton.Text = AppResources.RemoveButtonText;
            ApplicationBar.Buttons.Add(removeButton);
            removeButton.Click += RemoveClick;

            ApplicationBarIconButton saveButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/save.png", UriKind.Relative));
            saveButton.Text = AppResources.SaveButtonText;
            ApplicationBar.Buttons.Add(saveButton);
            saveButton.Click += SaveClick;

            ApplicationBarIconButton doneButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/check.png", UriKind.Relative));
            doneButton.Text = AppResources.DoneButtonText;
            ApplicationBar.Buttons.Add(doneButton);
            doneButton.Click += DoneClick;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            AccountsList.ItemsSource = App.GetAccounts();
        }

        private void AccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Account account = AccountsList.SelectedItem as Account;
            if (account != null)
            {
                AccountAddressBox.Text = account.Info.Address;
                AccountPasswordBox.Password = account.Info.Password;
            }
        }

        private void NewClick(object sender, EventArgs e)
        {
            AccountsList.SelectedItem = null;

            AccountAddressBox.Text = "@gmail.com";
            AccountPasswordBox.Password = string.Empty;
        }

        private void RemoveClick(object sender, EventArgs e)
        {
            Account account = AccountsList.SelectedItem as Account;
            if (account != null)
            {
                var accounts = App.GetAccounts();
                accounts.Remove(account);
                App.SetCurrentAccount(accounts.FirstOrDefault());

                AccountsList.SelectedItem = null;

                AppSettings.SaveAccounts(accounts.Select(ac => ac.Info).ToArray());

                account.DeleteAccount();
            }
            AccountAddressBox.Text = "@gmail.com";
            AccountPasswordBox.Password = string.Empty;
        }

        private void SaveClick(object sender, EventArgs e)
        {
            var accounts = App.GetAccounts();
            Account account = AccountsList.SelectedItem as Account;
            if (account != null
                && !string.Equals(account.Info.Address, AccountAddressBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                account.DeleteAccount();
                accounts.Remove(account);
            }

            if (account != null)
            {
                // Let them change casing, it's only a visual change.
                account.Info.Address = AccountAddressBox.Text;
                // Update password in place.
                account.Info.Password = AccountPasswordBox.Password;
            }
            else
            {
                accounts.Add(new Account(new AccountInfo()
                {
                    Address = AccountAddressBox.Text,
                    Password = AccountPasswordBox.Password
                }));

                AccountsList.SelectedIndex = accounts.Count - 1;
            }

            AppSettings.SaveAccounts(accounts.Select(ac => ac.Info).ToArray());
        }

        private void DoneClick(object sender, EventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}