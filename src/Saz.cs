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

namespace Sazzy
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;

    public static class Saz
    {
        enum MessageKind { Request, Response }

        /// <remarks>
        /// The <see cref="HttpMessage"/> arguments passed to the
        /// <paramref name="selector"/> function are disposed before this
        /// method returns.
        /// </remarks>

        public static IEnumerable<T> ReadCorrelated<T>(string path,
                                                       Func<string, HttpRequest,
                                                            string, HttpResponse, T> selector)
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Read);

            var entries = Enumerable.ToArray(
                from e in zip.Entries
                where e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                let fn = Path.GetFileNameWithoutExtension(e.Name)
                let tokens = fn.Split(new [] { '_' }, 2)
                select tokens.Length == 2 && tokens[0].Length > 0 && tokens[1].Length == 1
                       && char.ToLowerInvariant(tokens[1][0]) switch
                           {
                               'c' => MessageKind.Request,
                               's' => MessageKind.Response,
                               _ => (MessageKind?) null
                           }
                           is MessageKind mk
                       && int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                     ? new { Key = n, HttpMessageKind = mk, ZipEntry = e }
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
                select (req, rsp);

            foreach (var (req, rsp) in rrPairs)
            {
                using var requestStream = req.ZipEntry.Open();
                using var request = HttpMessageReader.ReadRequest(requestStream);
                using var responseStream = rsp.ZipEntry.Open();
                using var response = HttpMessageReader.ReadResponse(responseStream);
                yield return selector(req.ZipEntry.FullName, request,
                                      rsp.ZipEntry.FullName, response);
            }
        }
    }
}
