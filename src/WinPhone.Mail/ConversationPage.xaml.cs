using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Resources;

namespace WinPhone.Mail
{
    public partial class ConversationPage : PhoneApplicationPage
    {
        private ConversationThread Conversation { get; set; }

        public ConversationPage()
        {
            InitializeComponent();

            BuildLocalizedApplicationBar();
        }

        private void BuildLocalizedApplicationBar()
        {
            ApplicationBar = new ApplicationBar();

            ApplicationBarIconButton markUnreadButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/save.png", UriKind.Relative));
            markUnreadButton.Text = AppResources.MarkUnreadText;
            ApplicationBar.Buttons.Add(markUnreadButton);
            markUnreadButton.Click += MarkUnreadClick;

            ApplicationBarIconButton labelsButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/folder.png", UriKind.Relative));
            labelsButton.Text = AppResources.LabelsButtonText;
            ApplicationBar.Buttons.Add(labelsButton);
            labelsButton.Click += EditLabelsClick;

            ApplicationBarIconButton archiveButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/download.png", UriKind.Relative));
            archiveButton.Text = AppResources.ArchiveButtonText;
            ApplicationBar.Buttons.Add(archiveButton);
            archiveButton.Click += ArchiveClick;

            ApplicationBarIconButton deleteButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/delete.png", UriKind.Relative));
            deleteButton.Text = AppResources.DeleteButtonText;
            ApplicationBar.Buttons.Add(deleteButton);
            deleteButton.Click += DeleteClick;

            ApplicationBarMenuItem spamMenuItem = new ApplicationBarMenuItem(AppResources.SpamButtonText);
            ApplicationBar.MenuItems.Add(spamMenuItem);
            spamMenuItem.Click += SpamClick;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Account account = App.GetCurrentAccount();
            if (account != null)
            {
                Conversation = account.ActiveConversation;
                DataContext = Conversation;
            }

            base.OnNavigatedTo(e);
        }

        private void MessageHeader_Tap(object sender, GestureEventArgs e)
        {
            StackPanel panel = (StackPanel)sender;
            TextBlock bodyField = panel.Children[1] as TextBlock;
            if (bodyField.Visibility == System.Windows.Visibility.Collapsed)
            {
                bodyField.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                bodyField.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private async void MarkUnreadClick(object sender, EventArgs e)
        {
            Account account = App.GetCurrentAccount();
            await account.SetReadStatusAsync(Conversation.Messages, read: false);
            NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
        }

        private void EditLabelsClick(object sender, EventArgs e)
        {
            // throw new NotImplementedException();
        }

        private void ArchiveClick(object sender, EventArgs e)
        {
            // throw new NotImplementedException();
        }

        private void DeleteClick(object sender, EventArgs e)
        {
            // throw new NotImplementedException();
        }

        private void SpamClick(object sender, EventArgs e)
        {
            // throw new NotImplementedException();
        }
    }
}