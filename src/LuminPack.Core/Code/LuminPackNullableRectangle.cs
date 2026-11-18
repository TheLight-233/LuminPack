#if NET8_0_OR_GREATER
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using LuminPack.Core;
using LuminPack.Utility;

namespace LuminPack.Code;

/// <summary>Transfers the complete checked native or tagged payload of a rectangular nullable array.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminPackNullableRectangle
{
    private const int Tagged = 1;

    /// <summary>Writes a row-major block after its count has been written.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <typeparam name="TLayout">The generated storage layout descriptor.</typeparam>
    /// <param name="writer">The destination writer.</param>
    /// <param name="values">The existing rectangular array storage, disjoint from the writer.</param>
    /// <param name="encoding">The compiled storage mode.</param>
    /// <param name="writeTag">Whether several tagged models are available.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T, TLayout>(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<T?> values, int encoding, bool writeTag)
        where T : unmanaged
        where TLayout : struct, ILuminNullableStorageLayout
    {
        if (values.IsEmpty)
            return;
        ref T? first = ref MemoryMarshal.GetReference(values);
        if (encoding == Tagged)
        {
            int bytes = checked(values.Length * LuminStorageTag<T?, TLayout>.Width);
            writer.EnsureAdditionalCapacity(checked(bytes + (writeTag ? 1 : 0)));
            if (writeTag)
                writer.Write(LuminStorageTag<T?, TLayout>.Tag);
            LuminStorageTag<T?, TLayout>.Pack(ref first, values.Length, ref writer.Cursor);
            writer.Advance(bytes);
            return;
        }
        int nativeBytes = checked(values.Length * Unsafe.SizeOf<T?>());
        writer.EnsureAdditionalCapacity(nativeBytes);
        writer.Write(ref Unsafe.As<T?, byte>(ref first), nativeBytes);
    }

    /// <summary>Validates the complete payload and its Boolean storage before allocating the rectangular result.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <typeparam name="TLayout">The generated storage layout descriptor.</typeparam>
    /// <param name="reader">The checked input at the start of the payload.</param>
    /// <param name="count">The previously checked rectangular element count.</param>
    /// <param name="encoding">The compiled storage mode.</param>
    /// <param name="hasTag">Whether the nonempty payload carries a tag byte.</param>
    /// <param name="tag">The validated tag, or one for an implicit model.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate<T, TLayout>(scoped ref LuminPackReader reader, int count, int encoding, bool hasTag, out byte tag)
        where T : unmanaged
        where TLayout : struct, ILuminNullableStorageLayout
    {
        tag = 1;
        if (count == 0)
            return;
        int stride, presence, boolean = -1;
        if (encoding == Tagged)
        {
            if (hasTag)
                tag = reader.Read<byte>();
            stride = LuminStorageTag<T?, TLayout>.WidthOf(tag);
            presence = default(TLayout).ModelByteOffset(tag, 0);
            if (typeof(T) == typeof(bool))
                boolean = default(TLayout).ModelByteOffset(tag, 1);
        }
        else
        {
            stride = Unsafe.SizeOf<T?>();
            presence = 0;
            if (typeof(T) == typeof(bool))
            {
                T? sample = default;
                ref T payload = ref Unsafe.AsRef(in Nullable.GetValueRefOrDefaultRef(in sample));
                boolean = (int)Unsafe.ByteOffset(ref Unsafe.As<T?, byte>(ref sample), ref Unsafe.As<T, byte>(ref payload));
            }
        }
        int bytes = checked(count * stride);
        reader.EnsureCollectionPayload(reader.Position, bytes);
        ValidateFlags(ref reader.Cursor, count, stride, bytes, presence, boolean);
    }

    /// <summary>Copies a validated payload into the unpublished rectangular result.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <typeparam name="TLayout">The generated storage layout descriptor.</typeparam>
    /// <param name="reader">The checked input after any tag.</param>
    /// <param name="values">The new rectangular array storage.</param>
    /// <param name="encoding">The compiled storage mode.</param>
    /// <param name="tag">The validated or implicit tagged model.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Read<T, TLayout>(scoped ref LuminPackReader reader, scoped Span<T?> values, int encoding, byte tag)
        where T : unmanaged
        where TLayout : struct, ILuminNullableStorageLayout
    {
        if (values.IsEmpty)
            return;
        ref T? first = ref MemoryMarshal.GetReference(values);
        if (encoding == Tagged)
        {
            int bytes = checked(values.Length * LuminStorageTag<T?, TLayout>.WidthOf(tag));
            ref byte source = ref reader.Cursor;
            reader.Advance(bytes);
            LuminStorageTag<T?, TLayout>.Unpack(tag, ref source, values.Length, ref first);
            return;
        }
        reader.Read(ref Unsafe.As<T?, byte>(ref first), checked(values.Length * Unsafe.SizeOf<T?>()));
    }

    /// <summary>Rejects noncanonical presence and Boolean payload bytes in the validated payload window.</summary>
    /// <param name="source">The first byte of the complete payload.</param>
    /// <param name="count">The number of complete elements in the window.</param>
    /// <param name="stride">The native or modeled element width.</param>
    /// <param name="bytes">The validated total byte width.</param>
    /// <param name="presence">The in-element presence byte offset.</param>
    /// <param name="boolean">The in-element Boolean payload byte offset, or minus one.</param>
    [MethodImpl(MethodImplOptions.NoInlining | LuminPackJit.Optimized)]
    private static void ValidateFlags(ref byte source, int count, int stride, int bytes, int presence, int boolean)
    {
        if ((uint)presence >= (uint)stride || boolean >= stride)
            LuminPackExceptionHelper.ThrowInvalidDataException("Invalid nullable storage offsets.");

        int offset = 0;
        if (IntPtr.Size == 8 && BitConverter.IsLittleEndian && stride is 2 or 4 or 8)
        {
            ulong flags = 0xFEUL << (presence * 8);
            if (boolean >= 0)
                flags |= 0xFEUL << (boolean * 8);
            if (stride == 2)
                flags |= flags << 16;
            if (stride != 8)
                flags |= flags << 32;

            if (Avx512F.IsSupported && Vector512.IsHardwareAccelerated)
            {
                var mask = Vector512.Create(flags);
                for (; offset <= bytes - 64; offset += 64)
                    if (!Vector512.EqualsAll(Vector512.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref source, offset))) & mask,
                            Vector512<ulong>.Zero))
                        InvalidBooleanStorage();
            }
            if (Avx2.IsSupported)
            {
                var mask = Vector256.Create(flags);
                for (; offset <= bytes - 32; offset += 32)
                    if (!Vector256.EqualsAll(Vector256.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref source, offset))) & mask,
                            Vector256<ulong>.Zero))
                        InvalidBooleanStorage();
            }
            if (Vector128.IsHardwareAccelerated)
            {
                var mask = Vector128.Create(flags);
                for (; offset <= bytes - 16; offset += 16)
                    if (!Vector128.EqualsAll(Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref source, offset))) & mask,
                            Vector128<ulong>.Zero))
                        InvalidBooleanStorage();
            }
            for (; offset <= bytes - sizeof(ulong); offset += sizeof(ulong))
                if ((Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, offset)) & flags) != 0)
                    InvalidBooleanStorage();
        }

        int remaining = count - offset / stride;
        ref byte current = ref Unsafe.Add(ref source, offset);
        for (; remaining > 0; remaining--)
        {
            if (Unsafe.Add(ref current, presence) > 1 || boolean >= 0 && Unsafe.Add(ref current, boolean) > 1)
                InvalidBooleanStorage();
            current = ref Unsafe.Add(ref current, stride);
        }
    }

    /// <summary>Reports an invalid managed Boolean value in a nullable block.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InvalidBooleanStorage() =>
        LuminPackExceptionHelper.ThrowInvalidDataException("A nullable array contains a noncanonical Boolean storage byte.");
}
#endif
