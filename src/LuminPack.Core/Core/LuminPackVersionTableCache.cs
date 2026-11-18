using System.ComponentModel;
using System.Threading;

namespace LuminPack.Core;

/// <summary>Caches two validated width tables for one generated versioned schema.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LuminPackVersionTableCache
{
    internal const int MaximumTableBytes = 4096;
    internal readonly LuminPackVersionField[] Fields;
    private Entry? _first;
    private Entry? _second;

    /// <summary>Captures the receiving schema independently of the caller's array.</summary>
    /// <param name="fields">The receiving fields in declaration order.</param>
    public LuminPackVersionTableCache(LuminPackVersionField[] fields) => Fields = (LuminPackVersionField[])fields.Clone();

    /// <summary>Gets whether a new bounded entry can be admitted.</summary>
    internal bool HasSpace => Volatile.Read(ref _second) is null;

    /// <summary>Looks for an exact validated table at the beginning of the readable input.</summary>
    /// <param name="input">The readable bytes beginning at a width table.</param>
    /// <param name="count">The field count from the current record header.</param>
    /// <returns>The matching entry, or null.</returns>
    internal Entry? Find(ReadOnlySpan<byte> input, int count)
    {
        Entry? entry = Volatile.Read(ref _first);
        if (entry is not null && entry.Matches(input, count))
            return entry;
        entry = Volatile.Read(ref _second);
        return entry is not null && entry.Matches(input, count) ? entry : null;
    }

    /// <summary>Publishes a validated snapshot without replacing entries already in service.</summary>
    /// <param name="entry">The validated immutable table.</param>
    internal void Publish(Entry entry)
    {
        Entry? first = Interlocked.CompareExchange(ref _first, entry, null);
        if (first is not null && !first.Matches(entry.Bytes, entry.Slots.Length))
            Interlocked.CompareExchange(ref _second, entry, null);
    }

    /// <summary>Provides the immutable metadata and mapping for one complete width table.</summary>
    internal sealed class Entry
    {
        internal readonly byte[] Bytes;
        internal readonly int[] Slots;
        internal readonly int[] Widths;
        internal readonly byte[] Present;
        internal readonly ulong Fingerprint;

        /// <summary>Captures a parsed, privately owned width table.</summary>
        /// <param name="bytes">The validated input snapshot.</param>
        /// <param name="slots">The incoming member identities mapped to local slots.</param>
        /// <param name="widths">The incoming member widths.</param>
        /// <param name="present">The local fields supplied by this table.</param>
        internal Entry(byte[] bytes, int[] slots, int[] widths, byte[] present)
        {
            Bytes = bytes;
            Slots = slots;
            Widths = widths;
            Present = present;
            Fingerprint = LuminPackVersionField.Fingerprint(bytes);
        }

        /// <summary>Matches the record's field count and all metadata bytes.</summary>
        /// <param name="input">The complete available input range.</param>
        /// <param name="count">The current record's field count.</param>
        /// <returns>Whether this table is a prefix of the input.</returns>
        internal bool Matches(ReadOnlySpan<byte> input, int count) => count == Slots.Length && input.StartsWith(Bytes);

        /// <summary>Copies the resolved field information into the receiving operation's scratch buffers.</summary>
        /// <param name="slots">The local slot destinations.</param>
        /// <param name="widths">The field width destinations.</param>
        /// <param name="present">The local field presence destinations.</param>
        internal void CopyTo(Span<int> slots, Span<int> widths, Span<byte> present)
        {
            Slots.AsSpan().CopyTo(slots);
            Widths.AsSpan().CopyTo(widths);
            Present.AsSpan().CopyTo(present);
        }
    }
}
