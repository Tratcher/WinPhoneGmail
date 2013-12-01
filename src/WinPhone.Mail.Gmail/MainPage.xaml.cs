using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using WinPhone.Mail.Gmail.Resources;
using WinPhone.Mail.Gmail.Shared;
using WinPhone.Mail.Gmail.Shared.Accounts;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail.Gmail
{
    public partial class MainPage : PhoneApplicationPage
    {
        private ApplicationBarIconButton _composeButton;
        private ApplicationBarIconButton _syncButton;
        private ApplicationBarIconButton _labelsButton;
        private ApplicationBarIconButton _settingsButton;
        private ApplicationBarMenuItem _labelSettingsMenuItem;

        private ApplicationBarIconButton _markUnreadButton;
        private ApplicationBarIconButton _markAsReadButton;
        private ApplicationBarIconButton _editLabelsButton;
        private ApplicationBarIconButton _archiveButton;
        private ApplicationBarMenuItem _trashMenuItem;
        private ApplicationBarMenuItem _spamMenuItem;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            BuildLocalizedApplicationBar();

            PopulateApplicationBar();
        }

        private void BuildLocalizedApplicationBar()
        {
            ApplicationBar = new ApplicationBar();

            // Default view:
            _composeButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/new.png", UriKind.Relative));
            _composeButton.Text = AppResources.SyncButtonText;
            _composeButton.Click += Compose;

            _syncButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/sync.png", UriKind.Relative));
            _syncButton.Text = AppResources.SyncButtonText;
            _syncButton.Click += Sync;

            _labelsButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/folder.png", UriKind.Relative));
            _labelsButton.Text = AppResources.LabelsButtonText;
            _labelsButton.Click += SelectLabel;

            _settingsButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/feature.settings.png", UriKind.Relative));
            _settingsButton.Text = AppResources.SettingsButtonText;
            _settingsButton.Click += SettingsClick;

            _labelSettingsMenuItem = new ApplicationBarMenuItem(AppResources.LabelSettingsText);
            _labelSettingsMenuItem.Click += LabelSettings_Click;

            // Email select view:
            _markUnreadButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/save.png", UriKind.Relative));
            _markUnreadButton.Text = AppResources.MarkUnreadText;
            _markUnreadButton.Click += MarkUnreadClick;

            // TODO: Mark as read icon
            _markAsReadButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/cancel.png", UriKind.Relative));
            _markAsReadButton.Text = AppResources.MarkAsReadText;
            _markAsReadButton.Click += MarkAsReadClick;

            _editLabelsButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/folder.png", UriKind.Relative));
            _editLabelsButton.Text = AppResources.LabelsButtonText;
            _editLabelsButton.Click += EditLabelsClick;

            // TODO: Hide if this conversation is not in the inbox?
            _archiveButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/download.png", UriKind.Relative));
            _archiveButton.Text = AppResources.ArchiveButtonText;
            _archiveButton.Click += ArchiveClick;

            // TODO: Star/Unstar

            // TODO: Change to 'delete' in the Trash folder?
            _trashMenuItem = new ApplicationBarMenuItem(AppResources.TrashButtonText);
            _trashMenuItem.Click += TrashClick;

            // TODO: Hide when in the spam folder?
            _spamMenuItem = new ApplicationBarMenuItem(AppResources.SpamButtonText);
            _spamMenuItem.Click += SpamClick;

            // TODO: Select All
        }

        private void PopulateApplicationBar()
        {
            ApplicationBar.Buttons.Clear();
            ApplicationBar.MenuItems.Clear();

            if (MailList.IsSelectionEnabled)
            {
                ApplicationBar.Buttons.Add(_markUnreadButton);
                ApplicationBar.Buttons.Add(_markAsReadButton);
                ApplicationBar.Buttons.Add(_editLabelsButton);
                ApplicationBar.Buttons.Add(_archiveButton);
                ApplicationBar.MenuItems.Add(_trashMenuItem);
                ApplicationBar.MenuItems.Add(_spamMenuItem);
            }
            else
            {
                ApplicationBar.Buttons.Add(_composeButton);
                ApplicationBar.Buttons.Add(_syncButton);
                ApplicationBar.Buttons.Add(_labelsButton);
                ApplicationBar.Buttons.Add(_settingsButton);
                ApplicationBar.MenuItems.Add(_labelSettingsMenuItem);
            }
        }

        private void MailList_IsSelectionEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            PopulateApplicationBar();
        }

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            if (MailList.IsSelectionEnabled)
            {
                MailList.IsSelectionEnabled = false;
                e.Cancel = true;
            }

            base.OnBackKeyPress(e);
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

                var account = App.AccountManager.GetCurrentAccount();
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

        private void Compose(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/ComposePage.xaml", UriKind.Relative));
        }

        private void SelectLabel(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SelectLabelPage.xaml", UriKind.Relative));
        }

        private void SettingsClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private async void ConversationHeader_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            FrameworkElement grid = (FrameworkElement)sender;
            ConversationThread conversation = (ConversationThread)grid.DataContext;

            if (conversation != null)
            {
                Account account = App.AccountManager.GetCurrentAccount();
                if (account != null)
                {
                    await account.SelectConversationAsync(conversation);
                    NavigationService.Navigate(new Uri("/ConversationPage.xaml", UriKind.Relative));
                }
            }
        }

        // The time and star secton on the right side.
        private async void ConversationInfoHeader_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            FrameworkElement grid = (FrameworkElement)sender;
            ConversationThread conversation = (ConversationThread)grid.DataContext;

            ProgressIndicator.IsIndeterminate = true;
            try
            {
                if (conversation != null)
                {
                    Account account = App.AccountManager.GetCurrentAccount();
                    if (account != null)
                    {
                        if (conversation.HasStar)
                        {
                            // Remove the star from any starred messages
                            await account.SetStarAsync(conversation.Messages, starred: false);
                        }
                        else
                        {
                            // Add a star to the latest message
                            await account.SetStarAsync(conversation.Messages.First(), starred: true);
                        }

                        // Refresh the UI
                        grid.DataContext = null;
                        grid.DataContext = conversation;
                    }
                }
            }
            finally
            {
                ProgressIndicator.IsIndeterminate = false;
            }
        }

        void LabelSettings_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/LabelSettingsPage.xaml", UriKind.Relative));
        }

        private async void SyncIcon_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Account account = App.AccountManager.GetCurrentAccount();
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

        private void MarkUnreadClick(object sender, EventArgs e)
        {
            SetReadStatus(false);
        }

        private void MarkAsReadClick(object sender, EventArgs e)
        {
            SetReadStatus(true);
        }

        private async void SetReadStatus(bool readStatus)
        {
            ProgressIndicator.IsIndeterminate = true;
            try
            {
                IEnumerable<MailMessage> messages = new MailMessage[0];
                foreach (ConversationThread conversation in MailList.SelectedItems)
                {
                    messages = messages.Concat(conversation.Messages);
                }
                messages = messages.Where(message => message.Seen != readStatus);
                if (messages.Any())
                {
                    Account account = App.AccountManager.GetCurrentAccount();
                    await account.SetReadStatusAsync(messages.ToList(), read: readStatus);

                    // Force Refresh
                    var temp = MailList.ItemsSource;
                    MailList.ItemsSource = null;
                    MailList.ItemsSource = temp;
                }
            }
            finally
            {
                MailList.IsSelectionEnabled = false;
                ProgressIndicator.IsIndeterminate = false;
            }
        }

        private void EditLabelsClick(object sender, EventArgs e)
        {
            throw new NotImplementedException();
            /*
            NavigationService.Navigate(new Uri("/EditMessageLabelsPage.xaml", UriKind.Relative));
                MailList.IsSelectionEnabled = false;
            */
        }

        private async void ArchiveClick(object sender, EventArgs e)
        {
            ProgressIndicator.IsIndeterminate = true;
            try
            {
                IEnumerable<MailMessage> messages = new MailMessage[0];
                foreach (ConversationThread conversation in MailList.SelectedItems)
                {
                    messages = messages.Concat(conversation.Messages);
                }
                if (messages.Any())
                {
                    Account account = App.AccountManager.GetCurrentAccount();
                    await account.RemoveLabelAsync(messages.ToList(), GConstants.Inbox);

                    // Force Refresh
                    GetConversations();
                }
            }
            finally
            {
                MailList.IsSelectionEnabled = false;
                ProgressIndicator.IsIndeterminate = false;
            }
        }

        // TODO: Special case Trash folder? Perminately delete.
        // TODO: Remove from current label, storage.
        private async void TrashClick(object sender, EventArgs e)
        {
            ProgressIndicator.IsIndeterminate = true;
            try
            {
                IEnumerable<MailMessage> messages = new MailMessage[0];
                foreach (ConversationThread conversation in MailList.SelectedItems)
                {
                    messages = messages.Concat(conversation.Messages);
                }
                if (messages.Any())
                {
                    Account account = App.AccountManager.GetCurrentAccount();
                    await account.TrashAsync(messages.ToList(), isSpam: false);

                    // Force Refresh
                    GetConversations();
                }
            }
            finally
            {
                MailList.IsSelectionEnabled = false;
                ProgressIndicator.IsIndeterminate = false;
            }
        }

        // TODO: Special case Spam folder? Perminately delete.
        // TODO: Remove from current label, storage.
        private async void SpamClick(object sender, EventArgs e)
        {
            ProgressIndicator.IsIndeterminate = true;
            try
            {
                IEnumerable<MailMessage> messages = new MailMessage[0];
                foreach (ConversationThread conversation in MailList.SelectedItems)
                {
                    messages = messages.Concat(conversation.Messages);
                }
                if (messages.Any())
                {
                    Account account = App.AccountManager.GetCurrentAccount();
                    await account.TrashAsync(messages.ToList(), isSpam: true);

                    // Force Refresh
                    GetConversations();
                }
            }
            finally
            {
                MailList.IsSelectionEnabled = false;
                ProgressIndicator.IsIndeterminate = false;
            }
        }
    }
}