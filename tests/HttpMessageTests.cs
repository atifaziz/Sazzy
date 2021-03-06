namespace Sazzy.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using NUnit.Framework;
    using Sazzy;

    public class HttpMessageTests
    {
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
            using var hs = HttpMessageReader.ReadResponse(input);

            Assert.That(hs.Kind, Is.EqualTo(HttpMessageKind.Response));
            Assert.That(hs.ProtocolVersion, Is.EqualTo(new Version(1, 1)));
            Assert.That(hs.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(hs.ReasonPhrase, Is.EqualTo("OK"));

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
            using var hs = HttpMessageReader.ReadResponse(input);

            Assert.That(hs.Kind, Is.EqualTo(HttpMessageKind.Response));
            Assert.That(hs.ProtocolVersion, Is.EqualTo(new Version(1, 1)));
            Assert.That(hs.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(hs.ReasonPhrase, Is.EqualTo("OK"));

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

            Assert.That(hs.ContentLength, Is.EqualTo((HttpFieldStatus.Defined, 0)));
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
            using var hs = HttpMessageReader.ReadRequest(input);

            Assert.That(hs.ProtocolVersion, Is.EqualTo(new Version(1, 1)));
            Assert.That(hs.Url.OriginalString, Is.EqualTo("/"));
            Assert.That(hs.Method, Is.EqualTo("GET"));

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

            var rawBytes = message.GetRawBytes();
            using var ms = new MemoryStream(rawBytes);
            using var hs = HttpMessageReader.Read(ms);

            ((IHttpChunkedContentEventSource)hs.ContentStream).ChunkSizeRead +=
                (_, size) => chunkSizes.Add(size);

            Assert.That(hs.ProtocolVersion.Major, Is.EqualTo(message.HttpMajor));
            Assert.That(hs.ProtocolVersion.Minor, Is.EqualTo(message.HttpMinor));

            if (message.Type == HttpParserType.Request)
            {
                var request = (HttpRequest)hs;

                Assert.That(request.Method, Is.EqualTo(message.Method == HttpRequestMethod.MSearch
                                                       ? "M-SEARCH"
                                                       : message.Method.ToString().ToUpperInvariant()));

                Assert.That(request.Url.OriginalString, Is.EqualTo(message.RequestUrl));

                var exampleRequestUrl = new Lazy<Uri>(() =>
                    new Uri(new Uri("http://www.example.com/"), request.Url.OriginalString));

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
                    Assert.That(message.Method == HttpRequestMethod.MSearch ? path : "/" + path,
                                Is.EqualTo(message.RequestPath));
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
                var response = (HttpResponse)hs;
                Assert.That(response.StatusCode, Is.EqualTo((HttpStatusCode) message.StatusCode));
                Assert.That(response.ReasonPhrase, Is.EqualTo(message.ResponseStatus));
            }

            if (message.Body != null)
            {
                foreach (var ch in message.Body)
                    Assert.That((char)hs.ContentStream.ReadByte(), Is.EqualTo(ch));
            }

            Assert.That(hs.ContentStream.ReadByte(), Is.EqualTo(-1));

            if (message.Upgrade != null)
            {
                var i = checked((int)ms.Position);
                foreach (var ch in message.Upgrade)
                    Assert.That(ch, Is.EqualTo((char)rawBytes[i++]));
            }

            var headers = hs.Headers;
            Assert.That(hs.TrailingHeaders switch { null => headers, var ths => headers.Concat(ths) },
                        Is.EqualTo(message.Headers));

            if (message.ChunkLengths != null)
            {
                Assert.That(chunkSizes.Count, Is.EqualTo(message.NumChunksComplete));
                Assert.That(chunkSizes.SkipLast(1), Is.EqualTo(message.ChunkLengths));
            }
        }
    }
}
