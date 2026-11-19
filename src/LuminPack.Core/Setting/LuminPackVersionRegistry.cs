using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Option;

internal readonly struct LuminPackVersionRegistration(ulong schema, LuminPackVersionMapCache owner, byte[] descriptors, LuminPackDescriptorMap? map)
{
    internal readonly ulong Schema = schema;
    internal readonly LuminPackVersionMapCache Owner = owner;
    internal readonly byte[] Descriptors = descriptors;
    internal readonly LuminPackDescriptorMap? Map = map;
}

/// <summary>Retains ordered schema registrations for the current operation and its active scopes.</summary>
internal struct LuminPackVersionRegistry
{
    private const int SmallTable = 8;
    // Match the corresponding reference tables: the writer's half-full power-of-two index and the reader's ordinal array.
    private const int MaximumIndexedCount = 1 << 29;
    private const int MaximumSequentialCount = int.MaxValue / 2;
    private LuminPackVersionRegistration[]? _entries;
    private int[]? _index;
    private int _count;

    internal readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Resolves a checked ordinal to an active schema registration.</summary>
    /// <param name="ordinal">The ordinal recorded in a short header.</param>
    /// <returns>The earlier registration in the active scope.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly LuminPackVersionRegistration Get(uint ordinal)
    {
        if (ordinal >= (uint)_count)
            LuminPackExceptionHelper.ThrowFormatException("Unknown version-tolerant schema ordinal.");
        return _entries![ordinal];
    }

    /// <summary>Finds an active registration by exact schema and table.</summary>
    /// <param name="schema">The schema identity to find.</param>
    /// <param name="owner">The generated descriptor owner.</param>
    /// <param name="descriptors">The complete table bytes.</param>
    /// <returns>The registration ordinal or minus one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly int Find(ulong schema, LuminPackVersionMapCache owner, ReadOnlySpan<byte> descriptors)
    {
        if (_count > SmallTable)
            return FindIndexed(schema, owner, descriptors);
        for (int i = 0; i < _count; i++)
            if (Matches(in _entries![i], schema, owner, descriptors))
                return i;
        return -1;
    }

    /// <summary>Finds an exact schema registration using the open-addressed index.</summary>
    /// <param name="schema">The schema identity to find.</param>
    /// <param name="owner">The generated descriptor owner.</param>
    /// <param name="descriptors">The complete table bytes.</param>
    /// <returns>The registration ordinal or minus one.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private readonly int FindIndexed(ulong schema, LuminPackVersionMapCache owner, ReadOnlySpan<byte> descriptors)
    {
        var index = _index!;
        int mask = index.Length - 1;
        int ordinal;
        for (int slot = Hash(schema, mask); (ordinal = index[slot]) != 0; slot = (slot + 1) & mask)
            if (Matches(in _entries![ordinal - 1], schema, owner, descriptors))
                return ordinal - 1;
        return -1;
    }

    /// <summary>Checks the schema identity and confirms the exact descriptors across model owners.</summary>
    /// <param name="entry">The candidate registry entry.</param>
    /// <param name="schema">The schema identity to find.</param>
    /// <param name="owner">The generated descriptor owner.</param>
    /// <param name="descriptors">The complete table bytes.</param>
    /// <returns>Whether the entry exactly matches the supplied schema.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Matches(in LuminPackVersionRegistration entry, ulong schema, LuminPackVersionMapCache owner,
        ReadOnlySpan<byte> descriptors) =>
        entry.Schema == schema && (ReferenceEquals(entry.Owner, owner) || descriptors.SequenceEqual(entry.Descriptors));

    /// <summary>Appends a complete schema to the active operation and optionally indexes it for writing.</summary>
    /// <param name="entry">The validated schema registration.</param>
    /// <param name="indexBySchema">Whether this is the writer registry, which looks up schemas by identity.</param>
    internal void Add(in LuminPackVersionRegistration entry, bool indexBySchema)
    {
        int ordinal = _count;
        int maximumCount = indexBySchema ? MaximumIndexedCount : MaximumSequentialCount;
        if (ordinal >= maximumCount)
            LuminPackExceptionHelper.ThrowFormatException("The version-tolerant schema registry is full.");
        if (_entries is null || ordinal == _entries.Length)
            Array.Resize(ref _entries, _entries is null ? SmallTable : Math.Min(maximumCount, _entries.Length * 2));

        if (indexBySchema && ordinal >= SmallTable)
        {
            if (_index is null || ordinal >= _index.Length / 2)
            {
                var index = new int[_index is null ? SmallTable * 4 : _index.Length * 2];
                for (int i = 0; i < ordinal; i++)
                    Insert(index, _entries![i].Schema, i);
                _index = index;
            }
            else if (ordinal == SmallTable)
            {
                // A previous scope crossed the small-table boundary. Reindex the retained prefix.
                for (int i = 0; i < SmallTable; i++)
                    Insert(_index, _entries![i].Schema, i);
            }
            Insert(_index, entry.Schema, ordinal);
        }
        _entries![ordinal] = entry;
        _count = ordinal + 1;
    }

    /// <summary>Discards all registrations that followed a previously captured scope boundary.</summary>
    /// <param name="count">The number of earlier entries to retain.</param>
    internal void Truncate(int count)
    {
        if (count == _count)
            return;
        if ((uint)count > (uint)_count)
            LuminPackExceptionHelper.ThrowFormatException("Invalid version-tolerant schema scope.");

        if (_count > SmallTable && _index is not null)
        {
            // Remove in reverse insertion order so no retained probe chain can cross a removed slot.
            int end = count > SmallTable ? count : 0;
            for (int ordinal = _count - 1; ordinal >= end; ordinal--)
                Remove(_index, _entries![ordinal].Schema, ordinal);
        }
        Array.Clear(_entries!, count, _count - count);
        _count = count;
    }

    /// <summary>Selects the first open-addressing probe for a schema.</summary>
    /// <param name="schema">The schema identity.</param>
    /// <param name="mask">The index length minus one.</param>
    /// <returns>The first bucket to inspect.</returns>
    private static int Hash(ulong schema, int mask)
    {
        ulong mixed = unchecked((schema ^ (schema >> 33)) * 0xFF51AFD7ED558CCDul);
        return (int)((mixed ^ (mixed >> 32)) & (uint)mask);
    }

    /// <summary>Indexes a new ordinal after any earlier registrations.</summary>
    /// <param name="index">The open-addressed writer index.</param>
    /// <param name="schema">The schema identity.</param>
    /// <param name="ordinal">The ordered registry position.</param>
    private static void Insert(int[] index, ulong schema, int ordinal)
    {
        int mask = index.Length - 1;
        int slot = Hash(schema, mask);
        while (index[slot] != 0)
            slot = (slot + 1) & mask;
        index[slot] = ordinal + 1;
    }

    /// <summary>Removes the latest registration for one probe sequence.</summary>
    /// <param name="index">The open-addressed writer index.</param>
    /// <param name="schema">The schema identity.</param>
    /// <param name="ordinal">The ordered registry position.</param>
    private static void Remove(int[] index, ulong schema, int ordinal)
    {
        int mask = index.Length - 1;
        int slot = Hash(schema, mask);
        while (index[slot] != ordinal + 1)
            slot = (slot + 1) & mask;
        index[slot] = 0;
    }
}
