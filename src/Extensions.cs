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

    static partial class Extensions
    {
        public static void
            Deconstruct<TKey, TValue>(
                this KeyValuePair<TKey, TValue> pair,
                out TKey key, out TValue value) =>
            (key, value) = (pair.Key, pair.Value);

        public static T Pop<T>(this IList<T> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            var index = list.Count - 1;
            if (index < 0)
                throw new InvalidOperationException();
            var result = list[index];
            list.RemoveAt(index);
            return result;
        }
    }
}
