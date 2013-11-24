using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols.Gmail;
using WinPhone.Mail.Gmail.Storage;

namespace WinPhone.Mail.Gmail
{
    public class Label
    {
        public LabelInfo Info { get; set; }

        public List<ConversationThread> Conversations { get; set; }
    }
}
