using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Code;

public static partial class LuminPackMarshal
{
    /// <summary>Stages a raw-value record once and retains dense key storage when every decoded key matches in wire order.</summary>
    /// <typeparam name="TValue">The caller-certified raw unmanaged value contract.</typeparam>
    /// <typeparam name="TOptions">A supported constant framework string profile.</typeparam>
    /// <param name="reader">The original operation state after its validated dictionary count.</param>
    /// <param name="destination">The existing dictionary selected by the original reader.</param>
    /// <param name="count">The already validated positive pair count.</param>
    /// <returns>True after complete successful publication, including an ordinary staged rebuild on changed keys,
    /// or false before parsing an excluded destination.</returns>
    /// <remarks>This kernel retains the original pair rental and Clear/Add fallback. Stable keys additionally
    /// permit a value-only commit after a complete storage proof. A cold version-policy probe can allocate.
    /// No entry-value reference survives a decoder call.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadStableStringKeys<TValue, TOptions>(ref LuminPackReader reader,
        Dictionary<string, TValue> destination, int count)
        where TValue : unmanaged where TOptions : struct, ILuminPackOptions
    {
#if NET8_0_OR_GREATER
        return Unsafe.SizeOf<IntPtr>() == 8 && !UsesLegacyHashTableFreeEntryEncoding && ConstantStringProfile<TOptions>() &&
            ReadStableStringKeys<TValue, TOptions>(ref reader, destination, count);
#else
        return false;
#endif
    }

    /// <summary>Stages and commits a certified constant-profile raw-value dictionary record.</summary>
    /// <typeparam name="TValue">The caller-certified raw framework scalar.</typeparam>
    /// <typeparam name="TOptions">The original constant compiled string profile.</typeparam>
    /// <param name="reader">The caller state, published only after complete successful cleanup.</param>
    /// <param name="destination">The original existing dictionary.</param>
    /// <param name="count">The positive previously validated pair count.</param>
    /// <returns>Whether the complete staged operation handled the selected destination.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ReadStableStringKeys<TValue, TOptions>(ref LuminPackReader reader,
        Dictionary<string, TValue> destination, int count)
        where TValue : unmanaged where TOptions : struct, ILuminPackOptions
    {
#if NET8_0_OR_GREATER
        if (count <= 0 || destination is null || destination.Count != count ||
            !ReferenceEquals(destination.Comparer, EqualityComparer<string>.Default) ||
            !DictionaryView<string, TValue>.Recognized)
            return false;

        var initialView = GetDictionaryView(destination);
        if (initialView._count != count || initialView._freeCount != 0 || initialView._entries is null ||
            initialView._entries.Length < count)
            return false;

        LuminPackReader probe = default;
        reader.CopyTo(ref probe);
        var items = ArrayPool<KeyValuePair<string, TValue>>.Shared.Rent(count);
        try
        {
            // Rent can report diagnostic events. Traverse the actual post-rental destination exactly once.
            var view = GetDictionaryView(destination);
            var entries = view._entries;
            bool stable = destination.Count == count && view._count == count && view._freeCount == 0 &&
                entries is not null && entries.Length >= count &&
                ReferenceEquals(destination.Comparer, EqualityComparer<string>.Default);
            int version = view._version;
            var previous = destination.GetEnumerator();
            for (int index = 0; index < count; index++)
            {
                string? candidate = previous.MoveNext() ? previous.Current.Key : null;
                string? key = candidate;
                probe.Read<TOptions>(ref key);
                // Identity is accepted only after the original complete profile decoder compared the whole key.
                stable &= candidate is not null && ReferenceEquals(key, candidate);
                TValue value = default;
                // The certified raw read overwrites the complete old scalar; default string lookup has no observers.
                probe.Read(ref value);
                items[index] = new KeyValuePair<string, TValue>(key!, value);
            }

            // The policy probe can allocate on its first use; finish it before the final mutation proof.
            long policy = stable ? StableStringKeyVersion<TValue>.Policy : 0;
            stable &= view._version == version && view._count == count && view._freeCount == 0 &&
                ReferenceEquals(view._entries, entries) &&
                ReferenceEquals(destination.Comparer, EqualityComparer<string>.Default);
            // Capacity-only rehashing can create a chain that the original Clear/Add would randomize.
            // Keep that existing collision fallback instead of retaining such a bucket chain.
            if (stable && count > 101 && !StableStringKeyChains<TValue>(entries!, count))
                stable = false;
            if (stable)
            {
                ref var first = ref GetNotNullArrayReference(entries!);
                for (int index = 0; index < count; index++)
                {
                    ref TValue target = ref Unsafe.Add(ref first, (nint)(uint)index).Value;
                    TValue staged = items[index].Value;
                    CopyBlockUnaligned(ref Unsafe.As<TValue, byte>(ref target),
                        ref Unsafe.As<TValue, byte>(ref staged), (uint)Unsafe.SizeOf<TValue>());
                }
                int clearDelta = (int)(policy >> 32), addDelta = (int)policy;
                view._freeList = -1;
                view._version = unchecked(version + clearDelta + count * addDelta);
            }
            else
            {
                destination.Clear();
                for (int index = 0; index < count; index++)
                    destination.Add(items[index].Key, items[index].Value);
            }
        }
        finally
        {
            ArrayPool<KeyValuePair<string, TValue>>.Shared.Return(items, clearArray: true);
        }
        probe.CopyTo(ref reader);
        return true;
#else
        return false;
#endif
    }

    /// <summary>Declines bucket chains that must revisit the original public Add collision policy.</summary>
    /// <typeparam name="TValue">The certified raw scalar stored beside each immutable key.</typeparam>
    /// <param name="entries">The validated dense entry array.</param>
    /// <param name="count">The validated occupied entry count.</param>
    /// <returns>Whether every chain stays within the existing bucket-rebuild collision bound.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool StableStringKeyChains<TValue>(DictionaryView<string, TValue>.Entry[] entries, int count)
        where TValue : unmanaged
    {
        for (int index = 0; index < count; index++)
        {
            int previous = entries[index].Next;
            int collisions = 0;
            while (previous != -1)
            {
                if ((uint)previous >= (uint)count || ++collisions > 100)
                    return false;
                previous = entries[previous].Next;
            }
        }
        return true;
    }

    /// <summary>Excludes profile property getters that can execute consumer code during the storage proof.</summary>
    /// <typeparam name="TOptions">The caller's original compiled string profile.</typeparam>
    /// <returns>Whether the profile is a known constant framework profile or its fixed wrapper.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ConstantStringProfile<TOptions>() where TOptions : struct, ILuminPackOptions =>
        typeof(TOptions) == typeof(LuminPackUtf16LengthOptions) || typeof(TOptions) == typeof(LuminPackUtf8LengthOptions) ||
        typeof(TOptions) == typeof(LuminPackUtf16TokenOptions) || typeof(TOptions) == typeof(LuminPackUtf8TokenOptions) ||
        typeof(TOptions) == typeof(LuminPackDefaultOptions) ||
        typeof(TOptions) == typeof(LuminPackFixedOptions<LuminPackUtf16LengthOptions>) ||
        typeof(TOptions) == typeof(LuminPackFixedOptions<LuminPackUtf8LengthOptions>) ||
        typeof(TOptions) == typeof(LuminPackFixedOptions<LuminPackUtf16TokenOptions>) ||
        typeof(TOptions) == typeof(LuminPackFixedOptions<LuminPackUtf8TokenOptions>) ||
        typeof(TOptions) == typeof(LuminPackFixedOptions<LuminPackDefaultOptions>);

    /// <summary>Observes the actual public Clear/Add version deltas once, without retaining the probe dictionary.</summary>
    /// <typeparam name="TValue">The certified scalar storage in the probe dictionary.</typeparam>
    private static class StableStringKeyVersion<TValue> where TValue : unmanaged
    {
        /// <summary>The measured increments; no probe dictionary or user data is retained.</summary>
        internal static readonly long Policy = Probe();

        /// <summary>Measures the runtime's actual public dictionary version increments once.</summary>
        /// <returns>The Clear delta in the high word and the Add delta in the low word.</returns>
        private static long Probe()
        {
            var dictionary = new Dictionary<string, TValue>(3);
            var view = GetDictionaryView(dictionary);
            dictionary.Add(string.Empty, default);
            int before = view._version;
            dictionary.Add("x", default);
            int addDelta = unchecked(view._version - before);
            before = view._version;
            dictionary.Clear();
            int clearDelta = unchecked(view._version - before);
            return ((long)clearDelta << 32) | (uint)addDelta;
        }
    }
}
