using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinPhone.Mail.Protocols.Imap
{
    public static class ModifiedUtf7Encoding
    {
        /// <summary>
        /// Decodes modified UTF-7 according to RFC 3501 5.1.3: Mailbox International Naming Convention
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Decode(string input)
        {
            if (String.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            string result = input.Replace("&-", "&");

            for (int indexOfAmpersand = result.IndexOf('&'); indexOfAmpersand != -1; indexOfAmpersand = result.IndexOf('&', indexOfAmpersand + 1))
            {
                int indexOfMinus = result.IndexOf('-', indexOfAmpersand);
                if (indexOfMinus > 0)
                {
                    string substring = result.Substring(indexOfAmpersand + 1, indexOfMinus - indexOfAmpersand - 1);
                    string modifiedBase64 = "+" + substring.Replace(',', '/');
                    byte[] bytes = Encoding.UTF8.GetBytes(modifiedBase64);
                    result = result.Replace("&" + substring + "-", Utilities.UTF7.GetString(bytes, 0 , bytes.Length));
                }
            }

            return result;
        }

        /// <summary>
        /// Encodes to modified UTF-7 according to RFC 3501 5.1.3: Mailbox International Naming Convention
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Encode(string input)
        {
            if (String.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            if (input.All(IsPrintableAscii))
            {
                return input.Replace("&", "&-");
            }

            var result = new StringBuilder();
            var nonAsciiBuffer = new StringBuilder();
            foreach (char c in input)
            {
                if (IsPrintableAscii(c))
                {
                    if (nonAsciiBuffer.Length > 0)
                    {
                        result.Append(EncodeNonPrintableAsciiString(nonAsciiBuffer.ToString()));
                        nonAsciiBuffer.Clear();
                    }

                    if (c == '&')
                    {
                        result.Append("&-");
                    }
                    else
                    {
                        result.Append(c);
                    }
                }
                else
                {
                    nonAsciiBuffer.Append(c);
                }
            }

            if (nonAsciiBuffer.Length > 0)
            {
                result.Append(EncodeNonPrintableAsciiString(nonAsciiBuffer.ToString()));
            }

            return result.ToString();
        }

        private static string EncodeNonPrintableAsciiString(string nonAsciiString)
        {
            byte[] bytes = Utilities.UTF7.GetBytes(nonAsciiString);
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length).Replace('/', ',').Replace('+', '&');
        }

        private static bool IsPrintableAscii(char c)
        {
            return c >= '\x20' && c <= '\x7e';
        }
    }

}
