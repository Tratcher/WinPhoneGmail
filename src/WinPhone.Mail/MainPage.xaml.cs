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
            /*
            // Create a new menu item with the localized string from AppResources.
            ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
            ApplicationBar.MenuItems.Add(appBarMenuItem);
            */
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
            try
            {
                // todo: Progress bar
                var account = App.GetCurrentAccount();
                if (account != null)
                {
                    Label label = await account.GetLabelAsync(forceSync);
                    CurrentLabel.Text = label.Info.Name;

                    DataContext = label; // TODO: Bind direct to this so we don't have to set things like CurrentLabel.Text ourselves
                    MailList.ItemsSource = label.Conversations;
                }
                else
                {
                    CurrentLabel.Text = string.Empty;
                    MailList.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
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

        private void WriteLine(string value)
        {
            Output.Text += value + "\r\n";
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
    }
}