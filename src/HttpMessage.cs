#region Copyright 2018 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace Sazzy
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;

    public enum HttpMessageKind { Request, Response }

    public sealed class HttpRequest : IDisposable
    {
        HttpMessage _message;

        public HttpRequest(HttpMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (message.Kind != HttpMessageKind.Request) throw new ArgumentException("Invalid HTTP message kind.", nameof(message));
            _message = message;
        }

        public HttpMessage Message => _message ?? throw new ObjectDisposedException(nameof(HttpRequest));

        public string  Method      => Message.RequestMethod;
        public Uri     Url         => Message.RequestUrl;
        public Version HttpVersion => Message.HttpVersion;

        public void Dispose()
        {
            var message = _message;
            _message = null;
            message?.Dispose();
        }
    }

    public sealed class HttpResponse: IDisposable
    {
        HttpMessage _message;

        public HttpResponse(HttpMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (message.Kind != HttpMessageKind.Response) throw new ArgumentException("Invalid HTTP message kind.", nameof(message));
            _message = message;
        }

        public HttpMessage Message => _message ?? throw new ObjectDisposedException(nameof(HttpResponse));

        public HttpStatusCode StatusCode   => Message.StatusCode;
        public string         ReasonPhrase => Message.ReasonPhrase;
        public Version        HttpVersion  => Message.HttpVersion;

        public void Dispose()
        {
            var message = _message;
            _message = null;
            message?.Dispose();
        }
    }

    public static class HttpMessageReader
    {
        public static HttpMessage Read(Stream stream) =>
            new HttpMessage(stream);

        public static HttpRequest ReadRequest(Stream stream)
            => Read(stream, out var req, out _) == HttpMessageKind.Request ? req
             : throw new ArgumentException(null, nameof(stream));

        public static HttpResponse ReadResponse(Stream stream)
            => Read(stream, out _, out var rsp) == HttpMessageKind.Response ? rsp
             : throw new ArgumentException(null, nameof(stream));

        public static T Read<T>(Stream stream, Func<HttpRequest, T> requestSelector,
                                               Func<HttpResponse, T> responseSelector)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (requestSelector == null) throw new ArgumentNullException(nameof(requestSelector));
            if (responseSelector == null) throw new ArgumentNullException(nameof(responseSelector));

            return Read(stream, out var request, out var response) switch
            {
                HttpMessageKind.Request => requestSelector(request),
                HttpMessageKind.Response => responseSelector(response),
                _ => throw new Exception("Internal implementation error.")
            };
        }

        public static HttpMessageKind Read(Stream stream, out HttpRequest request,
                                                          out HttpResponse response)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var message = Read(stream);
            request = message.IsRequest ? new HttpRequest(message) : null;
            response = message.IsResponse ? new HttpResponse(message) : null;
            return message.Kind;
        }
    }

    delegate void ChunkSizeReadEventHandler(long size);
    delegate void TrailingHeadersReadEventHandler(IList<KeyValuePair<string, string>> headers);

    public sealed class HttpMessage : IDisposable
    {
        Stream _contentStream;
        bool _isContentStreamDisowned;

        internal HttpMessage(Stream input) :
            this(input, null) {}

        internal HttpMessage(Stream input, ChunkSizeReadEventHandler onChunkSizeRead)
        {
            Version version = null;
            string requestMethod = null;
            Uri requestUrl = null;
            HttpStatusCode responseStatusCode = 0;
            string responseReasonPhrase = null;
            long? contentLength = null;
            var chunked = false;
            var headers = new List<KeyValuePair<string, string>>();

            void OnRequestLine(string method, string url, string protocolVersion)
            {
                version = protocolVersion != null ? new Version(protocolVersion) : new Version(0, 9);
                requestMethod = method;
                requestUrl = new Uri(url, UriKind.RelativeOrAbsolute);
            }

            void OnResponseLine(string protocolVersion, int statusCode, string reasonPhrase)
            {
                version = new Version(protocolVersion);
                responseStatusCode = (HttpStatusCode) statusCode;
                responseReasonPhrase = reasonPhrase;
            }

            void OnHeader(string name, string value)
            {
                headers.Add(new KeyValuePair<string, string>(name, value));

                if (!string.IsNullOrEmpty(value))
                {
                    if ("Transfer-Encoding".Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        chunked = "chunked".Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
                    }
                    else if ("Content-Length".Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        contentLength = long.Parse(value, NumberStyles.AllowLeadingWhite
                                                         | NumberStyles.AllowTrailingWhite,
                                                    CultureInfo.InvariantCulture);
                    }
                }
            }

            void OnTrailingHeader(IList<KeyValuePair<string, string>> headers) =>
                TrailingHeaders = new ReadOnlyCollection<KeyValuePair<string, string>>(headers);

            HttpMessagePrologueParser.Parse(input,
                HttpMessagePrologueParser.CreateDelegatingSink(
                    OnRequestLine,
                    OnResponseLine,
                    OnHeader));

            HttpVersion     = version;
            RequestMethod   = requestMethod;
            RequestUrl      = requestUrl;
            StatusCode      = responseStatusCode;
            ReasonPhrase    = responseReasonPhrase;
            Headers         = new ReadOnlyCollection<KeyValuePair<string, string>>(headers);
            TrailingHeaders = chunked ? null : EmptyKeyValuePairs;
            ContentLength   = contentLength;

            var initialState
                = (   "GET"    .Equals(requestMethod, StringComparison.OrdinalIgnoreCase)
                   || "CONNECT".Equals(requestMethod, StringComparison.OrdinalIgnoreCase))
                && (contentLength ?? 0) == 0
                ? State.Eoi
                : chunked
                ? State.ReadChunkSize
                : State.CopyAll;

            _contentStream = new HttpContentStream(input, initialState, ContentLength,
                                                   onChunkSizeRead,
                                                   chunked ? OnTrailingHeader
                                                            : (TrailingHeadersReadEventHandler)null);
        }

        public HttpMessageKind Kind => RequestUrl != null ? HttpMessageKind.Request : HttpMessageKind.Response;

        public bool IsRequest       => Kind == HttpMessageKind.Request;
        public bool IsResponse      => Kind == HttpMessageKind.Response;

        public string StartLine     => ResponseLine ?? RequestLine;
        public string RequestLine   => IsRequest  ? string.Join(" ", RequestMethod, RequestUrl.OriginalString, "HTTP/" + HttpVersion) : null;
        public string ResponseLine  => IsResponse ? string.Join(" ", StatusCode.ToString("d"), ReasonPhrase, "HTTP/" + HttpVersion) : null;

        public Version HttpVersion       { get; }

        public string RequestMethod      { get; }
        public Uri RequestUrl            { get; }

        public HttpStatusCode StatusCode { get; }
        public string ReasonPhrase       { get; }

        public IReadOnlyCollection<KeyValuePair<string, string>> Headers { get; }
        public IReadOnlyCollection<KeyValuePair<string, string>> TrailingHeaders { get; private set; }

        Dictionary<string, string> _headerByName;

        public string this[string header]
        {
            get
            {
                if (_headerByName == null)
                {
                    _headerByName =
                        Headers.GroupBy(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase)
                               .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
                }

                return _headerByName.TryGetValue(header, out var value) ? value : null;
            }
        }

        public long? ContentLength { get; }

        public Stream ContentStream =>
            _contentStream ?? throw new ObjectDisposedException(nameof(HttpMessage));

        bool IsDisposed => ContentStream == null;

        public void DisownContentStream() => _isContentStreamDisowned = true;

        public void Dispose()
        {
            if (IsDisposed)
                return;

            var stream = _contentStream;
            _contentStream = null;
            if (!_isContentStreamDisowned)
                stream.Close();
        }

        static string ReadLine(Stream stream, StringBuilder lineBuilder)
        {
            lineBuilder.Length = 0;

            int b;
            char ch;
            while ((b = stream.ReadByte()) >= 0 && (ch = (char) b) != '\n')
            {
                if (ch != '\r' && ch != '\n')
                    lineBuilder.Append(ch);
            }
            return lineBuilder.ToString();
        }

        enum State { Eoi, CopyAll, CopyChunk, ReadChunkSize }

        static readonly KeyValuePair<string, string>[] EmptyKeyValuePairs = new KeyValuePair<string, string>[0];

        sealed class HttpContentStream : Stream
        {
            Stream _input;
            State _state;
            bool _disposed;
            readonly long? _contentLength;

            long? _remainingLength;
            StringBuilder _lineBuilder;

            ChunkSizeReadEventHandler _onChunkSizeRead;
            TrailingHeadersReadEventHandler _onTrailingHeadersRead;

            public HttpContentStream(Stream input, State state, long? length,
                                     ChunkSizeReadEventHandler onChunkSizeRead,
                                     TrailingHeadersReadEventHandler onTrailingHeadersRead)
            {
                _input = input;
                _state = state;
                _remainingLength = _contentLength = length;
                _onChunkSizeRead = onChunkSizeRead;
                _onTrailingHeadersRead = onTrailingHeadersRead;
            }

            StringBuilder LineBuilder => _lineBuilder ??= new StringBuilder();

            T Return<T>(T value) =>
                !_disposed ? value : throw new ObjectDisposedException(nameof(HttpContentStream));

            public override bool CanRead  => Return(true);
            public override bool CanSeek  => Return(false);
            public override bool CanWrite => Return(false);

            public override long Length =>
                This._contentLength is long n ? n : throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                _disposed = true;
                if (disposing)
                    Free();
            }

            void Free()
            {
                if (_input is null)
                    return;

                _input.Close();
                _input = null;
                _lineBuilder = null;
                _onChunkSizeRead = null;
                _onTrailingHeadersRead = null;
            }

            HttpContentStream This => Return(this);

            public override int Read(byte[] buffer, int offset, int count) =>
                Read(This, ArraySegment.Create(buffer, offset, count));

            int Read(HttpContentStream _, ArraySegment<byte> destination)
            {
                var result = 0;

                loop: switch (_state)
                {
                    case State.Eoi:
                    {
                        Free();
                        return result;
                    }
                    case State.CopyAll:
                    case State.CopyChunk:
                    {
                        while (_remainingLength > 0 || _remainingLength == null)
                        {
                            var count = (int) Math.Min(_remainingLength is long n
                                                       ? Math.Min(int.MaxValue, n)
                                                       : int.MaxValue,
                                                       destination.Count);

                            var read = _input.Read(destination.Array, destination.Offset, count);

                            result += read;
                            _remainingLength -= read;
                            destination = destination.Slice(read);

                            if (read == 0)
                                break;

                            if (destination.Count == 0)
                                return result;
                        }

                        if (_state == State.CopyChunk)
                        {
                            if (ReadLine().Length > 0)
                                throw new Exception("Invalid HTTP chunked transfer encoding.");
                            goto case State.ReadChunkSize;
                        }

                        if (_state == State.CopyAll)
                        {
                            _state = State.Eoi;
                            break;
                        }

                        throw new Exception("Internal implementation error.");
                    }
                    case State.ReadChunkSize:
                    {
                        // NOTE! Chunk extension is IGNORED; only the size is read and used.
                        //
                        //   chunk          = chunk-size [ chunk-extension ] CRLF
                        //                    chunk-data CRLF
                        //   chunk-size     = 1*HEX
                        //   chunk-extension= *( ";" chunk-ext-name [ "=" chunk-ext-val ] )
                        //   chunk-ext-name = token
                        //   chunk-ext-val  = token | quoted-string

                        var line = ReadLine();
                        var i = line.IndexOfAny(ChunkSizeDelimiters);
                        var chunkSize = int.Parse(i > 0 ? line.Substring(0, i) : line, NumberStyles.HexNumber);
                        _remainingLength = chunkSize;

                        _onChunkSizeRead?.Invoke(chunkSize);

                        if (chunkSize > 0)
                        {
                            _state = State.CopyChunk;
                        }
                        else
                        {
                            List<KeyValuePair<string, string>> headers = null;
                            foreach (var header in HttpMessagePrologueParser.ReadHeaders(_input))
                                (headers ??= new List<KeyValuePair<string, string>>()).Add(header);

                            _onTrailingHeadersRead?.Invoke(headers ?? (IList<KeyValuePair<string, string>>)EmptyKeyValuePairs);

                            _state = State.Eoi;
                        }

                        break;
                    }
                }

                goto loop;
            }

            static readonly char[] ChunkSizeDelimiters = { ';', ' ' };

            string ReadLine() => HttpMessage.ReadLine(_input, LineBuilder);

            #region Unsupported members

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() =>
                throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();

            public override void SetLength(long value) =>
                throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
                => throw new NotSupportedException();

            #endregion
        }
    }
}
