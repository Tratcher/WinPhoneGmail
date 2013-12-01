using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
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

            IsolatedStorageFileStream stream = _storage.OpenFile(path, FileMode.Open);
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
                            Store = bool.Parse(items[1]),
                            Color = items[2]
                        });
                    }
                    line = await reader.ReadLineAsync();
                }
            }

            return labels;
        }

        // Stores the list of labels and their settings.
        public async Task SaveLabelInfoAsync(List<LabelInfo> labels)
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
                    string line = string.Format(CultureInfo.InvariantCulture, "\"{0}\", {1}, {2}\r\n", 
                        label.Name, label.Store, label.Color);
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
                string conversationDir = Path.Combine(conversationsDir, conversation.ID);
                if (!_storage.DirectoryExists(conversationDir))
                {
                    _storage.CreateDirectory(conversationDir);
                }

                foreach (var message in conversation.Messages)
                {
                    await StoreMessageAsync(conversationDir, message);
                }
            }
        }

        // Get all the conversations listed under the given label
        public async Task<List<ConversationThread>> GetConversationsAsync(string labelName)
        {
            List<MessageIdInfo> messageIds = await GetLabelMessageListAsync(labelName);
            if (messageIds == null)
            {
                return null;
            }

            List<MailMessage> messages = new List<MailMessage>();

            foreach (var messageId in messageIds)
            {
                string uid = messageId.Uid;
                string googleUid = messageId.MessageId;
                string conversationId = messageId.ThreadId;
                string conversationDir = Path.Combine(AccountDir, _accountName, ConversationsDir, conversationId);

                MailMessage message = await GetMessageAsync(conversationDir, googleUid, uid);
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
            return conversations;
        }

        public Task StoreMessageAsync(MailMessage message)
        {
            string conversationDir = Path.Combine(AccountDir, _accountName, ConversationsDir, message.GetThreadId());
            return StoreMessageAsync(conversationDir, message);
        }

        private Task StoreMessageAsync(string conversationDir, MailMessage message)
        {
            string messageFile = Path.Combine(conversationDir, message.GetMessageId() + ".msg");

            using (Stream fileStream = _storage.CreateFile(messageFile))
            {
                // TODO: Async
                message.Save(fileStream);
            }
            return Task.FromResult(0);
        }

        private Task<MailMessage> GetMessageAsync(string conversationDir, string googleUid, string labelUid)
        {
            string messageFile = Path.Combine(conversationDir, googleUid + ".msg");
            if (!_storage.FileExists(messageFile))
            {
                return null;
            }
            using (Stream fileStream = _storage.OpenFile(messageFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                MailMessage message = new MailMessage();

                // TODO: Async
                message.Load(fileStream, headersOnly: false, maxLength: 1024 * 1024);
                message.Uid = labelUid;

                return Task.FromResult(message);
            }
        }

        public bool MessageIsStored(MailMessage message)
        {
            string conversationDir = Path.Combine(AccountDir, _accountName, ConversationsDir, message.GetThreadId());
            string messageFile = Path.Combine(conversationDir, message.GetMessageId() + ".msg");

            return _storage.FileExists(messageFile);
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
