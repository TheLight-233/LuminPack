using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

/// <summary>
/// Maps the exact runtime types of a Union root's fixed generated cases to dense case indexes for collection
/// partition dispatch. Built once per root by generated code; it keeps raw type identities and no <see cref="Type"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LuminUnionPartitionMap
{
    private const byte Unknown = 0xFF;
    private readonly nint[] _keys;
    private readonly byte[] _cases;
    private readonly ulong _multiplier;
    private readonly int _shift;

    /// <summary>The complete record width (narrow tag plus fixed payload) of each case index.</summary>
    public readonly int[] Widths;

    /// <summary>The case index of each narrow tag byte, or 0xFF for a tag without a fixed generated case.</summary>
    public readonly byte[] TagCases;

    /// <summary>Builds a collision-free multiplicative hash over the cases' type identities.</summary>
    /// <param name="cases">The exact case types in case-index order.</param>
    /// <param name="tags">The narrow wire tag of each case.</param>
    /// <param name="widths">The complete record width of each case.</param>
    public LuminUnionPartitionMap(Type[] cases, byte[] tags, int[] widths)
    {
        if (cases.Length == 0 || cases.Length >= Unknown || tags.Length != cases.Length || widths.Length != cases.Length)
            LuminPackExceptionHelper.ThrowArgumentException("A Union partition map needs one tag and width per case.");
        Widths = widths;
        TagCases = new byte[256];
        TagCases.AsSpan().Fill(Unknown);
        var identities = new nint[cases.Length];
        for (int index = 0; index < cases.Length; index++)
        {
            identities[index] = LuminPackMarshal.GetMethodTable(cases[index]);
            TagCases[tags[index]] = (byte)index;
        }
        // A fixed seed keeps the selected multiplier reproducible for the same type identities.
        ulong state = 0x9E3779B97F4A7C15UL;
        for (int bits = 2; bits <= 16; bits++)
        {
            if ((1 << bits) < cases.Length * 2) continue;
            for (int attempt = 0; attempt < 512; attempt++)
            {
                ulong multiplier = NextOdd(ref state);
                var keys = new nint[1 << bits];
                var indexes = new byte[1 << bits];
                bool collision = false;
                for (int index = 0; index < identities.Length && !collision; index++)
                {
                    int slot = (int)(((ulong)identities[index] * multiplier) >> (64 - bits));
                    if (keys[slot] != 0) collision = true;
                    keys[slot] = identities[index];
                    indexes[slot] = (byte)index;
                }
                if (collision) continue;
                _keys = keys;
                _cases = indexes;
                _multiplier = multiplier;
                _shift = 64 - bits;
                return;
            }
        }
        _keys = Array.Empty<nint>();
        _cases = Array.Empty<byte>();
        LuminPackExceptionHelper.ThrowInvalidOperationException("No collision-free Union partition hash was found.");
    }

    /// <summary>Returns the case index of a value's exact runtime type without a data-dependent branch.</summary>
    /// <param name="value">A non-null Union value.</param>
    /// <returns>The case index, or -1 for a type without a fixed generated case.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Classify(object value)
    {
        nint identity = LuminPackMarshal.GetMethodTable(value);
        int slot = (int)(((ulong)identity * _multiplier) >> _shift);
        return _keys[slot] == identity ? _cases[slot] : -1;
    }

    /// <summary>Advances a SplitMix64 sequence and returns an odd multiplier.</summary>
    /// <param name="state">The sequence state.</param>
    /// <returns>The next odd 64-bit value.</returns>
    private static ulong NextOdd(ref ulong state)
    {
        ulong value = state += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return (value ^ (value >> 31)) | 1UL;
    }
}

/// <summary>
/// Kernels of adaptive type-partitioned Union collection dispatch. A chunk whose case sequence is unpredictable
/// is classified without data-dependent branches, grouped by case and then written or read one case at a time at
/// precomputed offsets; predictable chunks keep the ordinary per-element virtual slot or tag switch.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminUnionPartition
{
    /// <summary>The maximum number of elements partitioned together.</summary>
    public const int ChunkLength = 256;

    /// <summary>The minimum number of remaining elements that are probed for partitioning.</summary>
    public const int MinimumLength = 32;

    /// <summary>The number of leading chunk elements whose case transitions are probed.</summary>
    private const int ProbeLength = 32;

    /// <summary>The number of following chunks that reuse a predictable verdict without probing.</summary>
    private const byte SkippedChunks = 7;

    /// <summary>Estimates whether per-element dispatch over a value chunk will be predicted well.</summary>
    /// <typeparam name="T">The Union root.</typeparam>
    /// <param name="map">The root's case map.</param>
    /// <param name="chunk">The chunk to classify.</param>
    /// <param name="successors">The previous successor of each case (0xFF when unknown) followed by the number of chunks that
    /// still reuse a predictable verdict; kept across chunks.</param>
    /// <returns>True when the ordinary per-element path should be used.</returns>
    public static bool IsPredictable<T>(LuminUnionPartitionMap map, ReadOnlySpan<T> chunk, Span<byte> successors) where T : class
    {
        if (chunk.Length < ProbeLength || Skip(successors)) return true;
        int hits = 0, known = 0, previous = -1;
        for (int index = 0; index < ProbeLength; index++)
        {
            T? item = chunk[index];
            if (item is null) return true;
            int current = map.Classify(item);
            if (current < 0) return true;
            Count(successors, previous, current, ref hits, ref known);
            previous = current;
        }
        return Decide(successors, hits, known);
    }

    /// <summary>Estimates whether per-element dispatch over encoded records will be predicted well.</summary>
    /// <param name="map">The root's case map.</param>
    /// <param name="input">The unread input window.</param>
    /// <param name="count">The number of records in the chunk.</param>
    /// <param name="successors">The previous successor of each case (0xFF when unknown) followed by the number of chunks that
    /// still reuse a predictable verdict; kept across chunks.</param>
    /// <returns>True when the ordinary per-element path should be used.</returns>
    public static bool IsPredictable(LuminUnionPartitionMap map, ReadOnlySpan<byte> input, int count, Span<byte> successors)
    {
        if (count < ProbeLength || Skip(successors)) return true;
        int hits = 0, known = 0, previous = -1, position = 0;
        for (int index = 0; index < ProbeLength; index++)
        {
            if ((uint)position >= (uint)input.Length) return true;
            int current = map.TagCases[input[position]];
            if (current == 0xFF) return true;
            position += map.Widths[current];
            Count(successors, previous, current, ref hits, ref known);
            previous = current;
        }
        return Decide(successors, hits, known);
    }

    /// <summary>Classifies a value chunk and assigns each record its absolute output offset.</summary>
    /// <typeparam name="T">The Union root.</typeparam>
    /// <param name="map">The root's case map.</param>
    /// <param name="chunk">The chunk to classify.</param>
    /// <param name="start">The absolute position of the first record.</param>
    /// <param name="cases">Receives each element's case index.</param>
    /// <param name="offsets">Receives each record's absolute offset.</param>
    /// <param name="groups">Receives the number of elements of each case.</param>
    /// <param name="end">Receives the absolute position after the last record.</param>
    /// <returns>False for a null element or a case without a fixed generated record.</returns>
    public static bool TryLayout<T>(LuminUnionPartitionMap map, ReadOnlySpan<T> chunk, int start, Span<byte> cases, Span<int> offsets,
        Span<int> groups, out int end) where T : class
    {
        groups.Clear();
        int[] widths = map.Widths;
        long position = start;
        for (int index = 0; index < chunk.Length; index++)
        {
            T? item = chunk[index];
            int current = item is null ? -1 : map.Classify(item);
            if (current < 0) { end = 0; return false; }
            cases[index] = (byte)current;
            offsets[index] = (int)position;
            position += widths[current];
            groups[current]++;
        }
        if (position > int.MaxValue) { end = 0; return false; }
        end = (int)position;
        return true;
    }

    /// <summary>Validates a chunk of encoded fixed records for fresh targets and records each record's offset.</summary>
    /// <typeparam name="T">The Union root.</typeparam>
    /// <param name="map">The root's case map.</param>
    /// <param name="input">The unread input window.</param>
    /// <param name="targets">The destination slots, which must all be empty.</param>
    /// <param name="cases">Receives each record's case index.</param>
    /// <param name="offsets">Receives each record's offset relative to <paramref name="input"/>.</param>
    /// <param name="groups">Receives the number of records of each case.</param>
    /// <param name="end">Receives the offset after the last record.</param>
    /// <returns>False for a reused target, an unknown, null or wide tag, or a truncated record.</returns>
    public static bool TryLayout<T>(LuminUnionPartitionMap map, ReadOnlySpan<byte> input, ReadOnlySpan<T> targets, Span<byte> cases,
        Span<int> offsets, Span<int> groups, out int end) where T : class
    {
        groups.Clear();
        byte[] tagCases = map.TagCases;
        int[] widths = map.Widths;
        int position = 0;
        for (int index = 0; index < targets.Length; index++)
        {
            if ((uint)position >= (uint)input.Length || targets[index] is not null) { end = 0; return false; }
            int current = tagCases[input[position]];
            if (current == 0xFF) { end = 0; return false; }
            cases[index] = (byte)current;
            offsets[index] = position;
            position += widths[current];
            groups[current]++;
        }
        if ((uint)position > (uint)input.Length) { end = 0; return false; }
        end = position;
        return true;
    }

    /// <summary>Orders element indexes by case with a counting sort.</summary>
    /// <param name="cases">Each element's case index.</param>
    /// <param name="groups">The element count of each case on entry; the exclusive end of each case group on return.</param>
    /// <param name="order">Receives element indexes grouped by case, in their original relative order.</param>
    public static void Order(ReadOnlySpan<byte> cases, Span<int> groups, Span<ushort> order)
    {
        int total = 0;
        for (int index = 0; index < groups.Length; index++)
        {
            int count = groups[index];
            groups[index] = total;
            total += count;
        }
        for (int index = 0; index < cases.Length; index++)
        {
            ref int slot = ref groups[cases[index]];
            order[slot] = (ushort)index;
            slot++;
        }
    }

    /// <summary>Counts whether a transition repeats its case's previous successor, without a data-dependent branch.</summary>
    /// <param name="successors">The successor table.</param>
    /// <param name="previous">The previous case, or -1 at the first element.</param>
    /// <param name="current">The current case.</param>
    /// <param name="hits">Transitions equal to the remembered successor.</param>
    /// <param name="known">Transitions whose predecessor had a remembered successor.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Count(Span<byte> successors, int previous, int current, ref int hits, ref int known)
    {
        if (previous < 0) return;
        ref byte last = ref successors[previous];
        hits += Bit(last == current);
        known += Bit(last != 0xFF);
        last = (byte)current;
    }

    /// <summary>Consumes one chunk of a previous predictable verdict.</summary>
    /// <param name="successors">The successor table whose last entry counts the remaining reused verdicts.</param>
    /// <returns>Whether the chunk reuses the verdict without probing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Skip(Span<byte> successors)
    {
        ref byte remaining = ref successors[successors.Length - 1];
        if (remaining == 0) return false;
        remaining--;
        return true;
    }

    /// <summary>Keeps per-element dispatch when at least two thirds of known transitions repeat, and reuses that verdict.</summary>
    /// <param name="successors">The successor table whose last entry counts the remaining reused verdicts.</param>
    /// <param name="hits">Repeated transitions.</param>
    /// <param name="known">Transitions with a remembered successor.</param>
    /// <returns>Whether the chunk is predictable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Decide(Span<byte> successors, int hits, int known)
    {
        bool predictable = known >= 8 && hits * 3 >= known * 2;
        if (predictable) successors[successors.Length - 1] = SkippedChunks;
        return predictable;
    }

    /// <summary>Converts a comparison to 0 or 1 without a branch.</summary>
    /// <param name="value">The comparison result.</param>
    /// <returns>1 when true, otherwise 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Bit(bool value) => Unsafe.As<bool, byte>(ref value);
}
