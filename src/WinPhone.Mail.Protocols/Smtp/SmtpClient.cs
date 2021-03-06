﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols.Smtp
{
    public class SmtpClient : TextClient
    {
        public async Task ConnectAsync(string host, string username, string password, int port = 25, bool secure = false, bool validateCertificate = true)
        {
            await ConnectAsync(host, port, secure, validateCertificate);
            string response = await SendCommandGetResponseAsync("EHLO");
            if (!response.StartsWith("250-mx.google.com"))
            {
                throw new InvalidOperationException("Unexpected response: " + response);
            }
            // Drain out extension data, one line at a time.
            while (response.StartsWith("250-"))
            {
                response = await GetResponseAsync();
            }
            await LoginAsync(username, password);
        }

        internal async override Task OnLoginAsync(string username, string password)
        {
            await SendCommandCheckResponseAsync("AUTH LOGIN", "334 VXNlcm5hbWU6"); // Base64 Username:

            // TODO UTF8 vs ANSI?
            string encodedUser = Convert.ToBase64String(Encoding.UTF8.GetBytes(username));
            await SendCommandCheckResponseAsync(encodedUser, "334 UGFzc3dvcmQ6"); // Base64 Password:
            
            // TODO UTF8 vs ANSI?
            string encodedPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
            await SendCommandCheckResponseAsync(encodedPassword, "235 2.7.0 Accepted");
        }

        internal override async Task OnLogoutAsync()
        {
            string response = await SendCommandGetResponseAsync("QUIT");
            // expect 221 something...
        }

        internal override void CheckResultOK(string response)
        {
            if (!IsResultOK(response))
            {
                response = response.Substring(response.IndexOf(" ")).Trim();
                throw new Exception(response);
            }
        }

        internal bool IsResultOK(string response)
        {
            string statusCode = response.Substring(0, response.IndexOf(" ")).Trim();
            return (statusCode.Length == 3 && statusCode[0] == '2');
        }

        // http://tools.ietf.org/html/rfc5321#section-3.3
        public async Task SendAsync(MailMessage message)
        {
            // Since it has been a common source of errors, it is worth noting that
            // spaces are not permitted on either side of the colon following FROM
            // in the MAIL command or TO in the RCPT command.

            // MAIL FROM:<reverse-path> [SP <mail-parameters> ] <CRLF>
            // expect "250 OK"
            await SendCommandCheckOKAsync("MAIL FROM:" + Utilities.GetSmtpAddress(message.From));

            // Loop foreach To, CC, and BCC:
            // RCPT TO:<forward-path> [ SP <rcpt-parameters> ] <CRLF>
            // expect "250 OK"
            foreach (MailAddress address in message.To.Concat(message.Cc).Concat(message.Bcc))
            {
                await SendCommandCheckOKAsync("RCPT TO:" + Utilities.GetSmtpAddress(address));
            }

            // DATA <CRLF>
            // expect "354 Intermediate"
            string response = await SendCommandGetResponseAsync("DATA");
            if (!response.StartsWith("354 "))
            {
                // 354  Go ahead m2sm20605123pbn.19 - gsmtp
                throw new InvalidOperationException("Unexepcted response: " + response);
            }

            await SendMessageAsync(message);

            // SMTP indicates the end of the mail data by sending a line containing only a "."
            // expect "250 OK"
            await SendCommandCheckOKAsync(".");
        }

        // TODO:
        // Encoding
        // Line wrapping
        // . padding
        private static readonly string[] SpecialHeaders = "Date,To,Cc,Reply-To,Bcc,Sender,From,Message-ID,Importance,Subject,Flags".Split(',');
        private async Task SendMessageAsync(MailMessage message)
        {
            StreamWriter writer = new StreamWriter(_Stream, Encoding, 1024, leaveOpen: true);
            // Send FROM, ReplyTo, TO, CC, etc. BUT NOT BCC.
            await writer.WriteLineAsync("Date: " + message.Date.GetRFC2060Date());
            await writer.WriteLineAsync(EncodeAddressLine("From: ", message.From));
            if (message.ReplyTo.Count > 0)
            {
                await writer.WriteLineAsync(EncodeAddressLine("Reply-To: ", message.ReplyTo));
            }
            await writer.WriteLineAsync(EncodeHeader("Subject: ", message.Subject));
            if (message.To.Count > 0)
            {
                await writer.WriteLineAsync(EncodeAddressLine("To: ", message.To));
            }
            if (message.Cc.Count > 0)
            {
                await writer.WriteLineAsync(EncodeAddressLine("Cc: ", message.Cc));
            }

            if (!string.IsNullOrEmpty(message.MessageID)) // TODO?
            {
                await writer.WriteLineAsync("Message-ID: " + message.MessageID);
            }

            var otherHeaders = message.Headers.Where(x => !SpecialHeaders.Contains(x.Key, StringComparer.InvariantCultureIgnoreCase));
            foreach (var header in otherHeaders)
            {
                await writer.WriteLineAsync(header.Key + ": " + header.Value); // TODO: Encodings, line length
            }
            if (message.Importance != MailPriority.Normal && message.Importance != MailPriority.None)
            {
                await writer.WriteLineAsync("Importance: " + ((int)message.Importance).ToString(CultureInfo.InvariantCulture));
            }
            /*
            string boundary = null;
            if (message.Attachments.Any())
            {
                boundary = string.Format("--boundary_" + Guid.NewGuid());
                await writer.WriteLineAsync("Content-Type: multipart/mixed; boundary=" + boundary);
            }
            */
            // signal end of headers
            await writer.WriteLineAsync();
            /*
            if (boundary != null)
            {
                await writer.WriteLineAsync("--" + boundary);
                await writer.WriteLineAsync();
            }
            */
            await EncodeBodyAsync(writer, message.Body);
            /*
            foreach (var att in message.Attachments)
            {
                // await writer.WriteLineAsync();
                await writer.WriteLineAsync("--" + boundary);
                await writer.WriteLineAsync(string.Join("\r\n", att.Headers.Select(h => string.Format("{0}: {1}", h.Key, h.Value))));
                await writer.WriteLineAsync();
                await EncodeBodyAsync(writer, message.Body);
            }
            // TODO: Alternate views

            if (boundary != null)
            {
                await writer.WriteLineAsync("--" + boundary + "--");
            }
            */
            await writer.FlushAsync();
        }

        // Generate MIME encoding if required.
        // =?utf-8?Q?Data?=
        // Honor line length limits
        // Header: =?utf-8?Q?Some=20Data?=
        //  =?utf-8?Q?More Data?=
        public static string EncodeHeader(string header, string value)
        {
            string prefix = string.Empty;
            string suffix = string.Empty;
            if (ContainsUnsafeCharacters(value))
            {
                prefix = "=?utf-8?Q?";
                suffix = "?=";
            }

            StringBuilder builder = new StringBuilder(header);
            builder.Append(prefix);

            // TODO: For un-encoded content it's preferred to break on existing whitespaces.
            // This may insert an extra white space in the subject after a line break.
            // TODO: This may generate a trailing line that's got nothing in it.
            int lineLength = builder.Length;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (IsSafeChar(c))
                {
                    builder.Append(c);
                    lineLength++;
                    if (lineLength >= 76 - suffix.Length)
                    {
                        builder.AppendLine(suffix);
                        builder.Append(' ');
                        builder.Append(prefix);
                        lineLength = prefix.Length;
                    }
                }
                else
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(new[] { c });

                    // Don't split an encoded character
                    if (lineLength >= 76 - suffix.Length - (bytes.Length * 3))
                    {
                        builder.AppendLine(suffix);
                        builder.Append(' ');
                        builder.Append(prefix);
                        lineLength = prefix.Length;
                    }

                    for (int j = 0; j < bytes.Length; j++)
                    {
                        AppendQuotedByte(builder, bytes[j]);
                    }
                }
            }

            builder.Append(suffix);

            return builder.ToString();
        }

        private static bool ContainsUnsafeCharacters(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!IsSafeChar(value[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static string EncodeAddressLine(string header, MailAddress address)
        {
            return EncodeAddressLine(header, new[] { address });
        }

        // The address line consists of coma seperated addresses, with or without quoted display names.
        public static string EncodeAddressLine(string header, IEnumerable<MailAddress> addresses)
        {
            // TODO: line wrapping, display name encoding/escaping, etc.
            StringBuilder builder = new StringBuilder(header);
            int lineLength = builder.Length;
            bool first = true;
            foreach (MailAddress address in addresses)
            {
                if (!first)
                {
                    builder.Append(", ");
                    lineLength += 2;
                }
                first = false;

                bool addBrackets = false;
                if (!string.IsNullOrWhiteSpace(address.DisplayName))
                {
                    addBrackets = true;

                    // The display name will either be in quotes or fully encoded.
                    // TODO: Officialy we need to line wrap very long display names, but that should be rare.
                    if (ContainsUnsafeCharacters(address.DisplayName))
                    {
                        byte[] encoded = Encoding.UTF8.GetBytes(address.DisplayName);
                        string base64Encoded = Convert.ToBase64String(encoded);

                        string prefix = "=?utf-8?B?";
                        string suffix = "?= ";
                        if (base64Encoded.Length + prefix.Length + suffix.Length + lineLength >= 76)
                        {
                            builder.Append("\r\n ");
                            lineLength = 1;
                        }

                        builder.Append(prefix);
                        builder.Append(base64Encoded);
                        builder.Append(suffix);
                        lineLength += prefix.Length + base64Encoded.Length + suffix.Length;
                    }
                    else
                    {
                        if (address.DisplayName.Length + 3 + lineLength >= 76)
                        {
                            builder.Append("\r\n ");
                            lineLength = 1;
                        }

                        builder.Append('"');
                        builder.Append(address.DisplayName);
                        builder.Append("\" ");
                        lineLength += address.DisplayName.Length + 3;
                    }
                }

                if (address.Address.Length + lineLength + (addBrackets ? 2 : 0) >= 76)
                {
                    builder.Append("\r\n ");
                    lineLength = 1;
                }

                if (addBrackets)
                {
                    builder.Append('<');
                    lineLength++;
                }

                // TODO: Punycode IDNs
                builder.Append(address.Address);
                lineLength += address.Address.Length;

                if (addBrackets)
                {
                    builder.Append('>');
                    lineLength++;
                }
            }

            return builder.ToString();
        }

        // Encode the body using quoted printable encoding and utf8
        // Line length limit 76. Put an '=' at the end of the line to indicate we added a soft line-break.
        // Normal CRLFs are included as is.
        // http://tools.ietf.org/html/rfc2045#section-6.7
        // Any lines starting with '.' must be padded with an additional '.'
        // Only Ascii dec 9, 32, 33-60, 62-126 inclusive are allowed un-encoded.
        // The last character on a line may not be a space or tab, these must be encoded.
        // Consider also encoding !"#$@[\]^`{|}~ for high compatibility.
        // For now we'll conservatively encode anything but alpha-numerics, space, and a few punctionations.
        public static async Task EncodeBodyAsync(StreamWriter writer, string body)
        {
            StringBuilder builder = new StringBuilder(100);
            bool priorCharWasSpace = false;

            for (int i = 0; i < body.Length; i++)
            {
                char c, next;
                c = body[i];
                if (c == '\r')
                {
                    if (i + 1 < body.Length)
                    {
                        next = body[i + 1];
                        if (next == '\n')
                        {
                            await FlushAsync(writer, builder, priorCharWasSpace, softBreak: false);
                            priorCharWasSpace = false;
                            i++; // Skip the next char.
                            continue;
                        }
                    }
                }

                if (IsSafeChar(c))
                {
                    // "Transparancy" - Any line starting with a dot must be padded with an extra so it's never
                    // mistaken for the end of the message.
                    if (c == '.' && builder.Length == 0)
                    {
                        builder.Append('.');
                    }
                    builder.Append(c);
                    priorCharWasSpace = (c == ' ');
                    if (builder.Length >= 76)
                    {
                        await FlushAsync(writer, builder, priorCharWasSpace, softBreak: true);
                        priorCharWasSpace = false;
                    }
                    continue;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(new[] { c });

                if (builder.Length >= 76 - (bytes.Length * 3))
                {
                    await FlushAsync(writer, builder, priorCharWasSpace, softBreak: true);
                    priorCharWasSpace = false;
                }

                for (int j = 0; j < bytes.Length; j++)
                {
                    AppendQuotedByte(builder, bytes[j]);
                }
                priorCharWasSpace = false;
            }

            if (builder.Length > 0)
            {
                await writer.WriteLineAsync(builder.ToString());
            }
        }

        private static async Task FlushAsync(StreamWriter writer, StringBuilder builder, bool priorCharWasSpace, bool softBreak)
        {
            if (priorCharWasSpace)
            {
                // Trailing whitespaces are not allowed, encode it.
                // TODO: line length limit?
                builder.Length--;
                builder.Append("=20");
            }
            if (softBreak)
            {
                builder.Append('='); // Soft line break;
            }
            await writer.WriteLineAsync(builder.ToString());
            builder.Clear();
        }

        // Only Ascii dec 9, 32, 33-60, 62-126 inclusive are allowed un-encoded.
        // Consider also encoding !"#$@[\]^`{|}~ for high compatibility.
        // For now we'll conservatively encode anything but alpha-numerics, space, and a few punctionations.
        private static bool IsSafeChar(char c)
        {
            return ('0' <= c && c <= '9')
                || ('a' <= c && c <= 'z')
                || ('A' <= c && c <= 'Z')
                || ' ' == c || ',' == c
                || '?' == c || '!' == c
                || '@' == c || ':' == c
                || '<' == c || '>' == c
                || '.' == c;
        }

        private static void AppendQuotedByte(StringBuilder builder, byte b)
        {
            builder.Append('=');
            byte upper = (byte)(b >> 4);
            byte lower = (byte)(b & 0x0F);
            AppendHexChar(builder, upper);
            AppendHexChar(builder, lower);
        }

        private static void AppendHexChar(StringBuilder builder, byte b)
        {
            if (b < 10)
            {
                builder.Append((char)(b + '0'));
            }
            else
            {
                builder.Append((char)(b - 10 + 'A'));
            }
        }
    }
}
