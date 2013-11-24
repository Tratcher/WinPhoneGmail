using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using WinPhone.Mail.Protocols;

namespace WinPhone.Mail.Gmail.UIHelpers
{
    public class ShortNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            List<MailMessage> messages = (List<MailMessage>)value;

            var names = messages.Select(message =>
                {
                    MailAddress address = message.From;
                    if (address == null)
                    {
                        return string.Empty;
                    }
                    if (string.IsNullOrWhiteSpace(address.DisplayName))
                    {
                        return address.Address.Split('@').First();
                    }
                    return address.DisplayName.Split(' ').First();
                });

            return string.Join(", ", names.Reverse());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
