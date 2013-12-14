using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols;
using Xunit;

namespace Tests
{
    public class Clients
    {
        /*
        [Fact]
        public async Task IDLE()
        {
            var mre1 = new System.Threading.ManualResetEventSlim(false);
            var mre2 = new System.Threading.ManualResetEventSlim(false);
            using (var imap = await GetClientAsync<ImapClient>())
            {
                bool fired = false;
                imap.MessageDeleted += (sender, e) =>
                {
                    fired = true;
                    mre2.Set();
                };

                var count = await imap.GetMessageCountAsync();
                count.ShouldBeInRange(1, int.MaxValue); // interrupt the idle thread

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Delete_Message();
                    mre1.Set();
                });

                mre1.Wait();
                mre2.Wait(TimeSpan.FromSeconds(15));//give the other thread a moment
                fired.ShouldBe();
            }
        }
        */
        /*
        [Fact]
        public async Task Message_With_Attachments()
        {
            using (var imap = GetClient<ImapClient>())
            {
                var msg = await imap.SearchMessagesAsync(SearchCondition.Larger(100 * 1000)).FirstOrDefault().Value;

                msg.Attachments.Count.ShouldBeInRange(1, int.MaxValue);
            }
        }
        */
        [Fact]
        public async Task Select_Folder()
        {
            using (var imap = await GetClientAsync<ImapClient>())
            {
                await imap.SelectMailboxAsync("Notes");
                (await imap.ExamineAsync("Notes")).UIDValidity.ShouldBeGreaterThan(0);
                (await imap.GetMessageCountAsync()).ShouldBeInRange(1, int.MaxValue);
            }
        }
        /*
        [Fact]
        public void Polish_Characters()
        {
            using (var imap = GetClient<ImapClient>())
            {
                var msg = imap.SearchMessages(SearchCondition.Subject("POLISH LANGUAGE TEST")).FirstOrDefault();
                msg.Value.ShouldBe();

                msg.Value.Body.ShouldContain("Cię e-mailem, kiedy Kupują");

            }
        }
        */
        [Fact]
        public async Task POP()
        {
            using (var client = await GetClientAsync<Pop3Client>("gmail", "pop3"))
            {
                var msg = await client.GetMessageAsync(0);
                Console.WriteLine(msg.Body);
            }
        }

        [Fact]
        public async Task Connections()
        {
            var accountsToTest = System.IO.Path.Combine(Environment.CurrentDirectory.Split(new[] { "\\aenetmail\\" }, StringSplitOptions.RemoveEmptyEntries).First(), "ae.net.mail.usernames.txt");
            var lines = System.IO.File.ReadAllLines(accountsToTest)
                    .Select(x => x.Split(','))
                    .Where(x => x.Length == 6)
                    .ToArray();

            lines.Any(x => x[0] == "imap").ShouldBe();
            lines.Any(x => x[0] == "pop3").ShouldBe();

            foreach (var line in lines)
                using (var mail = await GetClientAsync(line[0], line[1], int.Parse(line[2]), bool.Parse(line[3]), line[4], line[5]))
                {
                    (await mail.GetMessageCountAsync()).ShouldBeInRange(1, int.MaxValue);

                    var msg = await mail.GetMessageAsync(0, Scope.Headers);
                    msg.Subject.ShouldNotBeNullOrEmpty();
                    msg = await mail.GetMessageAsync(0, Scope.HeadersAndBody);
                    msg.Body.ShouldNotBeNullOrEmpty();

                    await mail.DisconnectAsync();
                    await mail.DisconnectAsync();
                }
        }

        [Fact]
        public void Search_Conditions()
        {
            var deleted = SearchCondition.Deleted();
            var seen = SearchCondition.Seen();
            var text = SearchCondition.Text("andy");

            deleted.ToString().ShouldBe("DELETED");
            deleted.Or(seen).ToString().ShouldBe("OR (DELETED) (SEEN)");
            seen.And(text).ToString().ShouldBe("(SEEN) (TEXT \"andy\")");

            var since = new DateTime(2000, 1, 1);
            SearchCondition.Undeleted().And(
                                    SearchCondition.From("david"),
                                    SearchCondition.SentSince(since)
                            ).Or(SearchCondition.To("andy"))
                    .ToString()
                    .ShouldBe("OR ((UNDELETED) (FROM \"david\") (SENTSINCE \"" + Utilities.GetRFC2060Date(since) + "\")) (TO \"andy\")");
        }
        /*
        [Fact]
        public void Search()
        {
            using (var imap = await GetClientAsync<ImapClient>())
            {
                var result = imap.SearchMessages(
                    //"OR ((UNDELETED) (FROM \"david\") (SENTSINCE \"01-Jan-2000 00:00:00\")) (TO \"andy\")"
                        SearchCondition.Undeleted().And(SearchCondition.From("david"), SearchCondition.SentSince(new DateTime(2000, 1, 1))).Or(SearchCondition.To("andy"))
                        );
                result.Length.ShouldBeInRange(1, int.MaxValue);
                result.First().Value.Subject.ShouldNotBeNullOrEmpty();

                result = imap.SearchMessages(new SearchCondition { Field = SearchCondition.Fields.Text, Value = "asdflkjhdlki2uhiluha829hgas" });
                result.Length.ShouldBe(0);
            }
        }
        */
        /*
        [Fact]
        public void Issue_49()
        {
            using (var client = async GetClientAsyncGetClient<ImapClient>())
            {
                var msg = client.SearchMessages(SearchCondition.Subject("aenetmail").And(SearchCondition.Subject("#49"))).Select(x => x.Value).FirstOrDefault();
                msg.ShouldBe();
                msg.AlternateViews.FirstOrDefault(x => x.ContentType.Contains("html")).Body.ShouldBe();
            }
        }
        */
        [Fact]
        public async Task Append_Mail()
        {
            using (var client = await GetClientAsync<ImapClient>())
            {
                var msg = new MailMessage
                {
                    Subject = "TEST",
                    Body = "Appended!"
                };
                msg.Date = DateTime.Now;

                await client.AppendMailAsync(msg, "Inbox");
            }
        }

        [Fact]
        public void Parse_Imap_Header()
        {
            var header = @"X-GM-THRID 1320777376118077475 X-GM-MSGID 1320777376118077475 X-GM-LABELS () UID 8286 RFC822.SIZE 9369 FLAGS (\Seen) BODY[] {9369}";

            var values = Utilities.ParseImapHeader(header);
            values["FLAGS"].ShouldBe(@"\Seen");
            values["UID"].ShouldBe("8286");
            values["X-GM-MSGID"].ShouldBe("1320777376118077475");
            values["X-GM-LABELS"].ShouldBeNullOrEmpty();
            values["RFC822.SIZE"].ShouldBe("9369");
        }

        [Fact]
        public async Task Get_Several_Messages()
        {
            int numMessages = 10;
            using (var imap = await GetClientAsync<ImapClient>())
            {
                var msgs = await imap.GetMessagesAsync(0, numMessages - 1, Scope.HeadersAndBody);
                msgs.Length.ShouldBe(numMessages);

                for (var i = 0; i < 1000; i++)
                {
                    var msg = await imap.GetMessageAsync(i);
                    msg.Subject.ShouldNotBeNullOrEmpty();
                    msg.Body.ShouldNotBeNullOrEmpty();
                    msg.ContentType.ShouldStartWith("text/");
                }

                msgs = await imap.GetMessagesAsync(0, numMessages - 1, Scope.Headers);
                msgs.Length.ShouldBe(numMessages);
                msgs.Count(x => string.IsNullOrEmpty(x.Subject)).ShouldBe(0);
            }
        }

        [Fact]
        public async Task Download_Message()
        {
            var filename = System.IO.Path.GetTempFileName();

            try
            {
                using (var imap = await GetClientAsync<ImapClient>())
                using (var file = new System.IO.FileStream(filename, System.IO.FileMode.Create))
                {
                    await imap.DownloadMessageAsync(file, 0, false);
                }

                using (var file = new System.IO.FileStream(filename, System.IO.FileMode.Open))
                {
                    var msg = new WinPhone.Mail.Protocols.MailMessage();
                    msg.Load(file, Scope.HeadersAndBody, maxLength: 0);
                    msg.Subject.ShouldNotBeNullOrEmpty();
                }

            }
            finally
            {
                File.Delete(filename);
            }
        }
        /*
        [Fact]
        public async Task Delete_Message()
        {
            using (var client = GetClient<ImapClient>())
            {
                var lazymsg = client.SearchMessages(SearchCondition.From("DRAGONEXT")).FirstOrDefault();
                var msg = lazymsg == null ? null : lazymsg.Value;
                msg.ShouldBe();

                var uid = msg.Uid;
                await client.DeleteMessageAsync(msg);

                msg = await client.GetMessageAsync(uid);
                Console.WriteLine(msg);
            }
        }
        */
        private string GetSolutionDirectory()
        {
            var dir = new System.IO.DirectoryInfo(Environment.CurrentDirectory);
            while (dir.GetFiles("*.sln").Length == 0)
            {
                dir = dir.Parent;
            }
            return dir.FullName;
        }

        private async Task<T> GetClientAsync<T>(string host = "gmail", string type = "imap") where T : class, IMailClient
        {
            var accountsToTest = System.IO.Path.Combine(GetSolutionDirectory(), "..\\ae.net.mail.usernames.txt");
            var lines = System.IO.File.ReadAllLines(accountsToTest)
                    .Select(x => x.Split(','))
                    .Where(x => x.Length == 6)
                    .ToArray();

            var line = lines.Where(x => x[0].Equals(type) && (x.ElementAtOrDefault(1) ?? string.Empty).Contains(host)).FirstOrDefault();
            return await GetClientAsync(line[0], line[1], int.Parse(line[2]), bool.Parse(line[3]), line[4], line[5]) as T;
        }

        private async Task<IMailClient> GetClientAsync(string type, string host, int port, bool ssl, string username, string password)
        {
            if ("imap".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                ImapClient client = new ImapClient(ImapClient.AuthMethods.Login);
                await client.ConnectAsync(host, username, password, port, ssl);
                return client;
            }

            if ("pop3".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                Pop3Client client = new Pop3Client();
                await client.ConnectAsync(host, username, password, port, ssl);
                return client;
            }

            throw new NotImplementedException(type + " is not implemented");
        }
    }
}
