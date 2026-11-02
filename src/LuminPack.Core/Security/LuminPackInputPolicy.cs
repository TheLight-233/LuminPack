using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Security;

/// <summary>Generated-operation checks for untrusted binary reads.</summary>
/// <remarks>
/// Untrusted records use the ordinary wire format. Generated reads call these checks at the actual read sites with
/// limits frozen into the generated code as constants. The checks hold no ambient state: only recursive values use the
/// reader's <see cref="LuminPackReader.InputDepth"/> counter.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminPackInputPolicy
{
    /// <summary>Rejects oversized input before decoding, transcoding or staging.</summary>
    /// <param name="bytes">The encoded byte count.</param>
    /// <param name="maximum">The declaration's fixed byte limit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckInput(long bytes, int maximum)
    {
        if ((ulong)bytes > (uint)maximum) Fail("encoded input");
    }

    /// <summary>Bounds JSON characters before scanning or renting transcoding storage.</summary>
    /// <param name="input">The caller's character document.</param>
    /// <param name="encoding">The compiled document encoding.</param>
    /// <param name="maximum">The compiled encoded byte limit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckCharacters(ReadOnlySpan<char> input, int encoding, int maximum)
    {
        if (maximum == int.MaxValue) return;
        // A UTF-8 encoding needs at most three bytes per UTF-16 code unit; the exact count is needed only near the limit.
        long units = input.Length;
        if (encoding == 1) CheckInput(units * 2, maximum);
        else if (units * 3 > maximum) CheckInput(System.Text.Encoding.UTF8.GetByteCount(input), maximum);
    }

    /// <summary>Reads at most one byte beyond a selected input cap before rejecting an oversized stream.</summary>
    /// <param name="input">The caller-owned source.</param>
    /// <param name="output">The facade-owned staging stream.</param>
    /// <param name="maximum">The compiled byte limit.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The bounded copy completion.</returns>
    public static async System.Threading.Tasks.Task CopyInputAsync(System.IO.Stream input, System.IO.Stream output, int maximum, System.Threading.CancellationToken cancellationToken)
    {
        // The staging block is pooled; a rented array may be longer, so each request stays within the block size.
        int block = Math.Min(8192, maximum == int.MaxValue ? 8192 : maximum + 1);
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(block);
        try
        {
            int total = 0;
            while (true)
            {
                int request = (int)Math.Min(block, (long)maximum - total + 1);
                int read = await input.ReadAsync(buffer, 0, request, cancellationToken).ConfigureAwait(false);
                if (read == 0) return;
                CheckInput((long)total + read, maximum);
                await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                total += read;
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Reads an object marker and requires the declared member count or the null marker.</summary>
    /// <param name="reader">The checked reader.</param>
    /// <param name="members">The generated member count.</param>
    /// <returns>The marker.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadMarker(ref LuminPackReader reader, byte members)
    {
        byte marker = reader.Read<byte>();
        if (marker != members && marker != LuminPackCode.NullObject) InvalidMarker(marker);
        return marker;
    }

    /// <summary>Reads an object marker and reports null, requiring the declared member count otherwise.</summary>
    /// <param name="reader">The checked reader.</param>
    /// <param name="members">The generated member count.</param>
    /// <returns>Whether the marker is the null marker.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadNullMarker(ref LuminPackReader reader, byte members)
    {
        byte marker = reader.Read<byte>();
        // The member count falls through; null and rejection take the branch.
        if (marker != members)
        {
            if (marker != LuminPackCode.NullObject) InvalidMarker(marker);
            return true;
        }
        return false;
    }

    /// <summary>Reads the member marker of an inline struct record, which has no null form.</summary>
    /// <param name="reader">The checked reader.</param>
    /// <param name="members">The generated member count.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadStructMarker(ref LuminPackReader reader, byte members)
    {
        byte marker = reader.Read<byte>();
        if (marker != members) InvalidMarker(marker);
    }

    /// <summary>Reads a collection count, rejecting counts above the frozen limit before any allocation.</summary>
    /// <param name="reader">The checked reader, which already bounds the count by the remaining input.</param>
    /// <param name="minimumElementSize">The minimum element wire width.</param>
    /// <param name="maximum">The frozen collection limit.</param>
    /// <returns>The count, or minus one for null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadLength(ref LuminPackReader reader, int minimumElementSize, int maximum)
    {
        int count = reader.ReadLength(minimumElementSize);
        if (count > maximum) Fail("collection length");
        return count;
    }

    /// <summary>Checks an array decoded by a runtime read kernel against the frozen collection limit.</summary>
    /// <typeparam name="T">The primitive element type.</typeparam>
    /// <param name="values">The decoded array, or null.</param>
    /// <param name="maximum">The frozen collection limit.</param>
    /// <remarks>The kernel already bounded its allocation by the remaining input.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckLength<T>(T[]? values, int maximum)
    {
        if (values is not null && values.Length > maximum) Fail("collection length");
    }

    /// <summary>Checks a list decoded by a runtime read kernel against the frozen collection limit.</summary>
    /// <typeparam name="T">The primitive element type.</typeparam>
    /// <param name="values">The decoded list, or null.</param>
    /// <param name="maximum">The frozen collection limit.</param>
    /// <remarks>The kernel already bounded its allocation by the remaining input.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckLength<T>(System.Collections.Generic.List<T>? values, int maximum)
    {
        if (values is not null && values.Count > maximum) Fail("collection length");
    }

    /// <summary>Reads a collection head with the frozen collection limit.</summary>
    /// <param name="reader">The checked reader.</param>
    /// <param name="length">Receives the count or null marker.</param>
    /// <param name="minimumElementSize">The minimum element wire width.</param>
    /// <param name="maximum">The frozen collection limit.</param>
    /// <returns>Whether the collection is nonnull.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadCollectionHead(ref LuminPackReader reader, out int length, int minimumElementSize, int maximum)
    {
        length = ReadLength(ref reader, minimumElementSize, maximum);
        return length != LuminPackCode.NullCollection;
    }

    /// <summary>Peeks a collection head with the frozen collection limit.</summary>
    /// <param name="reader">The checked reader.</param>
    /// <param name="length">Receives the count or null marker.</param>
    /// <param name="minimumElementSize">The minimum element wire width.</param>
    /// <param name="maximum">The frozen collection limit.</param>
    /// <returns>Whether the collection is nonnull.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PeekCollectionHeader(ref LuminPackReader reader, out int length, int minimumElementSize, int maximum)
    {
        bool present = reader.PeekCollectionHeader(out length, minimumElementSize);
        if (length > maximum) Fail("collection length");
        return present;
    }

    /// <summary>Reads a string with the frozen character limit.</summary>
    /// <typeparam name="TOptions">The exact binary string profile.</typeparam>
    /// <param name="reader">The checked reader, which bounds the payload by the remaining input.</param>
    /// <param name="current">The reusable candidate.</param>
    /// <param name="maximum">The frozen string limit in UTF-16 code units.</param>
    /// <returns>The decoded or reused string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? ReadString<TOptions>(ref LuminPackReader reader, string? current, int maximum) where TOptions : struct, ILuminPackOptions
    {
        // Every record's characters are bounded by its consumed bytes, so the decoder cannot allocate beyond the input.
        string? value = reader.Read<TOptions>(current);
        if (value is not null && value.Length > maximum) Fail("string length");
        return value;
    }

    /// <summary>Reads a string record by reference with the frozen character limit.</summary>
    /// <typeparam name="TOptions">The exact binary string profile.</typeparam>
    /// <param name="reader">The checked reader.</param>
    /// <param name="value">The reusable candidate and destination.</param>
    /// <param name="maximum">The frozen string limit in UTF-16 code units.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadStringRecord<TOptions>(ref LuminPackReader reader, ref string? value, int maximum) where TOptions : struct, ILuminPackOptions
        => value = ReadString<TOptions>(ref reader, value, maximum);

    /// <summary>Reads string slots with the frozen character limit, retaining unchanged references.</summary>
    /// <typeparam name="TOptions">The exact binary string profile.</typeparam>
    /// <param name="reader">The checked reader.</param>
    /// <param name="strings">Destination slots.</param>
    /// <param name="maximum">The frozen string limit in UTF-16 code units.</param>
    public static void ReadStrings<TOptions>(ref LuminPackReader reader, Span<string?> strings, int maximum) where TOptions : struct, ILuminPackOptions
    {
        for (int index = 0; index < strings.Length; index++)
        {
            string? current = strings[index];
            string? decoded = ReadString<TOptions>(ref reader, current, maximum);
            if (!ReferenceEquals(current, decoded)) strings[index] = decoded;
        }
    }

    /// <summary>Loads a Boolean from validated input storage and rejects noncanonical values.</summary>
    /// <param name="source">The first storage byte, already bounds-checked by the reader.</param>
    /// <returns>The canonical value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadBoolean(ref byte source)
    {
        byte value = source;
        if (value > 1) InvalidBoolean();
        // The checked storage is already canonical.
        return Unsafe.As<byte, bool>(ref value);
    }

    /// <summary>Loads a Boolean from validated input storage through the static reader kernel's signature.</summary>
    /// <param name="source">The first storage byte, already bounds-checked by the reader.</param>
    /// <param name="value">Receives the canonical value.</param>
    /// <returns>The consumed width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadBoolean(ref byte source, out bool value)
    {
        value = ReadBoolean(ref source);
        return sizeof(bool);
    }

    /// <summary>Reads a Boolean through the checked reader and rejects noncanonical values.</summary>
    /// <param name="reader">The checked reader.</param>
    /// <returns>The canonical value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadBoolean(ref LuminPackReader reader)
    {
        byte value = reader.Read<byte>();
        if (value > 1) InvalidBoolean();
        return Unsafe.As<byte, bool>(ref value);
    }

    /// <summary>Checks a Boolean already copied from input storage.</summary>
    /// <param name="value">The copied storage.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckBoolean(in bool value)
    {
        if (Unsafe.As<bool, byte>(ref Unsafe.AsRef(in value)) > 1) InvalidBoolean();
    }

    /// <summary>Checks Boolean elements copied in bulk from input storage.</summary>
    /// <param name="values">The copied elements.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckBooleans(ReadOnlySpan<bool> values)
    {
        if (!AreCanonicalBooleans(MemoryMarshal.AsBytes(values))) InvalidBoolean();
    }

    /// <summary>Checks a Boolean array copied in bulk from input storage.</summary>
    /// <param name="values">The copied array, or null.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckBooleans(bool[]? values)
    {
        if (values is not null) CheckBooleans(new ReadOnlySpan<bool>(values));
    }

    /// <summary>Checks a Boolean list copied in bulk from input storage.</summary>
    /// <param name="values">The copied list, or null.</param>
    public static void CheckBooleans(System.Collections.Generic.List<bool>? values)
    {
        if (values is null) return;
#if NET8_0_OR_GREATER
        CheckBooleans(CollectionsMarshal.AsSpan(values));
#else

        foreach (bool value in values) CheckBoolean(value);
#endif
    }

    /// <summary>Enters one recursive value and rejects nesting above the frozen limit.</summary>
    /// <param name="reader">The reader that owns the depth counter for this operation.</param>
    /// <param name="maximum">The frozen depth limit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnterDepth(ref LuminPackReader reader, int maximum)
    {
        if (++reader.InputDepth > maximum) Fail("nesting depth");
    }

    /// <summary>Leaves one recursive value after it completed successfully.</summary>
    /// <param name="reader">The reader that owns the depth counter.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExitDepth(ref LuminPackReader reader) => reader.InputDepth--;

    /// <summary>Rejects legacy member-compressed operations in untrusted graphs.</summary>
    /// <remarks>Retained for previously generated code; current untrusted graphs check compressed counts instead.</remarks>
    public static void RejectLegacyInputCompression()
        => LuminPackExceptionHelper.ThrowNotSupportedException("Untrusted input does not support legacy member-compressed operations.");

    /// <summary>Checks the element count of a compressed contiguous record before its storage is allocated.</summary>
    /// <param name="input">The record, starting at its element count.</param>
    /// <param name="maximum">The frozen collection limit.</param>
    /// <returns>The unchanged record.</returns>
    /// <remarks>A compressed record's count is not bounded by its input bytes. A short record is left to the original reader's bounds check.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> CheckCompressedCount(ReadOnlySpan<byte> input, int maximum)
    {
        if (input.Length >= sizeof(int) && Unsafe.ReadUnaligned<int>(ref MemoryMarshal.GetReference(input)) > maximum) Fail("collection");
        return input;
    }

    /// <summary>Returns a back reference of the expected type.</summary>
    /// <typeparam name="T">The reference-tracked type expected at this position.</typeparam>
    /// <param name="value">The object registered under the input's reference id.</param>
    /// <returns>The typed reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadReference<T>(object value)
    {
        if (value is T typed) return typed;
        InvalidReference();
        return default!;
    }

    /// <summary>Rejects a graph containing a shape the untrusted-input option does not accept.</summary>
    /// <param name="reason">The first unsupported shape.</param>
    public static void RejectUnsupported(string reason) => LuminPackExceptionHelper.ThrowNotSupportedException(reason);

    /// <summary>Rejects any Boolean storage bit except the low bit without reading outside the span.</summary>
    /// <param name="bytes">The complete Boolean storage.</param>
    /// <returns>Whether every byte is zero or one.</returns>
    /// <remarks>Each path ORs all bytes into one accumulator and tests it once; its final load overlaps the previous one
    /// inside the span instead of running a scalar tail.</remarks>
    internal static bool AreCanonicalBooleans(ReadOnlySpan<byte> bytes)
    {
        int length = bytes.Length;
        ref byte first = ref MemoryMarshal.GetReference(bytes);
#if NET8_0_OR_GREATER
        if (Avx512F.IsSupported && Avx512BW.IsSupported && length >= 64)
        {
            var any = Vector512<byte>.Zero;
            nuint last = (nuint)(uint)(length - 64);
            for (nuint index = 0; index < last; index += 64) any |= Vector512.LoadUnsafe(ref first, index);
            any |= Vector512.LoadUnsafe(ref first, last);
            return Vector512.EqualsAll(any & Vector512.Create((byte)0xFE), Vector512<byte>.Zero);
        }
        if (Avx2.IsSupported && length >= 32)
        {
            var any = Vector256<byte>.Zero;
            nuint last = (nuint)(uint)(length - 32);
            for (nuint index = 0; index < last; index += 32) any |= Vector256.LoadUnsafe(ref first, index);
            any |= Vector256.LoadUnsafe(ref first, last);
            return Vector256.EqualsAll(any & Vector256.Create((byte)0xFE), Vector256<byte>.Zero);
        }
        if ((Sse2.IsSupported || AdvSimd.IsSupported) && length >= 16)
        {
            var any = Vector128<byte>.Zero;
            nuint last = (nuint)(uint)(length - 16);
            for (nuint index = 0; index < last; index += 16) any |= Vector128.LoadUnsafe(ref first, index);
            any |= Vector128.LoadUnsafe(ref first, last);
            return Vector128.EqualsAll(any & Vector128.Create((byte)0xFE), Vector128<byte>.Zero);
        }
#endif
        if (length >= 8)
        {
            ulong any = 0;
            for (int index = 0; index < length - 8; index += 8) any |= BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(index, 8));
            any |= BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(length - 8, 8));
            return (any & 0xFEFEFEFEFEFEFEFEUL) == 0;
        }
        if (length >= 4)
            return ((BinaryPrimitives.ReadUInt32LittleEndian(bytes) | BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(length - 4, 4))) & 0xFEFEFEFEu) == 0;
        int bits = 0;
        for (int index = 0; index < length; index++) bits |= Unsafe.Add(ref first, index);
        return (bits & 0xFE) == 0;
    }

    /// <summary>Reads a fixed field batch whose Boolean input bytes lie in one eight-byte window.</summary>
    /// <param name="reader">The checked reader.</param><param name="destination">The batch storage.</param>
    /// <param name="byteCount">The batch width.</param><param name="window">The tested window offset.</param>
    /// <param name="mask">The compile-time mask of non-low Boolean bits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadBatch(ref LuminPackReader reader, ref byte destination, int byteCount, int window, ulong mask)
    {
        if (!reader.ReadCanonicalBatch(ref destination, byteCount, window, mask)) InvalidBoolean();
    }

    /// <summary>Reads a fixed field batch whose Boolean input bytes lie in one four-byte window.</summary>
    /// <param name="reader">The checked reader.</param><param name="destination">The batch storage.</param>
    /// <param name="byteCount">The batch width.</param><param name="window">The tested window offset.</param>
    /// <param name="mask">The compile-time mask of non-low Boolean bits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadBatch(ref LuminPackReader reader, ref byte destination, int byteCount, int window, uint mask)
    {
        if (!reader.ReadCanonicalBatch(ref destination, byteCount, window, mask)) InvalidBoolean();
    }

    /// <summary>Reads a Boolean array whose copy validates every element in the same pass.</summary>
    /// <param name="reader">The checked reader.</param>
    /// <param name="value">The reusable destination.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadBooleans(ref LuminPackReader reader, ref bool[]? value)
    {
        if (!reader.ReadCanonicalBooleans(ref value)) InvalidBoolean();
    }

    /// <summary>Copies Boolean storage and reports whether every byte is zero or one.</summary>
    /// <param name="source">The first input byte, validated for <paramref name="length"/> bytes.</param>
    /// <param name="destination">The first destination byte, owning <paramref name="length"/> bytes.</param>
    /// <param name="length">The positive byte count.</param>
    /// <returns>Whether the copied storage is canonical.</returns>
    /// <remarks>Each load is stored and ORed into one accumulator, so the input is read once. The final load overlaps the previous
    /// one inside both ranges instead of running a scalar tail. Kept out of line so wide vector state never leaks into a caller.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool CopyBooleans(ref byte source, ref byte destination, int length)
    {
#if NET8_0_OR_GREATER
        if (Vector256.IsHardwareAccelerated && length >= 32)
        {
            var any = Vector256<byte>.Zero;
            nuint last = (nuint)(uint)(length - 32);
            for (nuint index = 0; index < last; index += 32)
            {
                var block = Vector256.LoadUnsafe(ref source, index);
                block.StoreUnsafe(ref destination, index);
                any |= block;
            }
            var tail = Vector256.LoadUnsafe(ref source, last);
            tail.StoreUnsafe(ref destination, last);
            return ((any | tail) & Vector256.Create((byte)0xFE)) == Vector256<byte>.Zero;
        }
        if (Vector128.IsHardwareAccelerated && length >= 16)
        {
            var any = Vector128<byte>.Zero;
            nuint last = (nuint)(uint)(length - 16);
            for (nuint index = 0; index < last; index += 16)
            {
                var block = Vector128.LoadUnsafe(ref source, index);
                block.StoreUnsafe(ref destination, index);
                any |= block;
            }
            var tail = Vector128.LoadUnsafe(ref source, last);
            tail.StoreUnsafe(ref destination, last);
            return ((any | tail) & Vector128.Create((byte)0xFE)) == Vector128<byte>.Zero;
        }
#endif
        // 32-bit targets keep typed unaligned access within four bytes.
        if (Unsafe.SizeOf<nint>() == 8 && length >= 8)
        {
            ulong any = 0;
            int last = length - 8;
            for (int index = 0; index < last; index += 8)
            {
                ulong block = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, index));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, index), block);
                any |= block;
            }
            ulong final = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, last));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, last), final);
            return ((any | final) & 0xFEFEFEFEFEFEFEFEUL) == 0;
        }
        if (length >= 4)
        {
            uint any = 0;
            int last = length - 4;
            for (int index = 0; index < last; index += 4)
            {
                uint block = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, index));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, index), block);
                any |= block;
            }
            uint final = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, last));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, last), final);
            return ((any | final) & 0xFEFEFEFEu) == 0;
        }
        int bits = 0;
        for (int index = 0; index < length; index++)
        {
            byte value = Unsafe.Add(ref source, index);
            Unsafe.Add(ref destination, index) = value;
            bits |= value;
        }
        return (bits & 0xFE) == 0;
    }

    // Rejections throw directly and stay inlinable, so the JIT proves them no-return and moves their blocks out of the hot path.
    /// <summary>Rejects an exceeded input limit with a catchable data exception.</summary>
    /// <param name="name">The limit that was exceeded.</param>
    [DoesNotReturn]
    private static void Fail(string name) => throw new System.IO.InvalidDataException("Untrusted input exceeded its " + name + " limit.");

    /// <summary>Rejects a back reference to an object of another type.</summary>
    [DoesNotReturn]
    private static void InvalidReference() => throw new System.IO.InvalidDataException("A reference id names an object of another type.");

    /// <summary>Rejects an object marker that disagrees with the generated member count.</summary>
    /// <param name="marker">The rejected marker.</param>
    [DoesNotReturn]
    private static void InvalidMarker(byte marker) => throw new System.IO.InvalidDataException("The object member header " + marker + " disagrees with its schema.");

    /// <summary>Rejects a Boolean whose storage is neither zero nor one.</summary>
    [DoesNotReturn]
    private static void InvalidBoolean() => throw new System.IO.InvalidDataException("A Boolean has a noncanonical storage value.");
}
