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
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    public enum HttpMessageKind { Request, Response }

    public enum HttpFieldStatus { Missing, Error, Defined }

    public abstract class HttpMessage : IDisposable
    {
        Stream _contentStream;
        bool _isContentStreamDisowned;

        protected HttpMessage(HttpMessageKind kind,
                              Version protocolVersion,
                              IReadOnlyCollection<KeyValuePair<string, string>> headers,
                              Stream contentStream,
                              IReadOnlyCollection<KeyValuePair<string, string>> trailingHeaders)
        {
            Kind = kind;
            ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
            Headers = headers ?? throw new ArgumentNullException(nameof(headers));
            _contentStream = contentStream;
            TrailingHeaders = trailingHeaders;
        }

        public HttpMessageKind Kind { get; }

        public abstract string StartLine { get; }
        public Version ProtocolVersion   { get; }

        public IReadOnlyCollection<KeyValuePair<string, string>> Headers { get; }
        public IReadOnlyCollection<KeyValuePair<string, string>> TrailingHeaders { get; private set; }

        internal void InitializeTrailingHeaders(IReadOnlyCollection<KeyValuePair<string, string>> headers)
        {
            Debug.Assert(TrailingHeaders == null);
            TrailingHeaders = headers;
        }

        Dictionary<string, string> _headerByName;

        public string this[string header]
        {
            get
            {
                if (_headerByName == null)
                {
                    _headerByName =
                        Headers.GroupBy(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase)
                               .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
                }

                return _headerByName.TryGetValue(header, out var value) ? value : null;
            }
        }

        (HttpFieldStatus, long)? _cachedContentLength;

        public (HttpFieldStatus Status, long Value) ContentLength =>
            TryGetHeader(ref _cachedContentLength, "Content-Length",
                s => long.TryParse(s, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                                   CultureInfo.InvariantCulture, out var v) ? (true, v) : default);

        (HttpFieldStatus, T) TryGetHeader<T>(ref (HttpFieldStatus, T)? field,
                                              string name, Func<string,
                                              (bool, T)> parser) =>
            field ??= this[name] switch
            {
                null => (HttpFieldStatus.Missing, default),
                string s => parser(s) is (true, var v) ? (HttpFieldStatus.Defined, v)
                                                       : (HttpFieldStatus.Error, default),
            };

        public Stream ContentStream =>
            _contentStream ?? throw new ObjectDisposedException(nameof(HttpMessage));

        protected bool IsDisposed => ContentStream == null;

        public void DisownContentStream() => _isContentStreamDisowned = true;

        public void Dispose()
        {
            if (IsDisposed)
                return;

            var stream = _contentStream;
            _contentStream = null;
            if (!_isContentStreamDisowned)
                stream.Close();
        }

        internal static readonly KeyValuePair<string, string>[] EmptyKeyValuePairs = new KeyValuePair<string, string>[0];

        public override string ToString() => StartLine;
    }
}
