using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        int size = Unsafe.SizeOf<T>();
        long width = (long)values.Length * (1L + size);
        if (RecordRoom() < width)
            return false;
        ref byte cursor = ref Current;
        int index = 0;
        if (values.Length > size)
        {
            ref byte first = ref Unsafe.As<T?, byte>(ref MemoryMarshal.GetReference(values));
            long offset = (long)Unsafe.ByteOffset(ref cursor, ref first);
            long sourceBytes = (long)values.Length * Unsafe.SizeOf<T?>();
            if (offset >= width || offset <= -sourceBytes)
            {
                // A speculative payload never passes the minimum width of the remaining records.
                int stop = values.Length - size;
                do
                {
                    T? value = values[index];
                    int present = value.HasValue ? 1 : 0;
                    cursor = unchecked((byte)(present - 1));
                    T payload = value.GetValueOrDefault();
                    WriteReserved(ref Unsafe.Add(ref cursor, 1), in payload);
                    cursor = ref Unsafe.Add(ref cursor, 1 + present * size);
                }
                while (++index < stop);
            }
        }
        for (; index < values.Length; index++)
            cursor = ref WriteReservedNullable(ref cursor, values[index]);
        CommitRecords(ref cursor);
        return true;
    }
}
