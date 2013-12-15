using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail.Gmail.Shared.Storage
{
    public class MailStorage
    {
        private const string AccountDir = "Accounts";
        private const string LabelsFile = "Labels.csv";
        private const string LabelsDir = "Labels";
        private const string ConversationsDir = "Conversations";
        private const string HeadersFile = "Headers.txt";
        private const string FlagsFile = "Flags.txt";
        private const string MessageLabelsFile = "Labels.txt";
        private const string MessagePartFile = "BodyPart{0}.txt";

        private readonly string _accountName;
        private readonly IsolatedStorageFile _storage;

        public MailStorage(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new ArgumentNullException("accountName");
            }

            _accountName = accountName;
            _storage = IsolatedStorageFile.GetUserStoreForApplication();
        }

        // Gets the list of labels and their settings.
        public async Task<List<LabelInfo>> GetLabelInfoAsync()
        {
            string path = Path.Combine(AccountDir, _accountName, LabelsFile);

            if (!_storage.FileExists(path))
            {
                return null;
            }

            List<LabelInfo> labels = new List<LabelInfo>();

            IsolatedStorageFileStream stream = _storage.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using (StreamReader reader = new StreamReader(stream.AsInputStream().AsStreamForRead()))
            {
                string line = await reader.ReadLineAsync();
                while (line != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        List<string> items = Utilities.SplitQuotedList(line, ',');
                        Debug.Assert(items.Count == 3);
                        labels.Add(new LabelInfo()
                        {
                            Name = Utilities.RemoveQuotes(items[0]),
                            StoreMessages = bool.Parse(items[1]),
                            StoreAttachments = bool.Parse(items[2]),
                            Color = items[3],
                        });
                    }
                    line = await reader.ReadLineAsync();
                }
            }

            return labels;
        }

        // Stores the list of labels and their settings.
        public async Task SaveLabelInfoAsync(IList<LabelInfo> labels)
        {
            if (labels == null || labels.Count == 0)
            {
                throw new ArgumentNullException("labels");
            }

            string dir = Path.Combine(AccountDir, _accountName);
            string path = Path.Combine(dir, LabelsFile);

            if (!_storage.DirectoryExists(dir))
            {
                _storage.CreateDirectory(dir);
            }
            IsolatedStorageFileStream stream = _storage.OpenFile(path, FileMode.OpenOrCreate);
            using (StreamWriter writer = new StreamWriter(stream.AsOutputStream().AsStreamForWrite()))
            {
                for (int i = 0; i < labels.Count; i++)
                {
                    LabelInfo label = labels[i];
                    string line = string.Format(CultureInfo.InvariantCulture, "\"{0}\", {1}, {2}, {3}\r\n",
                        label.Name, label.StoreMessages, label.StoreAttachments, label.Color);
                    await writer.WriteAsync(line);
                }
            }
        }

        // Stores a list of all the conversation IDs associated with this label.
        // One id per line.
        public Task StoreLabelMessageListAsync(string labelName, List<ConversationThread> conversations)
        {
            List<MessageIdInfo> messageIds = new List<MessageIdInfo>();

            foreach (var conversation in conversations)
            {
                foreach (var message in conversation.Messages)
                {
                    messageIds.Add(new MessageIdInfo()
                    {
                        Uid = message.Uid,
                        MessageId = message.GetMessageId(),
                        ThreadId = message.GetThreadId()
                    });
                }
            }

            return StoreLabelMessageListAsync(labelName, messageIds);
        }

        // Stores a list of all the conversation IDs associated with this label.
        // One id per line.
        public async Task StoreLabelMessageListAsync(string labelName, List<MessageIdInfo> messageIds)
        {
            string labelsDir = Path.Combine(AccountDir, _accountName, LabelsDir);
            string labelsFilePath = Path.Combine(labelsDir, EscapeLabelName(labelName) + ".csv");

            if (!_storage.DirectoryExists(labelsDir))
            {
                _storage.CreateDirectory(labelsDir);
            }

            using (var writer = new StreamWriter(_storage.CreateFile(labelsFilePath)))
            {
                foreach (var ids in messageIds)
                {
                    await writer.WriteLineAsync(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2}", ids.Uid, ids.MessageId, ids.ThreadId));
                }
            }
        }

        // Gets a list of all the conversation IDs associated with this label.
        // One id per line.
        public async Task<List<MessageIdInfo>> GetLabelMessageListAsync(string labelName)
        {
            string labelsDir = Path.Combine(AccountDir, _accountName, LabelsDir);
            string labelsFilePath = Path.Combine(labelsDir, EscapeLabelName(labelName) + ".csv");

            if (!_storage.DirectoryExists(labelsDir) || !_storage.FileExists(labelsFilePath))
            {
                return null;
            }

            List<MessageIdInfo> messageIds = new List<MessageIdInfo>();

            using (var reader = new StreamReader(_storage.OpenFile(labelsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                string line = await reader.ReadLineAsync();
                while (line != null)
                {
                    string[] parts = line.Split(',');
                    MessageIdInfo ids = new MessageIdInfo() { Uid = parts[0], MessageId = parts[1], ThreadId = parts[2] };
                    messageIds.Add(ids);
                    line = await reader.ReadLineAsync();
                }
            }

            return messageIds;
        }

        public Task DeleteLabelMessageListAsync(string labelName)
        {
            string labelsDir = Path.Combine(AccountDir, _accountName, LabelsDir);
            string labelsFilePath = Path.Combine(labelsDir, EscapeLabelName(labelName) + ".csv");

            if (!_storage.DirectoryExists(labelsDir) || !_storage.FileExists(labelsFilePath))
            {
                return Task.FromResult(0);
            }

            // Remove label index file.
            _storage.DeleteFile(labelsFilePath);

            // TODO: Remove any message threads that were only referenced by this thread index.
            // This can be done by opening the threads and identifying other labels on the messages.
            // If the message thread is not listed in any of those label indexes then it should be deleted.

            return Task.FromResult(0);
        }

        // GMail system labels look like: [Gmail]/All Mail.  You can also make nested labels like Label/SubLabel.
        // You can't make a file name with the forward slash, so escape it.
        private static string EscapeLabelName(string labelName)
        {
            return labelName.Replace('/', '^');
        }

        // Stores all the given conversations
        public async Task StoreConverationsAsync(List<ConversationThread> conversations)
        {
            string conversationsDir = Path.Combine(AccountDir, _accountName, ConversationsDir);

            foreach (var conversation in conversations)
            {
                foreach (var message in conversation.Messages)
                {
                    await StoreMessageAsync(message);
                }
            }
        }

        // Get all the conversations listed under the given label
        public async Task<List<ConversationThread>> GetConversationsAsync(string labelName, Scope scope)
        {
            List<MessageIdInfo> messageIds = await GetLabelMessageListAsync(labelName);
            if (messageIds == null)
            {
                return null;
            }

            List<MailMessage> messages = new List<MailMessage>();

            foreach (var messageId in messageIds)
            {
                MailMessage message = await GetMessageAsync(messageId.ThreadId, messageId.MessageId, messageId.Uid, scope);
                if (message != null)
                {
                    messages.Add(message);
                }
            }

            List<ConversationThread> conversations = new List<ConversationThread>();

            // Group by thread ID
            foreach (IGrouping<string, MailMessage> group in messages.GroupBy(message => message.GetThreadId()))
            {
                conversations.Add(new ConversationThread(group.OrderByDescending(message => message.Date).ToList()));
            }
            return conversations.OrderByDescending(message => message.LatestDate).ToList();
        }

        // Break the message up into parts: Flags, labels, headers/structure, and body parts
        public async Task StoreMessageAsync(MailMessage message)
        {
            await StoreMessageFlagsAsync(message.GetThreadId(), message.GetMessageId(), Utilities.FlagsToFlagString(message.Flags));
            await StoreMessageLabelsAsync(message.GetThreadId(), message.GetMessageId(), message.GetLabelsHeader());
            await StoreMessageHeadersAsync(message);
            if (message.Scope >= Scope.HeadersAndBodySnyppit)
            {
                // TODO: Verify BodyId is set.
                // Body parts
                if (!message.AlternateViews.Any() && !message.Attachments.Any())
                {
                    // Simple body
                    await StoreMessagePartAsync(message.GetThreadId(), message.GetMessageId(), message.BodyId, message.Body);
                }
                else
                {
                    // Multipart body
                    foreach (Attachment view in message.AlternateViews)
                    {
                        await StoreMessagePartAsync(message.GetThreadId(), message.GetMessageId(), view.BodyId, view.Body);
                    }
                    foreach (Attachment attachment in message.Attachments)
                    {
                        await StoreMessagePartAsync(message.GetThreadId(), message.GetMessageId(), attachment.BodyId, attachment.Body);
                    }
                }
            }
        }

        private async Task<MailMessage> GetMessageAsync(string conversationId, string messageId, string labelUid, Scope scope)
        {
            MailMessage message = await GetMessageHeadersAsync(conversationId, messageId);
            string labels = await GetMessageLabelsAsync(conversationId, messageId);
            string flags = await GetMessageFlagsAsync(conversationId, messageId);
            if (message == null || labels == null || flags == null)
            {
                return null;
            }
            message.SetLabels(labels);
            message.SetFlags(flags);
            message.Uid = labelUid;

            if (scope > Scope.HeadersAndMime)
            {
                if (message.AlternateViews.Any())
                {
                    foreach (Attachment view in message.AlternateViews)
                    {
                        view.Body = await GetMessagePartAsync(conversationId, messageId, view.BodyId);
                    }
                }
                else
                {
                    message.Body = await GetMessagePartAsync(conversationId, messageId, message.BodyId);
                }
            }

            return message;
        }

        public bool HasMessageFlags(string conversationId, string messageId)
        {
            string flagsFile = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId, FlagsFile);
            return _storage.FileExists(flagsFile);
        }

        public Task StoreMessageFlagsAsync(MailMessage message)
        {
            return StoreMessageFlagsAsync(message.GetThreadId(), message.GetMessageId(), Utilities.FlagsToFlagString(message.Flags));
        }

        public async Task StoreMessageFlagsAsync(string conversationId, string messageId, string flags)
        {
            string messageDir = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId);
            if (!_storage.DirectoryExists(messageDir))
            {
                _storage.CreateDirectory(messageDir);
            }
            string flagsFile = Path.Combine(messageDir, FlagsFile);
            Stream stream = _storage.CreateFile(flagsFile);
            using (StreamWriter writer = new StreamWriter(stream))
            {
                await writer.WriteLineAsync(flags);
            }
        }

        public async Task<string> GetMessageFlagsAsync(string conversationId, string messageId)
        {
            string messageDir = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId);
            string flagsFile = Path.Combine(messageDir, FlagsFile);
            if (!_storage.DirectoryExists(messageDir) || !_storage.FileExists(flagsFile))
            {
                return null;
            }
            Stream stream = _storage.OpenFile(flagsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using (StreamReader reader = new StreamReader(stream))
            {
                return await reader.ReadLineAsync();
            }
        }

        public bool HasMessageLables(string conversationId, string messageId)
        {
            string labelsFile = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId, LabelsFile);
            return _storage.FileExists(labelsFile);
        }

        public Task StoreMessageLabelsAsync(MailMessage message)
        {
            return StoreMessageLabelsAsync(message.GetThreadId(), message.GetMessageId(), message.GetLabelsHeader());
        }

        public async Task StoreMessageLabelsAsync(string conversationId, string messageId, string labels)
        {
            string messageDir = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId);
            if (!_storage.DirectoryExists(messageDir))
            {
                _storage.CreateDirectory(messageDir);
            }
            string labelsFile = Path.Combine(messageDir, MessageLabelsFile);
            Stream stream = _storage.CreateFile(labelsFile);
            using (StreamWriter writer = new StreamWriter(stream))
            {
                await writer.WriteLineAsync(labels);
            }
        }

        public async Task<string> GetMessageLabelsAsync(string conversationId, string messageId)
        {
            string messageDir = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId);
            string labelsFile = Path.Combine(messageDir, MessageLabelsFile);
            if (!_storage.DirectoryExists(messageDir) || !_storage.FileExists(labelsFile))
            {
                return null;
            }
            Stream stream = _storage.OpenFile(labelsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using (StreamReader reader = new StreamReader(stream))
            {
                return await reader.ReadLineAsync();
            }
        }

        public bool HasMessageHeaders(string conversationId, string messageId)
        {
            string headersFile = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId, HeadersFile);
            return _storage.FileExists(headersFile);
        }

        public Task StoreMessageHeadersAsync(MailMessage headers)
        {
            string messageDir = Path.Combine(AccountDir, _accountName, ConversationsDir, headers.GetThreadId(), headers.GetMessageId());
            if (!_storage.DirectoryExists(messageDir))
            {
                _storage.CreateDirectory(messageDir);
            }
            string headersFile = Path.Combine(messageDir, HeadersFile);
            using (Stream stream = _storage.CreateFile(headersFile))
            {
                // TODO: Async
                headers.Save(stream);
            }
            return Task.FromResult(0);
        }

        public Task<MailMessage> GetMessageHeadersAsync(string conversationId, string messageId)
        {
            string messageDir = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId);
            string headersFile = Path.Combine(messageDir, HeadersFile);
            if (!_storage.DirectoryExists(messageDir) || !_storage.FileExists(headersFile))
            {
                return null;
            }
            Stream stream = _storage.OpenFile(headersFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using (stream)
            {
                // TODO: Async
                MailMessage message = new MailMessage();
                message.Load(stream, Scope.HeadersAndMime, 0);
                return Task.FromResult(message);
            }
        }

        public bool HasMessagePart(string conversationId, string messageId, string partNumber)
        {
            string partFile = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId, 
                string.Format(CultureInfo.InvariantCulture, MessagePartFile, partNumber));
            return _storage.FileExists(partFile);
        }

        public async Task StoreMessagePartAsync(string conversationId, string messageId, string partNumber, string bodyPart)
        {
            string messageDir = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId);
            if (!_storage.DirectoryExists(messageDir))
            {
                _storage.CreateDirectory(messageDir);
            }
            string partFile = Path.Combine(messageDir, string.Format(CultureInfo.InvariantCulture, MessagePartFile, partNumber));
            using (StreamWriter writer = new StreamWriter(_storage.CreateFile(partFile), Encoding.UTF8))
            {
                await writer.WriteAsync(bodyPart);
            }
        }

        public async Task<string> GetMessagePartAsync(string conversationId, string messageId, string partNumber)
        {
            string messageDir = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId, messageId);
            string partFile = Path.Combine(messageDir, string.Format(CultureInfo.InvariantCulture, MessagePartFile, partNumber));
            if (!_storage.DirectoryExists(messageDir) || !_storage.FileExists(partFile))
            {
                return null;
            }
            Stream stream = _storage.OpenFile(partFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public void DeleteAccount()
        {
            string dir = Path.Combine(AccountDir, _accountName);
            if (_storage.DirectoryExists(dir))
            {
                DeleteDirectory(_storage, dir);
            }
        }

        public static void ClearAll()
        {
            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
            if (storage.DirectoryExists(AccountDir))
            {
                DeleteDirectory(storage, AccountDir);
            }

            MailStorage.DeleteTempFiles();
        }

        // Can't delete unless empty. Must recursively delete files and folders
        private static void DeleteDirectory(IsolatedStorageFile storage, string dir)
        {
            foreach (var file in storage.GetFileNames(Path.Combine(dir, "*")))
            {
                storage.DeleteFile(Path.Combine(dir, file));
            }
            foreach (var subDir in storage.GetDirectoryNames(Path.Combine(dir, "*")))
            {
                DeleteDirectory(storage, Path.Combine(dir, subDir));
            }
            storage.DeleteDirectory(dir);
        }

        public static async Task<StorageFile> SaveAttachmentToTempAsync(Attachment attachment)
        {
            StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(attachment.Filename, CreationCollisionOption.ReplaceExisting);
            IRandomAccessStream randomStream = await file.OpenAsync(FileAccessMode.ReadWrite);
            Stream writeStream = randomStream.AsStream();
            attachment.Save(writeStream);
            writeStream.Close();
            return file;
        }

        public static async void DeleteTempFiles()
        {
            IReadOnlyList<StorageFile> files = await ApplicationData.Current.LocalFolder.GetFilesAsync();
            foreach (StorageFile file in files)
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }
    }
}
