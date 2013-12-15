using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using WinPhone.Mail.Gmail.Resources;
using WinPhone.Mail.Gmail.Shared.Accounts;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail.Gmail
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

            // TODO: Hide if this conversation is not in the inbox?
            ApplicationBarIconButton archiveButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/download.png", UriKind.Relative));
            archiveButton.Text = AppResources.ArchiveButtonText;
            ApplicationBar.Buttons.Add(archiveButton);
            archiveButton.Click += ArchiveClick;

            ApplicationBarIconButton replyAllButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.reply.email.png", UriKind.Relative));
            replyAllButton.Text = AppResources.ReplyAllButtonText;
            ApplicationBar.Buttons.Add(replyAllButton);
            replyAllButton.Click += ReplyAll;

            ApplicationBarMenuItem replyMenuItem = new ApplicationBarMenuItem(AppResources.ReplyButtonText);
            ApplicationBar.MenuItems.Add(replyMenuItem);
            replyMenuItem.Click += Reply;

            ApplicationBarMenuItem forwardMenuItem = new ApplicationBarMenuItem(AppResources.ForwardButtonText);
            ApplicationBar.MenuItems.Add(forwardMenuItem);
            forwardMenuItem.Click += Forward;

            // TODO: Change to 'delete' in the Trash folder?
            ApplicationBarMenuItem trashMenuItem = new ApplicationBarMenuItem(AppResources.TrashButtonText);
            ApplicationBar.MenuItems.Add(trashMenuItem);
            trashMenuItem.Click += TrashClick;

            // TODO: Hide when in the spam folder?
            ApplicationBarMenuItem spamMenuItem = new ApplicationBarMenuItem(AppResources.SpamButtonText);
            ApplicationBar.MenuItems.Add(spamMenuItem);
            spamMenuItem.Click += SpamClick;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Account account = App.AccountManager.GetCurrentAccount();
            if (account != null)
            {
                Conversation = account.ActiveConversation;
                DataContext = null; // Force refresh after editing labels
                DataContext = Conversation;
            }
            base.OnNavigatedTo(e);
        }

        private async void MessageHeader_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            StackPanel panel = (StackPanel)sender;
            Point point = e.GetPosition(panel);

            // For clicks on the right side, apply them to the star.
            if (point.X > (panel.ActualWidth * 0.8))
            {
                // Add/remove star
                MailMessage message = (MailMessage)panel.DataContext;

                Account account = App.AccountManager.GetCurrentAccount();
                await account.SetStarAsync(message, starred: !message.Flagged);

                // Refresh the UI
                panel.DataContext = null;
                panel.DataContext = message;
            }
            else
            {
                WebBrowser bodyField = (WebBrowser)panel.Children[1];
                ListBox attachmentsFieled = (ListBox)panel.Children[2];
                if (bodyField.Visibility == System.Windows.Visibility.Collapsed)
                {
                    bodyField.Visibility = System.Windows.Visibility.Visible;
                    attachmentsFieled.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    bodyField.Visibility = System.Windows.Visibility.Collapsed;
                    attachmentsFieled.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }

        private async void MarkUnreadClick(object sender, EventArgs e)
        {
            Account account = App.AccountManager.GetCurrentAccount();
            await account.SetReadStatusAsync(Conversation.Messages, read: false);
            NavigationService.GoBack();
        }

        private void EditLabelsClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/EditMessageLabelsPage.xaml", UriKind.Relative));
        }

        private async void ArchiveClick(object sender, EventArgs e)
        {
            Account account = App.AccountManager.GetCurrentAccount();
            await account.RemoveLabelAsync(Conversation.Messages, GConstants.Inbox);
            NavigationService.GoBack();
        }

        private async void TrashClick(object sender, EventArgs e)
        {
            // TODO: Full delete items already in Trash or Spam?
            Account account = App.AccountManager.GetCurrentAccount();
            await account.TrashAsync(Conversation.Messages, isSpam: false);
            NavigationService.GoBack();
        }

        private async void SpamClick(object sender, EventArgs e)
        {
            // TODO: Full delete items already in Trash or Spam?
            Account account = App.AccountManager.GetCurrentAccount();
            await account.TrashAsync(Conversation.Messages, isSpam: true);
            NavigationService.GoBack();
        }

        private void MessageView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            StackPanel panel = (StackPanel)sender;
            WebBrowser browser = (WebBrowser)panel.Children[1];
            MailMessage message = (MailMessage)panel.DataContext;

            // TODO: Check for alternate views, prefer HTML.

            // TODO: Resize the browser to fit the content?
            // http://dan.clarke.name/2011/05/resizing-wp7-webbrowser-height-to-fit-content/

            ObjectWHeaders view = message.GetHtmlView() ?? message.GetTextView() ?? message;

            string body = view.Body ?? "Unable to load body.";
            // Content-type detection.
            if (string.IsNullOrEmpty(view.ContentType) || view.ContentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
            {
                body = body.Replace("\r\n", "<br>");
            }

            int end = body.IndexOf(">");
            if (body.TrimStart('\r', '\n', '\t', ' ').StartsWith("<!DOCTYPE") && end > 0)
            {
                // Strip off the DOCTYPE: http://www.w3schools.com/tags/tag_doctype.asp
                body = body.Substring(end + 1);
            }

            browser.NavigateToString(body);
        }

        private async void AttachmentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox attachmentsList = (ListBox)sender;
            Attachment attachment = (Attachment)attachmentsList.SelectedItem;
            FrameworkElement panel = (FrameworkElement)attachmentsList.Parent;
            MailMessage message = (MailMessage)panel.DataContext;
            if (attachment == null)
            {
                return;
            }

            Account account = App.AccountManager.GetCurrentAccount();
            await account.OpenAttachmentAsync(message, attachment);
        }

        // Force links to open in the normal browser rather than inline.
        private void BodyField_Navigating(object sender, NavigatingEventArgs e)
        {
            e.Cancel = true;
            WebBrowserTask task = new WebBrowserTask();
            task.Uri = e.Uri;
            task.Show();
        }

        private void ReplyAll(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/ComposePage.xaml?ReplyAll", UriKind.Relative));
        }

        private void Reply(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/ComposePage.xaml?Reply", UriKind.Relative));
        }

        private void Forward(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/ComposePage.xaml?Forward", UriKind.Relative));
        }
    }
}