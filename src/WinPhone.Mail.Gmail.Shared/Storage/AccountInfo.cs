﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Gmail.Shared.Storage
{
    public class AccountInfo
    {
        public string Address { get; set; }
        public string Password { get; set; }
        public string DisplayName { get; set; }
        public TimeSpan Frequency { get; set; }
        public TimeSpan Range { get; set; }
        public NotificationOptions Notifications { get; set; }
        public int NewMailCount { get; set; }
    }
}
