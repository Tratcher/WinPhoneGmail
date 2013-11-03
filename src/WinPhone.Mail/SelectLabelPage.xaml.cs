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

namespace WinPhone.Mail
{
    public partial class SelectLabelPage : PhoneApplicationPage
    {
        public SelectLabelPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var account = App.GetCurrentAccount();
            if (account != null)
            {
                Mailbox[] labels = await account.GetLabelsAsync();
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
                Mailbox mailbox = LabelList.SelectedItem as Mailbox;
                await account.SelectLabelAsync(mailbox);
            }
            NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
        }
    }
}