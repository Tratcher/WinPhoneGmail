using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols.Smtp;
using Xunit;

namespace WinPhone.Mail.Protocols.Tests
{
    public class EncodingTests
    {
        [Fact]
        public async Task EncodeBody()
        {
            string body =
@"This line is normal
This line has a . in it
This line is very long and should get broken up, after a few more characters maybe
this line ends in three spaces   
this line should end in spaces, if it gets wrapped like that                  but did it?
";
            string expected =
@"This line is normal
This line has a =2E in it
This line is very long and should get broken up, after a few more characters=
 maybe
this line ends in three spaces  =20
this line should end in spaces, if it gets wrapped like that               =20=
  but did it?
";

            MemoryStream stream = new MemoryStream();
            Encoding utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            StreamWriter writer = new StreamWriter(stream, utf8, 1024, leaveOpen: true);
            await SmtpClient.EncodeBodyAsync(writer, body);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            string result = new StreamReader(stream, Encoding.ASCII).ReadToEnd();

            Assert.Equal(expected, result);
        }
    }
}
