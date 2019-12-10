namespace Sazzy.Tests
{
    using System;
    using System.IO;

    static class Extensions
    {
        public static MemoryStream Buffer(this Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var buffer
                = input.CanSeek
                ? new MemoryStream(checked((int) (input.Length - input.Position)))
                : new MemoryStream();
            input.CopyTo(buffer);
            buffer.Position = 0;
            return buffer;
        }
    }
}
