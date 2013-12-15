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
                label.Info.StoreMessages = !label.Info.StoreMessages;

                await account.SaveLabelSettingsAsync();

                if (!label.Info.StoreMessages)
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

        private async void StoreAttachmentsCheckBox_Click(object sender, RoutedEventArgs e)
        {
            Account account = App.AccountManager.GetCurrentAccount();
            if (account != null)
            {
                Label label = account.ActiveLabel;
                label.Info.StoreAttachments = !label.Info.StoreAttachments;

                await account.SaveLabelSettingsAsync();

                // TODO: Purge saved attachments?
            }
        }
    }
}