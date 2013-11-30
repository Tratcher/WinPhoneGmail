using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WinPhone.Mail.Gmail.UIHelpers
{
    public class ReadStatusConverter : IValueConverter
    {
        private static Brush Gray = new SolidColorBrush(Colors.LightGray);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            SolidColorBrush unreadBrush = (SolidColorBrush)Application.Current.Resources["PhoneBackgroundBrush"];
            SolidColorBrush readBrush = (SolidColorBrush)Application.Current.Resources["PhoneDisabledBrush"];

            bool hasUnread = (bool)value;
            return hasUnread ? unreadBrush : readBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
