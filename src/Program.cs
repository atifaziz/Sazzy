namespace Sazzy
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;

    static class Program
    {
        static void Main(string[] args)
        {
            using (var stream = File.OpenRead(args[0]))
            using (var output = Console.OpenStandardOutput())
                CopyHttpContent(stream, output);
        }

        static readonly char[] Colon = { ':' };

        enum State { Headers, Body, ChunkSize, BodyChunk }

        static void CopyHttpContent(Stream input, Stream output)
        {
            var chunked = false;
            var state = State.Headers;
            var chunkSize = 0;
            var contentLength = -1;
            var lineBuilder = new StringBuilder();

            while (true)
            {
                switch (state)
                {
                    case State.Headers:
                    {
                        var line = ReadLine(input);
                        if (string.IsNullOrEmpty(line))
                        {
                            state = chunked ? State.ChunkSize : State.Body;
                        }
                        else
                        {
                            var pair = line.Split(Colon, 2);
                            if (pair.Length > 1)
                            {
                                var (header, value) = (pair[0].Trim(), pair[1]);
                                if ("Transfer-Encoding".Equals(header, StringComparison.OrdinalIgnoreCase))
                                {
                                    chunked = "chunked".Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
                                }
                                else if ("Content-Length".Equals(header, StringComparison.OrdinalIgnoreCase))
                                {
                                    contentLength = int.Parse(value, NumberStyles.Integer & ~NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                                }
                            }
                        }

                        break;
                    }
                    case State.ChunkSize:
                    {
                        chunkSize = int.Parse(ReadLine(input), NumberStyles.HexNumber);
                        if (chunkSize == 0)
                            return;
                        state = State.BodyChunk;
                        break;
                    }
                    case State.BodyChunk:
                    {
                        CopyBody(chunkSize);

                        var line = ReadLine(input);
                        if (!string.IsNullOrEmpty(line))
                            throw new Exception("Invalid HTTP chunked transfer encoding.");

                        state = State.ChunkSize;
                        break;
                    }
                    case State.Body:
                    {
                        CopyBody(contentLength);
                        return;
                    }
                }
            }

            void CopyBody(int size)
            {
                var buffer = new byte[size];

                while (size > 0)
                {
                    var read = input.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                        throw new Exception("Unexpected end of HTTP content.");
                    output.Write(buffer, 0, read);
                    size -= read;
                }
            }

            string ReadLine(Stream stream)
            {
                lineBuilder.Length = 0;
                int b;
                char character;
                while ((b = stream.ReadByte()) >= 0 && (character = (char) b) != '\n')
                {
                    if (character != '\r' && character != '\n')
                        lineBuilder.Append(character);
                }
                return lineBuilder.ToString();
            }
        }
    }
}
