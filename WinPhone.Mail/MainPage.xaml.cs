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

namespace WinPhone.Mail
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Sample code to localize the ApplicationBar
            BuildLocalizedApplicationBar();
        }

        // Sample code for building a localized ApplicationBar
        private void BuildLocalizedApplicationBar()
        {
            // Set the page's ApplicationBar to a new instance of ApplicationBar.
            ApplicationBar = new ApplicationBar();

            // Create a new button and set the text value to the localized string from AppResources.
            ApplicationBarIconButton syncButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
            syncButton.Text = AppResources.AppBarButtonText;
            ApplicationBar.Buttons.Add(syncButton);
            syncButton.Click += Sync;

            // Create a new button and set the text value to the localized string from AppResources.
            ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
            appBarButton.Text = AppResources.AppBarButtonText;
            ApplicationBar.Buttons.Add(appBarButton);

            // Create a new menu item with the localized string from AppResources.
            ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
            ApplicationBar.MenuItems.Add(appBarMenuItem);
        }

        private async void Sync(object sender, EventArgs e)
        {
            string username = "tracher@gmail.com";
            string password = "";

            try
            {
                using (var gmail = new GmailImapClient())
                {
                    await gmail.ConnectAsync(username, password);
                    MailMessage[] messages = await gmail.Client.GetMessagesAsync(0, 15, true, false);
                    foreach (var message in messages)
                    {
                        WriteLine(message.Subject);
                    }

                    MailList.ItemsSource = messages;
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
            }
        }

        private void WriteLine(string value)
        {
            Output.Text += value + "\r\n";
        }
    }
}