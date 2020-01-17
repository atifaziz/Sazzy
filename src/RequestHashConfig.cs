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

    public sealed class RequestHashConfig
    {
        public static readonly RequestHashConfig Default =
            new RequestHashConfig(HttpMessageHasher.RequestMethod(),
                                  HttpMessageHasher.RequestUrl(),
                                  HttpMessageHasher.HttpVersion(),
                                  HttpMessageHasher.Headers(),
                                  HttpMessageHasher.Content(),
                                  HttpMessageHasher.TrailingHeaders());

        RequestHashConfig(RequestHashConfig that) :
            this(that.Method, that.Url, that.Version,
                 that.Headers, that.Content, that.TrailingHeaders) {}

        RequestHashConfig(HttpMessageHashHandler method, HttpMessageHashHandler url, HttpMessageHashHandler version,
                          HttpMessageHashHandler headers, HttpMessageHashHandler content, HttpMessageHashHandler trailingHeaders)
        {
            Method = method;
            Url = url;
            Version = version;
            Headers = headers;
            Content = content;
            TrailingHeaders = trailingHeaders;
        }

        public HttpMessageHashHandler Method          { get; private set; }
        public HttpMessageHashHandler Url             { get; private set; }
        public HttpMessageHashHandler Version         { get; private set; }
        public HttpMessageHashHandler Headers         { get; private set; }
        public HttpMessageHashHandler Content         { get; private set; }
        public HttpMessageHashHandler TrailingHeaders { get; private set; }

        RequestHashConfig With(HttpMessageHashHandler current,
                               HttpMessageHashHandler update,
                               Action<RequestHashConfig, HttpMessageHashHandler> updater) =>
            update == current ? this : UpdateNew(updater, update);

        RequestHashConfig UpdateNew(Action<RequestHashConfig, HttpMessageHashHandler> updater,
                                    HttpMessageHashHandler update)
        {
            var config = new RequestHashConfig(this);
            updater(config, update);
            return config;
        }

        public RequestHashConfig WithMethod         (HttpMessageHashHandler value) => With(Method         , value, (c, v) => c.Method = v);
        public RequestHashConfig WithUrl            (HttpMessageHashHandler value) => With(Url            , value, (c, v) => c.Url = v);
        public RequestHashConfig WithVersion        (HttpMessageHashHandler value) => With(Version        , value, (c, v) => c.Version = v);
        public RequestHashConfig WithHeaders        (HttpMessageHashHandler value) => With(Headers        , value, (c, v) => c.Headers = v);
        public RequestHashConfig WithContent        (HttpMessageHashHandler value) => With(Content        , value, (c, v) => c.Content = v);
        public RequestHashConfig WithTrailingHeaders(HttpMessageHashHandler value) => With(TrailingHeaders, value, (c, v) => c.TrailingHeaders = v);

        public string HashString(HashAlgorithmName hashAlgorithm, HttpRequest request) =>
            Hash(hashAlgorithm, request).ToHexadecimalString();

        public byte[] Hash(HashAlgorithmName hashAlgorithm, HttpRequest request) =>
            request.Hash(hashAlgorithm,
                         Method          ?? HttpMessageHasher.Nop,
                         Url             ?? HttpMessageHasher.Nop,
                         Version         ?? HttpMessageHasher.Nop,
                         Headers         ?? HttpMessageHasher.Nop,
                         Content         ?? HttpMessageHasher.Nop,
                         TrailingHeaders ?? HttpMessageHasher.Nop);
    }
}
