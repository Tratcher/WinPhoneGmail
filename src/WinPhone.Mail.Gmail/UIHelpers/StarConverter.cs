﻿using System;
using System.Collections.Generic;
using System.Windows.Data;
using System.Windows.Media;
using WinPhone.Mail.Protocols;

namespace WinPhone.Mail.Gmail.UIHelpers
{
    public class StarConverter : IValueConverter
    {
        private static Brush Yellow = new SolidColorBrush(Colors.Yellow);

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Flags flags = (Flags)value;

            // TODO: Super Stars - No IMAP support - Search term “has:blue-star”? http://googlesystem.blogspot.com/2008/07/gmail-superstars.html

            return (flags & Flags.Flagged) == Flags.Flagged ? Yellow : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}