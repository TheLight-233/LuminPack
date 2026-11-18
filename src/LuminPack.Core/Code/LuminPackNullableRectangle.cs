#if NET8_0_OR_GREATER
using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using LuminPack.Core;
using LuminPack.Utility;

namespace LuminPack.Code;

/// <summary>Transfers checked native or tagged nullable storage in generated collections.</summary>
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

    /// <summary>Writes a counted contiguous nullable collection, including an aliased retained destination.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <typeparam name="TLayout">The generated storage layout descriptor.</typeparam>
    /// <param name="writer">The destination writer.</param>
    /// <param name="values">The source elements.</param>
    /// <param name="encoding">The compiled storage mode.</param>
    /// <param name="writeTag">Whether several tagged models are available.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteCollection<T, TLayout>(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<T?> values, int encoding, bool writeTag)
        where T : unmanaged
        where TLayout : struct, ILuminNullableStorageLayout
    {
        int count = values.Length;
        if (count == 0)
        {
            writer.EnsureAdditionalCapacity(sizeof(int));
            writer.WriteCollectionHeader(0);
            return;
        }
        int stride = encoding == Tagged ? LuminStorageTag<T?, TLayout>.Width : Unsafe.SizeOf<T?>();
        int bytes = checked(count * stride);
        int size = checked(sizeof(int) + (writeTag ? 1 : 0) + bytes);
        ref T? first = ref MemoryMarshal.GetReference(values);
        if (MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T?, byte>(ref first), checked(count * Unsafe.SizeOf<T?>()))
                .Overlaps(writer.Buffer))
        {
            WriteAliased<T, TLayout>(ref writer, values, encoding, writeTag);
            return;
        }
        writer.EnsureAdditionalCapacity(size);
        writer.WriteCollectionHeader(count);
        if (encoding == Tagged)
        {
            if (writeTag)
                writer.Write(LuminStorageTag<T?, TLayout>.Tag);
            LuminStorageTag<T?, TLayout>.Pack(ref first, count, ref writer.Cursor);
            writer.Advance(bytes);
        }
        else
            writer.Write(ref Unsafe.As<T?, byte>(ref first), bytes);
    }

    /// <summary>Captures aliased source storage before writing any header or growing the destination.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <typeparam name="TLayout">The generated storage layout descriptor.</typeparam>
    /// <param name="writer">The destination writer.</param>
    /// <param name="values">The source elements overlapping the retained destination.</param>
    /// <param name="encoding">The compiled storage mode.</param>
    /// <param name="writeTag">Whether several tagged models are available.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteAliased<T, TLayout>(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<T?> values, int encoding, bool writeTag)
        where T : unmanaged
        where TLayout : struct, ILuminNullableStorageLayout
    {
        // Preserve padding in Native storage, including padding in null records.
        int bytes = checked(values.Length * Unsafe.SizeOf<T?>());
        byte[] scratch = ArrayPool<byte>.Shared.Rent(bytes);
        try
        {
            ref byte copy = ref MemoryMarshal.GetArrayDataReference(scratch);
            ref byte source = ref Unsafe.As<T?, byte>(ref MemoryMarshal.GetReference(values));
            LuminPackMarshal.CopyBlockUnaligned(ref copy, ref source, (uint)bytes);
            WriteCollection<T, TLayout>(ref writer,
                MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, T?>(ref copy), values.Length), encoding, writeTag);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
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
        if (encoding != Tagged)
        {
            ValidateNative<T>(ref reader, count);
            return;
        }
        if (hasTag)
            tag = reader.Read<byte>();
        int stride = LuminStorageTag<T?, TLayout>.WidthOf(tag);
        int presence = default(TLayout).ModelByteOffset(tag, 0);
        int boolean = typeof(T) == typeof(bool) ? default(TLayout).ModelByteOffset(tag, 1) : -1;
        int bytes = checked(count * stride);
        reader.EnsureCollectionPayload(reader.Position, bytes);
        ValidateFlags(ref reader.Cursor, count, stride, bytes, presence, boolean);
    }

    /// <summary>Checks native nullable records without advancing past them.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <param name="reader">The input at the first native record.</param>
    /// <param name="count">The previously checked element count.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateNative<T>(scoped ref LuminPackReader reader, int count) where T : unmanaged
    {
        if (count == 0)
            return;
        int stride = Unsafe.SizeOf<T?>();
        int bytes = checked(count * stride);
        reader.EnsureCollectionPayload(reader.Position, bytes);
        ValidateFlags(ref reader.Cursor, count, stride, bytes, 0, NativeBooleanOffset<T>());
    }

    /// <summary>Reads a complete checked native nullable record in a per-element collection loop.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <param name="reader">The input at the native record.</param>
    /// <param name="value">The element to publish after validation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadNative<T>(scoped ref LuminPackReader reader, scoped ref T? value) where T : unmanaged
    {
        int stride = Unsafe.SizeOf<T?>();
        ref byte source = ref reader.Cursor;
        reader.EnsureCollectionPayload(reader.Position, stride);
        if (source > 1 || typeof(T) == typeof(bool) && Unsafe.Add(ref source, NativeBooleanOffset<T>()) > 1)
            InvalidBooleanStorage();
        reader.Read(ref Unsafe.As<T?, byte>(ref value), stride);
    }

    /// <summary>Gets the actual native Boolean payload offset, when the underlying type is Boolean.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <returns>The actual offset, or minus one when no Boolean payload needs validation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NativeBooleanOffset<T>() where T : unmanaged
    {
        if (typeof(T) != typeof(bool))
            return -1;
        T? sample = default;
        ref T payload = ref Unsafe.AsRef(in Nullable.GetValueRefOrDefaultRef(in sample));
        return (int)Unsafe.ByteOffset(ref Unsafe.As<T?, byte>(ref sample), ref Unsafe.As<T, byte>(ref payload));
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

    /// <summary>Transfers a validated collection block into writable storage that may alias the input.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <typeparam name="TLayout">The generated storage layout descriptor.</typeparam>
    /// <param name="reader">The input after validating the block and consuming any tag.</param>
    /// <param name="values">The destination elements.</param>
    /// <param name="encoding">The compiled storage mode.</param>
    /// <param name="tag">The validated or implicit tagged model.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadCollection<T, TLayout>(scoped ref LuminPackReader reader, scoped Span<T?> values, int encoding, byte tag)
        where T : unmanaged
        where TLayout : struct, ILuminNullableStorageLayout
    {
        if (values.IsEmpty)
            return;
        int width = encoding == Tagged ? LuminStorageTag<T?, TLayout>.WidthOf(tag) : Unsafe.SizeOf<T?>();
        ref byte target = ref Unsafe.As<T?, byte>(ref MemoryMarshal.GetReference(values));
        var destination = MemoryMarshal.CreateSpan(ref target, checked(values.Length * Unsafe.SizeOf<T?>()));
        var source = MemoryMarshal.CreateReadOnlySpan(ref reader.Cursor, checked(values.Length * width));
        if (!source.Overlaps(destination))
        {
            Read<T, TLayout>(ref reader, values, encoding, tag);
            return;
        }
        reader.Advance(source.Length);
        if (encoding == Tagged)
            ReadAliased<T, TLayout>(source, values, tag);
        else
            source.CopyTo(destination);
    }

    /// <summary>Stages an overlapping tagged payload so layout conversion has disjoint ranges.</summary>
    /// <typeparam name="T">The reference-free underlying nullable type.</typeparam>
    /// <typeparam name="TLayout">The generated storage layout descriptor.</typeparam>
    /// <param name="source">The validated wire payload.</param>
    /// <param name="values">The existing destination elements.</param>
    /// <param name="tag">The validated or implicit tagged model.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReadAliased<T, TLayout>(scoped ReadOnlySpan<byte> source, scoped Span<T?> values, byte tag)
        where T : unmanaged
        where TLayout : struct, ILuminNullableStorageLayout
    {
        byte[] scratch = ArrayPool<byte>.Shared.Rent(source.Length);
        try
        {
            source.CopyTo(scratch);
            LuminStorageTag<T?, TLayout>.Unpack(tag, ref MemoryMarshal.GetArrayDataReference(scratch), values.Length,
                ref MemoryMarshal.GetReference(values));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
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

        if (count == 1)
        {
            if (Unsafe.Add(ref source, presence) > 1 || boolean >= 0 && Unsafe.Add(ref source, boolean) > 1)
                goto InvalidStorage;
            return;
        }

        int offset = 0;
        if (IntPtr.Size == 8 && BitConverter.IsLittleEndian && stride is 2 or 4 or 8 or 16)
        {
            ulong low = presence < 8 ? 0xFEUL << (presence * 8) : 0;
            ulong high = presence >= 8 ? 0xFEUL << ((presence - 8) * 8) : 0;
            if (boolean >= 8)
                high |= 0xFEUL << ((boolean - 8) * 8);
            else if (boolean >= 0)
                low |= 0xFEUL << (boolean * 8);
            if (stride != 16)
            {
                if (stride == 2)
                    low |= low << 16;
                if (stride != 8)
                    low |= low << 32;
                high = low;
            }

            // Each vector contains complete periods. Padding and arbitrary payload bytes are masked out.
            var period = Vector128.Create(low, high);

            if (Avx512F.IsSupported && Vector512.IsHardwareAccelerated)
            {
                var mask = Vector512.Create(Vector256.Create(period, period), Vector256.Create(period, period));
                for (; offset <= bytes - 256; offset += 256)
                {
                    ref byte start = ref Unsafe.Add(ref source, offset);
                    var group = Vector512.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref start)) |
                        Vector512.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref start, 64))) |
                        Vector512.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref start, 128))) |
                        Vector512.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref start, 192)));
                    if (!Vector512.EqualsAll(group & mask, Vector512<ulong>.Zero))
                        goto InvalidStorage;
                }
                for (; offset <= bytes - 64; offset += 64)
                    if (!Vector512.EqualsAll(Vector512.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref source, offset))) & mask,
                            Vector512<ulong>.Zero))
                        goto InvalidStorage;
            }
            if (Avx2.IsSupported)
            {
                var mask = Vector256.Create(period, period);
                for (; offset <= bytes - 128; offset += 128)
                {
                    ref byte start = ref Unsafe.Add(ref source, offset);
                    var group = Vector256.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref start)) |
                        Vector256.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref start, 32))) |
                        Vector256.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref start, 64))) |
                        Vector256.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref start, 96)));
                    if (!Vector256.EqualsAll(group & mask, Vector256<ulong>.Zero))
                        goto InvalidStorage;
                }
                for (; offset <= bytes - 32; offset += 32)
                    if (!Vector256.EqualsAll(Vector256.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref source, offset))) & mask,
                            Vector256<ulong>.Zero))
                        goto InvalidStorage;
            }
            if (Vector128.IsHardwareAccelerated)
            {
                var mask = period;
                for (; offset <= bytes - 64; offset += 64)
                {
                    ref byte start = ref Unsafe.Add(ref source, offset);
                    var group = Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref start)) |
                        Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref start, 16))) |
                        Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref start, 32))) |
                        Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref start, 48)));
                    if (!Vector128.EqualsAll(group & mask, Vector128<ulong>.Zero))
                        goto InvalidStorage;
                }
                for (; offset <= bytes - 16; offset += 16)
                    if (!Vector128.EqualsAll(Vector128.LoadUnsafe(ref Unsafe.As<byte, ulong>(ref Unsafe.Add(ref source, offset))) & mask,
                            Vector128<ulong>.Zero))
                        goto InvalidStorage;
            }
            if (stride != 16)
                for (; offset <= bytes - sizeof(ulong); offset += sizeof(ulong))
                    if ((Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, offset)) & low) != 0)
                        goto InvalidStorage;
        }

        int remaining = count - offset / stride;
        ref byte current = ref Unsafe.Add(ref source, offset);
        for (; remaining > 0; remaining--)
        {
            if (Unsafe.Add(ref current, presence) > 1 || boolean >= 0 && Unsafe.Add(ref current, boolean) > 1)
                goto InvalidStorage;
            current = ref Unsafe.Add(ref current, stride);
        }
        return;

    InvalidStorage:
        InvalidBooleanStorage();
    }

    /// <summary>Reports an invalid managed Boolean value in a nullable block.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InvalidBooleanStorage() =>
        LuminPackExceptionHelper.ThrowInvalidDataException("A nullable array contains a noncanonical Boolean storage byte.");
}
#endif
