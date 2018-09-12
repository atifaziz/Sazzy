namespace Sazzy
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Text;

    public sealed class HttpContentStream : Stream
    {
        public static HttpContentStream Open(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            if (!input.CanRead)
                throw new ArgumentException(null, nameof(input));

            long? contentLength = null;
            var chunked = false;
            var headers = new List<KeyValuePair<string, string>>();
            var lineBuilder = new StringBuilder();

            while (true)
            {
                var line = ReadLine(input, lineBuilder);

                if (string.IsNullOrEmpty(line))
                {
                    return new HttpContentStream(
                        input,
                        chunked ? State.ReadChunkSize : State.CopyAll,
                        new ReadOnlyCollection<KeyValuePair<string, string>>(headers),
                        contentLength, lineBuilder);
                }

                var pair = line.Split(Colon, 2);
                if (pair.Length > 1)
                {
                    var (header, value) = (pair[0].Trim(), pair[1]);
                    headers.Add(new KeyValuePair<string, string>(header, value));

                    if ("Transfer-Encoding".Equals(header, StringComparison.OrdinalIgnoreCase))
                        chunked = "chunked".Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
                    else if ("Content-Length".Equals(header, StringComparison.OrdinalIgnoreCase))
                        contentLength = long.Parse(value, NumberStyles.Integer & ~NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                }
            }
        }

        Stream _input;
        State _state;
        bool _disposed;
        readonly IReadOnlyCollection<KeyValuePair<string, string>> _headers;
        readonly long? _contentLength;

        long _remainingLength;
        StringBuilder _lineBuilder;

        HttpContentStream(Stream input, State state,
                          IReadOnlyCollection<KeyValuePair<string, string>> headers,
                          long? length,
                          StringBuilder lineBuilder)
        {
            _input = input;
            _state = state;
            _headers = headers;
            _remainingLength = (_contentLength = length) ?? 0;
            _lineBuilder = lineBuilder;
        }

        T Return<T>(T value) =>
            !_disposed ? value : throw new ObjectDisposedException(nameof(HttpContentStream));

        public IReadOnlyCollection<KeyValuePair<string, string>> Headers =>
            This._headers;

        public override bool CanRead  => Return(true);
        public override bool CanSeek  => Return(false);
        public override bool CanWrite => Return(false);

        public long? ContentLength => This._contentLength;

        public override long Length =>
            ContentLength is long n ? n : throw new NotSupportedException();

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

        static readonly char[] Colon = { ':' };

        enum State { Eoi, CopyAll, CopyChunk, ReadChunkSize }

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
                        if (ReadLine(_input).Length > 0)
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
                        int.Parse(ReadLine(_input), NumberStyles.HexNumber);

                    _state = chunkSize == 0 ? State.Eoi : State.CopyChunk;
                    break;
                }
            }

            goto loop;
        }

        string ReadLine(Stream stream) => ReadLine(stream, _lineBuilder);

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
