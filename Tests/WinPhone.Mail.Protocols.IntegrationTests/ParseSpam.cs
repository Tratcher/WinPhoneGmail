using WinPhone.Mail.Protocols;
using Shouldly;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Tests {
	/// <summary>
	/// Spam downloaded from http://www.untroubled.org/spam/
	/// </summary>
	public class ParseSpam {

		[Fact]
		public void Parse_Spam() {
			var dir = System.IO.Path.Combine(Environment.CurrentDirectory.Split(new[] { "AE.Net.Mail" }, StringSplitOptions.RemoveEmptyEntries)[0],
				"AE.Net.Mail\\Tests\\Spam");
			var files = System.IO.Directory.GetFiles(dir, "*.lorien", System.IO.SearchOption.AllDirectories);

			var mindate = new DateTime(1900, 1, 1).Ticks;
			var maxdate = DateTime.MaxValue.Ticks;
			var rxSubject = new Regex(@"^Subject\:\s+\S+");
			MailMessage msg = new MailMessage();
			for (var i = 0; i < files.Length; i++) {
				var file = files[i];
				var txt = System.IO.File.ReadAllText(file);
				using (var stream = System.IO.File.OpenRead(file))
					msg.Load(stream, Scope.HeadersAndBody, (int)stream.Length);

				if (msg.ContentTransferEncoding.IndexOf("quoted", StringComparison.OrdinalIgnoreCase) == -1) {
					continue;
				}

				if (string.IsNullOrEmpty(msg.Body)) {
					continue;
				}

				try {

					msg.Date.Ticks.ShouldBeInRange(mindate, maxdate);
                    Assert.True(!string.IsNullOrEmpty(msg.Subject) || !rxSubject.IsMatch(txt));
					//msg.From.ShouldBe();
					if (msg.To.Count > 0) msg.To.First().ShouldBe();
					if (msg.Cc.Count > 0) msg.Cc.First().ShouldBe();
					if (msg.Bcc.Count > 0) msg.Bcc.First().ShouldBe();

					(msg.Body ?? string.Empty).Trim().ShouldNotBeNullOrEmpty();


				} catch (Exception ex) {
					Console.WriteLine(ex);
					Console.WriteLine(txt);
					throw;
				}
			}
		}
	}
}
