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
                               .ToDictionary(g => g.Key,
                                             //
                                             // RFC 2616
                                             // http://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.2
                                             //
                                             // Multiple message-header fields with the same field-name
                                             // MAY be present in a message if and only if the entire
                                             // field-value for that header field is defined as a
                                             // comma-separated list [i.e., #(values)]. It MUST be
                                             // possible to combine the multiple header fields into one
                                             // "field-name: field-value" pair, without changing the
                                             // semantics of the message, by appending each subsequent
                                             // field-value to the first, each separated by a comma.
                                             // The order in which header fields with the same
                                             // field-name are received is therefore significant to the
                                             // interpretation of the combined field value, and thus a
                                             // proxy MUST NOT change the order of these field values
                                             // when a message is forwarded.
                                             //
                                             // RFC 7230, Section 3.2.2: Field Order
                                             // https://tools.ietf.org/html/rfc7230#section-3.2.2
                                             //
                                             // Note: In practice, the "Set-Cookie" header field
                                             // ([RFC6265]) often appears multiple times in a response
                                             // message and does not use the list syntax, violating the
                                             // above requirements on multiple header fields with the
                                             // same name. Since it cannot be combined into a single
                                             // field-value, recipients ought to handle "Set-Cookie"
                                             // as a special case while processing header fields.
                                             //
                                             // Using the exception list from "nsHttpHeaderArray.h"
                                             // from mozilla-central:
                                             //
                                             // https://github.com/bnoordhuis/mozilla-central/blob/c41009f7ae12524a21f8178fdbcc72ebf9a35fcf/netwerk/protocol/http/nsHttpHeaderArray.h#L185-L187
                                             //
                                             g => string.Join(   string.Equals(g.Key, "Set-Cookie"        , StringComparison.OrdinalIgnoreCase)
                                                              || string.Equals(g.Key, "WWW-Authenticate"  , StringComparison.OrdinalIgnoreCase)
                                                              || string.Equals(g.Key, "Proxy-Authenticate", StringComparison.OrdinalIgnoreCase)
                                                              ? "\n" : ",",
                                                              from v in g
                                                              where !string.IsNullOrWhiteSpace(v)
                                                              select v),
                                             StringComparer.OrdinalIgnoreCase);
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
