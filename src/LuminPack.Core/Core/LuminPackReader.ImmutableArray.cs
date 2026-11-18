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
    public ImmutableArray<T> ReadRawImmutableArray<T>() where T : unmanaged => ReadRawImmutableArray(default(ImmutableArray<T>));

    /// <summary>Reads one checked binary record, retaining an existing immutable array with identical storage.</summary>
    /// <typeparam name="T">The element's reference-free binary storage type.</typeparam>
    /// <param name="current">The existing immutable array, or the default value.</param>
    /// <returns>The complete array, with the default value for a null collection marker.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArray<T> ReadRawImmutableArray<T>(ImmutableArray<T> current) where T : unmanaged
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

        ref byte payload = ref Unsafe.Add(ref input, sizeof(int));
        if (!current.IsDefault && current.Length == count &&
            MemoryMarshal.CreateReadOnlySpan(ref payload, bytes).SequenceEqual(MemoryMarshal.AsBytes(current.AsSpan())))
        {
            AdvanceValidated(recordBytes);
            return current;
        }

        var array = LuminPackMarshal.AllocateUninitializedArray<T>(count);
        ref byte destination = ref Unsafe.As<T, byte>(ref LuminPackMarshal.GetNotNullArrayReference(array));
        LuminPackMarshal.CopyBlockUnaligned(ref destination, ref payload, (uint)bytes, destinationIsNew: true);
        AdvanceValidated(recordBytes);
        return ImmutableCollectionsMarshal.AsImmutableArray(array);
    }
}
#endif
