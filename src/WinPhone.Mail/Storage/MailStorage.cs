using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols;
using WinPhone.Mail.Protocols.Gmail;

namespace WinPhone.Mail.Storage
{
    public static class MailStorage
    {
        private const string AccountDir = "Accounts";
        private const string LabelsFile = "Labels.csv";
        private const string LabelsDir = "Labels";
        private const string ConversationsDir = "Conversations";

        // Gets the list of labels and their settings.
        public static async Task<List<LabelInfo>> GetLabelsAsync(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new ArgumentNullException("account");
            }

            string path = Path.Combine(AccountDir, accountName, LabelsFile);

            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
            if (!storage.FileExists(path))
            {
                return null;
            }

            List<LabelInfo> labels = new List<LabelInfo>();

            IsolatedStorageFileStream stream = storage.OpenFile(path, FileMode.Open);
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
                            Sync = bool.Parse(items[1]),
                            Color = items[2]
                        });
                    }
                    line = await reader.ReadLineAsync();
                }
            }

            return labels;
        }

        // Stores the list of labels and their settings.
        public static async Task SaveLabelsAsync(string accountName, List<LabelInfo> labels)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new ArgumentNullException("account");
            }
            if (labels == null || labels.Count == 0)
            {
                throw new ArgumentNullException("labels");
            }

            string dir = Path.Combine(AccountDir, accountName);
            string path = Path.Combine(dir, LabelsFile);

            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();

            if (!storage.DirectoryExists(dir))
            {
                storage.CreateDirectory(dir);
            }
            IsolatedStorageFileStream stream = storage.OpenFile(path, FileMode.OpenOrCreate);
            using (StreamWriter writer = new StreamWriter(stream.AsOutputStream().AsStreamForWrite()))
            {
                for (int i = 0; i < labels.Count; i++)
                {
                    LabelInfo label = labels[i];
                    string line = string.Format(CultureInfo.InvariantCulture, "\"{0}\", {1}, {2}\r\n", 
                        label.Name, label.Sync, label.Color);
                    await writer.WriteAsync(line);
                }
            }
        }

        // Stores a list of all the conversation IDs associated with this label.
        // One id per line.
        public static async Task StoreLabelConversationListAsync(string accountName, string labelName, List<ConversationThread> conversations)
        {
            string labelsDir = Path.Combine(AccountDir, accountName, LabelsDir);
            string labelsFilePath = Path.Combine(labelsDir, labelName + ".csv");

            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();

            if (!storage.DirectoryExists(labelsDir))
            {
                storage.CreateDirectory(labelsDir);
            }

            using (var writer = new StreamWriter(storage.CreateFile(labelsFilePath)))
            {
                foreach (var conversation in conversations)
                {
                    await writer.WriteLineAsync(conversation.ID);
                }
            }
        }

        // Gets a list of all the conversation IDs associated with this label.
        // One id per line.
        private static async Task<List<string>> GetLabelConversationListAsync(string accountName, string labelName)
        {
            string labelsDir = Path.Combine(AccountDir, accountName, LabelsDir);
            string labelsFilePath = Path.Combine(labelsDir, labelName + ".csv");

            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();

            if (!storage.DirectoryExists(labelsDir) || !storage.FileExists(labelsFilePath))
            {
                return null;
            }

            List<string> conversationIds = new List<string>();

            using (var reader = new StreamReader(storage.OpenFile(labelsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                string line = await reader.ReadLineAsync();
                while (line != null)
                {
                    conversationIds.Add(line);
                    line = await reader.ReadLineAsync();
                }
            }

            return conversationIds;
        }

        // Stores all the given conversations
        public static async Task StoreConverationsAsync(string accountName, List<ConversationThread> conversations)
        {
            string conversationsDir = Path.Combine(AccountDir, accountName, ConversationsDir);

            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();

            foreach (var conversation in conversations)
            {
                string conversationDir = Path.Combine(conversationsDir, conversation.ID);
                if (!storage.DirectoryExists(conversationDir))
                {
                    storage.CreateDirectory(conversationDir);
                }

                foreach (var message in conversation.Messages)
                {
                    await StoreMessageAsync(storage, conversationDir, message);
                }
            }
        }

        // Get all the conversations listed under the given label
        public static async Task<List<ConversationThread>> GetConversationsAsync(string accountName, string labelName)
        {
            List<string> converationIds = await GetLabelConversationListAsync(accountName, labelName);
            if (converationIds == null)
            {
                return null;
            }

            List<ConversationThread> conversations = new List<ConversationThread>();

            foreach (var converationId in converationIds)
            {
                ConversationThread conversation = await GetConversationAsync(accountName, converationId);
                if (conversation != null)
                {
                    conversations.Add(conversation);
                }
            }

            return conversations;
        }

        private static async Task<ConversationThread> GetConversationAsync(string accountName, string converationId)
        {
            string conversationDir = Path.Combine(AccountDir, accountName, ConversationsDir, converationId);

            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();

            if (!storage.DirectoryExists(conversationDir))
            {
                return null;
            }

            List<MailMessage> messages = new List<MailMessage>();

            foreach (string messageFile in storage.GetFileNames(conversationDir + @"\*.msg"))
            {
                MailMessage message = await GetMessageAsync(storage, Path.Combine(conversationDir, messageFile));
                messages.Add(message);
            }

            messages = messages.OrderByDescending(message => message.Date).ToList();

            return new ConversationThread(messages);
        }

        private static Task StoreMessageAsync(IsolatedStorageFile storage, string conversationDir, MailMessage message)
        {
            string messageFile = Path.Combine(conversationDir, message.Uid + ".msg");

            using (Stream fileStream = storage.CreateFile(messageFile))
            {
                // TODO: Async
                message.Save(fileStream);
            }
            return Task.FromResult(0);
        }

        private static Task<MailMessage> GetMessageAsync(IsolatedStorageFile storage, string messageFile)
        {
            using (Stream fileStream = storage.OpenFile(messageFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                MailMessage message = new MailMessage();

                // TODO: Async
                message.Load(fileStream, headersOnly: false, maxLength: 1024 * 1024);

                return Task.FromResult(message);
            }
        }

        public static Task DeleteLabelAsync(string labelName)
        {
            // TODO: Remove label index file.

            // TODO: Remove any message threads that were only referenced by this thread index.
            // This can be done by opening the threads and identifying other labels on the messages.
            // If the message thread is not listed in any of those label indexes then it should be deleted.

            return Task.FromResult(0);
        }

        public static void DeleteAccount(string accountName)
        {
            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
            string dir = Path.Combine(AccountDir, accountName);
            if (storage.DirectoryExists(dir))
            {
                DeleteDirectory(storage, dir);
            }
        }

        public static void ClearAll()
        {
            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
            if (storage.DirectoryExists(AccountDir))
            {
                DeleteDirectory(storage, AccountDir);
            }
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
    }
}
