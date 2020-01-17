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

    public sealed class HttpRequest : HttpMessage
    {
        internal HttpRequest(string method, Uri url, Version protocolVersion,
                             IReadOnlyCollection<KeyValuePair<string, string>> headers,
                             Stream contentStream,
                             IReadOnlyCollection<KeyValuePair<string, string>> trailingHeaders) :
            base(HttpMessageKind.Request, protocolVersion, headers, contentStream, trailingHeaders)
        {
            Method = method;
            Url = url ?? throw new ArgumentNullException(nameof(url));
        }

        public override string StartLine =>
            string.Join(" ", Method, Url.OriginalString, "HTTP/" + ProtocolVersion.ToString(2));

        public string  Method { get; }
        public Uri     Url    { get; }
    }
}
