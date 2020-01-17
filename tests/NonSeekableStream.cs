namespace Sazzy.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    sealed class NonSeekableStream : Stream
    {
        readonly Stream _stream;

        public NonSeekableStream(Stream stream) =>
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

        public override bool CanSeek => false;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        // Remaining members delegate to underlying stream

        public override void Flush() =>
            _stream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _stream.Read(buffer, offset, count);

        public override void SetLength(long value) =>
            _stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _stream.Write(buffer, offset, count);

        public override bool CanRead  => _stream.CanRead;
        public override bool CanWrite => _stream.CanWrite;

        public override long Length => _stream.Length;

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
            _stream.BeginRead(buffer, offset, count, callback, state);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
            _stream.BeginWrite(buffer, offset, count, callback, state);

        public override void Close() =>
            _stream.Close();

        public override void CopyTo(Stream destination, int bufferSize) =>
            _stream.CopyTo(destination, bufferSize);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            _stream.CopyToAsync(destination, bufferSize, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _stream.Dispose();
        }

        public override ValueTask DisposeAsync() =>
            _stream.DisposeAsync();

        public override int EndRead(IAsyncResult asyncResult) =>
            _stream.EndRead(asyncResult);

        public override void EndWrite(IAsyncResult asyncResult) =>
            _stream.EndWrite(asyncResult);

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _stream.FlushAsync(cancellationToken);

        public override int Read(Span<byte> buffer) =>
            _stream.Read(buffer);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stream.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken()) =>
            _stream.ReadAsync(buffer, cancellationToken);

        public override int ReadByte() =>
            _stream.ReadByte();

        public override void Write(ReadOnlySpan<byte> buffer) =>
            _stream.Write(buffer);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stream.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stream.WriteAsync(buffer, cancellationToken);

        public override void WriteByte(byte value) =>
            _stream.WriteByte(value);

        public override bool CanTimeout  => _stream.CanTimeout;
        public override int ReadTimeout  => _stream.ReadTimeout;
        public override int WriteTimeout => _stream.WriteTimeout;
    }
}
