namespace Sazzy.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using NUnit.Framework;
    using Sazzy;

    public class HttpContentStreamTests
    {
        [Test]
        public void OpenWithNullStream()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                HttpContentStream.Open(null));
            Assert.That(e.ParamName, Is.EqualTo("input"));
        }

        [Test]
        public void ChunkedTransferEncoding()
        {
            const string crlf = "\r\n";
            const string response
                = "HTTP/1.1 200 OK" + crlf
                + "Content-Type: text/plain" + crlf
                + "Transfer-Encoding: chunked" + crlf
                + "" + crlf
                + "7" + crlf
                + "Mozilla" + crlf
                + "9" + crlf
                + "Developer" + crlf
                + "7" + crlf
                + "Network" + crlf
                + "0" + crlf
                + crlf;

            var ascii = Encoding.ASCII;
            var input = new MemoryStream(ascii.GetBytes(response));
            var hs = HttpContentStream.Open(input);
            var output = new MemoryStream();
            hs.CopyTo(output);
            var content = ascii.GetString(output.ToArray());

            Assert.That(content, Is.EqualTo("MozillaDeveloperNetwork"));
        }
    }
}
