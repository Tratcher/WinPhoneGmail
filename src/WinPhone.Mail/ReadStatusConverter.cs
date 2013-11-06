﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using WinPhone.Mail.Protocols;

namespace WinPhone.Mail
{
    public class ReadStatusConverter : IValueConverter
    {
        private static Brush Gray = new SolidColorBrush(Colors.LightGray);

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool hasUnread = (bool)value;
            return hasUnread ? null : Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}