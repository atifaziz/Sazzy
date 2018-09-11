namespace Sazzy
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    static class Program
    {
        static void Main(string[] args)
        {
            using (var stream = File.Open(args[0], FileMode.Open, FileAccess.Read))
            using (var output = Console.OpenStandardOutput())
                CopyHttpContent(stream, output);
        }

        enum State { Headers, ChunkSize, Body }

        static void CopyHttpContent(Stream input, Stream output)
        {
            var chunked = false;
            var state = State.Headers;
            var chunkSize = 0;
            var contentLength = -1;

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
                        else if (line.Equals("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
                        {
                            chunked = true;
                        }
                        else if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            contentLength = int.Parse(Regex.Match(line, @"(?<=Content-Length: )\d+$").Value);
                        }

                        break;
                    }
                    case State.ChunkSize:
                    {
                        chunkSize = int.Parse(ReadLine(input), NumberStyles.HexNumber);
                        if (chunkSize == 0)
                            return;
                        state = State.Body;
                        break;
                    }
                    case State.Body when chunked:
                    {
                        var buffer = new byte[chunkSize];

                        while (chunkSize > 0)
                        {
                            var read = input.Read(buffer, 0, buffer.Length);
                            if (read == 0)
                                throw new Exception("Unexpected end of HTTP content.");
                            output.Write(buffer, 0, read);
                            chunkSize -= read;
                        }

                        var line = ReadLine(input);
                        if (!string.IsNullOrEmpty(line))
                        {
                            throw new Exception("Expected empty line but was " + line);
                        }

                        state = State.ChunkSize;
                        break;
                    }
                    case State.Body:
                    {
                        var buffer = new byte[contentLength];

                        while (contentLength > 0)
                        {
                            var read = input.Read(buffer, 0, buffer.Length);
                            if (read == 0)
                                throw new Exception("Unexpected end of HTTP content.");
                            output.Write(buffer, 0, read);
                            contentLength -= read;
                        }

                        return;
                    }
                }
            }

            string ReadLine(Stream stream)
            {
                var result = new StringBuilder();
                int b;
                char character;
                while ((b = stream.ReadByte()) >= 0 && (character = (char) b) != '\n')
                {
                    if (character != '\r' && character != '\n')
                        result.Append(character);
                }
                return result.ToString();
            }
        }
    }
}
