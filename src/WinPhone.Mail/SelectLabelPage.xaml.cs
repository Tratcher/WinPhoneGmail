using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using WinPhone.Mail.Resources;
using WinPhone.Mail.Storage;

namespace WinPhone.Mail
{
    public partial class SelectLabelPage : PhoneApplicationPage
    {
        public SelectLabelPage()
        {
            InitializeComponent();

            BuildLocalizedApplicationBar();
        }

        private void BuildLocalizedApplicationBar()
        {
            ApplicationBar = new ApplicationBar();

            ApplicationBarIconButton syncButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/sync.png", UriKind.Relative));
            syncButton.Text = AppResources.SyncButtonText;
            ApplicationBar.Buttons.Add(syncButton);
            syncButton.Click += ForceSync;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            GetAccounts();
            GetLabelsAsync();
        }

        private void ForceSync(object sender, EventArgs e)
        {
            GetLabelsAsync(forceSync: true);
        }

        private void GetAccounts()
        {
            var accounts = App.GetAccounts();
            if (accounts.Count < 2)
            {
               AccountList.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                AccountList.ItemsSource = accounts;
                AccountList.SelectedItem = App.GetCurrentAccount();
            }
        }

        private async void GetLabelsAsync(bool forceSync = false)
        {
            ProgressIndicator.IsIndeterminate = true;
            try
            {
                var account = App.GetCurrentAccount();
                if (account != null)
                {
                    List<LabelInfo> labels = await account.GetLabelsAsync(forceSync);
                    LabelList.ItemsSource = labels;
                }
                else
                {
                    LabelList.ItemsSource = null;
                }
            }
            finally
            {
                ProgressIndicator.IsIndeterminate = false;
            }
        }

        private void AccountList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            App.SetCurrentAccount((Account)AccountList.SelectedItem);
            GetLabelsAsync();
        }

        private async void LabelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ProgressIndicator.IsIndeterminate = true;
            try
            {
                var account = App.GetCurrentAccount();
                if (account != null)
                {
                    LabelInfo label = LabelList.SelectedItem as LabelInfo;
                    if (label != null)
                    {
                        await account.SelectLabelAsync(label);
                    }
                }
            }
            finally
            {
                ProgressIndicator.IsIndeterminate = false;
            }
            NavigationService.GoBack();
        }
    }
}