using System;

namespace WinPhone.Mail.Protocols
{
    public class MailAddress
    {
        public MailAddress(string address)
            : this(address, string.Empty)
        {
        }

        public MailAddress(string address, string displayName)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentNullException(address);
            }

            Address = address;
            DisplayName = displayName ?? string.Empty;
        }

        public string Address { get; private set; }

        public string DisplayName { get; private set; }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return string.Format("\"{0}\" <{1}>", DisplayName, Address);
            }

            return Address;
        }
    }
}
