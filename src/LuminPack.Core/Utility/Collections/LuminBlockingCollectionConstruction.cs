#if NET8_0_OR_GREATER
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Copies the populated prefix of an array into the default queue of a new unbounded collection.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The rented array holding the decoded elements.</param>
    /// <param name="count">The number of decoded elements.</param>
    /// <returns>The collection containing the decoded elements in input order.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlockingCollection<T> CreateBlockingCollection<T>(T[] items, int count)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)items.Length, nameof(count));
        IEnumerable<T> source = count == items.Length ? items : count == 0 ? Array.Empty<T>() : new QueueCopySource<T>(items, count);
        return new BlockingCollection<T>(new ConcurrentQueue<T>(source));
    }

    /// <summary>Provides the prefix count and its first enumerator to the queue's synchronous copying constructor.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The array borrowed for the duration of the constructor.</param>
    /// <param name="count">The length of the populated prefix.</param>
    private sealed class QueueCopySource<T>(T[] items, int count) : ICollection<T>, IReadOnlyCollection<T>, IEnumerator<T>
    {
        private int _index = -2;

        /// <inheritdoc/>
        public int Count => count;

        /// <inheritdoc/>
        public bool IsReadOnly => true;

        /// <inheritdoc/>
        public T Current => (uint)_index < (uint)count ? items[_index] : throw new InvalidOperationException();

        /// <inheritdoc/>
        object? IEnumerator.Current => Current;

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            if (_index != -2)
                return ((IEnumerable<T>)new ArraySegment<T>(items, 0, count)).GetEnumerator();
            _index = -1;
            return this;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (_index < count)
                _index++;
            return _index < count;
        }

        /// <inheritdoc/>
        public void Reset() => _index = -1;

        /// <inheritdoc/>
        public void Dispose() { }

        /// <inheritdoc/>
        public void CopyTo(T[] destination, int index) => Array.Copy(items, 0, destination, index, count);

        /// <inheritdoc/>
        public bool Contains(T item) => Array.IndexOf(items, item, 0, count) >= 0;

        /// <inheritdoc/>
        public void Add(T item) => throw new NotSupportedException();

        /// <inheritdoc/>
        public void Clear() => throw new NotSupportedException();

        /// <inheritdoc/>
        public bool Remove(T item) => throw new NotSupportedException();
    }
}
#endif
