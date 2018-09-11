namespace Sazzy
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    static class Program
    {
        enum State { Headers, ChunkSize, Body, End };

        static void Main(string[] args)
        {
            var chunked = false;
            var state = State.Headers;
            var chunkSize = 0;
            var contentLength = -1;

            using (var stream = File.Open(args[0], FileMode.Open, FileAccess.Read))
            using (var output = Console.OpenStandardOutput())
            {
                while (state != State.End)
                {
                    switch (state)
                    {
                        case State.Headers:
                        {
                            var line = stream.ReadLine();
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
                            chunkSize = int.Parse(stream.ReadLine(), NumberStyles.HexNumber);
                            state = chunkSize > 0 ? State.Body : State.End;
                            break;
                        }
                        case State.Body when chunked:
                        {
                            var buffer = new byte[chunkSize];
                            do
                            {
                                var read = stream.Read(buffer, 0, buffer.Length);
                                if (read < 0)
                                    throw new Exception("");
                                output.Write(buffer, 0, read);
                                chunkSize -= read;
                            }
                            while (chunkSize > 0);

                            var line = stream.ReadLine();
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

                            do
                            {
                                var read = stream.Read(buffer, 0, buffer.Length);
                                if (read < 0)
                                    throw new Exception("");
                                output.Write(buffer, 0, read);
                                contentLength -= read;
                            }
                            while (contentLength > 0);

                            state = State.End;
                            break;
                        }
                    }
                }
            }
        }
    }
}
