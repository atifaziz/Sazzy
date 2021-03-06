#region Copyright 2020 Atif Aziz. All rights reserved.
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
    using System.Buffers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    public delegate Task HttpMessageHashHandler(HttpMessage message, ArrayPool<byte> pool,
                                                Action<ArraySegment<byte>> writer);

    public static class HttpMessageHasher
    {
        static HttpMessageHashHandler String(Func<HttpMessage, string> f, Encoding encoding) =>
            (message, pool, writer) => String(f(message), encoding)(message, pool, writer);

        static HttpMessageHashHandler String(string s, Encoding encoding) =>
            (message, pool, writer) =>
            {
                var size = encoding.GetByteCount(s);
                var buffer = pool.Rent(size);
                try
                {
                    var length = encoding.GetBytes(s, 0, s.Length, buffer, 0);
                    writer(new ArraySegment<byte>(buffer, 0, length));
                }
                finally
                {
                    pool.Return(buffer);
                }
                return Task.CompletedTask;
            };

        static readonly HttpMessageHashHandler RequestMethodHashHandler =
            String(m => ((HttpRequest)m).Method.ToUpperInvariant(), Encoding.ASCII);

        static readonly HttpMessageHashHandler RequestUrlHashHandler =
            String(m => ((HttpRequest)m).Url.OriginalString, Encoding.ASCII);

        static readonly HttpMessageHashHandler StatusCodeHashHandler =
            String(m => ((int)((HttpResponse)m).StatusCode).ToString(CultureInfo.InvariantCulture), Encoding.ASCII);

        static readonly HttpMessageHashHandler ReasonPhraseHashHandler =
            String(m => ((HttpResponse)m).ReasonPhrase, Encoding.ASCII);

        static readonly HttpMessageHashHandler ProtocolVersionHashHandler =
            String(m => m.ProtocolVersion.ToString(2), Encoding.ASCII);

        internal static HttpMessageHashHandler Nop => delegate { return Task.CompletedTask; };

        public static HttpMessageHashHandler ProtocolVersion() => ProtocolVersionHashHandler;
        public static HttpMessageHashHandler RequestMethod()   => RequestMethodHashHandler;
        public static HttpMessageHashHandler RequestUrl()      => RequestUrlHashHandler;
        public static HttpMessageHashHandler StatusCode()      => StatusCodeHashHandler;
        public static HttpMessageHashHandler ReasonPhrase()    => ReasonPhraseHashHandler;

        public static HttpMessageHashHandler Headers() =>
            Headers(m => m.Headers);

        public static HttpMessageHashHandler TrailingHeaders() =>
            Headers(m => m.TrailingHeaders);

        public static HttpMessageHashHandler Headers(Func<HttpMessage, IEnumerable<KeyValuePair<string, string>>> filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            // http://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.2
            //
            // Multiple message-header fields with the same field-name MAY be
            // present in a message if and only if the entire field-value for
            // that header field is defined as a comma-separated list [i.e.,
            // #(values)]. It MUST be possible to combine the multiple header
            // fields into one "field-name: field-value" pair, without
            // changing the semantics of the message, by appending each
            // subsequent field-value to the first, each separated by a comma.
            // The order in which header fields with the same field-name are
            // received is therefore significant to the interpretation of the
            // combined field value, and thus a proxy MUST NOT change the
            // order of these field values when a message is forwarded.

            return (message, pool, writer) =>
                Collect(from h in filter(message)
                        group h.Value.Trim() by h.Key.ToLowerInvariant() into h
                        orderby h.Key
                        select Collect(h.Select(v => String(v, Encoding.ASCII))
                                        .Prepend(String(h.Key, Encoding.ASCII))))
                    (message, pool, writer);
        }

        static readonly HttpMessageHashHandler ContentHashHandler =
            async (message, pool, writer) =>
            {
                var buffer = pool.Rent(4096);
                try
                {
                    int read;
                    while ((read = await message.ContentStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                        writer(new ArraySegment<byte>(buffer, 0, read));
                }
                finally
                {
                    pool.Return(buffer);
                }
            };

        public static HttpMessageHashHandler Content() => ContentHashHandler;

        public static string HashString(this HttpMessage message, HashAlgorithmName hashAlgorithm) =>
            Hash(message, hashAlgorithm).ToHexadecimalString();

        public static byte[] Hash(this HttpMessage message, HashAlgorithmName hashAlgorithm) =>
            message switch
            {
                null => throw new ArgumentNullException(nameof(message)),
                HttpRequest m => RequestHashConfig.Default.Hash(hashAlgorithm, m),
                var m => ResponseHashConfig.Default.Hash(hashAlgorithm, (HttpResponse)m)
            };

        public static string HashString(this HttpMessage message, HashAlgorithmName hashAlgorithm,
                                        HttpMessageHashHandler handler,
                                        params HttpMessageHashHandler[] handlers) =>
            Hash(message, hashAlgorithm, handler, handlers).ToHexadecimalString();

        public static byte[] Hash(this HttpMessage message, HashAlgorithmName hashAlgorithm,
                                  HttpMessageHashHandler handler,
                                  params HttpMessageHashHandler[] handlers)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            using var hash = IncrementalHash.CreateHash(hashAlgorithm);
            handler = Collect(handlers.Prepend(handler));
            var task = handler(message, ArrayPool<byte>.Create(),
                               buffer => hash.AppendData(buffer.Array, buffer.Offset, buffer.Count));
            task.GetAwaiter().GetResult();
            return hash.GetHashAndReset();
        }

        public static HttpMessageHashHandler Collect(IEnumerable<HttpMessageHashHandler> hashers)
        {
            if (hashers == null) throw new ArgumentNullException(nameof(hashers));

            return async (message, pool, writer) =>
            {
                foreach (var hasher in hashers)
                    await hasher(message, pool, writer).ConfigureAwait(false);
            };
        }

        public static HttpMessageHashHandler Collect(params HttpMessageHashHandler[] hashers) =>
            Collect(hashers.AsEnumerable());
    }
}
