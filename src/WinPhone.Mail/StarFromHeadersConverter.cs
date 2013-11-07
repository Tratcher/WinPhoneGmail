using System;
using System.Collections.Generic;
using System.Windows.Data;
using System.Windows.Media;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail
{
    public class StarFromHeadersConverter : IValueConverter
    {
        private static Brush Yellow = new SolidColorBrush(Colors.Yellow);

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            HeaderDictionary headers = (HeaderDictionary)value;
            List<string> labels = headers.GetLabels();
            // Includes normal labels and special ones too.  Filter out known special labels
            // "\\Sent" Family "\\Important" "\\Starred" Geeky

            // Super Stars - No IMAP support - Search term “has:blue-star”? http://googlesystem.blogspot.com/2008/07/gmail-superstars.html

            return labels.Contains("\"\\\\Starred\"") ? Yellow : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
