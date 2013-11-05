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

namespace WinPhone.Mail.Storage
{
    public static class MailStorage
    {
        private const string AccountDir = "Accounts";
        private const string LabelsFile = "Labels.csv";

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

        public static Task DeleteLabelAsync(string labelName)
        {
            // TODO: Remove label index file.

            // TODO: Remove any message threads that were only referenced by this thread index.
            // This can be done by opening the threads and identifying other labels on the messages.
            // If the message thread is not listed in any of those label indexes then it should be deleted.

            return Task.FromResult(0);
        }
    }
}
