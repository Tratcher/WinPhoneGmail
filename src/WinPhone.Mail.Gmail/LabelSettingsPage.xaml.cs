using Microsoft.Phone.Controls;
using System.Windows;
using System.Windows.Navigation;
using WinPhone.Mail.Gmail.Shared;
using WinPhone.Mail.Gmail.Shared.Accounts;

namespace WinPhone.Mail.Gmail
{
    public partial class LabelSettingsPage : PhoneApplicationPage
    {
        public LabelSettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Account account = App.AccountManager.GetCurrentAccount();
            if (account != null)
            {
                Label label = account.ActiveLabel;
                DataContext = label.Info;
            }

            base.OnNavigatedTo(e);
        }

        private async void StoreLocallyCheckBox_Click(object sender, RoutedEventArgs e)
        {
            Account account = App.AccountManager.GetCurrentAccount();
            if (account != null)
            {
                Label label = account.ActiveLabel;
                label.Info.Store = !label.Info.Store;

                await account.SaveLabelSettingsAsync();

                if (!label.Info.Store)
                {
                    // Remove from local storage
                    await account.MailStorage.DeleteLabelMessageListAsync(label.Info.Name);
                }
                else
                {
                    // Store any locally sync'd mail
                    if (label.Conversations != null)
                    {
                        await account.MailStorage.StoreLabelMessageListAsync(label.Info.Name, label.Conversations);
                        await account.MailStorage.StoreConverationsAsync(label.Conversations);
                    }
                }
            }
        }
    }
}