using System;
using System.Windows.Data;
using System.Windows.Media;
using WinPhone.Mail.Protocols;

namespace WinPhone.Mail
{
    public class StarFromMessageConverter : IValueConverter
    {
        private static Brush Yellow = new SolidColorBrush(Colors.Yellow);

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            MailMessage message = (MailMessage)value;

            // TODO: Super Stars - No IMAP support - Search term “has:blue-star”? http://googlesystem.blogspot.com/2008/07/gmail-superstars.html

            return message.Flagged ? Yellow : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
