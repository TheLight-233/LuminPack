#if NET8_0_OR_GREATER
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Security;

namespace LuminPack.Core;

public ref partial struct LuminPackReader
{
    /// <summary>Reads marker-prefixed nullable integers into an existing element span.</summary>
    /// <param name="values">The destination span whose length is the validated collection count.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ReadFramedNullableInt32s(scoped Span<int?> values)
    {
        const int recordWidth = 1 + sizeof(int);
        ref byte cursor = ref Cursor;
        long remaining = (long)Unsafe.ByteOffset(ref cursor, ref _end);
        ref byte destination = ref Unsafe.As<int?, byte>(ref MemoryMarshal.GetReference(values));
        long offset = (long)Unsafe.ByteOffset(ref cursor, ref destination);
        int index = 0;
        if ((ulong)remaining <= (uint)_buffer.Length &&
            (offset >= remaining || offset <= -(long)values.Length * Unsafe.SizeOf<int?>()))
        {
            while (index < values.Length && remaining >= recordWidth)
            {
                // A full record is readable. Zero encodes present and 255 encodes null.
                int present = unchecked((byte)(cursor + 1));
                if (present > 1)
                    break;
                int payload = Read<int>(ref Unsafe.Add(ref cursor, 1)) & -present;
                ref int? slot = ref values[index++];
                slot = payload;
                Unsafe.As<int?, bool>(ref slot) = present != 0;
                int width = 1 + present * sizeof(int);
                cursor = ref Unsafe.Add(ref cursor, width);
                remaining -= width;
            }
            Cursor = ref cursor;
        }
        for (; index < values.Length; index++)
            values[index] = LuminPackInputPolicy.ReadNullMarker(ref this, 0) ? null : Read<int>();
    }
}
#endif
