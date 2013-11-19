using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols
{
    public static class MailAddressParser
    {
        // This is content from a message, it's presumed to be well formatted, but might require decoding.
        public static List<MailAddress> ParseAddressHeader(string header)
        {
            var mailAddresses = new List<MailAddress>();

            // TODO: possible null header value?

            List<string> addresses = Utilities.SplitQuotedList(header, ',');

            for (int i = 0; i < addresses.Count; i++)
            {
                string address = addresses[i];
                if (!string.IsNullOrWhiteSpace(address))
                {
                    address = address.Trim();
                    string displayName = string.Empty;

                    int bracketIndex = address.IndexOf('<');
                    if (bracketIndex >= 0 && address.EndsWith(">"))
                    {
                        // TODO: Decode
                        displayName = address.Substring(0, bracketIndex).Trim();
                        address = address.Substring(bracketIndex + 1, address.Length - bracketIndex - 2).Trim();

                        // Remove quotes, if any.
                        if (displayName.Length > 1 && displayName[0] == '"' && displayName[displayName.Length - 1] == '"')
                        {
                            displayName = displayName.Substring(1, displayName.Length - 2);
                        }
                    }

                    var mailAddress = new MailAddress(address, displayName);
                    mailAddresses.Add(mailAddress);
                }
            }

            return mailAddresses;
        }

        // This is user input, expect garbage.
        public static List<MailAddress> ParseAddressField(string field)
        {
            // TODO: Robust parser
            return ParseAddressHeader(field);
        }
    }
}
