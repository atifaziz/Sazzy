#region Copyright Joyent, Inc. and other Node contributors.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//
#endregion

namespace Sazzy.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using static HttpRequestMethod;
    using IHeaderCollection = System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<string, string>>;

    public enum HttpParserType { Request, Response }
    public enum HttpRequestMethod { Get, Post, Report, Connect, MSearch, Purge, Search, Patch, Link, Unlink, Source }

    public static class NodeHttpParserTestData
    {
        // https://github.com/nodejs/http-parser/blob/77310eeb839c4251c07184a5db8885a572a08352/test.c

        public sealed class Message
        {
            public string Id                 { get; }
            public string Name               { get; } // for debugging purposes
            public string Raw                { get; }
            public HttpParserType Type       { get; }
            public HttpRequestMethod Method  { get; }
            public int StatusCode            { get; }
            public string ResponseStatus     { get; }
            public string RequestPath        { get; }
            public string RequestUrl         { get; }
            public string Fragment           { get; }
            public string QueryString        { get; }
            public string Body               { get; }
            public int BodySize              { get; }
            public string Host               { get; }
            public string UserInfo           { get; }
            public uint Port                 { get; }
            public IHeaderCollection Headers { get; }
            public bool ShouldKeepAlive      { get; }

            public int NumChunksComplete => ChunkLengths?.Count + 1 ?? 0;
            public IReadOnlyCollection<int> ChunkLengths { get; }

            public string Upgrade            { get; } // upgraded body

            public short HttpMajor           { get; }
            public short HttpMinor           { get; }

            public bool MessageCompleteOnEof { get; }

            public string Ignore             { get; }

            public static Message
                Request(
                    string id,
                    string name,
                    string raw,
                    HttpRequestMethod method  = default,
                    string requestPath        = default,
                    string requestUrl         = default,
                    IHeaderCollection headers = default,
                    string fragment           = default,
                    string queryString        = default,
                    string body               = default,
                    int bodySize              = default,
                    string host               = default,
                    string userInfo           = default,
                    uint port                 = default,
                    bool shouldKeepAlive      = default,
                    IReadOnlyCollection<int> chunkLengths = default,
                    string upgrade            = default,
                    short httpMajor           = default,
                    short httpMinor           = default,
                    bool messageCompleteOnEof = default,
                    string ignore             = default) =>
                new Message(HttpParserType.Request, id, name, raw,
                            method              : method,
                            requestPath         : requestPath,
                            requestUrl          : requestUrl,
                            headers             : headers,
                            fragment            : fragment,
                            queryString         : queryString,
                            body                : body,
                            bodySize            : bodySize,
                            host                : host,
                            userInfo            : userInfo,
                            port                : port,
                            shouldKeepAlive     : shouldKeepAlive,
                            chunkLengths        : chunkLengths,
                            upgrade             : upgrade,
                            httpMajor           : httpMajor,
                            httpMinor           : httpMinor,
                            messageCompleteOnEof: messageCompleteOnEof,
                            ignore              : ignore);

            public static Message
                Response(
                    string id,
                    string name,
                    string raw,
                    int statusCode            = default,
                    string responseStatus     = default,
                    IHeaderCollection headers = default,
                    string body               = default,
                    int bodySize              = default,
                    bool shouldKeepAlive      = default,
                    IReadOnlyCollection<int> chunkLengths = default,
                    string upgrade            = default,
                    short httpMajor           = default,
                    short httpMinor           = default,
                    bool messageCompleteOnEof = default,
                    string ignore             = default) =>
                new Message(HttpParserType.Response, id, name, raw,
                            statusCode          : statusCode,
                            responseStatus      : responseStatus,
                            headers             : headers,
                            body                : body,
                            bodySize            : bodySize,
                            shouldKeepAlive     : shouldKeepAlive,
                            chunkLengths        : chunkLengths,
                            upgrade             : upgrade,
                            httpMajor           : httpMajor,
                            httpMinor           : httpMinor,
                            messageCompleteOnEof: messageCompleteOnEof,
                            ignore              : ignore);

            Message(
                HttpParserType type,
                string id,
                string name,
                string raw,
                HttpRequestMethod method  = default,
                int statusCode            = default,
                string responseStatus     = default,
                string requestPath        = default,
                string requestUrl         = default,
                IHeaderCollection headers = default,
                string fragment           = default,
                string queryString        = default,
                string body               = default,
                int bodySize              = default,
                string host               = default,
                string userInfo           = default,
                uint port                 = default,
                bool shouldKeepAlive      = default,
                IReadOnlyCollection<int> chunkLengths = default,
                string upgrade            = default,
                short httpMajor           = default,
                short httpMinor           = default,
                bool messageCompleteOnEof = default,
                string ignore             = default)
            {
                Id                   = id;
                Name                 = name;
                Raw                  = raw;
                Type                 = type;
                Method               = method;
                StatusCode           = statusCode;
                ResponseStatus       = responseStatus;
                RequestPath          = requestPath;
                RequestUrl           = requestUrl;
                Headers              = headers ?? Array.Empty<KeyValuePair<string, string>>();
                Fragment             = fragment;
                QueryString          = queryString;
                Body                 = body;
                BodySize             = bodySize;
                Host                 = host;
                UserInfo             = userInfo;
                Port                 = port;
                ShouldKeepAlive      = shouldKeepAlive;
                ChunkLengths         = chunkLengths;
                Upgrade              = upgrade;
                HttpMajor            = httpMajor;
                HttpMinor            = httpMinor;
                MessageCompleteOnEof = messageCompleteOnEof;
                Ignore               = ignore;
            }

            public MemoryStream OpenRawStream() =>
                new MemoryStream(Raw.Select(ch => (byte) ch).ToArray());

            public override string ToString() =>
                $"{{ {Type}, {Id}, \"{Name}\" }}";
        }

        #region Requests

        static IHeaderCollection Header(params (string Name, string Value)[] pairs) =>
            pairs.Select(e => KeyValuePair.Create(e.Name, e.Value)).ToList();

        static IReadOnlyCollection<int> ChunkLength(params int[] lengths) => lengths;

        public static readonly IEnumerable<Message> RequestMessages = new[]
        {
            Message.Request("CURL_GET",
                name: "curl get",
                raw: "GET /test HTTP/1.1\r\n"
                    + "User-Agent: curl/7.18.0 (i486-pc-linux-gnu) libcurl/7.18.0 OpenSSL/0.9.8g zlib/1.2.3.3 libidn/1.1\r\n"
                    + "Host: 0.0.0.0:5000\r\n"
                    + "Accept: */*\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/test",
                requestUrl: "/test",
                headers: Header(
                    ("User-Agent", "curl/7.18.0 (i486-pc-linux-gnu) libcurl/7.18.0 OpenSSL/0.9.8g zlib/1.2.3.3 libidn/1.1"),
                    ("Host", "0.0.0.0:5000"),
                    ("Accept", "*/*")),
                body: ""
            ),

            Message.Request("FIREFOX_GET",
                name: "firefox get",
                raw: "GET /favicon.ico HTTP/1.1\r\n"
                    + "Host: 0.0.0.0:5000\r\n"
                    + "User-Agent: Mozilla/5.0 (X11; U; Linux i686; en-US; rv:1.9) Gecko/2008061015 Firefox/3.0\r\n"
                    + "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8\r\n"
                    + "Accept-Language: en-us,en;q=0.5\r\n"
                    + "Accept-Encoding: gzip,deflate\r\n"
                    + "Accept-Charset: ISO-8859-1,utf-8;q=0.7,*;q=0.7\r\n"
                    + "Keep-Alive: 300\r\n"
                    + "Connection: keep-alive\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/favicon.ico",
                requestUrl: "/favicon.ico",
                headers: Header(
                    ("Host", "0.0.0.0:5000"),
                    ("User-Agent", "Mozilla/5.0 (X11; U; Linux i686; en-US; rv:1.9) Gecko/2008061015 Firefox/3.0"),
                    ("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"),
                    ("Accept-Language", "en-us,en;q=0.5"),
                    ("Accept-Encoding", "gzip,deflate"),
                    ("Accept-Charset", "ISO-8859-1,utf-8;q=0.7,*;q=0.7"),
                    ("Keep-Alive", "300"),
                    ("Connection", "keep-alive")),
                body: ""
            ),

            Message.Request("DUMBLUCK",
                name: "dumbluck",
                raw: "GET /dumbluck HTTP/1.1\r\n"
                    + "aaaaaaaaaaaaa:++++++++++\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/dumbluck",
                requestUrl: "/dumbluck",
                headers: Header(
                    ("aaaaaaaaaaaaa", "++++++++++")),
                body: ""
            ),

            Message.Request("FRAGMENT_IN_URI",
                name: "fragment in url",
                raw: "GET /forums/1/topics/2375?page=1#posts-17408 HTTP/1.1\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "page=1",
                fragment: "posts-17408",
                requestPath: "/forums/1/topics/2375"
                /* XXX request url does include fragment? */,
                requestUrl: "/forums/1/topics/2375?page=1#posts-17408",
                body: ""
            ),

            Message.Request("GET_NO_HEADERS_NO_BODY",
                name: "get no headers no body",
                raw: "GET /get_no_headers_no_body/world HTTP/1.1\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false /* would need Connection: close */,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/get_no_headers_no_body/world",
                requestUrl: "/get_no_headers_no_body/world",
                body: ""
            ),

            Message.Request("GET_ONE_HEADER_NO_BODY",
                name: "get one header no body",
                raw: "GET /get_one_header_no_body HTTP/1.1\r\n"
                    + "Accept: */*\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false /* would need Connection: close */,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/get_one_header_no_body",
                requestUrl: "/get_one_header_no_body",
                headers: Header(
                    ("Accept", "*/*")),
                body: ""
            ),

            Message.Request("GET_FUNKY_CONTENT_LENGTH",
                name: "get funky content length body hello",
                raw: "GET /get_funky_content_length_body_hello HTTP/1.0\r\n"
                    + "conTENT-Length: 5\r\n"
                    + "\r\n"
                    + "HELLO",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 0,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/get_funky_content_length_body_hello",
                requestUrl: "/get_funky_content_length_body_hello",
                headers: Header(
                    ("conTENT-Length", "5")),
                body: "HELLO"
            ),

            Message.Request("POST_IDENTITY_BODY_WORLD",
                name: "post identity body world",
                raw: "POST /post_identity_body_world?q=search#hey HTTP/1.1\r\n"
                    + "Accept: */*\r\n"
                    + "Transfer-Encoding: identity\r\n"
                    + "Content-Length: 5\r\n"
                    + "\r\n"
                    + "World",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Post,
                queryString: "q=search",
                fragment: "hey",
                requestPath: "/post_identity_body_world",
                requestUrl: "/post_identity_body_world?q=search#hey",
                headers: Header(
                    ("Accept", "*/*"),
                    ("Transfer-Encoding", "identity"),
                    ("Content-Length", "5")),
                body: "World"
            ),

            Message.Request("POST_CHUNKED_ALL_YOUR_BASE",
                name: "post - chunked body: all your base are belong to us",
                raw: "POST /post_chunked_all_your_base HTTP/1.1\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "1e\r\nall your base are belong to us\r\n"
                    + "0\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Post,
                queryString: "",
                fragment: "",
                requestPath: "/post_chunked_all_your_base",
                requestUrl: "/post_chunked_all_your_base",
                headers: Header(
                    ("Transfer-Encoding", "chunked")),
                body: "all your base are belong to us",
                chunkLengths: ChunkLength(0x1e)
            ),

            Message.Request("TWO_CHUNKS_MULT_ZERO_END",
                name: "two chunks ; triple zero ending",
                raw: "POST /two_chunks_mult_zero_end HTTP/1.1\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "5\r\nhello\r\n"
                    + "6\r\n world\r\n"
                    + "000\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Post,
                queryString: "",
                fragment: "",
                requestPath: "/two_chunks_mult_zero_end",
                requestUrl: "/two_chunks_mult_zero_end",
                headers: Header(
                    ("Transfer-Encoding", "chunked")),
                body: "hello world",
                chunkLengths: ChunkLength(5, 6)
            ),

            Message.Request("CHUNKED_W_TRAILING_HEADERS",
                ignore: "Pending support for chunked trailer part.",
                name: "chunked with trailing headers. blech.",
                raw: "POST /chunked_w_trailing_headers HTTP/1.1\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "5\r\nhello\r\n"
                    + "6\r\n world\r\n"
                    + "0\r\n"
                    + "Vary: *\r\n"
                    + "Content-Type: text/plain\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Post,
                queryString: "",
                fragment: "",
                requestPath: "/chunked_w_trailing_headers",
                requestUrl: "/chunked_w_trailing_headers",
                headers: Header(
                    ("Transfer-Encoding", "chunked"),
                    ("Vary", "*"),
                    ("Content-Type", "text/plain")),
                body: "hello world",
                chunkLengths: ChunkLength(5, 6)
            ),

            Message.Request("CHUNKED_W_NONSENSE_AFTER_LENGTH",
                //ignore: "Pending review/fix.",
                name: "with nonsense after the length",
                raw: "POST /chunked_w_nonsense_after_length HTTP/1.1\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "5; ilovew3;whattheluck=aretheseparametersfor\r\nhello\r\n"
                    + "6; blahblah; blah\r\n world\r\n"
                    + "0\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Post,
                queryString: "",
                fragment: "",
                requestPath: "/chunked_w_nonsense_after_length",
                requestUrl: "/chunked_w_nonsense_after_length",
                headers: Header(
                    ("Transfer-Encoding", "chunked")),
                body: "hello world",
                chunkLengths: ChunkLength(5, 6)
            ),

            Message.Request("WITH_QUOTES",
                name: "with quotes",
                raw: "GET /with_\"stupid\"_quotes?foo=\"bar\" HTTP/1.1\r\n\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "foo=\"bar\"",
                fragment: "",
                requestPath: "/with_\"stupid\"_quotes",
                requestUrl: "/with_\"stupid\"_quotes?foo=\"bar\"",
                body: ""
            ),

            Message.Request("APACHEBENCH_GET",
                /* The server receiving this request SHOULD NOT wait for EOF
                 * to know that content-length == 0.
                 * How to represent this in a unit test? message_complete_on_eof
                 * Compare with NO_CONTENT_LENGTH_RESPONSE.
                 */
                name: "apachebench get",
                raw: "GET /test HTTP/1.0\r\n"
                    + "Host: 0.0.0.0:5000\r\n"
                    + "User-Agent: ApacheBench/2.3\r\n"
                    + "Accept: */*\r\n\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 0,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/test",
                requestUrl: "/test",
                headers: Header(
                    ("Host", "0.0.0.0:5000"),
                    ("User-Agent", "ApacheBench/2.3"),
                    ("Accept", "*/*")),
                body: ""
            ),

            Message.Request("QUERY_URL_WITH_QUESTION_MARK_GET",
                /* Some clients include '?' characters in query strings.
                 */
                name: "query url with question mark",
                raw: "GET /test.cgi?foo=bar?baz HTTP/1.1\r\n\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "foo=bar?baz",
                fragment: "",
                requestPath: "/test.cgi",
                requestUrl: "/test.cgi?foo=bar?baz",
                body: ""
            ),

            Message.Request("PREFIX_NEWLINE_GET",
                ignore: "Pending fix.",
                /* Some clients, especially after a POST in a keep-alive connection,
                 * will send an extra CRLF before the next request
                 */
                name: "newline prefix get",
                raw: "\r\nGET /test HTTP/1.1\r\n\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/test",
                requestUrl: "/test",
                body: ""
            ),

            Message.Request("UPGRADE_REQUEST",
                name: "upgrade request",
                raw: "GET /demo HTTP/1.1\r\n"
                    + "Host: example.com\r\n"
                    + "Connection: Upgrade\r\n"
                    + "Sec-WebSocket-Key2: 12998 5 Y3 1  .P00\r\n"
                    + "Sec-WebSocket-Protocol: sample\r\n"
                    + "Upgrade: WebSocket\r\n"
                    + "Sec-WebSocket-Key1: 4 @1  46546xW%0l 1 5\r\n"
                    + "Origin: http://example.com\r\n"
                    + "\r\n"
                    + "Hot diggity dogg",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/demo",
                requestUrl: "/demo",
                upgrade: "Hot diggity dogg",
                headers: Header(
                    ("Host", "example.com"),
                    ("Connection", "Upgrade"),
                    ("Sec-WebSocket-Key2", "12998 5 Y3 1  .P00"),
                    ("Sec-WebSocket-Protocol", "sample"),
                    ("Upgrade", "WebSocket"),
                    ("Sec-WebSocket-Key1", "4 @1  46546xW%0l 1 5"),
                    ("Origin", "http://example.com")),
                body: ""
            ),

            Message.Request("CONNECT_REQUEST",
                name: "connect request",
                raw: "CONNECT 0-home0.netscape.com:443 HTTP/1.0\r\n"
                    + "User-agent: Mozilla/1.1N\r\n"
                    + "Proxy-authorization: basic aGVsbG86d29ybGQ=\r\n"
                    + "\r\n"
                    + "some data\r\n"
                    + "and yet even more data",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 0,
                method: Connect,
                queryString: "",
                fragment: "",
                requestPath: "",
                requestUrl: "0-home0.netscape.com:443",
                upgrade: "some data\r\nand yet even more data",
                headers: Header(
                    ("User-agent", "Mozilla/1.1N"),
                    ("Proxy-authorization", "basic aGVsbG86d29ybGQ=")),
                body: ""
            ),

            Message.Request("REPORT_REQ",
                name: "report request",
                raw: "REPORT /test HTTP/1.1\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Report,
                queryString: "",
                fragment: "",
                requestPath: "/test",
                requestUrl: "/test",
                body: ""
            ),

            Message.Request("NO_HTTP_VERSION",
                name: "request with no http version",
                raw: "GET /\r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 0,
                httpMinor: 9,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/",
                requestUrl: "/",
                body: ""
            ),

            Message.Request("MSEARCH_REQ",
                ignore: "Pending review.",
                name: "m-search request",
                raw: "M-SEARCH * HTTP/1.1\r\n"
                    + "HOST: 239.255.255.250:1900\r\n"
                    + "MAN: \"ssdp:discover\"\r\n"
                    + "ST: \"ssdp:all\"\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: MSearch,
                queryString: "",
                fragment: "",
                requestPath: "*",
                requestUrl: "*",
                headers: Header(
                    ("HOST", "239.255.255.250:1900"),
                    ("MAN", "\"ssdp:discover\""),
                    ("ST", "\"ssdp:all\"")),
                body: ""
            ),

            Message.Request("LINE_FOLDING_IN_HEADER",
                ignore: "Pending review.",
                name: "line folding in header value",
                raw: "GET / HTTP/1.1\r\n"
                    + "Line1:   abc\r\n"
                    + "\tdef\r\n"
                    + " ghi\r\n"
                    + "\t\tjkl\r\n"
                    + "  mno \r\n"
                    + "\t \tqrs\r\n"
                    + "Line2: \t line2\t\r\n"
                    + "Line3:\r\n"
                    + " line3\r\n"
                    + "Line4: \r\n"
                    + " \r\n"
                    + "Connection:\r\n"
                    + " close\r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/",
                requestUrl: "/",
                headers: Header(
                    ("Line1", "abc\tdef ghi\t\tjkl  mno \t \tqrs"),
                    ("Line2", "line2\t"),
                    ("Line3", "line3"),
                    ("Line4", ""),
                    ("Connection", "close")),
                body: ""
            ),


            Message.Request("QUERY_TERMINATED_HOST",
                name: "host terminated by a query string",
                raw: "GET http://hypnotoad.org?hail=all HTTP/1.1\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "hail=all",
                fragment: "",
                requestPath: "",
                requestUrl: "http://hypnotoad.org?hail=all",
                host: "hypnotoad.org",
                body: ""
            ),

            Message.Request("QUERY_TERMINATED_HOSTPORT",
                name: "host:port terminated by a query string",
                raw: "GET http://hypnotoad.org:1234?hail=all HTTP/1.1\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "hail=all",
                fragment: "",
                requestPath: "",
                requestUrl: "http://hypnotoad.org:1234?hail=all",
                host: "hypnotoad.org",
                port: 1234,
                body: ""
            ),

            Message.Request("SPACE_TERMINATED_HOSTPORT",
                name: "host:port terminated by a space",
                raw: "GET http://hypnotoad.org:1234 HTTP/1.1\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "",
                requestUrl: "http://hypnotoad.org:1234",
                host: "hypnotoad.org",
                port: 1234,
                body: ""
            ),

            Message.Request("PATCH_REQ",
                name: "PATCH request",
                raw: "PATCH /file.txt HTTP/1.1\r\n"
                    + "Host: www.example.com\r\n"
                    + "Content-Type: application/example\r\n"
                    + "If-Match: \"e0023aa4e\"\r\n"
                    + "Content-Length: 10\r\n"
                    + "\r\n"
                    + "cccccccccc",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Patch,
                queryString: "",
                fragment: "",
                requestPath: "/file.txt",
                requestUrl: "/file.txt",
                headers: Header(
                    ("Host", "www.example.com"),
                    ("Content-Type", "application/example"),
                    ("If-Match", "\"e0023aa4e\""),
                    ("Content-Length", "10")),
                body: "cccccccccc"
            ),

            Message.Request("CONNECT_CAPS_REQUEST",
                name: "connect caps request",
                raw: "CONNECT HOME0.NETSCAPE.COM:443 HTTP/1.0\r\n"
                    + "User-agent: Mozilla/1.1N\r\n"
                    + "Proxy-authorization: basic aGVsbG86d29ybGQ=\r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 0,
                method: Connect,
                queryString: "",
                fragment: "",
                requestPath: "",
                requestUrl: "HOME0.NETSCAPE.COM:443",
                upgrade: "",
                headers: Header(
                    ("User-agent", "Mozilla/1.1N"),
                    ("Proxy-authorization", "basic aGVsbG86d29ybGQ=")),
                body: ""
            ),

#if !HTTP_PARSER_STRICT

            Message.Request("UTF8_PATH_REQ",
                ignore: "Pending review.",
                name: "utf-8 path request",
                raw: "GET /δ¶/δt/pope?q=1#narf HTTP/1.1\r\n"
                    + "Host: github.com\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "q=1",
                fragment: "narf",
                requestPath: "/δ¶/δt/pope",
                requestUrl: "/δ¶/δt/pope?q=1#narf",
                headers: Header(
                    ("Host", "github.com")),
                body: ""
            ),

            Message.Request("HOSTNAME_UNDERSCORE",
                name: "hostname underscore",
                raw: "CONNECT home_0.netscape.com:443 HTTP/1.0\r\n"
                    + "User-agent: Mozilla/1.1N\r\n"
                    + "Proxy-authorization: basic aGVsbG86d29ybGQ=\r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 0,
                method: Connect,
                queryString: "",
                fragment: "",
                requestPath: "",
                requestUrl: "home_0.netscape.com:443",
                upgrade: "",
                headers: Header(
                    ("User-agent", "Mozilla/1.1N"),
                    ("Proxy-authorization", "basic aGVsbG86d29ybGQ=")),
                body: ""
            ),

#endif  // !HTTP_PARSER_STRICT */

            /* see https://github.com/ry/http-parser/issues/47 */
            Message.Request("EAT_TRAILING_CRLF_NO_CONNECTION_CLOSE",
                name: "eat CRLF between requests, no \"Connection: close\" header",
                raw: "POST / HTTP/1.1\r\n"
                    + "Host: www.example.com\r\n"
                    + "Content-Type: application/x-www-form-urlencoded\r\n"
                    + "Content-Length: 4\r\n"
                    + "\r\n"
                    + "q=42\r\n" /* note the trailing CRLF */,
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Post,
                queryString: "",
                fragment: "",
                requestPath: "/",
                requestUrl: "/",
                upgrade: null,
                headers: Header(
                    ("Host", "www.example.com"),
                    ("Content-Type", "application/x-www-form-urlencoded"),
                    ("Content-Length", "4")),
                body: "q=42"
            ),

            /* see https://github.com/ry/http-parser/issues/47 */
            Message.Request("EAT_TRAILING_CRLF_WITH_CONNECTION_CLOSE",
                name: "eat CRLF between requests even if \"Connection: close\" is set",
                raw: "POST / HTTP/1.1\r\n"
                    + "Host: www.example.com\r\n"
                    + "Content-Type: application/x-www-form-urlencoded\r\n"
                    + "Content-Length: 4\r\n"
                    + "Connection: close\r\n"
                    + "\r\n"
                    + "q=42\r\n" /* note the trailing CRLF */,
                shouldKeepAlive: false,
                messageCompleteOnEof: false /* input buffer isn't empty when on_message_complete is called */,
                httpMajor: 1,
                httpMinor: 1,
                method: Post,
                queryString: "",
                fragment: "",
                requestPath: "/",
                requestUrl: "/",
                upgrade: null,
                headers: Header(
                    ("Host", "www.example.com"),
                    ("Content-Type", "application/x-www-form-urlencoded"),
                    ("Content-Length", "4"),
                    ("Connection", "close")),
                body: "q=42"
            ),

            Message.Request("PURGE_REQ",
                name: "PURGE request",
                raw: "PURGE /file.txt HTTP/1.1\r\n"
                    + "Host: www.example.com\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Purge,
                queryString: "",
                fragment: "",
                requestPath: "/file.txt",
                requestUrl: "/file.txt",
                headers: Header(
                    ("Host", "www.example.com")),
                body: ""
            ),

            Message.Request("SEARCH_REQ",
                name: "SEARCH request",
                raw: "SEARCH / HTTP/1.1\r\n"
                    + "Host: www.example.com\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Search,
                queryString: "",
                fragment: "",
                requestPath: "/",
                requestUrl: "/",
                headers: Header(
                    ("Host", "www.example.com")),
                body: ""
            ),

            Message.Request("PROXY_WITH_BASIC_AUTH",
                name: "host:port and basic_auth",
                raw: "GET http://a%12:b!&*$@hypnotoad.org:1234/toto HTTP/1.1\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                fragment: "",
                requestPath: "/toto",
                requestUrl: "http://a%12:b!&*$@hypnotoad.org:1234/toto",
                host: "hypnotoad.org",
                userInfo: "a%12:b!&*$",
                port: 1234,
                body: ""
            ),

            Message.Request("LINE_FOLDING_IN_HEADER_WITH_LF",
                ignore: "Pending review.",
                name: "line folding in header value",
                raw: "GET / HTTP/1.1\n"
                    + "Line1:   abc\n"
                    + "\tdef\n"
                    + " ghi\n"
                    + "\t\tjkl\n"
                    + "  mno \n"
                    + "\t \tqrs\n"
                    + "Line2: \t line2\t\n"
                    + "Line3:\n"
                    + " line3\n"
                    + "Line4: \n"
                    + " \n"
                    + "Connection:\n"
                    + " close\n"
                    + "\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/",
                requestUrl: "/",
                headers: Header(
                    ("Line1", "abc\tdef ghi\t\tjkl  mno \t \tqrs"),
                    ("Line2", "line2\t"),
                    ("Line3", "line3"),
                    ("Line4", ""),
                    ("Connection", "close")),
                body: ""
            ),

            Message.Request("CONNECTION_MULTI",
                name: "multiple connection header values with folding",
                raw: "GET /demo HTTP/1.1\r\n"
                    + "Host: example.com\r\n"
                    + "Connection: Something,\r\n"
                    + " Upgrade, ,Keep-Alive\r\n"
                    + "Sec-WebSocket-Key2: 12998 5 Y3 1  .P00\r\n"
                    + "Sec-WebSocket-Protocol: sample\r\n"
                    + "Upgrade: WebSocket\r\n"
                    + "Sec-WebSocket-Key1: 4 @1  46546xW%0l 1 5\r\n"
                    + "Origin: http://example.com\r\n"
                    + "\r\n"
                    + "Hot diggity dogg",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/demo",
                requestUrl: "/demo",
                upgrade: "Hot diggity dogg",
                headers: Header(
                    ("Host", "example.com"),
                    ("Connection", "Something, Upgrade, ,Keep-Alive"),
                    ("Sec-WebSocket-Key2", "12998 5 Y3 1  .P00"),
                    ("Sec-WebSocket-Protocol", "sample"),
                    ("Upgrade", "WebSocket"),
                    ("Sec-WebSocket-Key1", "4 @1  46546xW%0l 1 5"),
                    ("Origin", "http://example.com")),
                body: ""
            ),

            Message.Request("CONNECTION_MULTI_LWS",
                name: "multiple connection header values with folding and lws",
                raw: "GET /demo HTTP/1.1\r\n"
                    + "Connection: keep-alive, upgrade\r\n"
                    + "Upgrade: WebSocket\r\n"
                    + "\r\n"
                    + "Hot diggity dogg",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/demo",
                requestUrl: "/demo",
                upgrade: "Hot diggity dogg",
                headers: Header(
                    ("Connection", "keep-alive, upgrade"),
                    ("Upgrade", "WebSocket")),
                body: ""
            ),

            Message.Request("CONNECTION_MULTI_LWS_CRLF",
                ignore: "Pending review.",
                name: "multiple connection header values with folding and lws",
                raw: "GET /demo HTTP/1.1\r\n"
                    + "Connection: keep-alive, \r\n upgrade\r\n"
                    + "Upgrade: WebSocket\r\n"
                    + "\r\n"
                    + "Hot diggity dogg",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Get,
                queryString: "",
                fragment: "",
                requestPath: "/demo",
                requestUrl: "/demo",
                upgrade: "Hot diggity dogg",
                headers: Header(
                    ("Connection", "keep-alive,  upgrade"),
                    ("Upgrade", "WebSocket")),
                body: ""
            ),

            Message.Request("UPGRADE_POST_REQUEST",
                name: "upgrade post request",
                raw: "POST /demo HTTP/1.1\r\n"
                    + "Host: example.com\r\n"
                    + "Connection: Upgrade\r\n"
                    + "Upgrade: HTTP/2.0\r\n"
                    + "Content-Length: 15\r\n"
                    + "\r\n"
                    + "sweet post body"
                    + "Hot diggity dogg",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Post,
                requestPath: "/demo",
                requestUrl: "/demo",
                upgrade: "Hot diggity dogg",
                headers: Header(
                    ("Host", "example.com"),
                    ("Connection", "Upgrade"),
                    ("Upgrade", "HTTP/2.0"),
                    ("Content-Length", "15")),
                body: "sweet post body"
            ),

            Message.Request("CONNECT_WITH_BODY_REQUEST",
                ignore: "Pending review.",
                name: "connect with body request",
                raw: "CONNECT foo.bar.com:443 HTTP/1.0\r\n"
                    + "User-agent: Mozilla/1.1N\r\n"
                    + "Proxy-authorization: basic aGVsbG86d29ybGQ=\r\n"
                    + "Content-Length: 10\r\n"
                    + "\r\n"
                    + "blarfcicle",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 0,
                method: Connect,
                requestUrl: "foo.bar.com:443",
                upgrade: "blarfcicle",
                headers: Header(
                    ("User-agent", "Mozilla/1.1N"),
                    ("Proxy-authorization", "basic aGVsbG86d29ybGQ="),
                    ("Content-Length", "10")),
                body: ""
            ),

            /* Examples from the Internet draft for LINK/UNLINK methods:
             * https://tools.ietf.org/id/draft-snell-link-method-01.html#rfc.section.5
             */

            Message.Request("LINK_REQUEST",
                name: "link request",
                raw: "LINK /images/my_dog.jpg HTTP/1.1\r\n"
                    + "Host: example.com\r\n"
                    + "Link: <http://example.com/profiles/joe>; rel=\"tag\"\r\n"
                    + "Link: <http://example.com/profiles/sally>; rel=\"tag\"\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Link,
                requestPath: "/images/my_dog.jpg",
                requestUrl: "/images/my_dog.jpg",
                queryString: "",
                fragment: "",
                headers: Header(
                    ("Host", "example.com"),
                    ("Link", "<http://example.com/profiles/joe>; rel=\"tag\""),
                    ("Link", "<http://example.com/profiles/sally>; rel=\"tag\"")),
                body: ""
            ),

            Message.Request("UNLINK_REQUEST",
                name: "unlink request",
                raw: "UNLINK /images/my_dog.jpg HTTP/1.1\r\n"
                    + "Host: example.com\r\n"
                    + "Link: <http://example.com/profiles/sally>; rel=\"tag\"\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Unlink,
                requestPath: "/images/my_dog.jpg",
                requestUrl: "/images/my_dog.jpg",
                queryString: "",
                fragment: "",
                headers: Header(
                    ("Host", "example.com"),
                    ("Link", "<http://example.com/profiles/sally>; rel=\"tag\"")),
                body: ""
            ),

            Message.Request("SOURCE_REQUEST",
                name: "source request",
                raw: "SOURCE /music/sweet/music HTTP/1.1\r\n"
                    + "Host: example.com\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                method: Source,
                requestPath: "/music/sweet/music",
                requestUrl: "/music/sweet/music",
                queryString: "",
                fragment: "",
                headers: Header(
                    ("Host", "example.com")),
                body: ""
            )
        };

        #endregion

        #region Responses

        public static readonly IEnumerable<Message> ResponseMessages = new[]
        {
            Message.Response("GOOGLE_301",
                ignore: "Pending review.",
                name: "google 301",
                raw: "HTTP/1.1 301 Moved Permanently\r\n"
                    + "Location: http://www.google.com/\r\n"
                    + "Content-Type: text/html; charset=UTF-8\r\n"
                    + "Date: Sun, 26 Apr 2009 11:11:49 GMT\r\n"
                    + "Expires: Tue, 26 May 2009 11:11:49 GMT\r\n"
                    + "X-$PrototypeBI-Version: 1.6.0.3\r\n" /* $ char in header field */
                    + "Cache-Control: public, max-age=2592000\r\n"
                    + "Server: gws\r\n"
                    + "Content-Length:  219  \r\n"
                    + "\r\n"
                    + "<HTML><HEAD><meta http-equiv=\"content-type\" content=\"text/html;charset=utf-8\">\n"
                    + "<TITLE>301 Moved</TITLE></HEAD><BODY>\n"
                    + "<H1>301 Moved</H1>\n"
                    + "The document has moved\n"
                    + "<A HREF=\"http://www.google.com/\">here</A>.\r\n"
                    + "</BODY></HTML>\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 301,
                responseStatus: "Moved Permanently",
                headers: Header(
                    ("Location", "http://www.google.com/"),
                    ("Content-Type", "text/html; charset=UTF-8"),
                    ("Date", "Sun, 26 Apr 2009 11:11:49 GMT"),
                    ("Expires", "Tue, 26 May 2009 11:11:49 GMT"),
                    ("X-$PrototypeBI-Version", "1.6.0.3"),
                    ("Cache-Control", "public, max-age=2592000"),
                    ("Server", "gws"),
                    ("Content-Length", "219  ")),
                body: "<HTML><HEAD><meta http-equiv=\"content-type\" content=\"text/html;charset=utf-8\">\n"
                  + "<TITLE>301 Moved</TITLE></HEAD><BODY>\n"
                  + "<H1>301 Moved</H1>\n"
                  + "The document has moved\n"
                  + "<A HREF=\"http://www.google.com/\">here</A>.\r\n"
                  + "</BODY></HTML>\r\n"
            ),

            Message.Response("NO_CONTENT_LENGTH_RESPONSE",
                ignore: "Pending fix.",
                /* The client should wait for the server's EOF. That is, when content-length
                 * is not specified, and "Connection: close", the end of body is specified
                 * by the EOF.
                 * Compare with APACHEBENCH_GET
                 */
                name: "no content-length response",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Date: Tue, 04 Aug 2009 07:59:32 GMT\r\n"
                    + "Server: Apache\r\n"
                    + "X-Powered-By: Servlet/2.5 JSP/2.1\r\n"
                    + "Content-Type: text/xml; charset=utf-8\r\n"
                    + "Connection: close\r\n"
                    + "\r\n"
                    + "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                    + "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\">\n"
                    + "  <SOAP-ENV:Body>\n"
                    + "    <SOAP-ENV:Fault>\n"
                    + "       <faultcode>SOAP-ENV:Client</faultcode>\n"
                    + "       <faultstring>Client Error</faultstring>\n"
                    + "    </SOAP-ENV:Fault>\n"
                    + "  </SOAP-ENV:Body>\n"
                    + "</SOAP-ENV:Envelope>",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Date", "Tue, 04 Aug 2009 07:59:32 GMT"),
                    ("Server", "Apache"),
                    ("X-Powered-By", "Servlet/2.5 JSP/2.1"),
                    ("Content-Type", "text/xml; charset=utf-8"),
                    ("Connection", "close")),
                body: "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                  + "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\">\n"
                  + "  <SOAP-ENV:Body>\n"
                  + "    <SOAP-ENV:Fault>\n"
                  + "       <faultcode>SOAP-ENV:Client</faultcode>\n"
                  + "       <faultstring>Client Error</faultstring>\n"
                  + "    </SOAP-ENV:Fault>\n"
                  + "  </SOAP-ENV:Body>\n"
                  + "</SOAP-ENV:Envelope>"
            ),

            Message.Response("NO_HEADERS_NO_BODY_404",
                name: "404 no headers no body",
                raw: "HTTP/1.1 404 Not Found\r\n\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 404,
                responseStatus: "Not Found",
                body: ""
            ),

            Message.Response("NO_REASON_PHRASE",
                ignore: "Pending review/fix.",
                name: "301 no response phrase",
                raw: "HTTP/1.1 301\r\n\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 301,
                responseStatus: "",
                body: ""
            ),

            Message.Response("TRAILING_SPACE_ON_CHUNKED_BODY",
                name: "200 trailing space on chunked body",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Content-Type: text/plain\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "25  \r\n"
                    + "This is the data in the first chunk\r\n"
                    + "\r\n"
                    + "1C\r\n"
                    + "and this is the second one\r\n"
                    + "\r\n"
                    + "0  \r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Content-Type", "text/plain"),
                    ("Transfer-Encoding", "chunked")),
                bodySize: 37 + 28,
                body:
                 "This is the data in the first chunk\r\n"
                    + "and this is the second one\r\n",
                chunkLengths: ChunkLength(0x25, 0x1c)
            ),

            Message.Response("NO_CARRIAGE_RET",
                ignore: "Pending fix.",
                name: "no carriage ret",
                raw: "HTTP/1.1 200 OK\n"
                    + "Content-Type: text/html; charset=utf-8\n"
                    + "Connection: close\n"
                    + "\n"
                    + "these headers are from http://news.ycombinator.com/",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Content-Type", "text/html; charset=utf-8"),
                    ("Connection", "close")),
                body: "these headers are from http://news.ycombinator.com/"
            ),

            Message.Response("PROXY_CONNECTION",
                name: "proxy connection",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Content-Type: text/html; charset=UTF-8\r\n"
                    + "Content-Length: 11\r\n"
                    + "Proxy-Connection: close\r\n"
                    + "Date: Thu, 31 Dec 2009 20:55:48 +0000\r\n"
                    + "\r\n"
                    + "hello world",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Content-Type", "text/html; charset=UTF-8"),
                    ("Content-Length", "11"),
                    ("Proxy-Connection", "close"),
                    ("Date", "Thu, 31 Dec 2009 20:55:48 +0000")),
                body: "hello world"
            ),

            Message.Response("UNDERSTORE_HEADER_KEY",
                // shown by
                // curl -o /dev/null -v "http://ad.doubleclick.net/pfadx/DARTSHELLCONFIGXML;dcmt=text/xml;"
                name: "underscore header key",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Server: DCLK-AdSvr\r\n"
                    + "Content-Type: text/xml\r\n"
                    + "Content-Length: 0\r\n"
                    + "DCLK_imp: v7;x;114750856;0-0;0;17820020;0/0;21603567/21621457/1;;~okv=;dcmt=text/xml;;~cs=o\r\n\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Server", "DCLK-AdSvr"),
                    ("Content-Type", "text/xml"),
                    ("Content-Length", "0"),
                    ("DCLK_imp", "v7;x;114750856;0-0;0;17820020;0/0;21603567/21621457/1;;~okv=;dcmt=text/xml;;~cs=o")),
                body: ""
            ),

            Message.Response("BONJOUR_MADAME_FR",
                /* The client should not merge two headers fields when the first one doesn't
                 * have a value.
                 */
                name: "bonjourmadame.fr",
                raw: "HTTP/1.0 301 Moved Permanently\r\n"
                    + "Date: Thu, 03 Jun 2010 09:56:32 GMT\r\n"
                    + "Server: Apache/2.2.3 (Red Hat)\r\n"
                    + "Cache-Control: public\r\n"
                    + "Pragma: \r\n"
                    + "Location: http://www.bonjourmadame.fr/\r\n"
                    + "Vary: Accept-Encoding\r\n"
                    + "Content-Length: 0\r\n"
                    + "Content-Type: text/html; charset=UTF-8\r\n"
                    + "Connection: keep-alive\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 0,
                statusCode: 301,
                responseStatus: "Moved Permanently",
                headers: Header(
                    ("Date", "Thu, 03 Jun 2010 09:56:32 GMT"),
                    ("Server", "Apache/2.2.3 (Red Hat)"),
                    ("Cache-Control", "public"),
                    ("Pragma", ""),
                    ("Location", "http://www.bonjourmadame.fr/"),
                    ("Vary", "Accept-Encoding"),
                    ("Content-Length", "0"),
                    ("Content-Type", "text/html; charset=UTF-8"),
                    ("Connection", "keep-alive")),
                body: ""
            ),

            Message.Response("RES_FIELD_UNDERSCORE",
                /* Should handle spaces in header fields */
                name: "field underscore",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Date: Tue, 28 Sep 2010 01:14:13 GMT\r\n"
                    + "Server: Apache\r\n"
                    + "Cache-Control: no-cache, must-revalidate\r\n"
                    + "Expires: Mon, 26 Jul 1997 05:00:00 GMT\r\n"
                    + ".et-Cookie: PlaxoCS=1274804622353690521; path=/; domain=.plaxo.com\r\n"
                    + "Vary: Accept-Encoding\r\n"
                    + "_eep-Alive: timeout=45\r\n" /* semantic value ignored */

                    + "_onnection: Keep-Alive\r\n" /* semantic value ignored */

                    + "Transfer-Encoding: chunked\r\n"
                    + "Content-Type: text/html\r\n"
                    + "Connection: close\r\n"
                    + "\r\n"
                    + "0\r\n\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Date", "Tue, 28 Sep 2010 01:14:13 GMT"),
                    ("Server", "Apache"),
                    ("Cache-Control", "no-cache, must-revalidate"),
                    ("Expires", "Mon, 26 Jul 1997 05:00:00 GMT"),
                    (".et-Cookie", "PlaxoCS=1274804622353690521; path=/; domain=.plaxo.com"),
                    ("Vary", "Accept-Encoding"),
                    ("_eep-Alive", "timeout=45"),
                    ("_onnection", "Keep-Alive"),
                    ("Transfer-Encoding", "chunked"),
                    ("Content-Type", "text/html"),
                    ("Connection", "close")),
                body: "",
                chunkLengths: ChunkLength()),

            Message.Response("NON_ASCII_IN_STATUS_LINE",
                /* Should handle non-ASCII in status line */
                name: "non-ASCII in status line",
                raw: "HTTP/1.1 500 Oriëntatieprobleem\r\n"
                    + "Date: Fri, 5 Nov 2010 23:07:12 GMT+2\r\n"
                    + "Content-Length: 0\r\n"
                    + "Connection: close\r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 500,
                responseStatus: "Oriëntatieprobleem",
                headers: Header(
                    ("Date", "Fri, 5 Nov 2010 23:07:12 GMT+2"),
                    ("Content-Length", "0"),
                    ("Connection", "close")),
                body: ""
            ),

            Message.Response("HTTP_VERSION_0_9",
                /* Should handle HTTP/0.9 */
                name: "http version 0.9",
                raw: "HTTP/0.9 200 OK\r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 0,
                httpMinor: 9,
                statusCode: 200,
                responseStatus: "OK",
                body: ""
            ),

            Message.Response("NO_CONTENT_LENGTH_NO_TRANSFER_ENCODING_RESPONSE",
                ignore: "Pending fix.",
                /* The client should wait for the server's EOF. That is, when neither
                 * content-length nor transfer-encoding is specified, the end of body
                 * is specified by the EOF.
                 */
                name: "neither content-length nor transfer-encoding response",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Content-Type: text/plain\r\n"
                    + "\r\n"
                    + "hello world",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Content-Type", "text/plain")),
                body: "hello world"
            ),

            Message.Response("NO_BODY_HTTP10_KA_200",
                name: "HTTP/1.0 with keep-alive and EOF-terminated 200 status",
                raw: "HTTP/1.0 200 OK\r\n"
                    + "Connection: keep-alive\r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 1,
                httpMinor: 0,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Connection", "keep-alive")),
                body: ""
            ),

            Message.Response("NO_BODY_HTTP10_KA_204",
                name: "HTTP/1.0 with keep-alive and a 204 status",
                raw: "HTTP/1.0 204 No content\r\n"
                    + "Connection: keep-alive\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 0,
                statusCode: 204,
                responseStatus: "No content",
                headers: Header(
                    ("Connection", "keep-alive")),
                body: ""
            ),

            Message.Response("NO_BODY_HTTP11_KA_200",
                name: "HTTP/1.1 with an EOF-terminated 200 status",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                body: ""
            ),

            Message.Response("NO_BODY_HTTP11_KA_204",
                name: "HTTP/1.1 with a 204 status",
                raw: "HTTP/1.1 204 No content\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 204,
                responseStatus: "No content",
                body: ""
            ),

            Message.Response("NO_BODY_HTTP11_NOKA_204",
                name: "HTTP/1.1 with a 204 status and keep-alive disabled",
                raw: "HTTP/1.1 204 No content\r\n"
                    + "Connection: close\r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 204,
                responseStatus: "No content",
                headers: Header(
                    ("Connection", "close")),
                body: ""
            ),

            Message.Response("NO_BODY_HTTP11_KA_CHUNKED_200",
                name: "HTTP/1.1 with chunked endocing and a 200 response",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "0\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Transfer-Encoding", "chunked")),
                body: "",
                chunkLengths: ChunkLength()),

#if !HTTP_PARSER_STRICT

            Message.Response("SPACE_IN_FIELD_RES",
                /* Should handle spaces in header fields */
                name: "field space",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Server: Microsoft-IIS/6.0\r\n"
                    + "X-Powered-By: ASP.NET\r\n"
                    + "en-US Content-Type: text/xml\r\n" /* this is the problem */
                    + "Content-Type: text/xml\r\n"
                    + "Content-Length: 16\r\n"
                    + "Date: Fri, 23 Jul 2010 18:45:38 GMT\r\n"
                    + "Connection: keep-alive\r\n"
                    + "\r\n"
                    + "<xml>hello</xml>" /* fake body */,
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Server", "Microsoft-IIS/6.0"),
                    ("X-Powered-By", "ASP.NET"),
                    ("en-US Content-Type", "text/xml"),
                    ("Content-Type", "text/xml"),
                    ("Content-Length", "16"),
                    ("Date", "Fri, 23 Jul 2010 18:45:38 GMT"),
                    ("Connection", "keep-alive")),
                body: "<xml>hello</xml>"
            ),

#endif // !HTTP_PARSER_STRICT */

            Message.Response("AMAZON_COM",
                name: "amazon.com",
                raw: "HTTP/1.1 301 MovedPermanently\r\n"
                    + "Date: Wed, 15 May 2013 17:06:33 GMT\r\n"
                    + "Server: Server\r\n"
                    + "x-amz-id-1: 0GPHKXSJQ826RK7GZEB2\r\n"
                    + "p3p: policyref=\"http://www.amazon.com/w3c/p3p.xml\",CP=\"CAO DSP LAW CUR ADM IVAo IVDo CONo OTPo OUR DELi PUBi OTRi BUS PHY ONL UNI PUR FIN COM NAV INT DEM CNT STA HEA PRE LOC GOV OTC \"\r\n"
                    + "x-amz-id-2: STN69VZxIFSz9YJLbz1GDbxpbjG6Qjmmq5E3DxRhOUw+Et0p4hr7c/Q8qNcx4oAD\r\n"
                    + "Location: http://www.amazon.com/Dan-Brown/e/B000AP9DSU/ref=s9_pop_gw_al1?_encoding=UTF8&refinementId=618073011&pf_rd_m=ATVPDKIKX0DER&pf_rd_s=center-2&pf_rd_r=0SHYY5BZXN3KR20BNFAY&pf_rd_t=101&pf_rd_p=1263340922&pf_rd_i=507846\r\n"
                    + "Vary: Accept-Encoding,User-Agent\r\n"
                    + "Content-Type: text/html; charset=ISO-8859-1\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "1\r\n"
                    + "\n\r\n"
                    + "0\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 301,
                responseStatus: "MovedPermanently",
                headers: Header(
                    ("Date", "Wed, 15 May 2013 17:06:33 GMT"),
                    ("Server", "Server"),
                    ("x-amz-id-1", "0GPHKXSJQ826RK7GZEB2"),
                    ("p3p", "policyref=\"http://www.amazon.com/w3c/p3p.xml\",CP=\"CAO DSP LAW CUR ADM IVAo IVDo CONo OTPo OUR DELi PUBi OTRi BUS PHY ONL UNI PUR FIN COM NAV INT DEM CNT STA HEA PRE LOC GOV OTC \""),
                    ("x-amz-id-2", "STN69VZxIFSz9YJLbz1GDbxpbjG6Qjmmq5E3DxRhOUw+Et0p4hr7c/Q8qNcx4oAD"),
                    ("Location", "http://www.amazon.com/Dan-Brown/e/B000AP9DSU/ref=s9_pop_gw_al1?_encoding=UTF8&refinementId=618073011&pf_rd_m=ATVPDKIKX0DER&pf_rd_s=center-2&pf_rd_r=0SHYY5BZXN3KR20BNFAY&pf_rd_t=101&pf_rd_p=1263340922&pf_rd_i=507846"),
                    ("Vary", "Accept-Encoding,User-Agent"),
                    ("Content-Type", "text/html; charset=ISO-8859-1"),
                    ("Transfer-Encoding", "chunked")),
                body: "\n",
                chunkLengths: ChunkLength(1)
            ),

            Message.Response("EMPTY_REASON_PHRASE_AFTER_SPACE",
                ignore: "Pending review/fix.",
                name: "empty reason phrase after space",
                raw: "HTTP/1.1 200 \r\n"
                    + "\r\n",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "",
                body: ""
            ),

            Message.Response("CONTENT_LENGTH_X",
                name: "Content-Length-X",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Content-Length-X: 0\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "2\r\n"
                    + "OK\r\n"
                    + "0\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                headers: Header(
                    ("Content-Length-X", "0"),
                    ("Transfer-Encoding", "chunked")),
                body: "OK",
                chunkLengths: ChunkLength(2)
            ),

            Message.Response("HTTP_101_RESPONSE_WITH_UPGRADE_HEADER",
                name: "HTTP 101 response with Upgrade header",
                raw: "HTTP/1.1 101 Switching Protocols\r\n"
                    + "Connection: upgrade\r\n"
                    + "Upgrade: h2c\r\n"
                    + "\r\n"
                    + "proto",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 101,
                responseStatus: "Switching Protocols",
                upgrade: "proto",
                headers: Header(
                    ("Connection", "upgrade"),
                    ("Upgrade", "h2c")
                )),

            Message.Response("HTTP_101_RESPONSE_WITH_UPGRADE_HEADER_AND_CONTENT_LENGTH",
                name: "HTTP 101 response with Upgrade and Content-Length header",
                raw: "HTTP/1.1 101 Switching Protocols\r\n"
                    + "Connection: upgrade\r\n"
                    + "Upgrade: h2c\r\n"
                    + "Content-Length: 4\r\n"
                    + "\r\n"
                    + "body"
                    + "proto",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 101,
                responseStatus: "Switching Protocols",
                body: "body",
                upgrade: "proto",
                headers: Header(
                    ("Connection", "upgrade"),
                    ("Upgrade", "h2c"),
                    ("Content-Length", "4")
                )),

            Message.Response("HTTP_101_RESPONSE_WITH_UPGRADE_HEADER_AND_TRANSFER_ENCODING",
                name: "HTTP 101 response with Upgrade and Transfer-Encoding header",
                raw: "HTTP/1.1 101 Switching Protocols\r\n"
                    + "Connection: upgrade\r\n"
                    + "Upgrade: h2c\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "2\r\n"
                    + "bo\r\n"
                    + "2\r\n"
                    + "dy\r\n"
                    + "0\r\n"
                    + "\r\n"
                    + "proto",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 101,
                responseStatus: "Switching Protocols",
                body: "body",
                upgrade: "proto",
                headers: Header(
                    ("Connection", "upgrade"),
                    ("Upgrade", "h2c"),
                    ("Transfer-Encoding", "chunked")),
                chunkLengths: ChunkLength(2, 2)
            ),

            Message.Response("HTTP_200_RESPONSE_WITH_UPGRADE_HEADER",
                ignore: "Pending fix.",
                name: "HTTP 200 response with Upgrade header",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Connection: upgrade\r\n"
                    + "Upgrade: h2c\r\n"
                    + "\r\n"
                    + "body",
                shouldKeepAlive: false,
                messageCompleteOnEof: true,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                body: "body",
                upgrade: null,
                headers: Header(
                    ("Connection", "upgrade"),
                    ("Upgrade", "h2c")
                )),

            Message.Response("HTTP_200_RESPONSE_WITH_UPGRADE_HEADER_AND_CONTENT_LENGTH",
                name: "HTTP 200 response with Upgrade and Content-Length header",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Connection: upgrade\r\n"
                    + "Upgrade: h2c\r\n"
                    + "Content-Length: 4\r\n"
                    + "\r\n"
                    + "body",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                body: "body",
                upgrade: null,
                headers: Header(
                    ("Connection", "upgrade"),
                    ("Upgrade", "h2c"),
                    ("Content-Length", "4")
                )),

            Message.Response("HTTP_200_RESPONSE_WITH_UPGRADE_HEADER_AND_TRANSFER_ENCODING",
                name: "HTTP 200 response with Upgrade and Transfer-Encoding header",
                raw: "HTTP/1.1 200 OK\r\n"
                    + "Connection: upgrade\r\n"
                    + "Upgrade: h2c\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n"
                    + "2\r\n"
                    + "bo\r\n"
                    + "2\r\n"
                    + "dy\r\n"
                    + "0\r\n"
                    + "\r\n",
                shouldKeepAlive: true,
                messageCompleteOnEof: false,
                httpMajor: 1,
                httpMinor: 1,
                statusCode: 200,
                responseStatus: "OK",
                body: "body",
                upgrade: null,
                headers: Header(
                    ("Connection", "upgrade"),
                    ("Upgrade", "h2c"),
                    ("Transfer-Encoding", "chunked")),
                chunkLengths: ChunkLength(2, 2)
            )
        };

        #endregion
    }
}
