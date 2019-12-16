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
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;

    static class Program
    {
        enum MessageKind { Request, Response }

        static void Wain(string[] args)
        {
            var arg = args.Length > 0
                    ? args[0]
                    : throw new Exception("Missing file specification.");

            if (IsZipFile(arg))
            {
                using var zip = ZipFile.Open(arg, ZipArchiveMode.Read);

                var entries = Enumerable.ToArray(
                    from e in zip.Entries
                    where e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                    let fn = Path.GetFileNameWithoutExtension(e.Name)
                    let tokens = fn.Split('_', 2)
                    select tokens.Length == 2 && tokens[0].Length > 0 && tokens[1].Length == 1
                           && char.ToLowerInvariant(tokens[1][0]) switch
                              {
                                  'c' => MessageKind.Request,
                                  's' => MessageKind.Response,
                                  _   => (MessageKind?)null
                              }
                              is MessageKind mk
                           && int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                         ? new
                         {
                             Key = n,
                             HttpMessageKind = mk,
                             e.Name,
                             e.FullName,
                             Content = Streamable.Create(e.Open),
                         }
                         : null
                    into e
                    where e != null
                    select e);

                var rrPairs =
                    from req in entries
                    where req.HttpMessageKind == MessageKind.Request
                    join rsp in from rsp in entries
                                where rsp.HttpMessageKind == MessageKind.Response
                                select rsp
                        on req.Key equals rsp.Key
                    orderby req.Key
                    select (Request: req, rsp);

                var i = 0;
                foreach (var (req, rsp) in rrPairs)
                {
                    foreach (var e in new[] { req, rsp })
                    {
                        Console.WriteLine($":{e.FullName}");
                        Console.WriteLine();
                        using var input = e.Content.Open();
                        Dump(input, Console.Out);
                        Console.WriteLine();
                    }
                    i++;
                }
            }
            else
            {
                using var input = File.OpenRead(arg);
                Dump(input, Console.Error, Console.OpenStandardOutput());
            }
        }

        static void Dump(Stream input, TextWriter headerWriter, Stream contentStream = null)
        {
            using var message = new HttpMessage(input);

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
