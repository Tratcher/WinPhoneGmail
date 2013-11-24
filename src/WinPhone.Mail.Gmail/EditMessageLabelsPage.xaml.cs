using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Navigation;
using WinPhone.Mail.Gmail.Resources;
using WinPhone.Mail.Gmail.Storage;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail.Gmail
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
            List<string> activeLabels = GmailExtensions.GetNonSpecialLabels(Conversation.Labels);
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
            // Filter out special labels that don't match mailbox names.
            List<string> labelsBefore = GmailExtensions.GetNonSpecialLabels(Conversation.Labels);
            List<string> labelsAfter = LabelList.SelectedItems.Cast<LabelInfo>()
                .Select(info => info.Name).ToList();
            
            Account account = App.GetCurrentAccount();
            List<string> addTo = new List<string>();
            List<string> removeFrom = new List<string>();

            // Diff before and after.  For new labels, add that label.
            // For old labels, remove that label.
            SyncUtilities.CompareLists(labelsBefore, labelsAfter, input => input,
                (before, after) => { }, // Match, do nothing
                (before) => removeFrom.Add(before),
                (after) => addTo.Add(after) // Added
                );

            // Do additions before removals.  Additions will fail if you first remove the current label because you'll 
            // have deleted the message from the current mailbox.
            foreach (string label in addTo)
            {
                await account.AddLabelAsync(Conversation.Messages, label); // Added
            }
            foreach (string label in removeFrom)
            {
                await account.RemoveLabelAsync(Conversation.Messages, label); // Removed
            }

            NavigationService.GoBack();
        }
    }
}