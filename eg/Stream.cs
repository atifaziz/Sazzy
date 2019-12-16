#region Copyright (c) 2018 Atif Aziz. All rights reserved.
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
    using System.IO;

    interface IStreamable
    {
        Stream Open();
    }

    static class Streamable
    {
        public static IStreamable Create(Func<Stream> opener) =>
            new DelegatingStreamable(opener);

        public static IStreamable ReadFile(string path) =>
            Create(() => File.OpenRead(path));

        sealed class DelegatingStreamable : IStreamable
        {
            readonly Func<Stream> _opener;

            public DelegatingStreamable(Func<Stream> opener) =>
                _opener = opener ?? throw new ArgumentNullException(nameof(opener));

            public Stream Open() => _opener();
        }
    }
}
