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
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    public interface IHttpMessagePrologueParseSink
    {
        void OnRequestLine(string method, string url, string version);
        void OnResponseLine(string version, int statusCode, string reasonPhrase);
        void OnHeader(string name, string value);
    }

    public static class HttpMessagePrologueParser
    {
        static readonly char[] Colon = { ':' };
        static readonly char[] Whitespace = { '\x20', '\t' };

        public static void Parse(Stream input, IHttpMessagePrologueParseSink sink)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            if (!input.CanRead)
                throw new ArgumentException(null, nameof(input));

            string headerName = null;
            string headerValue = null;

            var lineBuilder = new StringBuilder();

            var startLine = HttpLine.Read(input, lineBuilder).Trim();

            var match = Regex.Match(startLine, @"^HTTP/(0\.9|[1-9]\.[0-9])\x20+([1-5][0-9]{2})(?:\x20+(.+))?$");
            if (match.Success)
            {
                var groups = match.Groups;
                var version = groups[1].Value;
                var statusCode = int.Parse(groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture);
                var reasonPhrase = groups[3].Value;
                sink.OnResponseLine(version, statusCode, reasonPhrase);
            }
            else if ((match = Regex.Match(startLine, @"^([A-Za-z]+)\x20+([^\x20]+)(?:\x20+HTTP/([1-9]\.[0-9]))?$")).Success)
            {
                var groups = match.Groups;
                var method  = groups[1].Value;
                var url = groups[2].Value;
                var version = groups[3].Success ? groups[3].Value : null;
                sink.OnRequestLine(method, url, version);
            }
            else
            {
                throw new FormatException("Invalid HTTP request line or status response:" + startLine);
            }

            while (true)
            {
                var line = HttpLine.Read(input, lineBuilder);

                if (string.IsNullOrEmpty(line))
                    break;

                if (headerName != null && line[0] == ' ' || line[0] == '\t')
                {
                    headerValue = headerValue + line;
                }
                else
                {
                    if (headerName != null)
                        sink.OnHeader(headerName, headerValue);

                    var pair = line.Split(Colon, 2);
                    if (pair.Length != 2)
                        continue;

                    headerName = pair[0].Trim(Whitespace);
                    headerValue = pair[1].Trim(Whitespace);
                }
            }

            if (headerName != null)
                sink.OnHeader(headerName, headerValue);
        }

        public static IHttpMessagePrologueParseSink
            CreateDelegatingSink(
                Action<string, string, string> requestLineVisitor,
                Action<string, int, string> responseLineVisitor,
                Action<string, string> headerVisitor) =>
            new DelegatingSink(requestLineVisitor, responseLineVisitor, headerVisitor);

        sealed class DelegatingSink : IHttpMessagePrologueParseSink
        {
            readonly Action<string, string, string> _onRequestLine;
            readonly Action<string, int, string> _onResponseLine;
            readonly Action<string, string> _onHeader;

            public DelegatingSink(
                Action<string, string, string> onRequestLine,
                Action<string, int, string> onResponseLine,
                Action<string, string> onHeader)
            {
                _onRequestLine = onRequestLine;
                _onResponseLine = onResponseLine;
                _onHeader = onHeader;
            }

            public void OnRequestLine(string method, string url, string version) =>
                _onRequestLine?.Invoke(method, url, version);

            public void OnResponseLine(string version, int statusCode, string reasonPhrase) =>
                _onResponseLine?.Invoke(version, statusCode, reasonPhrase);

            public void OnHeader(string name, string value) =>
                _onHeader?.Invoke(name, value);
        }
    }
}
