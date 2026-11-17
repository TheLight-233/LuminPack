using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Core;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Reads a complete raw pair window through the existing fresh or ordered reuse dictionary path.</summary>
    /// <typeparam name="TKey">The raw key storage.</typeparam>
    /// <typeparam name="TValue">The raw value storage.</typeparam>
    /// <param name="reader">The state after the positive collection count.</param>
    /// <param name="value">The existing dictionary or the freshly completed result.</param>
    /// <param name="count">The declared number of pairs.</param>
    /// <returns>Whether a complete window was available and read.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryReadConcurrentRawPairs<TKey, TValue>(scoped ref LuminPackReader reader,
        scoped ref ConcurrentDictionary<TKey, TValue>? value, int count) where TKey : unmanaged where TValue : unmanaged
    {
        if (count < 16)
            return false;

        int keyWidth = Unsafe.SizeOf<TKey>();
        int pairWidth = keyWidth + Unsafe.SizeOf<TValue>();
        ref byte source = ref reader.CurrentReference;
        if (!reader.TryAdvanceFixedPayload(count, pairWidth))
            return false;

        if (value is null)
        {
            var builder = BeginConcurrentDictionary<TKey, TValue>(count);
            do
            {
                TKey key = LuminPackReader.Read<TKey>(ref source);
                TValue item = LuminPackReader.Read<TValue>(ref Unsafe.Add(ref source, keyWidth));
                builder.Add(key, item);
                source = ref Unsafe.Add(ref source, pairWidth);
            } while (--count != 0);
            value = builder.Complete();
            return true;
        }

        var refill = BeginConcurrentDictionaryRefill(value, count, reuseValues: false);
        bool ordered = refill.Ordered;
        do
        {
            if (ordered)
                refill.NextKey();
            TKey key = LuminPackReader.Read<TKey>(ref source);
            TValue previous = default;
            ordered = ordered && refill.Find(key, out previous);
            // The comparer can observe the key before the corresponding value is loaded.
            TValue item = LuminPackReader.Read<TValue>(ref Unsafe.Add(ref source, keyWidth));
            if (ordered)
                refill.Store(key, item, found: true, previous);
            else
                value.TryAdd(key, item);
            source = ref Unsafe.Add(ref source, pairWidth);
        } while (--count != 0);
        refill.Complete();
        return true;
    }

    /// <summary>Writes pairs from the established weakly consistent traversal into bounded raw runs.</summary>
    /// <typeparam name="TKey">The raw key type.</typeparam>
    /// <typeparam name="TValue">The raw value type.</typeparam>
    /// <param name="writer">The output state after the counted header.</param>
    /// <param name="dictionary">The source dictionary.</param>
    /// <returns>The number of entries actually observed.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int WriteConcurrentRawPairs<TKey, TValue>(ref LuminPackWriter writer, ConcurrentDictionary<TKey, TValue> dictionary)
        where TKey : unmanaged where TValue : unmanaged
    {
        int keyWidth = Unsafe.SizeOf<TKey>(), valueWidth = Unsafe.SizeOf<TValue>();
        int width = keyWidth + valueWidth;
        var items = EnumerateConcurrentDictionary(dictionary);
        int count = 0;
        try
        {
            bool more = items.MoveNext();
            while (more)
            {
                int capacity = writer.RecordRoom() / width;
                if (capacity <= 0)
                {
                    var pair = items.Current;
                    writer.CheckBuffer();
                    writer.EnsureAdditionalCapacity(keyWidth);
                    TKey key = pair.Key;
                    writer.Write(in key);
                    writer.CheckBuffer();
                    writer.EnsureAdditionalCapacity(valueWidth);
                    TValue value = pair.Value;
                    writer.Write(in value);
                    count++;
                    more = items.MoveNext();
                    continue;
                }

                ref byte cursor = ref writer.CurrentReference;
                int written = 0;
                do
                {
                    var pair = items.Current;
                    LuminPackWriter.Write(ref cursor, pair.Key);
                    LuminPackWriter.Write(ref Unsafe.Add(ref cursor, keyWidth), pair.Value);
                    cursor = ref Unsafe.Add(ref cursor, width);
                    written++;
                    more = items.MoveNext();
                } while (more && written < capacity);
                writer.CompleteRun(written * width);
                count += written;
            }
        }
        finally
        {
            items.Dispose();
        }
        return count;
    }

    /// <summary>Writes the immutable stack in enumeration order with a local cursor within each proven output window.</summary>
    /// <typeparam name="T">The raw element type.</typeparam>
    /// <param name="writer">The output state after the counted header.</param>
    /// <param name="stack">The exact framework stack.</param>
    /// <param name="checkCapacity">Whether the original element formatter checks the full scalar capacity.</param>
    /// <returns>The number of elements written.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int WriteImmutableRawStack<T>(ref LuminPackWriter writer, ImmutableStack<T> stack, bool checkCapacity) where T : unmanaged
    {
        int width = Unsafe.SizeOf<T>();
        var items = stack.GetEnumerator();
        int count = 0;
        bool more = items.MoveNext();
        while (more)
        {
            int capacity = writer.RecordRoom() / width;
            if (capacity <= 0)
            {
                T value = items.Current;
                writer.CheckBuffer();
                if (checkCapacity)
                    writer.EnsureAdditionalCapacity(width);
                writer.Write(in value);
                count++;
                more = items.MoveNext();
                continue;
            }

            ref byte cursor = ref writer.CurrentReference;
            int written = 0;
            do
            {
                LuminPackWriter.Write(ref cursor, items.Current);
                cursor = ref Unsafe.Add(ref cursor, width);
                written++;
                more = items.MoveNext();
            } while (more && written < capacity);
            writer.CompleteRun(written * width);
            count += written;
        }
        return count;
    }
}
