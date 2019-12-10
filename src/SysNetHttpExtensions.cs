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
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public static class HttpMessageExtensions
    {
        static readonly HashSet<string> ContentHeaderNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Allow",
                "Content-Disposition",
                "Content-Encoding",
                "Content-Language",
                "Content-Length",
                "Content-Location",
                "Content-MD5",
                "Content-Range",
                "Content-Type",
                "Expires",
                "Last-Modified",
            };

        public static HttpResponseMessage ToResponseMessage(this HttpMessage message) =>
            ToResponseMessage(message, null);

        public static HttpResponseMessage ToResponseMessage(this HttpMessage message, HttpRequestMessage request)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (!message.IsResponse) throw new ArgumentException("HTTP message does not represent a response message.", nameof(message));

            var response = new HttpResponseMessage(message.StatusCode)
            {
                RequestMessage = request,
                Version        = message.HttpVersion,
                ReasonPhrase   = message.ReasonPhrase,
                Content        = new StreamContent(message.ContentStream),
            };

            HttpHeaders contentHeaders = response.Content.Headers;

            var headers =
                from h in message.Headers
                select new
                {
                    Name = h.Key, h.Value,
                    Headers = ContentHeaderNames.Contains(h.Key)
                            ? contentHeaders
                            : response.Headers,
                };

            foreach (var e in headers)
                e.Headers.TryAddWithoutValidation(e.Name, e.Value);

            message.DisownContentStream();

            return response;
        }

        static readonly Dictionary<string, HttpMethod> HttpMethods = new[]
            {
                HttpMethod.Get    ,
                HttpMethod.Post   ,
                HttpMethod.Put    ,
                HttpMethod.Delete ,
                HttpMethod.Options,
                HttpMethod.Head   ,
                HttpMethod.Trace  ,
            }
            .ToDictionary(e => e.Method, e => e, StringComparer.OrdinalIgnoreCase);

        static HttpMethod ParseHttpMethod(string method) =>
            HttpMethods.TryGetValue(method, out var m) ? m : throw new FormatException($"'{method}' is not a valid HTTP method.");

        public static HttpRequestMessage ToRequestMessage(this HttpMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (!message.IsRequest) throw new ArgumentException("HTTP message does not represent a request message.", nameof(message));

            var method = ParseHttpMethod(message.RequestMethod);

            var request = new HttpRequestMessage(method, message.RequestUrl)
            {
                Version = message.HttpVersion,
                Content = new StreamContent(message.ContentStream),
            };

            HttpHeaders contentHeaders = request.Content.Headers;

            var headers =
                from h in message.Headers
                select new
                {
                    Name = h.Key, h.Value,
                    Headers = ContentHeaderNames.Contains(h.Key)
                            ? contentHeaders
                            : request.Headers,
                };

            foreach (var e in headers)
                e.Headers.TryAddWithoutValidation(e.Name, e.Value);

            message.DisownContentStream();

            return request;
        }
    }
}
