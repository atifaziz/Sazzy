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
                                                Func<ArraySegment<byte>, Task> writer);

    public static class HttpMessageHasher
    {
        static HttpMessageHashHandler String(Func<HttpMessage, string> f, Encoding encoding) =>
            (message, pool, writer) => String(f(message), encoding)(message, pool, writer);

        static HttpMessageHashHandler String(string s, Encoding encoding) =>
            async (message, pool, writer) =>
            {
                var size = encoding.GetByteCount(s);
                var buffer = pool.Rent(size);
                try
                {
                    var length = encoding.GetBytes(s, 0, s.Length, buffer, 0);
                    await writer(new ArraySegment<byte>(buffer, 0, length)).ConfigureAwait(false);
                }
                finally
                {
                    pool.Return(buffer);
                }
            };

        [ThreadStatic] static byte[] _singleByteBuffer;

        static byte[] SingleByteBuffer => _singleByteBuffer ??= new byte[1];

        static HttpMessageHashHandler Literal(char ch) =>
            (message, pool, writer) =>
            {
                var buffer = SingleByteBuffer;
                buffer[0] = checked((byte)ch);
                return writer(new ArraySegment<byte>(buffer, 0, buffer.Length));
            };

        static readonly HttpMessageHashHandler Colon = Literal(':');

        static readonly HttpMessageHashHandler RequestMethodHashHandler =
            String(m => m.RequestMethod.ToUpperInvariant(), Encoding.ASCII);

        static readonly HttpMessageHashHandler RequestUrlHashHandler =
            String(m => m.RequestUrl.OriginalString, Encoding.ASCII);

        static readonly HttpMessageHashHandler StatusCodeHashHandler =
            String(m => ((int) m.StatusCode).ToString(CultureInfo.InvariantCulture), Encoding.ASCII);

        static readonly HttpMessageHashHandler ReasonPhraseHashHandler =
            String(m => m.ReasonPhrase, Encoding.ASCII);

        static readonly HttpMessageHashHandler HttpVersionHashHandler =
            String(m => m.HttpVersion.ToString(2), Encoding.ASCII);

        internal static HttpMessageHashHandler Nop => delegate { return Task.CompletedTask; };

        public static HttpMessageHashHandler HttpVersion()   => HttpVersionHashHandler;
        public static HttpMessageHashHandler RequestMethod() => RequestMethodHashHandler;
        public static HttpMessageHashHandler RequestUrl()    => RequestUrlHashHandler;
        public static HttpMessageHashHandler StatusCode()    => StatusCodeHashHandler;
        public static HttpMessageHashHandler ReasonPhrase()  => ReasonPhraseHashHandler;

        public static HttpMessageHashHandler Headers() =>
            Headers(m => m.Headers);

        public static HttpMessageHashHandler TrailingHeaders() =>
            Headers(m => m.TrailingHeaders);

        public static HttpMessageHashHandler Headers(Func<HttpMessage, IEnumerable<KeyValuePair<string, string>>> filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            return (message, pool, writer) =>
                Collect(from h in filter(message)
                        select new KeyValuePair<string, string>(h.Key.ToLowerInvariant(),
                                                                h.Value.Trim())
                        into h
                        orderby h.Key
                        select Collect(String(h.Key, Encoding.ASCII), Colon,
                                       String(h.Value, Encoding.ASCII)))
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
                        await writer(new ArraySegment<byte>(buffer, 0, read)).ConfigureAwait(false);
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
                var m when m.IsRequest => m.Hash(hashAlgorithm, RequestMethod(), RequestUrl(), HttpVersion(), Headers(), Content(), TrailingHeaders()),
                var m => m.Hash(hashAlgorithm, HttpVersion(), StatusCode(), ReasonPhrase(), Headers(), Content(), TrailingHeaders())
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
            Collect(handlers.Prepend(handler))(message, ArrayPool<byte>.Create(), buffer =>
            {
                hash.AppendData(buffer.Array, buffer.Offset, buffer.Count);
                return Task.CompletedTask;
            });
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
