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

namespace Sazzy.Sample
{
    using System;
    using System.IO;

    static class Program
    {
        static void Wain(string[] args)
        {
            var arg = args.Length > 0
                    ? args[0]
                    : throw new Exception("Missing file specification.");

            if (IsZipFile(arg))
            {
                foreach (var e in Saz.ReadCorrelated(arg, (reqn, req, rspn, rsp) => new
                {
                    Request  = new { FullName = reqn, req.Message },
                    Response = new { FullName = rspn, rsp.Message },
                }))
                {
                    foreach (var r in new[] { e.Request, e.Response })
                    {
                        Console.WriteLine($":{r.FullName}");
                        Console.WriteLine();
                        Dump(r.Message, Console.Out);
                        Console.WriteLine();
                    }
                }
            }
            else
            {
                using var input = File.OpenRead(arg);
                using var message = HttpMessageReader.Read(input);
                Dump(message, Console.Error, Console.OpenStandardOutput());
            }
        }

        static void Dump(HttpMessage message, TextWriter headerWriter, Stream contentStream = null)
        {
            headerWriter.WriteLine(message.StartLine);

            foreach (var (name, value) in message.Headers)
                headerWriter.WriteLine(name + ": " + value);

            var chunked = string.Equals(message["Transfer-Encoding"]?.Trim(), "chunked", StringComparison.OrdinalIgnoreCase);

            if (contentStream == null && !chunked)
                return;

            message.ContentStream.CopyTo(contentStream ?? Stream.Null);

            foreach (var (name, value) in message.TrailingHeaders)
                headerWriter.WriteLine(name + ": " + value);
        }

        static bool IsZipFile(string path)
        {
            using var fs = File.OpenRead(path);
            return fs.Length >= 2 && fs.ReadByte() == 'P' && fs.ReadByte() == 'K';
        }

        static int Main(string[] args)
        {
            try
            {
                Wain(args);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return 0xbad;
            }
        }
    }
}
