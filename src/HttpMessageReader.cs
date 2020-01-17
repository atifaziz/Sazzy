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
    using System.Net;
    using System.Text;

    public interface IHttpChunkedContentEventSource
    {
        event EventHandler<IList<KeyValuePair<string, string>>> TrailingHeadersRead;
        event EventHandler<long> ChunkSizeRead;
    }

    public static class HttpMessageReader
    {
        public static HttpMessage Read(Stream stream)
        {
            Version version = null;
            string requestMethod = null;
            Uri requestUrl = null;
            HttpStatusCode responseStatusCode = 0;
            string responseReasonPhrase = null;
            long? contentLength = null;
            var chunked = false;
            var headerList = new List<KeyValuePair<string, string>>();

            void OnRequestLine(string method, string url, string protocolVersion)
            {
                version = protocolVersion != null ? new Version(protocolVersion) : new Version(0, 9);
                requestMethod = method;
                requestUrl = new Uri(url, UriKind.RelativeOrAbsolute);
            }

            void OnResponseLine(string protocolVersion, int statusCode, string reasonPhrase)
            {
                version = new Version(protocolVersion);
                responseStatusCode = (HttpStatusCode)statusCode;
                responseReasonPhrase = reasonPhrase;
            }

            void OnHeader(string name, string value)
            {
                headerList.Add(new KeyValuePair<string, string>(name, value));

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

            HttpMessagePrologueParser.Parse(stream,
                HttpMessagePrologueParser.CreateDelegatingSink(
                    OnRequestLine,
                    OnResponseLine,
                    OnHeader));

            var trailingHeaders = chunked ? null : HttpMessage.EmptyKeyValuePairs;

            var initialState
                = ("GET".Equals(requestMethod, StringComparison.OrdinalIgnoreCase)
                   || "CONNECT".Equals(requestMethod, StringComparison.OrdinalIgnoreCase))
                && (contentLength ?? 0) == 0
                ? State.Eoi
                : chunked
                ? State.ReadChunkSize
                : State.CopyAll;

            var contentStream = new HttpContentStream(stream, initialState, contentLength);

            var headers = new ReadOnlyCollection<KeyValuePair<string, string>>(headerList);

            var message = requestMethod != null
                        ? (HttpMessage)new HttpRequest(requestMethod, requestUrl, version, headers, contentStream, trailingHeaders)
                        : new HttpResponse(version, responseStatusCode, responseReasonPhrase, headers, contentStream, trailingHeaders);

            if (chunked)
            {
                contentStream.TrailingHeadersRead +=
                    (_, hs) => message.InitializeTrailingHeaders(new ReadOnlyCollection<KeyValuePair<string, string>>(hs));
            }

            return message;
        }

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
            (request, response) = message is HttpRequest req
                                ? (req, (HttpResponse)null)
                                : ((HttpRequest)null, (HttpResponse)message);
            return message.Kind;
        }

        static string ReadLine(Stream stream, StringBuilder lineBuilder)
        {
            lineBuilder.Length = 0;

            int b;
            char ch;
            while ((b = stream.ReadByte()) >= 0 && (ch = (char)b) != '\n')
            {
                if (ch != '\r' && ch != '\n')
                    lineBuilder.Append(ch);
            }
            return lineBuilder.ToString();
        }
        enum State { Eoi, CopyAll, CopyChunk, ReadChunkSize }

        sealed class HttpContentStream : Stream, IHttpChunkedContentEventSource
        {
            Stream _input;
            State _state;
            bool _disposed;
            readonly long? _contentLength;

            long? _remainingLength;
            StringBuilder _lineBuilder;

            public HttpContentStream(Stream input, State state, long? length)
            {
                _input = input;
                _state = state;
                _remainingLength = _contentLength = length;
            }

            public event EventHandler<long> ChunkSizeRead;
            public event EventHandler<IList<KeyValuePair<string, string>>> TrailingHeadersRead;

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
                            var count = (int)Math.Min(_remainingLength is long n
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

                        ChunkSizeRead?.Invoke(this, chunkSize);

                        if (chunkSize > 0)
                        {
                            _state = State.CopyChunk;
                        }
                        else
                        {
                            List<KeyValuePair<string, string>> headers = null;
                            foreach (var header in HttpMessagePrologueParser.ReadHeaders(_input))
                                (headers ??= new List<KeyValuePair<string, string>>()).Add(header);

                            TrailingHeadersRead?.Invoke(this, headers ?? (IList<KeyValuePair<string, string>>)HttpMessage.EmptyKeyValuePairs);

                            _state = State.Eoi;
                        }

                        break;
                    }
                }

                goto loop;
            }

            static readonly char[] ChunkSizeDelimiters = { ';', ' ' };

            string ReadLine() => HttpMessageReader.ReadLine(_input, LineBuilder);

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
