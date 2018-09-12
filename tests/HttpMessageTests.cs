namespace Sazzy.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using NUnit.Framework;
    using Sazzy;

    public class HttpMessageTests
    {
        [Test]
        public void InitWithNullStream()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                new HttpMessage(null));
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
            var hs = new HttpMessage(input);

            Assert.That(hs.StartLine, Is.EqualTo("HTTP/1.1 200 OK"));

            Assert.That(hs.Headers.Count, Is.EqualTo(2));

            using (var h = hs.Headers.GetEnumerator())
            {
                Assert.That(h.MoveNext(), Is.True);
                Assert.That(h.Current.Key, Is.EqualTo("Content-Type"));
                Assert.That(h.Current.Value, Is.EqualTo("text/plain"));

                Assert.That(h.MoveNext(), Is.True);
                Assert.That(h.Current.Key, Is.EqualTo("Transfer-Encoding"));
                Assert.That(h.Current.Value, Is.EqualTo("chunked"));

                Assert.That(h.MoveNext(), Is.False);
            }

            var output = new MemoryStream();
            hs.ContentStream.CopyTo(output);
            var content = ascii.GetString(output.ToArray());

            Assert.That(content, Is.EqualTo("MozillaDeveloperNetwork"));
        }
    }
}
