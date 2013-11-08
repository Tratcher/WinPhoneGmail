using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Imap;
using WinPhone.Mail.Storage;
using WinPhone.Mail.Resources;

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

        private async void ForceSync(object sender, EventArgs e)
        {
            // TODO: Progress, or at least a busy animation
            var account = App.GetCurrentAccount();
            if (account != null)
            {
                List<LabelInfo> labels = await account.GetLabelsAsync(forceSync: true);
                LabelList.ItemsSource = labels;
            }
            else
            {
                LabelList.ItemsSource = null;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var account = App.GetCurrentAccount();
            if (account != null)
            {
                List<LabelInfo> labels = await account.GetLabelsAsync();
                LabelList.ItemsSource = labels;
            }
            else
            {
                LabelList.ItemsSource = null;                
            }
        }

        private async void LabelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
            NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
        }
    }
}