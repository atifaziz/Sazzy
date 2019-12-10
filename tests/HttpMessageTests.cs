namespace Sazzy.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using NUnit.Framework;
    using Sazzy;

    public partial class HttpMessageTests
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
                + crlf
                + "7" + crlf
                + "Mozilla" + crlf
                + "9" + crlf
                + "Developer" + crlf
                + "7" + crlf
                + "Network" + crlf
                + "0" + crlf
                + crlf;

            var ascii = Encoding.ASCII;
            using var input = new MemoryStream(ascii.GetBytes(response));
            using var hs = new HttpMessage(input);

            Assert.That(hs.IsResponse, Is.True);
            Assert.That(hs.HttpVersion, Is.EqualTo(new Version(1, 1)));
            Assert.That(hs.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(hs.ReasonPhrase, Is.EqualTo("OK"));

            Assert.That(hs.RequestUrl, Is.Null);
            Assert.That(hs.RequestMethod, Is.Null);

            Assert.That(hs.Headers.Count, Is.EqualTo(2));

            using var h = hs.Headers.GetEnumerator();

            Assert.That(h.MoveNext(), Is.True);
            Assert.That(h.Current.Key, Is.EqualTo("Content-Type"));
            Assert.That(h.Current.Value, Is.EqualTo("text/plain"));

            Assert.That(h.MoveNext(), Is.True);
            Assert.That(h.Current.Key, Is.EqualTo("Transfer-Encoding"));
            Assert.That(h.Current.Value, Is.EqualTo("chunked"));

            Assert.That(h.MoveNext(), Is.False);

            using var output = new MemoryStream();
            hs.ContentStream.CopyTo(output);
            var content = ascii.GetString(output.ToArray());

            Assert.That(content, Is.EqualTo("MozillaDeveloperNetwork"));
        }

        [Test]
        public void HeaderValuesAreOptional()
        {
            const string crlf = "\r\n";
            const string response
                = "HTTP/1.1 200 OK" + crlf
                + "Content-Type: text/plain" + crlf
                + "Content-Length:" + crlf
                + "Content-Length: 0" + crlf
                + crlf;

            var ascii = Encoding.ASCII;
            using var input = new MemoryStream(ascii.GetBytes(response));
            using var hs = new HttpMessage(input);

            Assert.That(hs.IsResponse, Is.True);
            Assert.That(hs.HttpVersion, Is.EqualTo(new Version(1, 1)));
            Assert.That(hs.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(hs.ReasonPhrase, Is.EqualTo("OK"));

            Assert.That(hs.RequestUrl, Is.Null);
            Assert.That(hs.RequestMethod, Is.Null);

            Assert.That(hs.Headers.Count, Is.EqualTo(3));

            using var h = hs.Headers.GetEnumerator();

            Assert.That(h.MoveNext(), Is.True);
            Assert.That(h.Current.Key, Is.EqualTo("Content-Type"));
            Assert.That(h.Current.Value, Is.EqualTo("text/plain"));

            Assert.That(h.MoveNext(), Is.True);
            Assert.That(h.Current.Key, Is.EqualTo("Content-Length"));
            Assert.That(h.Current.Value, Is.EqualTo(string.Empty));

            Assert.That(h.MoveNext(), Is.True);
            Assert.That(h.Current.Key, Is.EqualTo("Content-Length"));
            Assert.That(h.Current.Value, Is.EqualTo("0"));

            Assert.That(h.MoveNext(), Is.False);

            Assert.That(hs.ContentLength, Is.EqualTo(0));
        }

        [Test]
        public void HeaderFolding()
        {
            const string crlf = "\r\n";

            var ua = new[]
            {
                "Mozilla/5.0 (Macintosh; Intel Mac OS X x.y; rv:42.0)",
                " \t \t Gecko/20100101 ",
                "\t \t Firefox/42.0",
            };

            var request
                = "GET / HTTP/1.1" + crlf
                + "User-Agent: " + string.Join(crlf, ua) + crlf
                + "Host: www.example.com" + crlf
                + crlf;

            var ascii = Encoding.ASCII;
            using var input = new MemoryStream(ascii.GetBytes(request));
            using var hs = new HttpMessage(input);

            Assert.That(hs.HttpVersion, Is.EqualTo(new Version(1, 1)));
            Assert.That(hs.RequestUrl.OriginalString, Is.EqualTo("/"));
            Assert.That(hs.RequestMethod, Is.EqualTo("GET"));

            Assert.That(hs.StatusCode, Is.EqualTo((HttpStatusCode) 0));
            Assert.That(hs.ReasonPhrase, Is.Null);

            Assert.That(hs.Headers.Count, Is.EqualTo(2));

            using var h = hs.Headers.GetEnumerator();

            Assert.That(h.MoveNext(), Is.True);
            Assert.That(h.Current.Key, Is.EqualTo("User-Agent"));
            Assert.That(h.Current.Value, Is.EqualTo(string.Join(string.Empty, ua)));

            Assert.That(h.MoveNext(), Is.True);
            Assert.That(h.Current.Key, Is.EqualTo("Host"));
            Assert.That(h.Current.Value, Is.EqualTo("www.example.com"));

            Assert.That(h.MoveNext(), Is.False);
        }

        static IEnumerable<TestCaseData> NodeHttpParserTestCases() =>
            from m in NodeHttpParserTestData.RequestMessages.Concat(NodeHttpParserTestData.ResponseMessages)
            let data = new TestCaseData(m).SetDescription(m.Name)
            select m.Ignore is string r ? data.Ignore(r) : data;

        [TestCaseSource(nameof(NodeHttpParserTestCases))]
        public void NodeHttpParserSample(NodeHttpParserTestData.Message message)
        {
            var chunkSizes = new List<long>();

            void OnChunkSizeRead(long size) =>
                chunkSizes.Add(size);

            var ms = message.OpenRawStream();
            var hs = new HttpMessage(ms, OnChunkSizeRead);

            Assert.That(hs.HttpVersion.Major, Is.EqualTo(message.HttpMajor));
            Assert.That(hs.HttpVersion.Minor, Is.EqualTo(message.HttpMinor));

            if (message.Type == HttpParserType.Request)
            {
                Assert.That(hs.RequestMethod, Is.EqualTo(message.Method.ToString().ToUpperInvariant()));
                Assert.That(hs.RequestUrl.OriginalString, Is.EqualTo(message.RequestUrl));

                var exampleRequestUrl = new Lazy<Uri>(() =>
                    new Uri(new Uri("http://www.example.com/"), hs.RequestUrl.OriginalString));

                if (message.Host != null)
                    Assert.That(exampleRequestUrl.Value.Host, Is.EqualTo(message.Host));

                if (message.Port > 0)
                    Assert.That(exampleRequestUrl.Value.Port, Is.EqualTo(message.Port));

                if (message.UserInfo != null)
                {
                    var userInfo = exampleRequestUrl.Value.GetComponents(UriComponents.UserInfo, UriFormat.UriEscaped);
                    Assert.That(userInfo, Is.EqualTo(message.UserInfo));
                }

                if (!string.IsNullOrEmpty(message.RequestPath))
                {
                    var path = exampleRequestUrl.Value.GetComponents(UriComponents.Path, UriFormat.Unescaped);
                    Assert.That("/" + path, Is.EqualTo(message.RequestPath));
                }

                if (message.QueryString != null)
                {
                    var qs = exampleRequestUrl.Value.GetComponents(UriComponents.Query, UriFormat.Unescaped);
                    Assert.That(qs.TrimStart('?'), Is.EqualTo(message.QueryString));
                }

                if (message.Fragment != null)
                {
                    var fragment = exampleRequestUrl.Value.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
                    Assert.That(fragment.TrimStart('#'), Is.EqualTo(message.Fragment));
                }
            }
            else
            {
                Assert.That(hs.StatusCode, Is.EqualTo((HttpStatusCode) message.StatusCode));
                Assert.That(hs.ReasonPhrase, Is.EqualTo(message.ResponseStatus));
            }

            Assert.That(hs.Headers, Is.EqualTo(message.Headers));

            if (!TestContent(hs.ContentStream, message.Body))
                TestContent(ms.Buffer(), message.Upgrade);

            if (message.ChunkLengths != null)
            {
                Assert.That(chunkSizes.Count, Is.EqualTo(message.NumChunksComplete));
                Assert.That(chunkSizes.SkipLast(1), Is.EqualTo(message.ChunkLengths));
            }

            bool TestContent(Stream content, string body)
            {
                if (body == null)
                    return false;

                using (var e = body.GetEnumerator())
                for (var offset = 0; e.MoveNext(); offset++)
                {
                    var b = content.ReadByte();
                    Assert.That(b, Is.EqualTo(e.Current), "Byte offset = {0}", offset);
                }

                Assert.That(content.ReadByte(), Is.EqualTo(-1), "EOF.");
                return true;
            }
        }
    }
}
