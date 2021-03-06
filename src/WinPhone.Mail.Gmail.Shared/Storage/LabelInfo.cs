﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Gmail.Shared.Storage
{
    public class LabelInfo
    {
        public string Name { get; set; }

        public bool StoreMessages { get; set; }

        public bool StoreAttachments { get; set; }

        public string Color { get; set; }

        public DateTime? LastSync { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
