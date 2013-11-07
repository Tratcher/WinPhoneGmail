using Microsoft.Phone.Controls;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail
{
    public partial class ConversationPage : PhoneApplicationPage
    {
        private ConversationThread Conversation { get; set; }

        public ConversationPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Account account = App.GetCurrentAccount();
            if (account != null)
            {
                Conversation = account.ActiveConversation;
                DataContext = Conversation;
            }

            base.OnNavigatedTo(e);
        }

        private void MessageHeader_Tap(object sender, GestureEventArgs e)
        {
            StackPanel panel = (StackPanel)sender;
            TextBlock bodyField = panel.Children[1] as TextBlock;
            if (bodyField.Visibility == System.Windows.Visibility.Collapsed)
            {
                bodyField.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                bodyField.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
    }
}