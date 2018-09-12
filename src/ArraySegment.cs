namespace Sazzy
{
    using System;

    static class ArraySegment
    {
        public static ArraySegment<T> Create<T>(T[] array) =>
            Create(array, 0, array.Length);

        public static ArraySegment<T> Create<T>(T[] array, int offset, int count) =>
            new ArraySegment<T>(array, offset, count);
    }

    partial class Extensions
    {
        public static ArraySegment<T> WithOffset<T>(this ArraySegment<T> segment, int value) =>
            ArraySegment.Create(segment.Array, value, segment.Count);

        public static ArraySegment<T> WithCount<T>(this ArraySegment<T> segment, int value) =>
            ArraySegment.Create(segment.Array, segment.Offset, value);

        public static ArraySegment<T> Slice<T>(this ArraySegment<T> segment, int index) =>
            segment.Array == null
            ? throw new InvalidOperationException()
            : ((uint) index > (uint) segment.Count
            ? throw new ArgumentOutOfRangeException(nameof(index))
            : new ArraySegment<T>(segment.Array, segment.Offset + index, segment.Count - index));

        public static ArraySegment<T> Slice<T>(this ArraySegment<T> segment, int index, int count) =>
            segment.Array == null
            ? throw new InvalidOperationException()
            : ((uint)index > (uint)segment.Count || (uint)count > (uint)(segment.Count - index)
            ? throw new ArgumentOutOfRangeException(nameof(index))
            : new ArraySegment<T>(segment.Array, segment.Offset + index, count));
    }
}
