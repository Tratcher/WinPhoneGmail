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
            return message.Headers.GetLabels();
        }

        // Returns message labels, including special labels
        public static List<string> GetLabels(this HeaderDictionary headers)
        {
            // Space separated list with special items in quotes.
            string rawLabels = headers["X-GM-LABELS"].Value;
            return Utilities.SplitQuotedList(rawLabels, ' ');
        }

        public static string GetThreadId(this MailMessage message)
        {
            return message.Headers["X-GM-THRID"].Value;
        }

        public static string GetMessageId(this MailMessage message)
        {
            return message.Headers["X-GM-MSGID"].Value;
        }

        public static void AddLabel(this MailMessage message, string labelName)
        {
            // Space separated list with special items in quotes.
            string rawLabels = message.Headers["X-GM-LABELS"].Value;
            List<string> labels = Utilities.SplitQuotedList(rawLabels, ' ');
            labels.Add(labelName);
            message.Headers["X-GM-LABELS"] = new HeaderValue(string.Join(" ", labels.Distinct()));
        }
    }
}
