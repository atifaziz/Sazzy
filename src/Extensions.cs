namespace Sazzy
{
    using System.Collections.Generic;

    static partial class Extensions
    {
        public static void
            Deconstruct<TKey, TValue>(
                this KeyValuePair<TKey, TValue> pair,
                out TKey key, out TValue value) =>
            (key, value) = (pair.Key, pair.Value);
    }
}