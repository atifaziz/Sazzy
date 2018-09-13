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
    using System.Text.RegularExpressions;

    public sealed class HttpMessage : IDisposable
    {
        static readonly char[] Colon = { ':' };
        static readonly char[] Whitespace = { '\x20', '\t' };

        Stream _contentStream;

        public HttpMessage(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            if (!input.CanRead)
                throw new ArgumentException(null, nameof(input));

            var chunked = false;
            var headers = new List<KeyValuePair<string, string>>();
            var lineBuilder = new StringBuilder();

            var startLine = StartLine = ReadLine(input, lineBuilder).Trim();

            var match = Regex.Match(startLine, @"^HTTP/([1-9]\.[0-9])\x20+([1-5][0-9]{2})\x20+(.+)");
            if (match.Success)
            {
                var groups = match.Groups;
                HttpVersion = new Version(groups[1].Value);
                StatusCode = (HttpStatusCode) int.Parse(groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture);
                ReasonPhrase = groups[3].Value;
            }
            else if ((match = Regex.Match(startLine, @"^([A-Za-z]+)\x20+[^\x20]+\x20+HTTP/([1-9]\.[0-9])")).Success)
            {
                var groups = match.Groups;
                RequestMethod = groups[1].Value;
                RequestUrl = new Uri(groups[2].Value, UriKind.Relative);
                HttpVersion = new Version(groups[3].Value);
            }

            while (true)
            {
                var line = ReadLine(input, lineBuilder);

                if (string.IsNullOrEmpty(line))
                    break;

                var pair = line.Split(Colon, 2);
                if (pair.Length > 1)
                {
                    var (header, value) = (pair[0].Trim(Whitespace), pair[1].Trim(Whitespace));
                    headers.Add(new KeyValuePair<string, string>(header, value));

                    if ("Transfer-Encoding".Equals(header, StringComparison.OrdinalIgnoreCase))
                        chunked = "chunked".Equals(value, StringComparison.OrdinalIgnoreCase);
                    else if ("Content-Length".Equals(header, StringComparison.OrdinalIgnoreCase))
                        ContentLength = long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
                }
            }

            Headers = new ReadOnlyCollection<KeyValuePair<string, string>>(headers);
            _contentStream = new HttpContentStream(
                input,
                chunked ? State.ReadChunkSize : State.CopyAll,
                ContentLength, lineBuilder);
        }

        public string StartLine          { get; }

        public bool IsRequest            => RequestUrl != null;
        public bool IsResponse           => !IsRequest;

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

            public HttpContentStream(Stream input, State state,
                                     long? length, StringBuilder lineBuilder)
            {
                _input = input;
                _state = state;
                _remainingLength = (_contentLength = length) ?? 0;
                _lineBuilder = lineBuilder;
            }

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

            string ReadLine() => HttpMessage.ReadLine(_input, _lineBuilder);

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
