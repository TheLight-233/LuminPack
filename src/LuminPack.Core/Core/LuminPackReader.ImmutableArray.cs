#if NET8_0_OR_GREATER
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Core;

public ref partial struct LuminPackReader
{
    /// <summary>Reads one checked binary record into a new immutable array of raw elements.</summary>
    /// <typeparam name="T">The element's reference-free binary storage type.</typeparam>
    /// <returns>The complete array, with the default value for a null collection marker.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArray<T> ReadRawImmutableArray<T>() where T : unmanaged
    {
        ref byte input = ref Current;
        int available = ValidateCurrentCollectionPayload(ref input, sizeof(int));
        int count = Read<int>(ref input);
        if (count < -1)
            LuminPackExceptionHelper.ThrowInvalidCollectionLength(count);
        if (count <= 0)
        {
            AdvanceValidated(sizeof(int));
            return count == 0 ? ImmutableArray<T>.Empty : default;
        }

        int bytes = checked(count * Unsafe.SizeOf<T>());
        int recordBytes = checked(sizeof(int) + bytes);
        if (bytes > available)
            LuminPackExceptionHelper.ThrowSpanOutOfRange(recordBytes);

        var array = LuminPackMarshal.AllocateUninitializedArray<T>(count);
        ref byte destination = ref Unsafe.As<T, byte>(ref LuminPackMarshal.GetNotNullArrayReference(array));
        LuminPackMarshal.CopyBlockUnaligned(ref destination, ref Unsafe.Add(ref input, sizeof(int)), (uint)bytes, destinationIsNew: true);
        AdvanceValidated(recordBytes);
        return ImmutableCollectionsMarshal.AsImmutableArray(array);
    }
}
#endif
