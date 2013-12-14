using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            string username = "tracher@gmail.com";
            string password = "";

            try
            {
                using (var imap = new GmailImapClient(username, password))
                {
                    IList<GmailMessageInfo> messageInfos = imap.GetCurrentMessageIdsAsync(DateTime.Now - TimeSpan.FromDays(60)).Result;
                    foreach (var messageInfo in messageInfos)
                    {
                        imap.Client.GetBodyPartAsync(messageInfos.Select(ids => ids.Uid), true,
                            new[] { GConstants.MessageIdHeader, GConstants.ThreadIdHeader }, "1",
                            async (stream, size) =>
                            {
                                Console.WriteLine();
                            },
                            CancellationToken.None).Wait();

                        Console.WriteLine(messageInfo.ThreadId + " " + messageInfo.MessageId + " " + messageInfo.Flags + " " + messageInfo.Labels);
                    }
                }

                /*
                using (var smtp = new GmailSmtpClient(username, password))
                {
                    MailMessage message = new MailMessage();
                    message.From = new MailAddress(username);
                    message.To.Add(new MailAddress(username));
                    message.Cc.Add(new MailAddress(username));
                    message.Bcc.Add(new MailAddress(username));
                    message.Subject = "Test Message " + DateTime.Now;
                    message.ContentType = "text/plain";
                    message.Body = "This is a plain text message. It has a few new lines\r\nin it. It should also be long enough to go over the normal 72 character limit. Maybe.\r\nI should really add some special characters in here.";

                    smtp.SendAsync(message).Wait();
                }
                */
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
