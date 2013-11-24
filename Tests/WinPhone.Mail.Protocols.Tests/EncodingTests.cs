using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinPhone.Mail.Protocols.Smtp;
using Xunit;
using Xunit.Extensions;

namespace WinPhone.Mail.Protocols.Tests
{
    public class EncodingTests
    {
        [Fact]
        public async Task EncodeBody()
        {
            string body =
@"This line is normal
.This line started with a dot
This line is very long and should get broken up, after a few more characters maybe
this line ends in three spaces   
this line should end in spaces, if it gets wrapped like that                  but did it?
";
            string expected =
@"This line is normal
..This line started with a dot
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

        [Fact]
        public void DontEncodeNormalHeader()
        {
            string header = "foo: ";
            string value = "Hello World";
            string expected = "foo: Hello World";

            string result = SmtpClient.EncodeHeader(header, value);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void DontEncodeNormalHeaderButAddLineBreaks()
        {
            string header = "foo: ";
            string value = "Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World Hello World";
            string expected = "foo: Hello World Hello World Hello World Hello World Hello World Hello World\r\n  Hello World Hello World Hello World";

            string result = SmtpClient.EncodeHeader(header, value);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EncodeHeader()
        {
            string header = "foo: ";
            string value = "Hello = World 1234567890 \r \n stuff";
            string expected = "foo: =?utf-8?Q?Hello =3D World 1234567890 =0D =0A stuff?=";

            string result = SmtpClient.EncodeHeader(header, value);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EncodeHeaderAndAddLineBreak()
        {
            string header = "foo: ";
            string value = "Hello = World 1234567890 \r \n stuff World Hello World Hello World Hello World Hello World";
            string expected = "foo: =?utf-8?Q?Hello =3D World 1234567890 =0D =0A stuff World Hello World Hello Wor?=\r\n =?utf-8?Q?ld Hello World Hello World?=";

            string result = SmtpClient.EncodeHeader(header, value);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EncodeAddressHeaderOneEmail()
        {
            string header = "To: ";
            MailAddress address = new MailAddress("hello@example.com");
            string expected = "To: hello@example.com";

            string result = SmtpClient.EncodeAddressLine(header, address);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EncodeAddressHeaderEmailAndDisplay()
        {
            string header = "To: ";
            MailAddress address = new MailAddress("hello@example.com", "Hello World");
            string expected = "To: \"Hello World\" <hello@example.com>";

            string result = SmtpClient.EncodeAddressLine(header, address);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EncodeAddressHeaderTwoEmails()
        {
            string header = "To: ";
            List<MailAddress> address = new List<MailAddress>();
            address.Add(new MailAddress("hello@example.com"));
            address.Add(new MailAddress("hello2@example.com"));
            string expected = "To: hello@example.com, hello2@example.com";

            string result = SmtpClient.EncodeAddressLine(header, address);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EncodeAddressHeaderSeveral()
        {
            string header = "To: ";
            List<MailAddress> address = new List<MailAddress>();
            address.Add(new MailAddress("hello@example.com"));
            address.Add(new MailAddress("hello2@example.com", "Hello2 World"));
            address.Add(new MailAddress("hello3@example.com"));
            address.Add(new MailAddress("hello4@example.com", "Hello4 World"));
            address.Add(new MailAddress("hello5@example.com", "Hello5 World"));
            address.Add(new MailAddress("hello6@example.com"));
            address.Add(new MailAddress("hello7@example.com"));
            address.Add(new MailAddress("hello8@example.com"));
            address.Add(new MailAddress("hello9@example.com", "Hello9 World"));
            address.Add(new MailAddress("hello10@example.com"));
            string expected =
@"To: hello@example.com, ""Hello2 World"" <hello2@example.com>, 
 hello3@example.com, ""Hello4 World"" <hello4@example.com>, ""Hello5 World"" 
 <hello5@example.com>, hello6@example.com, hello7@example.com, 
 hello8@example.com, ""Hello9 World"" <hello9@example.com>, 
 hello10@example.com";

            string result = SmtpClient.EncodeAddressLine(header, address);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EncodeAddressHeaderEncodedDisplay()
        {
            string header = "To: ";
            MailAddress address = new MailAddress("hello@example.com", "Hello 如果您对该域名感兴趣 World");
            string expected =
@"To: =?utf-8?B?SGVsbG8g5aaC5p6c5oKo5a+56K+l5Z+f5ZCN5oSf5YW06LajIFdvcmxk?= 
 <hello@example.com>";

            string result = SmtpClient.EncodeAddressLine(header, address);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EncodeAddressHeaderMultipleEncodedDisplay()
        {
            string header = "To: ";
            List<MailAddress> address = new List<MailAddress>();
            address.Add(new MailAddress("hello@example.com"));
            address.Add(new MailAddress("hello1@example.com", "Hello 如果您对该域名感兴趣 World"));
            address.Add(new MailAddress("hello2@example.com", "Hello 如果您对该域名感兴趣 World"));
            address.Add(new MailAddress("hello4@example.com"));
            string expected =
@"To: hello@example.com, 
 =?utf-8?B?SGVsbG8g5aaC5p6c5oKo5a+56K+l5Z+f5ZCN5oSf5YW06LajIFdvcmxk?= 
 <hello1@example.com>, 
 =?utf-8?B?SGVsbG8g5aaC5p6c5oKo5a+56K+l5Z+f5ZCN5oSf5YW06LajIFdvcmxk?= 
 <hello2@example.com>, hello4@example.com";

            string result = SmtpClient.EncodeAddressLine(header, address);
            Assert.Equal(expected, result);
        }
    }
}
