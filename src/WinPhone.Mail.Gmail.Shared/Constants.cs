using System;

namespace WinPhone.Mail.Gmail.Shared
{
    public class Constants
    {
        public class Sync
        {
            public static readonly TimeSpan AsItemsArrive = TimeSpan.Zero;
            public static readonly TimeSpan Manual = TimeSpan.MaxValue;
            public static readonly TimeSpan DefaultFrequency = TimeSpan.FromMinutes(30);
        }

        public class Range
        {
            public static readonly TimeSpan DefaultRange = TimeSpan.FromDays(31);
        }
    }
}
