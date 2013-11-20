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

namespace WinPhone.Mail
{
    public partial class ComposePage : PhoneApplicationPage
    {
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