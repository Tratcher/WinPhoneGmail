using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using WinPhone.Mail.Protocols;

namespace WinPhone.Mail
{
    public class LabelsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            List<string> labels = (List<string>)value;
            // Includes normal labels and special ones too.  Filter out known special labels
            // "\\Sent" Family "\\Important" "\\Starred" Geeky
            // TODO: Label color
            return string.Join(" ", labels.Where(label => !label.StartsWith("\"\\")));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
