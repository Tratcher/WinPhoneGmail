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
            string rawLabels = headers[GConstants.LabelsHeader].Value;
            return Utilities.SplitQuotedList(rawLabels, ' ');
        }

        public static string GetThreadId(this MailMessage message)
        {
            return message.Headers[GConstants.ThreadIdHeader].Value;
        }

        public static string GetMessageId(this MailMessage message)
        {
            return message.Headers[GConstants.MessageIdHeader].Value;
        }

        public static void AddLabel(this MailMessage message, string labelName)
        {
            // Space separated list with special items in quotes.
            string rawLabels = message.Headers[GConstants.LabelsHeader].Value;
            List<string> labels = Utilities.SplitQuotedList(rawLabels, ' ');
            labels.Add(labelName);
            message.Headers[GConstants.LabelsHeader] = new HeaderValue(string.Join(" ", labels.Distinct()));
        }

        public static bool RemoveLabel(this MailMessage message, string labelName)
        {
            // Space separated list with special items in quotes.
            string rawLabels = message.Headers[GConstants.LabelsHeader].Value;
            List<string> labels = Utilities.SplitQuotedList(rawLabels, ' ');
            labels = labels.Where(label => !label.Equals(labelName)).ToList();
            message.Headers[GConstants.LabelsHeader] = new HeaderValue(string.Join(" ", labels));
            return !string.Equals(rawLabels, message.Headers[GConstants.LabelsHeader].Value);
        }

        // Labels like "\"\\\\foo\"" are special. Normal labels just look like "label"
        public static List<string> GetNonSpecialLabels(List<string> labels)
        {
            return labels.Where(label => !label.StartsWith("\"\\")).ToList();
        }
    }
}
