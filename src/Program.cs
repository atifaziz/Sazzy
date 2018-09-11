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
            var stream = File.Open(args[0], FileMode.Open, FileAccess.Read);
            var sb = new StringBuilder();

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
                        if (stream.Read(buffer, 0, chunkSize) < 0)
                            throw new Exception("");
                        sb.Append(Encoding.ASCII.GetString(buffer, 0, chunkSize));

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
                        sb.Append(stream.ReadToEnd(contentLength));
                        state = State.End;
                        break;
                    }
                }
            }
            Console.WriteLine(sb.ToString());
        }
    }
}
