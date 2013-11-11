using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using WinPhone.Mail.Storage;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Resources;
using System.Threading.Tasks;

namespace WinPhone.Mail
{
    public partial class EditMessageLabelsPage : PhoneApplicationPage
    {
        private ConversationThread Conversation { get; set; }

        public EditMessageLabelsPage()
        {
            InitializeComponent();

            BuildLocalizedApplicationBar();
        }

        private void BuildLocalizedApplicationBar()
        {
            ApplicationBar = new ApplicationBar();

            ApplicationBarIconButton saveButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/save.png", UriKind.Relative));
            saveButton.Text = AppResources.SaveButtonText;
            ApplicationBar.Buttons.Add(saveButton);
            saveButton.Click += SaveClick;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Account account = App.GetCurrentAccount();
            if (account != null)
            {
                Conversation = account.ActiveConversation;
                GetLabelsAsync();
            }

            base.OnNavigatedTo(e);
        }

        private async void GetLabelsAsync()
        {
            var account = App.GetCurrentAccount();
            if (account == null)
            {
                throw new InvalidOperationException("How did you get to this page without an account?");
            }

            List<LabelInfo> labels = await account.GetLabelsAsync();
            List<string> activeLabels = Conversation.Labels;
            LabelList.ItemsSource = labels;

            // Select all currently enabled labels
            // TODO: Special case the current label, and maybe the INBOX?
            foreach (var label in labels.Where(label => activeLabels.Contains(label.Name)))
            {
                LabelList.SelectedItems.Add(label);
            }
        }

        private async void SaveClick(object sender, EventArgs e)
        {
            List<string> labelsBefore = Conversation.Labels;
            List<string> labelsAfter = LabelList.SelectedItems.Cast<LabelInfo>()
                .Select(info => info.Name).ToList();
            
            Account account = App.GetCurrentAccount();

            // TODO: Diff before and after.  For new labels, copy to that label.
            // For old labels, delete from that label.
            // TODO: Special case the current label, and maybe the INBOX?
            await SyncUtilities.CompareListsAsync(labelsBefore, labelsAfter, input => input,
                (before, after) => Task.FromResult(0), // Match, do nothing
                (before) => account.RemoveLabelAsync(Conversation.Messages, before), // Removed
                (after) => account.AddLabelAsync(Conversation.Messages, after) // Added
                );

            NavigationService.GoBack();
        }
    }
}