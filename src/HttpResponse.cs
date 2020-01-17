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
    using System.Collections.Generic;
    using System.IO;
    using System.Net;

    public sealed class HttpResponse : HttpMessage
    {
        internal HttpResponse(Version httpVersion, HttpStatusCode statusCode, string reasonPhrase,
                              IReadOnlyCollection<KeyValuePair<string, string>> headers,
                              Stream contentStream,
                              IReadOnlyCollection<KeyValuePair<string, string>> trailingHeaders) :
            base(HttpMessageKind.Response, httpVersion, headers, contentStream, trailingHeaders)
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
        }

        public override string StartLine =>
            string.Join(" ", StatusCode.ToString("d"), ReasonPhrase, "HTTP/" + HttpVersion.ToString(2));

        public HttpStatusCode StatusCode   { get; }
        public string         ReasonPhrase { get; }
    }
}
