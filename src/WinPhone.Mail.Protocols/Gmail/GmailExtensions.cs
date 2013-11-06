using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols.Gmail
{
    public static class GmailExtensions
    {
        // Returns message labels, including special labels
        public static List<string> GetLabels(this MailMessage message)
        {
            // Space separated list with special items in quotes.
            string rawLabels = message.Headers["X-GM-LABELS"].Value;
            return Utilities.SplitQuotedList(rawLabels, ' ');
        }

        public static string GetThreadId(this MailMessage message)
        {
            return message.Headers["X-GM-THRID"].Value;
        }
    }
}
