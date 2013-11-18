using System;
using System.Collections.Generic;
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
            await writer.WriteLineAsync("From: " + message.From);
            if (message.ReplyTo.Count > 0)
            {
                await writer.WriteLineAsync("Reply-To: " + string.Join("; ", message.ReplyTo.Select(x => x.ToString())));
            }
            await writer.WriteLineAsync("Subject: " + message.Subject);
            await writer.WriteLineAsync("To: " + string.Join("; ", message.To.Select(x => x.ToString())));
            await writer.WriteLineAsync("Cc: " + string.Join("; ", message.Cc.Select(x => x.ToString())));

            if (!string.IsNullOrEmpty(message.MessageID)) // TODO?
            {
                await writer.WriteLineAsync("Message-ID: " + message.MessageID);
            }

            var otherHeaders = message.Headers.Where(x => !SpecialHeaders.Contains(x.Key, StringComparer.InvariantCultureIgnoreCase));
            foreach (var header in otherHeaders)
            {
                await writer.WriteLineAsync(header.Key + ": " + header.Value);
            }
            if (message.Importance != MailPriority.Normal)
            {
                await writer.WriteLineAsync("Importance: " + (int)message.Importance);
            }

            string boundary = null;
            if (message.Attachments.Any())
            {
                boundary = string.Format("--boundary_" + Guid.NewGuid());
                await writer.WriteLineAsync("Content-Type: multipart/mixed; boundary=" + boundary);
            }

            // signal end of headers
            await writer.WriteLineAsync();

            if (boundary != null)
            {
                await writer.WriteLineAsync("--" + boundary);
                await writer.WriteLineAsync();
            }

            await writer.WriteLineAsync(message.Body);

            foreach (var att in message.Attachments)
            {
                // await writer.WriteLineAsync();
                await writer.WriteLineAsync("--" + boundary);
                await writer.WriteLineAsync(string.Join("\r\n", att.Headers.Select(h => string.Format("{0}: {1}", h.Key, h.Value))));
                await writer.WriteLineAsync();
                await writer.WriteLineAsync(att.Body);
            }

            // TODO: Alternate views

            if (boundary != null)
            {
                await writer.WriteLineAsync("--" + boundary + "--");
            }

            await writer.FlushAsync();
        }
    }
}
