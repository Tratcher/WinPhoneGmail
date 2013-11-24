using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using WinPhone.Mail.Gmail.Storage;

namespace WinPhone.Mail.Gmail.UIHelpers
{
    public class StoreStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            LabelInfo labelInfo = (LabelInfo)value;
            if (labelInfo.Store)
            {
                return new BitmapImage(new Uri("/Assets/AppBar/save.small.png", UriKind.Relative));
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
