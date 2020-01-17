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

        public static HttpResponseMessage ToResponseMessage(this HttpResponse response) =>
            ToResponseMessage(response, null);

        public static HttpResponseMessage ToResponseMessage(this HttpResponse response, HttpRequestMessage request)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            var rsp = new HttpResponseMessage(response.StatusCode)
            {
                RequestMessage = request,
                Version        = response.HttpVersion,
                ReasonPhrase   = response.ReasonPhrase,
                Content        = new StreamContent(response.ContentStream),
            };

            HttpHeaders contentHeaders = rsp.Content.Headers;

            var headers =
                from h in response.Headers
                select new
                {
                    Name = h.Key, h.Value,
                    Headers = ContentHeaderNames.Contains(h.Key)
                            ? contentHeaders
                            : rsp.Headers,
                };

            foreach (var e in headers)
                e.Headers.TryAddWithoutValidation(e.Name, e.Value);

            response.DisownContentStream();

            return rsp;
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

        public static HttpRequestMessage ToRequestMessage(this HttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var method = ParseHttpMethod(request.Method);

            var req = new HttpRequestMessage(method, request.Url)
            {
                Version = request.HttpVersion,
                Content = new StreamContent(request.ContentStream),
            };

            HttpHeaders contentHeaders = req.Content.Headers;

            var headers =
                from h in request.Headers
                select new
                {
                    Name = h.Key, h.Value,
                    Headers = ContentHeaderNames.Contains(h.Key)
                            ? contentHeaders
                            : req.Headers,
                };

            foreach (var e in headers)
                e.Headers.TryAddWithoutValidation(e.Name, e.Value);

            request.DisownContentStream();

            return req;
        }
    }
}
