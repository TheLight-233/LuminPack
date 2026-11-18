#if NET8_0_OR_GREATER
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
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
        ref byte end = ref _end;
        long remaining = (long)Unsafe.ByteOffset(ref cursor, ref end);
        ref int? first = ref MemoryMarshal.GetReference(values);
        long offset = (long)Unsafe.ByteOffset(ref cursor, ref Unsafe.As<int?, byte>(ref first));
        int index = 0;
        if ((ulong)remaining <= (uint)_buffer.Length &&
            (offset >= remaining || offset <= -(long)values.Length * Unsafe.SizeOf<int?>()))
        {
            while (index < values.Length && (long)Unsafe.ByteOffset(ref cursor, ref end) >= recordWidth)
            {
                byte marker = cursor;
                ref int? slot = ref Unsafe.Add(ref first, (nint)(uint)index);
                if (marker == 0)
                {
                    slot = Read<int>(ref Unsafe.Add(ref cursor, 1));
                    cursor = ref Unsafe.Add(ref cursor, recordWidth);
                }
                else if (marker == LuminPackCode.NullObject)
                {
                    slot = null;
                    cursor = ref Unsafe.Add(ref cursor, 1);
                }
                else
                    break;
                index++;
            }
            Cursor = ref cursor;
        }
        for (; index < values.Length; index++)
            values[index] = LuminPackInputPolicy.ReadNullMarker(ref this, 0) ? null : Read<int>();
    }
}
#endif
