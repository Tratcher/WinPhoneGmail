
namespace WinPhone.Mail.Protocols.Imap
{
    public class Mailbox
    {
        public Mailbox() : this(string.Empty) { }
        public Mailbox(string name)
        {
            Name = ModifiedUtf7Encoding.Decode(name);
            Flags = new string[0];
        }
        public virtual string Name { get; set; }
        public virtual int NumNewMsg { get; set; }
        public virtual int NumMsg { get; set; }
        public virtual int NumUnSeen { get; set; }
        public virtual int UIDValidity { get; set; }
        public virtual string[] Flags { get; set; }
        public virtual bool IsWritable { get; set; }

        internal void SetFlags(string flags)
        {
            Flags = flags.Split(' ');
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

