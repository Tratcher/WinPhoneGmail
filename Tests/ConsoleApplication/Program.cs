using WinPhone.Mail.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            string host = "imap.gmail.com";
            string username = "tracher@gmail.com";
            string password = "";
            int port = 993;
            bool useSsl = true;

            using (var imap = new ImapClient(ImapClient.AuthMethods.Login))
            {
                imap.ConnectAsync(host, username, password, port, useSsl).Wait();
                MailMessage[] messages = imap.GetMessages(0, 15, true, false);
                foreach (var message in messages)
                {
                    Console.WriteLine(message.Subject);
                }
            }
        }
    }
}
