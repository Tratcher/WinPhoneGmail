using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using WinPhone.Mail.Gmail.Shared.Accounts;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail.Gmail.UIHelpers
{
    public class AttachmentLineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Attachment attachment = (Attachment)value;
            Account account = App.AccountManager.GetCurrentAccount();
            ConversationThread thread = account.ActiveConversation;
            // Gind the message by matching the file name
            // TODO: This could be wrong because the same conversation thread may have
            // multiple messages with the same attachment name.
            MailMessage message = thread.Messages.Where(
                msg => msg.Attachments.FirstOrDefault(
                    att => att.Filename == attachment.Filename) != null).First();
            // TODO: Size
            if (attachment.Scope == Scope.HeadersAndBody 
                || account.MailStorage.HasMessagePart(message.GetThreadId(), message.GetMessageId(), attachment.BodyId))
            {
                return attachment.Filename + " (open)";
            }
            else
            {
                return attachment.Filename + " (download)";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
