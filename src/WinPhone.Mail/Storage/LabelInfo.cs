using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinPhone.Mail.Storage
{
    public class LabelInfo
    {
        public string Name { get; set; }

        public bool Store { get; set; }

        public string Color { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
