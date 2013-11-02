using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WinPhone.Mail.Protocols;

namespace WinPhone.Mail
{
    public class NameOrAddressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            MailAddress address = (MailAddress)value;
            if (string.IsNullOrWhiteSpace(address.DisplayName))
            {
                return address.Address;
            }
            return address.DisplayName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
