using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Core;

/// <summary>Retains validated incoming descriptor mappings for one generated receiving model.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LuminPackVersionMapCache
{
    private const int MaximumMaps = 8;
    private readonly LuminPackVersionSlot _slotOf;
    private Entry[]? _entries;
    private byte[]? _modelDescriptors;

    /// <summary>Creates the descriptor cache for one model.</summary>
    /// <param name="slotOf">The generated exact descriptor lookup.</param>
    public LuminPackVersionMapCache(LuminPackVersionSlot slotOf) => _slotOf = slotOf;

    /// <summary>Retains a stable copy of the owner model's immutable table for ordinal lookups.</summary>
    /// <param name="descriptors">The receiving model's descriptor bytes.</param>
    /// <returns>The shared immutable descriptor copy.</returns>
    internal byte[] GetModelDescriptors(ReadOnlySpan<byte> descriptors)
    {
        var copy = Volatile.Read(ref _modelDescriptors);
        if (copy is not null)
            return copy;
        copy = descriptors.ToArray();
        return Interlocked.CompareExchange(ref _modelDescriptors, copy, null) ?? copy;
    }

    /// <summary>Finds or builds the checked map for an incoming schema.</summary>
    /// <param name="schema">The incoming schema identity.</param>
    /// <param name="descriptors">The incoming descriptor bytes.</param>
    /// <returns>The validated cache entry.</returns>
    internal Entry Resolve(ulong schema, ReadOnlySpan<byte> descriptors)
    {
        var entries = Volatile.Read(ref _entries);
        var match = Find(entries, schema, descriptors);
        return match ?? ResolveMissing(entries, schema, descriptors);
    }

    /// <summary>Validates an uncached table and publishes its entry if the cache has space.</summary>
    /// <param name="entries">The previously observed cache entries.</param>
    /// <param name="schema">The incoming schema identity.</param>
    /// <param name="descriptors">The incoming descriptor bytes.</param>
    /// <returns>The validated existing or newly computed entry.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private Entry ResolveMissing(Entry[]? entries, ulong schema, ReadOnlySpan<byte> descriptors)
    {
        if (LuminPackVersionRecord.ComputeSchema(descriptors) != schema)
            LuminPackExceptionHelper.ThrowFormatException("Invalid version-tolerant schema identity.");

        var entry = new Entry(schema, descriptors.ToArray(), BuildMap(descriptors));
        while ((entries?.Length ?? 0) < MaximumMaps)
        {
            int count = entries?.Length ?? 0;
            var next = new Entry[count + 1];
            entries?.CopyTo(next, 0);
            next[count] = entry;
            var observed = Interlocked.CompareExchange(ref _entries, next, entries);
            if (ReferenceEquals(observed, entries))
                break;
            var winner = Find(observed, schema, descriptors);
            if (winner is not null)
                return winner;
            entries = observed;
        }
        return entry;
    }

    /// <summary>Looks up a schema and confirms the complete descriptor table on a hit.</summary>
    /// <param name="entries">The currently published cache entries.</param>
    /// <param name="schema">The incoming schema identity.</param>
    /// <param name="descriptors">The incoming descriptor bytes.</param>
    /// <returns>The exact matching entry, or null.</returns>
    private static Entry? Find(Entry[]? entries, ulong schema, ReadOnlySpan<byte> descriptors)
    {
        if (entries is not null)
            foreach (var entry in entries)
                if (entry.Schema == schema && descriptors.SequenceEqual(entry.Descriptors))
                    return entry;
        return null;
    }

    /// <summary>Validates incoming names and widths and resolves their receiving slots.</summary>
    /// <param name="descriptors">The complete incoming descriptor bytes.</param>
    /// <returns>The map and presence mask for this receiving model.</returns>
    private LuminPackDescriptorMap BuildMap(ReadOnlySpan<byte> descriptors)
    {
        int count = descriptors.Length / sizeof(ulong);
        var widths = new byte[count];
        var slots = new byte[count];
        var present = new VersionPresence();
        // At most 249 keys. Indices are one-based so zero marks a free bucket, even when a key is zero.
        const int Mask = 511;
        Span<int> keys = stackalloc int[Mask + 1];
        keys.Clear();
        for (int index = 0; index < count; index++)
        {
            ulong descriptor = LuminPackReader.Read<ulong>(
                ref MemoryMarshal.GetReference(descriptors.Slice(index * sizeof(ulong), sizeof(ulong))));
            uint nameKey = (uint)descriptor;
            byte width = (byte)(descriptor >> 56);
            if (width is > 250 and < 254)
                LuminPackExceptionHelper.ThrowFormatException("Invalid version-tolerant width code.");

            int bucket = (int)(unchecked(nameKey * 0x9E3779B9u) >> 23);
            for (int previous; (previous = keys[bucket]) != 0; bucket = (bucket + 1) & Mask)
            {
                uint previousKey = BinaryPrimitives.ReadUInt32LittleEndian(descriptors.Slice((previous - 1) * sizeof(ulong)));
                if (nameKey == previousKey)
                    LuminPackExceptionHelper.ThrowFormatException("Duplicate version-tolerant member identity.");
            }
            keys[bucket] = index + 1;

            int slot = _slotOf(nameKey, out ulong expected);
            if (slot >= 0)
            {
                if (descriptor != expected)
                    LuminPackExceptionHelper.ThrowFormatException("The version-tolerant member type or binary profile has changed.");
                if (!present.Add(slot))
                    LuminPackExceptionHelper.ThrowFormatException("Duplicate version-tolerant member identity.");
            }
            slots[index] = (byte)slot;
            widths[index] = width;
        }
        return new LuminPackDescriptorMap(slots, widths, present);
    }

    internal sealed class Entry(ulong schema, byte[] descriptors, LuminPackDescriptorMap map)
    {
        internal readonly ulong Schema = schema;
        internal readonly byte[] Descriptors = descriptors;
        internal readonly LuminPackDescriptorMap Map = map;
    }
}
