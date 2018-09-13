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

    public sealed class HttpMessage : IDisposable
    {
        Stream _contentStream;

        public HttpMessage(Stream input)
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
                version = new Version(protocolVersion);
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
                        chunked = "chunked".Equals(value, StringComparison.OrdinalIgnoreCase);
                    else if ("Content-Length".Equals(name, StringComparison.OrdinalIgnoreCase))
                        contentLength = long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
                }
            }

            HttpMessagePrologueParser.Parse(input,
                HttpMessagePrologueParser.CreateDelegatingSink(
                    OnRequestLine,
                    OnResponseLine,
                    OnHeader));

            HttpVersion    = version;
            RequestMethod  = requestMethod;
            RequestUrl     = requestUrl;
            StatusCode     = responseStatusCode;
            ReasonPhrase   = responseReasonPhrase;
            Headers        = new ReadOnlyCollection<KeyValuePair<string, string>>(headers);
            ContentLength  = contentLength;
            _contentStream = new HttpContentStream(input,
                                                   chunked ? State.ReadChunkSize : State.CopyAll,
                                                   ContentLength);
        }

        public bool IsRequest      => RequestUrl != null;
        public bool IsResponse     => !IsRequest;

        public string StartLine    => ResponseLine ?? RequestLine;
        public string RequestLine  => IsRequest  ? string.Join(" ", RequestMethod, RequestUrl.OriginalString, "HTTP/" + HttpVersion) : null;
        public string ResponseLine => IsResponse ? string.Join(" ", StatusCode.ToString("d"), ReasonPhrase, "HTTP/" + HttpVersion) : null;

        public Version HttpVersion       { get; }

        public string RequestMethod      { get; }
        public Uri RequestUrl            { get; }

        public HttpStatusCode StatusCode { get; }
        public string ReasonPhrase       { get; }

        public IReadOnlyCollection<KeyValuePair<string, string>> Headers { get; }

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

        public void Dispose()
        {
            if (IsDisposed)
                return;

            var stream = _contentStream;
            _contentStream = null;
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

        sealed class HttpContentStream : Stream
        {
            Stream _input;
            State _state;
            bool _disposed;
            readonly long? _contentLength;

            long _remainingLength;
            StringBuilder _lineBuilder;

            public HttpContentStream(Stream input, State state, long? length)
            {
                _input = input;
                _state = state;
                _remainingLength = (_contentLength = length) ?? 0;
            }

            StringBuilder LineBuilder => _lineBuilder ?? (_lineBuilder = new StringBuilder());

            T Return<T>(T value) =>
                !_disposed ? value : throw new ObjectDisposedException(nameof(HttpContentStream));

            public override bool CanRead  => Return(true);
            public override bool CanSeek  => Return(false);
            public override bool CanWrite => Return(false);

            public override long Length =>
                This._contentLength is long n ? n : throw new NotSupportedException();

            public override void Close()
            {
                _disposed = true;
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
                        while (_remainingLength > 0)
                        {
                            var read =
                                _input.Read(destination.Array, destination.Offset,
                                    (int) Math.Min(Math.Min(int.MaxValue, _remainingLength), destination.Count));

                            result += read;
                            _remainingLength -= read;
                            destination = destination.Slice(read);

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
                        var chunkSize = _remainingLength =
                            int.Parse(ReadLine(), NumberStyles.HexNumber);

                        _state = chunkSize == 0 ? State.Eoi : State.CopyChunk;
                        break;
                    }
                }

                goto loop;
            }

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
