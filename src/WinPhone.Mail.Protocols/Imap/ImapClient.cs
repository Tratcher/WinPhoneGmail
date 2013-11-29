using WinPhone.Mail.Protocols.Imap;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols
{
    public class ImapClient : TextClient, IMailClient
    {
        private string _SelectedMailbox;
        private int _tag = 0;
        private string[] _Capability;

        private bool _Idling;
        private Thread _IdleEvents;

        private string _FetchHeaders = null;

        public ImapClient(AuthMethods method = AuthMethods.Login)
        {
            AuthMethod = method;
        }

        public enum AuthMethods
        {
            Login,
            CRAMMD5,
            SaslOAuth
        }

        public enum StoreMode
        {
            Replace,
            Add,
            Remove,
        }

        public virtual AuthMethods AuthMethod { get; set; }

        public virtual string SelectedMailbox
        {
            get { return _SelectedMailbox; }
        }

        public async Task ConnectAsync(string host, string username, string password, int port = 143, bool secure = false, bool validateCertificate = true)
        {
            await ConnectAsync(host, port, secure, validateCertificate);
            await LoginAsync(username, password);
        }

        private string GetTag()
        {
            _tag++;
            return string.Format("xm{0:000} ", _tag);
        }

        public virtual async Task<bool> SupportsAsync(string command)
        {
            return (_Capability ?? await CapabilityAsync()).Contains(command, StringComparer.OrdinalIgnoreCase);
        }

        private EventHandler<MessageEventArgs> _NewMessage;
        public virtual event EventHandler<MessageEventArgs> NewMessage
        {
            add
            {
                _NewMessage += value;
                IdleStartAsync();
            }
            remove
            {
                _NewMessage -= value;
                if (!HasEvents)
                    IdleStopAsync();
            }
        }

        private EventHandler<MessageEventArgs> _MessageDeleted;
        public virtual event EventHandler<MessageEventArgs> MessageDeleted
        {
            add
            {
                _MessageDeleted += value;
                IdleStartAsync();
            }
            remove
            {
                _MessageDeleted -= value;
                if (!HasEvents)
                    IdleStopAsync();
            }
        }

        protected virtual async Task IdleStartAsync()
        {
            if (string.IsNullOrEmpty(_SelectedMailbox))
            {
                await SelectMailboxAsync("Inbox");
            }
            _Idling = true;
            if (!await SupportsAsync("IDLE"))
            {
                throw new InvalidOperationException("This IMAP server does not support the IDLE command");
            }
            await CheckMailboxSelectedAsync();
            await IdleResumeAsync();
        }

        protected virtual async Task IdlePauseAsync()
        {
            if (_IdleEvents == null || !_Idling)
                return;

            CheckConnectionStatus();
            await SendCommandAsync("DONE");
            if (!_IdleEvents.Join(2000))
                _IdleEvents.Abort();
            _IdleEvents = null;
        }

        protected virtual async Task IdleResumeAsync()
        {
            if (!_Idling)
                return;

            await IdleResumeCommandAsync();

            if (_IdleEvents == null)
            {
                _IdleARE = new AutoResetEvent(false);
                _IdleEvents = new Thread(WatchIdleQueue);
                _IdleEvents.Name = "_IdleEvents";
                _IdleEvents.Start();
            }
        }

        private async Task IdleResumeCommandAsync()
        {
            await SendCommandGetResponseAsync(GetTag() + "IDLE");
            if (_IdleARE != null) _IdleARE.Set();
        }

        private bool HasEvents
        {
            get
            {
                return _MessageDeleted != null || _NewMessage != null;
            }
        }

        protected virtual async Task IdleStopAsync()
        {
            _Idling = false;
            await IdlePauseAsync();
            if (_IdleEvents != null)
            {
                _IdleARE.Close();
                if (!_IdleEvents.Join(2000))
                    _IdleEvents.Abort();
                _IdleEvents = null;
            }
        }

        public virtual bool TryGetResponse(out string response, int millisecondsTimeout)
        {
            using (var mre = new ManualResetEventSlim(false))
            {
                string resp = response = null;
                Task.Run(async () =>
                {
                    resp = await GetResponseAsync();
                    mre.Set();
                });

                if (mre.Wait(millisecondsTimeout))
                {
                    response = resp;
                    return true;
                }
                else
                    return false;
            }
        }

        private static readonly int idleTimeout = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
        private static AutoResetEvent _IdleARE;
        private void WatchIdleQueue()
        {
            try
            {
                string last = null, resp;

                while (true)
                {
                    if (!TryGetResponse(out resp, idleTimeout))
                    {   //send NOOP every 20 minutes
                        NoopAsync(false);        //call noop without aborting this Idle thread
                        continue;
                    }

                    if (resp.Contains("OK IDLE"))
                        return;

                    var data = resp.Split(' ');
                    if (data[0] == "*" && data.Length >= 3)
                    {
                        var e = new MessageEventArgs { Client = this, MessageCount = int.Parse(data[1]) };
                        if (data[2].Is("EXISTS") && !last.Is("EXPUNGE") && e.MessageCount > 0)
                        {
                            ThreadPool.QueueUserWorkItem(callback => _NewMessage.Fire(this, e));    //Fire the event on a separate thread
                        }
                        else if (data[2].Is("EXPUNGE"))
                        {
                            _MessageDeleted.Fire(this, e);
                        }
                        last = data[2];
                    }
                }
            }
            catch (Exception) { }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (_IdleEvents != null)
                {
                    _IdleEvents.Abort();
                }
                if (_IdleARE != null)
                {
                    _IdleARE.Dispose();
                }
            }
            _IdleEvents = null;
            _IdleARE = null;
        }

        public virtual async Task AppendMailAsync(MailMessage email, string mailbox = null)
        {
            await IdlePauseAsync();

            mailbox = ModifiedUtf7Encoding.Encode(mailbox);
            string flags = String.Empty;
            var body = new StringBuilder();
            using (var txt = new System.IO.StringWriter(body))
                email.Save(txt);

            string size = body.Length.ToString();
            if (email.RawFlags.Length > 0)
            {
                flags = " (" + string.Join(" ", email.Flags) + ")";
            }

            if (mailbox == null)
                await CheckMailboxSelectedAsync();
            mailbox = mailbox ?? _SelectedMailbox;

            string command = GetTag() + "APPEND " + (mailbox ?? _SelectedMailbox).QuoteString() + flags + " {" + size + "}";
            string response = await SendCommandGetResponseAsync(command);
            if (response.StartsWith("+"))
            {
                response = await SendCommandGetResponseAsync(body.ToString());
            }
            await IdleResumeAsync();
        }

        public virtual Task NoopAsync()
        {
            return NoopAsync(true);
        }
        private async Task NoopAsync(bool pauseIdle)
        {
            if (pauseIdle)
                await IdlePauseAsync();
            else
                await SendCommandGetResponseAsync("DONE");

            var tag = GetTag();
            var response = await SendCommandGetResponseAsync(tag + "NOOP");
            while (!response.StartsWith(tag))
            {
                response = await GetResponseAsync();
            }

            if (pauseIdle)
                await IdleResumeAsync();
            else
                await IdleResumeCommandAsync();
        }

        public virtual async Task<string[]> CapabilityAsync()
        {
            await IdlePauseAsync();
            string command = GetTag() + "CAPABILITY";
            string response = await SendCommandGetResponseAsync(command);
            if (response.StartsWith("* CAPABILITY "))
                response = response.Substring(13);
            _Capability = response.Trim().Split(' ');
            await GetResponseAsync();
            await IdleResumeAsync();
            return _Capability;
        }

        public virtual Task<bool> CopyAsync(List<MailMessage> messages, string destination)
        {
            string keys = "UID " + string.Join(",", messages.Select(message => message.Uid));
            return CopyAsync(keys, destination);
        }

        public virtual async Task<bool> CopyAsync(string messageset, string destination)
        {
            await CheckMailboxSelectedAsync();
            await IdlePauseAsync();
            string prefix = null;
            if (messageset.StartsWith("UID ", StringComparison.OrdinalIgnoreCase))
            {
                messageset = messageset.Substring(4);
                prefix = "UID ";
            }

            string tag = GetTag();
            string command = string.Concat(tag, prefix, "COPY ", messageset, " " + ModifiedUtf7Encoding.Encode(destination).QuoteString());

            await SendCommandAsync(command);

            // Drain Expunge responses for cases where the item was removed from this mailbox by the server.
            bool moved = false;
            string response;
            while (true)
            {
                response = await GetResponseAsync();
                if (string.IsNullOrEmpty(response) || response.Contains(tag + "OK"))
                    break;

                if (response[0] != '*' || !response.Contains(" EXPUNGE"))
                    continue;

                // We could return the UIDs for the expunged messages, but they should match the input so we don't care.
                moved = true;

                response = await GetResponseAsync();
            }

            await IdleResumeAsync();
            return moved;
        }

        public virtual async Task CreateMailboxAsync(string mailbox)
        {
            await IdlePauseAsync();
            string command = GetTag() + "CREATE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString();
            await SendCommandCheckOKAsync(command);
            await IdleResumeAsync();
        }

        public virtual async Task DeleteMailboxAsync(string mailbox)
        {
            await IdlePauseAsync();
            string command = GetTag() + "DELETE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString();
            await SendCommandCheckOKAsync(command);
            await IdleResumeAsync();
        }

        public virtual async Task<Mailbox> ExamineAsync(string mailbox)
        {
            await IdlePauseAsync();

            Mailbox x = null;
            string tag = GetTag();
            string command = tag + "EXAMINE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString();
            string response = await SendCommandGetResponseAsync(command);
            if (response.StartsWith("*"))
            {
                x = new Mailbox(mailbox);
                while (response.StartsWith("*"))
                {
                    Match m;

                    m = Regex.Match(response, @"(\d+) EXISTS");
                    if (m.Groups.Count > 1)
                        x.NumMsg = m.Groups[1].ToString().ToInt();

                    m = Regex.Match(response, @"(\d+) RECENT");
                    if (m.Groups.Count > 1)
                        x.NumNewMsg = m.Groups[1].Value.ToInt();

                    m = Regex.Match(response, @"UNSEEN (\d+)");
                    if (m.Groups.Count > 1)
                        x.NumUnSeen = m.Groups[1].Value.ToInt();

                    m = Regex.Match(response, @"UIDVALIDITY (\d+)");
                    if (m.Groups.Count > 1)
                        x.UIDValidity = m.Groups[1].Value.ToInt();

                    m = Regex.Match(response, @" FLAGS \((.*?)\)");
                    if (m.Groups.Count > 1)
                        x.SetFlags(m.Groups[1].ToString());

                    response = await GetResponseAsync();
                }
                _SelectedMailbox = mailbox;
            }
            await IdleResumeAsync();
            return x;
        }

        public virtual async Task ExpungeAsync()
        {
            await CheckMailboxSelectedAsync();
            await IdlePauseAsync();

            string tag = GetTag();
            string command = tag + "EXPUNGE";
            string response = await SendCommandGetResponseAsync(command);
            while (response.StartsWith("*"))
            {
                response = await GetResponseAsync();
            }
            await IdleResumeAsync();
        }

        public virtual Task DeleteMessageAsync(MailMessage msg)
        {
            return DeleteMessageAsync(msg.Uid);
        }

        public virtual async Task DeleteMessageAsync(string uid)
        {
            await CheckMailboxSelectedAsync();
            await StoreAsync("UID " + uid, StoreMode.Replace, "\\Seen \\Deleted");
        }

        public virtual async Task DeleteMessagesAsync(List<string> uids)
        {
            await CheckMailboxSelectedAsync();
            await StoreAsync("UID " + string.Join(",", uids), StoreMode.Add, "\\Deleted");
        }

        public Task DeleteMessagesAsync(List<MailMessage> messages)
        {
            return AddFlagsAsync(Flags.Deleted, messages);
        }

        public virtual async Task MoveMessageAsync(string uid, string folderName)
        {
            await CheckMailboxSelectedAsync();
            await CopyAsync("UID " + uid, folderName);
            await DeleteMessageAsync(uid);
        }

        protected virtual Task CheckMailboxSelectedAsync()
        {
            if (string.IsNullOrEmpty(_SelectedMailbox))
            {
                return SelectMailboxAsync("INBOX");
            }
            return Task.FromResult(0);
        }

        public virtual Task<MailMessage> GetMessageAsync(string uid, bool headersonly = false)
        {
            return GetMessageAsync(uid, headersonly, setseen: false);
        }

        public virtual Task<MailMessage> GetMessageAsync(int index, bool headersonly = false)
        {
            return GetMessageAsync(index, headersonly, setseen: false);
        }

        public virtual async Task<MailMessage> GetMessageAsync(int index, bool headersonly, bool setseen)
        {
            return (await GetMessagesAsync(index, index, headersonly, setseen)).FirstOrDefault();
        }

        public virtual async Task<MailMessage> GetMessageAsync(string uid, bool headersonly, bool setseen)
        {
            return (await GetMessagesAsync(uid, uid, headersonly, setseen)).FirstOrDefault();
        }

        public virtual Task<MailMessage[]> GetMessagesAsync(string startUID, string endUID, bool headersonly = true, bool setseen = false)
        {
            return GetMessagesAsync(startUID, endUID, true, headersonly, setseen);
        }

        public virtual Task<MailMessage[]> GetMessagesAsync(int startIndex, int endIndex, bool headersonly = true, bool setseen = false)
        {
            return GetMessagesAsync((startIndex + 1).ToString(), (endIndex + 1).ToString(), false, headersonly, setseen);
        }

        public virtual Task DownloadMessageAsync(Stream stream, int index, bool setseen)
        {
            return GetMessagesAsync((index + 1).ToString(), (index + 1).ToString(), false, false, setseen, (message, size, headers) =>
            {
                Utilities.CopyStream(message, stream, size);
                return Task.FromResult<MailMessage>(null);
            });
        }

        public virtual Task DownloadMessageAsync(Stream stream, string uid, bool setseen)
        {
            return GetMessagesAsync(uid, uid, true, false, setseen, (message, size, headers) =>
            {
                Utilities.CopyStream(message, stream, size);
                return Task.FromResult<MailMessage>(null);
            });
        }

        public virtual async Task<MailMessage[]> GetMessagesAsync(string start, string end, bool uid, bool headersonly, bool setseen)
        {
            var x = new List<MailMessage>();

            await GetMessagesAsync(start, end, uid, headersonly, setseen,
                async (stream, size, imapHeaders) =>
                {
                    var mail = new MailMessage { Encoding = Encoding };
                    mail.Size = size;

                    if (imapHeaders["UID"] != null)
                        mail.Uid = imapHeaders["UID"];

                    if (imapHeaders["Flags"] != null)
                        mail.SetFlags(imapHeaders["Flags"]);

                    await _Stream.EnsureBufferAsync(mail.Size);
                    mail.Load(_Stream, headersonly, mail.Size);

                    foreach (var key in imapHeaders.Keys.Except(new[] { "UID", "Flags", "BODY[]", "BODY[HEADER]" }, StringComparer.OrdinalIgnoreCase))
                        mail.Headers.Add(key, new HeaderValue(imapHeaders[key]));

                    x.Add(mail);

                    return mail;
                }
            );

            return x.ToArray();
        }

        public virtual async Task GetMessagesAsync(string start, string end, bool uid, bool headersonly, bool setseen, Func<Stream, int, IDictionary<string, string>, Task<MailMessage>> action)
        {
            await CheckMailboxSelectedAsync();
            await IdlePauseAsync();

            string tag = GetTag();
            string command =
                tag
                + (uid ? "UID " : null)
                + "FETCH " + start + ":" + end
                + " (" + _FetchHeaders + "UID FLAGS BODY"
                + (setseen ? null : ".PEEK")
                + "[" + (headersonly ? "HEADER" : null) + "])";

            string response;

            await SendCommandAsync(command);
            while (true)
            {
                response = await GetResponseAsync();
                if (string.IsNullOrEmpty(response) || response.Contains(tag + "OK"))
                    break;

                if (response[0] != '*' || !response.Contains("FETCH ("))
                    continue;

                var imapHeaders = Utilities.ParseImapHeader(response.Substring(response.IndexOf('(') + 1));
                var size = (imapHeaders["BODY[HEADER]"] ?? imapHeaders["BODY[]"]).Trim('{', '}').ToInt();
                var msg = await action(_Stream, size, imapHeaders);

                response = await GetResponseAsync();
                var n = response.Trim().LastOrDefault();
                if (n != ')')
                {
                    System.Diagnostics.Debugger.Break();
                    RaiseWarning(null, "Expected \")\" in stream, but received \"" + response + "\"");
                }
            }

            await IdleResumeAsync();
        }

        public virtual async Task<Quota> GetQuotaAsync(string mailbox)
        {
            if (!(await SupportsAsync("NAMESPACE")))
                throw new Exception("This command is not supported by the server!");
            await IdlePauseAsync();

            Quota quota = null;
            string command = GetTag() + "GETQUOTAROOT " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString();
            string response = await SendCommandGetResponseAsync(command);
            string reg = "\\* QUOTA (.*?) \\((.*?) (.*?) (.*?)\\)";
            while (response.StartsWith("*"))
            {
                Match m = Regex.Match(response, reg);
                if (m.Groups.Count > 1)
                {
                    quota = new Quota(m.Groups[1].ToString(),
                                    m.Groups[2].ToString(),
                                    Int32.Parse(m.Groups[3].ToString()),
                                    Int32.Parse(m.Groups[4].ToString())
                            );
                    break;
                }
                response = await GetResponseAsync();
            }

            await IdleResumeAsync();
            return quota;
        }

        public virtual async Task<Mailbox[]> ListMailboxesAsync(string reference, string pattern)
        {
            await IdlePauseAsync();

            var x = new List<Mailbox>();
            string command = GetTag() + "LIST " + reference.QuoteString() + " " + pattern.QuoteString();
            const string reg = "\\* LIST \\(([^\\)]*)\\) \\\"([^\\\"]+)\\\" \\\"?([^\\\"]+)\\\"?";
            string response = await SendCommandGetResponseAsync(command);
            Match m = Regex.Match(response, reg);
            while (m.Groups.Count > 1)
            {
                Mailbox mailbox = new Mailbox(m.Groups[3].ToString());
                mailbox.SetFlags(m.Groups[1].ToString());
                x.Add(mailbox);
                response = await GetResponseAsync();
                m = Regex.Match(response, reg);
            }
            await IdleResumeAsync();
            return x.ToArray();
        }

        public virtual async Task<Mailbox[]> ListSuscribesMailboxesAsync(string reference, string pattern)
        {
            await IdlePauseAsync();

            var x = new List<Mailbox>();
            string command = GetTag() + "LSUB " + reference.QuoteString() + " " + pattern.QuoteString();
            string reg = "\\* LSUB \\(([^\\)]*)\\) \\\"([^\\\"]+)\\\" \\\"([^\\\"]+)\\\"";
            string response = await SendCommandGetResponseAsync(command);
            Match m = Regex.Match(response, reg);
            while (m.Groups.Count > 1)
            {
                Mailbox mailbox = new Mailbox(m.Groups[3].ToString());
                x.Add(mailbox);
                response = await GetResponseAsync();
                m = Regex.Match(response, reg);
            }
            await IdleResumeAsync();
            return x.ToArray();
        }

        internal override async Task OnLoginAsync(string login, string password)
        {
            string command = String.Empty;
            string result = String.Empty;
            string tag = GetTag();

            switch (AuthMethod)
            {
                /*
                case AuthMethods.CRAMMD5:
                    string key;
                    command = tag + "AUTHENTICATE CRAM-MD5";
                    result = SendCommandGetResponse(command);
                    // retrieve server key
                    key = result.Replace("+ ", "");
                    key = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(key));
                    // calculate hash
                    using (var kMd5 = new HMACMD5(Utilities.ASCII.GetBytes(password)))
                    {
                        byte[] hash1 = kMd5.ComputeHash(Utilities.ASCII.GetBytes(key));
                        key = BitConverter.ToString(hash1).ToLower().Replace("-", "");
                        result = Convert.ToBase64String(Utilities.ASCII.GetBytes(login + " " + key));
                        result = SendCommandGetResponse(result);
                    }
                    break;
                */
                case AuthMethods.Login:
                    command = tag + "LOGIN " + login.QuoteString() + " " + password.QuoteString();
                    result = await SendCommandGetResponseAsync(command);
                    break;

                case AuthMethods.SaslOAuth:
                    string sasl = "user=" + login + "\x01" + "auth=Bearer " + password + "\x01" + "\x01";
                    string base64 = Convert.ToBase64String(Encoding.GetBytes(sasl));
                    command = tag + "AUTHENTICATE XOAUTH2 " + base64;
                    result = await SendCommandGetResponseAsync(command);
                    break;

                default:
                    throw new NotSupportedException();
            }

            if (result.StartsWith("* CAPABILITY "))
            {
                _Capability = result.Substring(13).Trim().Split(' ');
                result = await GetResponseAsync();
            }

            if (!result.StartsWith(tag + "OK"))
            {
                if (result.StartsWith("+ ") && result.EndsWith("=="))
                {
                    string jsonErr = Utilities.DecodeBase64(result.Substring(2), Utilities.UTF7);
                    throw new Exception(jsonErr);
                }
                else
                    throw new Exception(result);
            }

            //if (Supports("COMPRESS=DEFLATE")) {
            //  SendCommandCheckOK(GetTag() + "compress deflate");
            //  _Stream0 = _Stream;
            // // _Reader = new System.IO.StreamReader(new System.IO.Compression.DeflateStream(_Stream0, System.IO.Compression.CompressionMode.Decompress, true), System.Text.Encoding.Default);
            // // _Stream = new System.IO.Compression.DeflateStream(_Stream0, System.IO.Compression.CompressionMode.Compress, true);
            //}

            if (await SupportsAsync("X-GM-EXT-1"))
            {
                _FetchHeaders = "X-GM-MSGID X-GM-THRID X-GM-LABELS ";
            }
        }

        internal override Task OnLogoutAsync()
        {
            if (IsConnected)
                return SendCommandAsync(GetTag() + "LOGOUT");

            return Task.FromResult(0);
        }

        public virtual async Task<Namespaces> Namespace()
        {
            if (!await SupportsAsync("NAMESPACE"))
                throw new NotSupportedException("This command is not supported by the server!");
            await IdlePauseAsync();

            string command = GetTag() + "NAMESPACE";
            string response = await SendCommandGetResponseAsync(command);

            if (!response.StartsWith("* NAMESPACE"))
            {
                throw new Exception("Unknown server response !");
            }

            response = response.Substring(12);
            Namespaces n = new Namespaces();
            //[TODO] be sure to parse correctly namespace when not all namespaces are present. NIL character
            string reg = @"\((.*?)\) \((.*?)\) \((.*?)\)$";
            Match m = Regex.Match(response, reg);
            if (m.Groups.Count != 4)
                throw new Exception("En error occure, this command is not fully supported !");
            string reg2 = "\\(\\\"(.*?)\\\" \\\"(.*?)\\\"\\)";
            Match m2 = Regex.Match(m.Groups[1].ToString(), reg2);
            while (m2.Groups.Count > 1)
            {
                n.ServerNamespace.Add(new Namespace(m2.Groups[1].Value, m2.Groups[2].Value));
                m2 = m2.NextMatch();
            }
            m2 = Regex.Match(m.Groups[2].ToString(), reg2);
            while (m2.Groups.Count > 1)
            {
                n.UserNamespace.Add(new Namespace(m2.Groups[1].Value, m2.Groups[2].Value));
                m2 = m2.NextMatch();
            }
            m2 = Regex.Match(m.Groups[3].ToString(), reg2);
            while (m2.Groups.Count > 1)
            {
                n.SharedNamespace.Add(new Namespace(m2.Groups[1].Value, m2.Groups[2].Value));
                m2 = m2.NextMatch();
            }
            await GetResponseAsync();
            await IdleResumeAsync();
            return n;
        }

        public virtual async Task<int> GetMessageCountAsync()
        {
            await CheckMailboxSelectedAsync();
            return await GetMessageCountAsync(null);
        }
        public virtual async Task<int> GetMessageCountAsync(string mailbox)
        {
            await IdlePauseAsync();

            string command = GetTag() + "STATUS " + Utilities.QuoteString(ModifiedUtf7Encoding.Encode(mailbox) ?? _SelectedMailbox) + " (MESSAGES)";
            string response = await SendCommandGetResponseAsync(command);
            string reg = @"\* STATUS.*MESSAGES (\d+)";
            int result = 0;
            while (response.StartsWith("*"))
            {
                Match m = Regex.Match(response, reg);
                if (m.Groups.Count > 1)
                    result = Convert.ToInt32(m.Groups[1].ToString());
                response = await GetResponseAsync();
                m = Regex.Match(response, reg);
            }
            await IdleResumeAsync();
            return result;
        }

        public virtual async Task RenameMailboxAsync(string frommailbox, string tomailbox)
        {
            await IdlePauseAsync();

            string command = GetTag() + "RENAME " + frommailbox.QuoteString() + " " + tomailbox.QuoteString();
            await SendCommandCheckOKAsync(command);
            await IdleResumeAsync();
        }

        public virtual Task<string[]> SearchAsync(SearchCondition criteria, bool uid = true)
        {
            return SearchAsync(criteria.ToString(), uid);
        }

        public virtual async Task<string[]> SearchAsync(string criteria, bool uid = true)
        {
            await CheckMailboxSelectedAsync();

            string isuid = uid ? "UID " : "";
            string tag = GetTag();
            string command = tag + isuid + "SEARCH " + criteria;
            string response = await SendCommandGetResponseAsync(command);

            if (!response.StartsWith("* SEARCH", StringComparison.InvariantCultureIgnoreCase) && !IsResultOK(response))
            {
                throw new Exception(response);
            }

            string temp;
            while (!(temp = await GetResponseAsync()).StartsWith(tag))
            {
                response += Environment.NewLine + temp;
            }

            var m = Regex.Match(response, @"^\* SEARCH (.*)");
            return m.Groups[1].Value.Trim().Split(' ').Where(x => !string.IsNullOrEmpty(x)).ToArray();
        }
        /*
        public virtual Lazy<MailMessage>[] SearchMessages(SearchCondition criteria, bool headersonly = false, bool setseen = false)
        {
            return Search(criteria, true)
                    .Select(x => new Lazy<MailMessage>(() => GetMessage(x, headersonly, setseen)))
                    .ToArray();
        }
        */
        public virtual async Task<Mailbox> SelectMailboxAsync(string mailboxName)
        {
            await IdlePauseAsync();

            mailboxName = ModifiedUtf7Encoding.Encode(mailboxName);
            var tag = GetTag();
            var command = tag + "SELECT " + mailboxName.QuoteString();
            var response = await SendCommandGetResponseAsync(command);
            if (IsResultOK(response))
                response = await GetResponseAsync();
            var mailbox = new Mailbox(mailboxName);
            Match match;

            while (response.StartsWith("*"))
            {
                if ((match = Regex.Match(response, @"\d+(?=\s+EXISTS)")).Success)
                    mailbox.NumMsg = match.Value.ToInt();

                else if ((match = Regex.Match(response, @"\d+(?=\s+RECENT)")).Success)
                    mailbox.NumNewMsg = match.Value.ToInt();

                else if ((match = Regex.Match(response, @"(?<=UNSEEN\s+)\d+")).Success)
                    mailbox.NumUnSeen = match.Value.ToInt();

                else if ((match = Regex.Match(response, @"(?<=\sFLAGS\s+\().*?(?=\))")).Success)
                    mailbox.SetFlags(match.Value);

                response = await GetResponseAsync();
            }

            CheckResultOK(response);
            mailbox.IsWritable = Regex.IsMatch(response, "READ.WRITE", RegexOptions.IgnoreCase);
            _SelectedMailbox = mailboxName;

            await IdleResumeAsync();
            return mailbox;
        }

        public virtual Task SetFlagsAsync(Flags flags, params MailMessage[] msgs)
        {
            return SetFlagsAsync(Utilities.FlagsToFlagString(flags), msgs);
        }

        public virtual async Task SetFlagsAsync(string flags, params MailMessage[] msgs)
        {
            await StoreAsync("UID " + string.Join(",", msgs.Select(x => x.Uid)), StoreMode.Replace, flags);
            foreach (var msg in msgs)
            {
                msg.SetFlags(flags);
            }
        }

        public virtual async Task AddFlagsAsync(Flags flags, IEnumerable<MailMessage> msgs)
        {
            await StoreAsync("UID " + string.Join(",", msgs.Select(x => x.Uid)), StoreMode.Add, Utilities.FlagsToFlagString(flags));
            foreach (var msg in msgs)
            {
                Flags newFlags = (msg.Flags | flags);
                msg.SetFlags(Utilities.FlagsToFlagString(newFlags));
            }
        }

        public virtual async Task RemoveFlagsAsync(Flags flags, IEnumerable<MailMessage> msgs)
        {
            await StoreAsync("UID " + string.Join(",", msgs.Select(x => x.Uid)), StoreMode.Remove, Utilities.FlagsToFlagString(flags));
            foreach (var msg in msgs)
            {
                Flags newFlags = (msg.Flags & ~flags);
                msg.SetFlags(Utilities.FlagsToFlagString(newFlags));
            }
        }

        public virtual async Task StoreAsync(string messageset, StoreMode mode, string flags)
        {
            await CheckMailboxSelectedAsync();
            await IdlePauseAsync();
            string prefix = null;
            if (messageset.StartsWith("UID ", StringComparison.OrdinalIgnoreCase))
            {
                messageset = messageset.Substring(4);
                prefix = "UID ";
            }

            string modeString = string.Empty;
            if (mode == StoreMode.Add) modeString = "+";
            else if (mode == StoreMode.Remove) modeString = "-";

            string command = string.Concat(GetTag(), prefix, "STORE ", messageset, " ", modeString, "FLAGS.SILENT (" + flags + ")");
            string response = await SendCommandGetResponseAsync(command);
            while (response.StartsWith("*"))
            {
                response = await GetResponseAsync();
            }
            CheckResultOK(response);
            await IdleResumeAsync();
        }

        public virtual async Task SuscribeMailboxAsync(string mailbox)
        {
            await IdlePauseAsync();

            string command = GetTag() + "SUBSCRIBE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString();
            await SendCommandCheckOKAsync(command);
            await IdleResumeAsync();
        }

        public virtual async Task UnSuscribeMailboxAsync(string mailbox)
        {
            await IdlePauseAsync();

            string command = GetTag() + "UNSUBSCRIBE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString();
            await SendCommandCheckOKAsync(command);
            await IdleResumeAsync();
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
            response = response.Substring(response.IndexOf(" ")).Trim();
            return response.ToUpper().StartsWith("OK");
        }
    }
}
