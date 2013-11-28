using System;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using WinPhone.Mail.Protocols;

namespace WinPhone.Mail.Gmail.UIHelpers
{
    // Combines the To and Cc fields for display in one line.
    public class ToAndCcConverter : IValueConverter
    {
        private static Brush Yellow = new SolidColorBrush(Colors.Yellow);

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            MailMessage message = (MailMessage)value;

            string to = "To: " + string.Join(", ", message.To);
            if (message.Cc.Any())
            {
                string cc = string.Join(", ", message.Cc);
                to = string.Join(", ", to, "CC: " + cc);
            }
            // BCC may be visible on messages I've sent.
            if (message.Bcc.Any())
            {
                string bcc = string.Join(", ", message.Bcc);
                to = string.Join(", ", to, "BCC: " + bcc);
            }
            return to;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
