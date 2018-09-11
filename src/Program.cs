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
                if (state == State.Headers)
                {
                    string line = stream.ReadLine();
                    if (string.IsNullOrEmpty(line))
                    {
                        if (chunked)
                        {
                            state = State.ChunkSize;
                        }
                        else
                        {
                            state = State.Body;
                        }
                    }
                    else if (line.Equals("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
                    {
                        chunked = true;
                    }
                    else if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        contentLength = int.Parse(Regex.Match(line, @"(?<=Content-Length: )\d+$").Value);
                    }
                }

                if (state == State.ChunkSize)
                {
                    chunkSize = int.Parse(stream.ReadLine(), NumberStyles.HexNumber);
                    if (chunkSize > 0)
                        state = State.Body;
                    else
                        state = State.End;
                }

                if (state == State.Body)
                {
                    if (chunked)
                    {
                        byte[] buffer = new byte[chunkSize];
                        if (stream.Read(buffer, 0, chunkSize) < 0)
                            throw new Exception("");
                        sb.Append(Encoding.ASCII.GetString(buffer, 0, chunkSize));

                        string line = stream.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                        {
                            throw new Exception("Expected empty line but was " + line);
                        }
                        state = State.ChunkSize;
                    }
                    else
                    {
                        sb.Append(stream.ReadToEnd(contentLength));
                        state = State.End;
                    }
                }
            }
            Console.WriteLine(sb.ToString());
        }
    }
}
