#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    private const int MaximumSortedListKeyCount = 4096;

    /// <summary>Seeds ordered string keys while preserving the normal sequential read and append operations.</summary>
    /// <typeparam name="TValue">The original raw Int32 or Int64 value.</typeparam>
    /// <typeparam name="TOptions">The selected compiled string profile.</typeparam>
    /// <param name="reader">The original operation state after the validated count.</param>
    /// <param name="destination">The existing sorted list.</param>
    /// <param name="length">The validated incoming count.</param>
    /// <returns>Whether the complete operation used the seeded read path.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryReadSeededSortedListKeys<TValue, TOptions>(
        scoped ref LuminPackReader reader, SortedList<string, TValue> destination, int length)
        where TValue : unmanaged where TOptions : struct, ILuminPackOptions
    {
        if ((typeof(TValue) != typeof(int) && typeof(TValue) != typeof(long)) ||
            length is <= 0 or > MaximumSortedListKeyCount || length != destination.Count ||
            length > (reader.Buffer.Length - reader.Position) / Unsafe.SizeOf<TValue>() ||
            !LuminPackMarshal.ConstantStringProfile<TOptions>() || !CanAppendSortedListOrdered(destination) ||
            !TryGetSortedListStorage(destination, out var keys, out _) || keys is null || keys.Length < length)
            return false;

        var snapshot = SortedListKeySnapshotCache.Items.TryTake();
        if (snapshot is null || snapshot.Length < length)
            snapshot = new string[length];
        try
        {
            Array.Copy(keys, snapshot, length);
            destination.Clear();
            var appendCulture = CultureInfo.CurrentCulture;
            bool canAppendOrdered = CanAppendSortedListOrdered(destination);
            for (int index = 0; index < length; index++)
            {
                string? key = snapshot[index];
                TValue value = default;
                reader.Read<TOptions>(ref key);
                reader.Read(ref Unsafe.As<TValue, byte>(ref value), Unsafe.SizeOf<TValue>());
                if (!canAppendOrdered || !ReferenceEquals(CultureInfo.CurrentCulture, appendCulture) ||
                    !TryAppendSortedListOrdered(destination, key!, value, validatedDestination: true))
                    destination.Add(key!, value);
            }
        }
        finally
        {
            Array.Clear(snapshot, 0, length);
            SortedListKeySnapshotCache.Items.TryStore(snapshot);
        }

        return true;
    }

    /// <summary>Owns at most four exclusively rented, cleared key snapshots.</summary>
    private static class SortedListKeySnapshotCache
    {
        /// <summary>The bounded callback-free key snapshot storage.</summary>
        internal static readonly AtomicSlotPool<string[]> Items = new(4);
    }
}
#endif
