using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Core;

public ref partial struct LuminPackWriter
{
    /// <summary>Writes a sequence of marker-prefixed nullable records in one bounded output window.</summary>
    /// <typeparam name="T">The native payload type stored after each non-null marker.</typeparam>
    /// <param name="values">The existing rectangular array's row-major elements.</param>
    /// <returns>Whether the entire sequence could be written without growth or fixed-output overreservation.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWriteFramedNullableSpan<T>(scoped ReadOnlySpan<T?> values) where T : unmanaged
    {
        if (values.IsEmpty)
            return true;
        if (RecordRoom() < (long)values.Length * (1L + Unsafe.SizeOf<T>()))
            return false;
        ref byte cursor = ref Current;
        foreach (ref readonly T? value in values)
            cursor = ref WriteReservedNullable(ref cursor, value);
        CommitRecords(ref cursor);
        return true;
    }
}
