﻿using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Navigation;
using WinPhone.Mail.Gmail.Resources;
using WinPhone.Mail.Gmail.Storage;

namespace WinPhone.Mail.Gmail
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

        private async void RemoveClick(object sender, EventArgs e)
        {
            Account account = AccountsList.SelectedItem as Account;
            if (account != null)
            {
                var accounts = App.GetAccounts();
                accounts.Remove(account);
                App.SetCurrentAccount(accounts.FirstOrDefault());

                AccountsList.SelectedItem = null;

                AppSettings.SaveAccounts(accounts.Select(ac => ac.Info).ToArray());

                account.DeleteAccountData();
                await account.LogoutAsync();
            }
            AccountAddressBox.Text = "@gmail.com";
            AccountPasswordBox.Password = string.Empty;
        }

        private async void SaveClick(object sender, EventArgs e)
        {
            var accounts = App.GetAccounts();
            Account account = AccountsList.SelectedItem as Account;
            if (account != null
                && !string.Equals(account.Info.Address, AccountAddressBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                account.DeleteAccountData();
                await account.LogoutAsync();
                accounts.Remove(account);
                account = null;
            }

            if (account != null)
            {
                // Let them change casing, it's only a visual change.
                account.Info.Address = AccountAddressBox.Text;
                // Update password in place.
                account.Info.Password = AccountPasswordBox.Password;
                await account.LogoutAsync();
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