using System;
using System.Collections.Generic;
using System.Windows.Data;
using System.Windows.Media;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail.Gmail.UIHelpers
{
    public class StarFromConversationConverter : IValueConverter
    {
        private static Brush Yellow = new SolidColorBrush(Colors.Yellow);

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            ConversationThread conversation = (ConversationThread)value;

            // TODO: Super Stars - No IMAP support - Search term “has:blue-star”? http://googlesystem.blogspot.com/2008/07/gmail-superstars.html

            return (conversation.HasStar) ? Yellow : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
