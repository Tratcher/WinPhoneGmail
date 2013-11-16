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
using WinPhone.Mail.Protocols;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Storage;
using System.Windows.Media.Imaging;

namespace WinPhone.Mail
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
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
            syncButton.Click += Sync;

            ApplicationBarIconButton labelsButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/folder.png", UriKind.Relative));
            labelsButton.Text = AppResources.LabelsButtonText;
            ApplicationBar.Buttons.Add(labelsButton);
            labelsButton.Click += SelectLabel;

            ApplicationBarIconButton settingsButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/feature.settings.png", UriKind.Relative));
            settingsButton.Text = AppResources.SettingsButtonText;
            ApplicationBar.Buttons.Add(settingsButton);
            settingsButton.Click += SettingsClick;

            ApplicationBarMenuItem labelSettingsMenuItem = new ApplicationBarMenuItem(AppResources.LabelSettingsText);
            ApplicationBar.MenuItems.Add(labelSettingsMenuItem);
            labelSettingsMenuItem.Click += LabelSettings_Click;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            GetConversations();
        }

        private void Sync(object sender, EventArgs e)
        {
            GetConversations(forceSync: true);
        }

        private async void GetConversations(bool forceSync = false)
        {
            ProgressIndicator.IsIndeterminate = true;
            try
            {
                // Force a binding refresh
                DataContext = null;
                MailList.ItemsSource = null;
                MailList.SelectedIndex = -1;

                var account = App.GetCurrentAccount();
                if (account != null)
                {
                    Label label = await account.GetLabelAsync(forceSync);

                    DataContext = label;
                    MailList.ItemsSource = label.Conversations;

                    SyncIcon.Source = label.Info.Store ? null : new BitmapImage(new Uri("/Assets/AppBar/not.png", UriKind.Relative));
                }
            }
            finally
            {
                ProgressIndicator.IsIndeterminate = false;
            }
        }

        private void SelectLabel(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SelectLabelPage.xaml", UriKind.Relative));
        }

        private void SettingsClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private async void MailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConversationThread conversation = (ConversationThread)MailList.SelectedItem;
            if (conversation != null)
            {
                Account account = App.GetCurrentAccount();
                if (account != null)
                {
                    await account.SelectConversationAsync(conversation);
                    NavigationService.Navigate(new Uri("/ConversationPage.xaml", UriKind.Relative));
                }
            }
        }

        void LabelSettings_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/LabelSettingsPage.xaml", UriKind.Relative));
        }

        private async void SyncIcon_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Account account = App.GetCurrentAccount();
            if (account != null)
            {
                Label label = account.ActiveLabel;
                if (!label.Info.Store)
                {
                    label.Info.Store = true;

                    await account.SaveLabelSettingsAsync();
                    SyncIcon.Source = null;

                    // Store any locally sync'd mail
                    if (label.Conversations != null)
                    {
                        await account.MailStorage.StoreLabelMessageListAsync(label.Info.Name, label.Conversations);
                        await account.MailStorage.StoreConverationsAsync(label.Conversations);
                    }
                    else
                    {
                        GetConversations();
                    }
                }
            }
        }
    }
}