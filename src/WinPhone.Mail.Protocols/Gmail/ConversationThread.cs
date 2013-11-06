﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols.Gmail
{
    public class ConversationThread
    {
        public ConversationThread(List<MailMessage> messages)
        {
            Messages = messages;
        }

        // Sorted most recent first
        public List<MailMessage> Messages { get; private set; }

        public DateTime LatestDate
        {
            get
            {
                return Messages.First().Date;
            }
        }

        public string Subject
        {
            get
            {
                return Messages.First().Subject;
            }
        }

        public List<string> Labels
        {
            get
            {
                // TODO: Cache?
                List<string> labels = new List<string>();
                foreach (var message in Messages)
                {
                    labels.AddRange(message.GetLabels());
                }
                return labels.Distinct().ToList();
            }
        }

        public bool HasUnread
        {
            get
            {
                foreach (var message in Messages)
                {
                    bool read = (message.Flags & Flags.Seen) == Flags.Seen;
                    if (!read)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}