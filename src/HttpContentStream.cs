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
                        chunked ? State.ReadChunkSize : State.Read,
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
        ArraySegment<byte> _buffer;

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
            _buffer = new ArraySegment<byte>();
            _lineBuilder = null;
        }

        HttpContentStream This => Return(this);

        static readonly char[] Colon = { ':' };

        enum State { Eoi, Read, Fill, ReadChunkSize, ReadChunk, FillChunk }

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
                    return 0;
                }
                case State.Fill:
                case State.FillChunk:
                {
                    var count = Math.Min(_buffer.Count, destination.Count);
                    Array.Copy(_buffer.Array, _buffer.Offset, destination.Array, destination.Offset, count);

                    result += count;
                    _buffer = _buffer.Slice(count);
                    destination = destination.Slice(count);

                    if (_buffer.Count > destination.Count)
                        return result;

                    if (_state == State.FillChunk)
                        goto case State.ReadChunk;

                    if (_state == State.Fill)
                        goto case State.Read;

                    throw new Exception("Internal implementation error.");
                }
                case State.Read:
                {
                    if (Read(ref _remainingLength) == 0)
                    {
                        _state = State.Eoi;
                        break;
                    }

                    _state = State.Fill;
                    break;
                }
                case State.ReadChunkSize:
                {
                    var chunkSize = _remainingLength =
                        int.Parse(ReadLine(_input), NumberStyles.HexNumber);

                    if (chunkSize == 0)
                    {
                        _state = State.Eoi;
                        break;
                    }

                    goto case State.ReadChunk;
                }
                case State.ReadChunk:
                {
                    if (Read(ref _remainingLength) == 0)
                    {
                        if (ReadLine(_input).Length > 0)
                            throw new Exception("Invalid HTTP chunked transfer encoding.");

                        goto case State.ReadChunkSize;
                    }

                    _state = State.FillChunk;
                    break;
                }
            }

            goto loop;

            int Read(ref long remainder)
            {
                if (_buffer.Array == null)
                    _buffer = ArraySegment.Create(new byte[4096], 0, 0);
                _buffer = _buffer.WithCount(_input.Read(_buffer.Array, 0, (int) Math.Min(remainder, _buffer.Array.Length)));
                remainder -= _buffer.Count;
                return _buffer.Count;
            }
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
