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

            ApplicationBarIconButton accountsButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/feature.settings.png", UriKind.Relative));
            accountsButton.Text = AppResources.AccountsButtonText;
            ApplicationBar.Buttons.Add(accountsButton);
            accountsButton.Click += Accounts;
            /*
            // Create a new menu item with the localized string from AppResources.
            ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
            ApplicationBar.MenuItems.Add(appBarMenuItem);
            */
        }

        private async void Sync(object sender, EventArgs e)
        {
            try
            {
                var accounts = ((App)App.Current).Accounts;
                if (accounts.Count > 0)
                {
                    Account account = accounts[0];
                    WriteLine("Connecting");
                    await account.ConnectAsync();
                    WriteLine("Getting messages");
                    MailMessage[] messages = await account.Imap.Client.GetMessagesAsync(0, 15, true, false);
                    messages = messages.Reverse().ToArray();
                    WriteLine("Got " + messages.Length + " messages");
                    MailList.ItemsSource = messages;
                }
                else
                {
                    WriteLine("No Accounts, using test data.");
                    MailMessage[] messages = new MailMessage[2];
                    messages[0] = new MailMessage()
                    {
                        Date = DateTime.Now,
                        Subject = "A medium length subject",
                        From = new MailAddress("user@domain.com", "From User"),
                        Headers = new HeaderDictionary()
                        {
                            { "X-GM-LABELS", new HeaderValue("\"\\\\Sent\" Family \"\\\\Important\" Geeky \"\\\\Starred\"") },
                        }
                    };
                    messages[1] = new MailMessage()
                    {
                        Date = DateTime.Now - TimeSpan.FromDays(3),
                        Subject = "A very long subject with lots of random short words that just keeps going and going and going and going and going",
                        From = new MailAddress("user@domain.com", "From User"),
                        Headers = new HeaderDictionary()
                        {
                            { "X-GM-LABELS", new HeaderValue("Geeky") },
                        }
                    };

                    MailList.ItemsSource = messages;
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
            }
        }

        private void Accounts(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/AccountsPage.xaml", UriKind.Relative));
        }

        private void WriteLine(string value)
        {
            Output.Text += value + "\r\n";
        }
    }
}