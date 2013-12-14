
namespace WinPhone.Mail.Gmail.Shared.Storage
{
    public class MessageIdInfo
    {
        public string Uid { get; set; }
        public string ThreadId { get; set; }
        public string MessageId { get; set; }

        public override bool Equals(object obj)
        {
            MessageIdInfo other = obj as MessageIdInfo;
            return (other != null && this.Uid.Equals(other.Uid) && this.ThreadId.Equals(other.ThreadId) && this.MessageId.Equals(other.MessageId));
        }
    }
}
