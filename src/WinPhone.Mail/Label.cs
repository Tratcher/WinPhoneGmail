﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Storage;

namespace WinPhone.Mail
{
    public class Label
    {
        public LabelInfo Info { get; set; }

        public List<ConversationThread> Conversations { get; set; }
    }
}
