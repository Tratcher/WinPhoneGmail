﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace WinPhone.Mail.Gmail.UIHelpers
{
    // If the date is today, only show the time. (2:11pm)
    // If the date is in the last week, only show the day of the week (Thur)
    // Otherwise show the month and day (Oct 10)
    public class RelativeDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime orrigin = (DateTime)value;
            DateTime now = DateTime.Now;
            TimeSpan elapsed = now - orrigin;

            if (elapsed.TotalDays > 365)
            {
                return orrigin.ToString("d MMM yyyy", culture); // 9 Oct 2012
            }
            else if (elapsed.TotalDays > 7)
            {
                return orrigin.ToString("d MMM", culture); // 9 Oct
            }
            else if (elapsed.TotalDays > 1)
            {
                return orrigin.ToString("ddd", culture); // Mon
            }

            return orrigin.ToString("h:mm tt", culture); // 9:01 pm
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
