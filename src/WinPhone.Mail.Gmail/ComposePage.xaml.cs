using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Navigation;
using WinPhone.Mail.Gmail.Resources;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail.Gmail
{
    public partial class ComposePage : PhoneApplicationPage
    {
        private List<KeyValuePair<string, string>> _additionalHeaders = new List<KeyValuePair<string, string>>();

        public ComposePage()
        {
            InitializeComponent();

            BuildLocalizedApplicationBar();
        }

        private void BuildLocalizedApplicationBar()
        {
            ApplicationBar = new ApplicationBar();

            ApplicationBarIconButton sendButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/feature.email.png", UriKind.Relative));
            sendButton.Text = AppResources.SendButtonText;
            ApplicationBar.Buttons.Add(sendButton);
            sendButton.Click += Send;

            // TODO: Need paperclip icon
            ApplicationBarIconButton attachButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/attach.png", UriKind.Relative));
            attachButton.Text = AppResources.AttachButtonText;
            ApplicationBar.Buttons.Add(attachButton);
            attachButton.Click += Attach;

            ApplicationBarIconButton discardButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/cancel.png", UriKind.Relative));
            discardButton.Text = AppResources.DiscardButtonText;
            ApplicationBar.Buttons.Add(discardButton);
            discardButton.Click += Discard;

            ApplicationBarIconButton dictateButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/microphone.png", UriKind.Relative));
            dictateButton.Text = AppResources.DictateButtonText;
            ApplicationBar.Buttons.Add(dictateButton);
            dictateButton.Click += Dictate;

            ApplicationBarMenuItem labelMenuItem = new ApplicationBarMenuItem(AppResources.LabelItemText);
            ApplicationBar.MenuItems.Add(labelMenuItem);
            labelMenuItem.Click += LabelClick;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // The query parameter is used to tell us if we're in a reply mode.
            string[] parts = e.Uri.ToString().Split('?');
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            {
                string query = parts[1];
                Account account = App.GetCurrentAccount();
                ConversationThread mailThread = account.ActiveConversation;
                MailMessage lastMessage = mailThread.Messages.First(); // They're in reverse order.

                if (query.Equals("Forward"))
                {
                    // No addresses
                    // TODO: Prefix subject
                    SubjectField.Text = lastMessage.Subject;
                }
                else if (query.Equals("ReplyAll"))
                {
                    // Include all addresses
                    if (lastMessage.ReplyTo.Count > 0)
                    {
                        ToField.Text = string.Join(", ", lastMessage.ReplyTo);
                    }
                    else
                    {
                        ToField.Text = string.Join(", ", new[] { lastMessage.From }.Concat(
                            lastMessage.To.Where(mailAddress =>
                                // Filter yourself out of the to line, unless you explicitly sent the e-mail.
                                !mailAddress.Address.Equals(account.Info.Address, StringComparison.OrdinalIgnoreCase)
                            )));
                    }
                    // TODO: CC

                    // TODO: Prefix subject
                    SubjectField.Text = lastMessage.Subject;

                    // For threading
                    _additionalHeaders.Add(new KeyValuePair<string, string>("In-Reply-To", lastMessage.MessageID));
                    _additionalHeaders.Add(new KeyValuePair<string, string>("References", lastMessage.MessageID));
                }
                else if (query.Equals("Reply"))
                {
                    // Include only the sender/reply-to
                    if (lastMessage.ReplyTo.Count > 0)
                    {
                        ToField.Text = string.Join(", ", lastMessage.ReplyTo);
                    }
                    else
                    {
                        ToField.Text = lastMessage.From.ToString();
                    }

                    // TODO: Prefix subject
                    SubjectField.Text = lastMessage.Subject;

                    // For threading
                    _additionalHeaders.Add(new KeyValuePair<string, string>("In-Reply-To", lastMessage.MessageID));
                    _additionalHeaders.Add(new KeyValuePair<string, string>("References", lastMessage.MessageID));
                }

                ObjectWHeaders view = lastMessage.GetTextView() ?? lastMessage;

                BodyField.Text = "\r\n\r\nOn "
                    + lastMessage.Date.ToString("ddd, MMM d, yyyy a\\t h:mm tt") + ", " + lastMessage.From + " wrote:\r\n\r\n> "
                    + view.Body.Replace("\r\n", "\r\n> ");
            }

            // TODO: Signature

            base.OnNavigatedTo(e);
        }

        private async void Send(object sender, EventArgs e)
        {
            Account account = App.GetCurrentAccount();
            if (account == null)
            {
                return;
            }

            // TODO: validate fields

            MailMessage message = new MailMessage();
            message.Date = DateTime.Now;
            message.From = new MailAddress(account.Info.Address); // TODO: From display name
            MailAddressParser.ParseAddressField(ToField.Text.Trim()).ForEach(message.To.Add);
            message.Subject = SubjectField.Text.Trim();
            message.ContentType = "text/plain; charset=utf-8";
            message.ContentTransferEncoding = "quoted-printable";
            message.Body = BodyField.Text;

            foreach (var pair in _additionalHeaders)
            {
                message.Headers.Add(pair.Key, new HeaderValue(pair.Value));
            }

            // TODO: Short term: Progress bar
            // TODO: Long term: Save to drafts and memory, then send in the background. Retry if no connectivity.
            await account.SendMessageAsync(message);

            NavigationService.GoBack();
        }

        private void Attach(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Discard(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Dictate(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        // Pre-add labels/stars
        private void LabelClick(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}