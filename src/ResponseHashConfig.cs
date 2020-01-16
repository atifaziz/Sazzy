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
    using System.Security.Cryptography;

    public sealed class ResponseHashConfig
    {
        public static readonly ResponseHashConfig Default =
            new ResponseHashConfig(HttpMessageHasher.HttpVersion(),
                                   HttpMessageHasher.StatusCode(),
                                   HttpMessageHasher.ReasonPhrase(),
                                   HttpMessageHasher.Headers(),
                                   HttpMessageHasher.Content(),
                                   HttpMessageHasher.TrailingHeaders());

        ResponseHashConfig(ResponseHashConfig that) :
            this(that.Version, that.StatusCode, that.ReasonPhrase,
                 that.Headers, that.Content, that.TrailingHeaders) {}

        public ResponseHashConfig(HttpMessageHashHandler version, HttpMessageHashHandler statusCode, HttpMessageHashHandler reasonPhrase,
                                  HttpMessageHashHandler headers, HttpMessageHashHandler content, HttpMessageHashHandler trailingHeaders)
        {
            Version = version;
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
            Headers = headers;
            Content = content;
            TrailingHeaders = trailingHeaders;
        }

        public HttpMessageHashHandler Version         { get; private set; }
        public HttpMessageHashHandler StatusCode      { get; private set; }
        public HttpMessageHashHandler ReasonPhrase    { get; private set; }
        public HttpMessageHashHandler Headers         { get; private set; }
        public HttpMessageHashHandler Content         { get; private set; }
        public HttpMessageHashHandler TrailingHeaders { get; private set; }

        ResponseHashConfig With(HttpMessageHashHandler current,
                                HttpMessageHashHandler update,
                                Action<ResponseHashConfig, HttpMessageHashHandler> updater) =>
            update == current ? this : UpdateNew(updater, update);

        ResponseHashConfig UpdateNew(Action<ResponseHashConfig, HttpMessageHashHandler> updater,
                                    HttpMessageHashHandler update)
        {
            var config = new ResponseHashConfig(this);
            updater(config, update);
            return config;
        }

        public ResponseHashConfig WithVersion        (HttpMessageHashHandler value) => With(Version        , value, (c, v) => c.Version = v);
        public ResponseHashConfig WithMethod         (HttpMessageHashHandler value) => With(StatusCode     , value, (c, v) => c.StatusCode = v);
        public ResponseHashConfig WithUrl            (HttpMessageHashHandler value) => With(ReasonPhrase   , value, (c, v) => c.ReasonPhrase = v);
        public ResponseHashConfig WithHeaders        (HttpMessageHashHandler value) => With(Headers        , value, (c, v) => c.Headers = v);
        public ResponseHashConfig WithContent        (HttpMessageHashHandler value) => With(Content        , value, (c, v) => c.Content = v);
        public ResponseHashConfig WithTrailingHeaders(HttpMessageHashHandler value) => With(TrailingHeaders, value, (c, v) => c.TrailingHeaders = v);

        public string HashString(HashAlgorithmName hashAlgorithm, HttpResponse response) =>
            Hash(hashAlgorithm, response).ToHexadecimalString();

        public byte[] Hash(HashAlgorithmName hashAlgorithm, HttpResponse response) =>
            response.Message.Hash(hashAlgorithm,
                                  Version         ?? HttpMessageHasher.Nop,
                                  StatusCode      ?? HttpMessageHasher.Nop,
                                  ReasonPhrase    ?? HttpMessageHasher.Nop,
                                  Headers         ?? HttpMessageHasher.Nop,
                                  Content         ?? HttpMessageHasher.Nop,
                                  TrailingHeaders ?? HttpMessageHasher.Nop);
    }
}
