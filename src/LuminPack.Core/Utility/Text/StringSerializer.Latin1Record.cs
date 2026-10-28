using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Internal;
#if NET8_0_OR_GREATER
using System.Buffers;
using System.Runtime.Intrinsics;
using System.Text;
#endif

namespace LuminPack.Utility;

/// <summary>Compact Latin-1 payloads of the UTF-16 length string record.</summary>
/// <remarks>
/// A UTF-16 length record stores a positive even header for its raw code-unit byte count. A string whose code units
/// are all at most U+00FF instead stores the odd header <c>(length &lt;&lt; 1) | 1</c> followed by one byte per
/// character. Both forms carry the character count in <c>header &gt;&gt; 1</c>; readers accept both, so every record
/// written before the compact form remains readable, and an older reader rejects a compact record as malformed
/// instead of misreading it.
/// </remarks>
public static unsafe partial class StringSerializer
{
    /// <summary>Encodes the compact record header of a Latin-1 payload.</summary>
    /// <param name="length">The character count.</param>
    /// <returns>The odd record header.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Latin1Header(int length) => (length << 1) | 1;

    /// <summary>Returns the payload width of a nonnegative UTF-16 length record header.</summary>
    /// <param name="header">The record header.</param>
    /// <returns>The payload byte count; negative headers stay negative.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Utf16RecordPayload(int header) => (header & 1) != 0 ? header >> 1 : header;

    /// <summary>Returns whether every code unit of a string fits one Latin-1 byte.</summary>
    /// <param name="value">The string.</param>
    /// <returns>Whether the compact payload represents the string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsLatin1(string value)
    {
#if NETSTANDARD2_1
        if (LuminPackBurst.TryIsLatin1(value.AsSpan(), out bool latin1)) return latin1;
#endif
        int length = value.Length;
        ref ushort chars = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(value.AsSpan()));
#if NET8_0_OR_GREATER
        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8)
        {
            if (length > 16)
                return IsLatin1Long(ref chars, (nuint)(uint)length);
            if (length >= 8)
                return ((Vector128.LoadUnsafe(ref chars) | Vector128.LoadUnsafe(ref chars, (nuint)(uint)(length - 8))) & Vector128.Create((ushort)0xFF00)) == Vector128<ushort>.Zero;
            if (length >= 4)
                return ((LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref chars)) |
                    LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref chars, (nint)(uint)(length - 4))))) & 0xFF00FF00FF00FF00UL) == 0;
        }
#endif
        for (int i = 0; i < length; i++)
        {
            if (Unsafe.Add(ref chars, (nint)(uint)i) > 0xFF)
                return false;
        }

        return true;
    }

#if NET8_0_OR_GREATER
    /// <summary>Checks more than sixteen characters by combining whole vectors and testing their high bytes.</summary>
    /// <param name="chars">The first character.</param>
    /// <param name="length">The character count, above sixteen.</param>
    /// <returns>Whether every character is at most U+00FF.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsLatin1Long(ref ushort chars, nuint length)
    {
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && length >= 32)
        {
            var high = Vector256.Create((ushort)0xFF00);
            // Four vectors per test keep the loop's dependency chain short; the overlapping tail reads only
            // characters inside the string.
            for (; i + 64 <= length; i += 64)
            {
                var any = Vector256.LoadUnsafe(ref chars, i) | Vector256.LoadUnsafe(ref chars, i + 16) |
                    Vector256.LoadUnsafe(ref chars, i + 32) | Vector256.LoadUnsafe(ref chars, i + 48);
                if ((any & high) != Vector256<ushort>.Zero)
                    return false;
            }

            var tail = Vector256.LoadUnsafe(ref chars, length - 16);
            for (; i + 16 < length; i += 16)
                tail |= Vector256.LoadUnsafe(ref chars, i);
            return (tail & high) == Vector256<ushort>.Zero;
        }

        var high128 = Vector128.Create((ushort)0xFF00);
        var rest = Vector128.LoadUnsafe(ref chars, length - 8);
        for (; i + 8 < length; i += 8)
            rest |= Vector128.LoadUnsafe(ref chars, i);
        return (rest & high128) == Vector128<ushort>.Zero;
    }
#endif

    /// <summary>Returns the exact payload width of a nonempty UTF-16 length record.</summary>
    /// <param name="value">The string.</param>
    /// <returns>One byte per character for Latin-1 text, otherwise two.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Utf16RecordPayloadLength(string value) => IsLatin1(value) ? value.Length : value.Length * sizeof(char);

    /// <summary>Writes a nonempty string's UTF-16 length record payload and returns its header.</summary>
    /// <param name="destination">The first payload byte, followed by room for <c>2 * value.Length</c> bytes.</param>
    /// <param name="value">The nonempty string.</param>
    /// <returns>The record header: the compact Latin-1 header, or the raw UTF-16 byte count.</returns>
    /// <remarks>Only the 1 to 16 character Latin-1 form is inlined; record sites stay small enough for the JIT to
    /// keep the writer's cursor in registers around them.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteUtf16RecordPayload(ref byte destination, string value)
    {
#if NET8_0_OR_GREATER
        int length = value.Length;
        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(length - 1) <= 15)
        {
            ref ushort chars = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(value.AsSpan()));
            if (length < 4)
            {
                // One to three characters: one or two characters fit four bytes and three fit eight, where the
                // string's terminating null supplies the remaining code unit, so no read leaves the string.
                ulong head = length == 3
                    ? LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref chars))
                    : LuminPackReader.Read<uint>(ref Unsafe.As<ushort, byte>(ref chars));
                if ((head & 0xFF00FF00FF00FF00UL) == 0)
                {
                    // The destination has room for two bytes per character: two for one character, four otherwise.
                    uint packed = PackLatin1(head);
                    if (length == 1)
                        LuminPackWriter.Write(ref destination, (ushort)packed);
                    else
                        LuminPackWriter.Write(ref destination, packed);
                    return Latin1Header(length);
                }
            }
            else if (length >= 8)
            {
                var first = Vector128.LoadUnsafe(ref chars);
                var last = Vector128.LoadUnsafe(ref chars, (nuint)(uint)(length - 8));
                if (((first | last) & Vector128.Create((ushort)0xFF00)) == Vector128<ushort>.Zero)
                {
                    var narrowed = Vector128.Narrow(first, last).AsUInt64();
                    LuminPackWriter.Write(ref destination, narrowed.ToScalar());
                    LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(length - 8)), narrowed.GetElement(1));
                    return Latin1Header(length);
                }
            }
            else
            {
                ulong head = LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref chars));
                ulong tail = LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref chars, (nint)(uint)(length - 4))));
                if (((head | tail) & 0xFF00FF00FF00FF00UL) == 0)
                {
                    LuminPackWriter.Write(ref destination, PackLatin1(head));
                    LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(length - 4)), PackLatin1(tail));
                    return Latin1Header(length);
                }
            }
        }
#endif
        return WriteUtf16RecordPayloadSlow(ref destination, value);
    }

#if NET8_0_OR_GREATER
    /// <summary>Writes the compact payload of a 1 to 16 character Latin-1 string, or nothing for other text.</summary>
    /// <param name="destination">The first payload byte, followed by room for <c>2 * value.Length</c> bytes.</param>
    /// <param name="value">A string of 1 to 16 characters, on a 64-bit target with accelerated 128-bit vectors.</param>
    /// <returns>The compact record header, or zero when a character is above U+00FF.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int TryWriteShortLatin1Payload(ref byte destination, string value)
    {
        int length = value.Length;
        ref ushort chars = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(value.AsSpan()));
        if (length <= 2)
        {
            if (length == 1)
            {
                ushort first = chars;
                if (first > 0xFF) return 0;
                destination = (byte)first;
            }
            else
            {
                uint pair = LuminPackReader.Read<uint>(ref Unsafe.As<ushort, byte>(ref chars));
                if ((pair & 0xFF00FF00U) != 0) return 0;
                LuminPackWriter.Write(ref destination, (ushort)(pair | (pair >> 8)));
            }
            return Latin1Header(length);
        }
        if (length >= 8)
        {
            var first = Vector128.LoadUnsafe(ref chars);
            var last = Vector128.LoadUnsafe(ref chars, (nuint)(uint)(length - 8));
            if (((first | last) & Vector128.Create((ushort)0xFF00)) != Vector128<ushort>.Zero)
                return 0;
            var narrowed = Vector128.Narrow(first, last).AsUInt64();
            LuminPackWriter.Write(ref destination, narrowed.ToScalar());
            LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(length - 8)), narrowed.GetElement(1));
            return Latin1Header(length);
        }

        // One to seven characters: the first four and the last four, where a terminating null completes a shorter
        // string, so no read leaves the string and no write leaves its two-bytes-per-character room.
        ulong head = length >= 4 || length == 3
            ? LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref chars))
            : LuminPackReader.Read<uint>(ref Unsafe.As<ushort, byte>(ref chars));
        ulong tail = length >= 4 ? LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref chars, (nint)(uint)(length - 4)))) : 0;
        if (((head | tail) & 0xFF00FF00FF00FF00UL) != 0)
            return 0;
        if (length >= 4)
        {
            LuminPackWriter.Write(ref destination, PackLatin1(head));
            LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(length - 4)), PackLatin1(tail));
        }
        else if (length == 1)
            LuminPackWriter.Write(ref destination, (ushort)PackLatin1(head));
        else
            LuminPackWriter.Write(ref destination, PackLatin1(head));
        return Latin1Header(length);
    }
#endif

    /// <summary>Writes any nonempty string's UTF-16 length record payload outside inlined record sites.</summary>
    /// <param name="destination">The first payload byte, followed by room for <c>2 * value.Length</c> bytes.</param>
    /// <param name="value">The nonempty string.</param>
    /// <returns>The record header.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int WriteUtf16RecordPayloadSlow(ref byte destination, string value)
    {
        ref char source = ref MemoryMarshal.GetReference(value.AsSpan());
        int length = value.Length;
#if NET8_0_OR_GREATER
        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(length - 17) <= 47)
        {
            if (TryNarrowLatin1Medium(ref Unsafe.As<char, ushort>(ref source), ref destination, length))
                return Latin1Header(length);
        }
        else
#endif
        if (TryNarrowLatin1(ref source, ref destination, length))
            return Latin1Header(length);
        // A code unit above U+00FF: the narrowed prefix is overwritten by the raw payload.
        int bytes = length * sizeof(char);
        SerializeUtf16(ref destination, ref Unsafe.As<char, byte>(ref source), (uint)bytes);
        return bytes;
    }

    /// <summary>Narrows characters to bytes when every one is at most U+00FF.</summary>
    /// <param name="source">The first character.</param>
    /// <param name="destination">Room for <paramref name="length"/> bytes.</param>
    /// <param name="length">The positive character count.</param>
    /// <returns>Whether every character narrowed; on false a prefix may have been written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryNarrowLatin1(ref char source, ref byte destination, int length)
    {
#if NETSTANDARD2_1
        if (LuminPackBurst.TryNarrowLatin1(ref source, ref destination, length, out bool narrowed)) return narrowed;
#endif
#if NET8_0_OR_GREATER
        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8)
        {
            ref ushort chars = ref Unsafe.As<char, ushort>(ref source);
            if (length > 16)
                return TryNarrowLatin1Long(ref chars, ref destination, (nuint)(uint)length);
            if (length >= 8)
            {
                // 8 to 16 characters: the first eight and the last eight cover the string.
                var first = Vector128.LoadUnsafe(ref chars);
                var last = Vector128.LoadUnsafe(ref chars, (nuint)(uint)(length - 8));
                if (((first | last) & Vector128.Create((ushort)0xFF00)) != Vector128<ushort>.Zero)
                    return false;
                var narrowed = Vector128.Narrow(first, last).AsUInt64();
                LuminPackWriter.Write(ref destination, narrowed.ToScalar());
                LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(length - 8)), narrowed.GetElement(1));
                return true;
            }

            if (length >= 4)
            {
                // 4 to 7 characters: the first four and the last four.
                ulong head = LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref chars));
                ulong tail = LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref chars, (nint)(uint)(length - 4))));
                if (((head | tail) & 0xFF00FF00FF00FF00UL) != 0)
                    return false;
                LuminPackWriter.Write(ref destination, PackLatin1(head));
                LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(length - 4)), PackLatin1(tail));
                return true;
            }
        }
#endif
        for (int i = 0; i < length; i++)
        {
            char c = Unsafe.Add(ref source, (nint)(uint)i);
            if (c > 0xFF)
                return false;
            Unsafe.Add(ref destination, (nint)(uint)i) = (byte)c;
        }

        return true;
    }

#if NET8_0_OR_GREATER
    /// <summary>Packs the low bytes of four characters.</summary>
    /// <param name="chars">Four characters whose high bytes are zero.</param>
    /// <returns>The four low bytes in order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackLatin1(ulong chars)
    {
        chars |= chars >> 8;
        chars &= 0x0000FFFF0000FFFFUL;
        return (uint)(chars | (chars >> 16));
    }

    /// <summary>Narrows 17 to 64 characters with a leading and an overlapping trailing block and no loop.</summary>
    /// <param name="source">The first character.</param>
    /// <param name="destination">Room for <paramref name="length"/> bytes.</param>
    /// <param name="length">The character count, from 17 to 64.</param>
    /// <returns>Whether every character narrowed; on false nothing was written.</returns>
    /// <remarks>Straight-line blocks keep the outlined record writer free of a loop whose layout would rely on
    /// synthesized profile data; longer strings use the separately profiled loop.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryNarrowLatin1Medium(ref ushort source, ref byte destination, int length)
    {
        if (length <= 32)
        {
            nuint back = (nuint)(uint)(length - 16);
            var a = Vector128.LoadUnsafe(ref source);
            var b = Vector128.LoadUnsafe(ref source, 8);
            var c = Vector128.LoadUnsafe(ref source, back);
            var d = Vector128.LoadUnsafe(ref source, back + 8);
            if (((a | b | c | d) & Vector128.Create((ushort)0xFF00)) != Vector128<ushort>.Zero)
                return false;
            Vector128.Narrow(a, b).StoreUnsafe(ref destination);
            Vector128.Narrow(c, d).StoreUnsafe(ref destination, back);
            return true;
        }

        if (!Vector256.IsHardwareAccelerated)
            return TryNarrowLatin1Long(ref source, ref destination, (nuint)(uint)length);
        nuint tail = (nuint)(uint)(length - 32);
        var e = Vector256.LoadUnsafe(ref source);
        var f = Vector256.LoadUnsafe(ref source, 16);
        var g = Vector256.LoadUnsafe(ref source, tail);
        var h = Vector256.LoadUnsafe(ref source, tail + 16);
        if (((e | f | g | h) & Vector256.Create((ushort)0xFF00)) != Vector256<ushort>.Zero)
            return false;
        Vector256.Narrow(e, f).StoreUnsafe(ref destination);
        Vector256.Narrow(g, h).StoreUnsafe(ref destination, tail);
        return true;
    }

    /// <summary>Narrows more than sixteen characters with whole vectors and an overlapping final block.</summary>
    /// <param name="source">The first character.</param>
    /// <param name="destination">Room for <paramref name="length"/> bytes.</param>
    /// <param name="length">The character count, above sixteen.</param>
    /// <returns>Whether every character narrowed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryNarrowLatin1Long(ref ushort source, ref byte destination, nuint length)
    {
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && length >= 32)
        {
            var high = Vector256.Create((ushort)0xFF00);
            // All lanes are in 0..255 after classification, where unsigned saturating pack equals truncation.
            // Retain the existing 256-bit width: the change removes redundant masks and per-block tail selection.
            // The bounded 128-bit path below remains the portable and ARM64 fallback.
            while (i + 64 <= length)
            {
                var a = Vector256.LoadUnsafe(ref source, i);
                var b = Vector256.LoadUnsafe(ref source, i + 16);
                var c = Vector256.LoadUnsafe(ref source, i + 32);
                var d = Vector256.LoadUnsafe(ref source, i + 48);
                if (((a | b | c | d) & high) != Vector256<ushort>.Zero)
                    return false;
                (global::System.Runtime.Intrinsics.X86.Avx2.IsSupported
                    ? global::System.Runtime.Intrinsics.X86.Avx2.Permute4x64(global::System.Runtime.Intrinsics.X86.Avx2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16()).AsUInt64(), 0xD8).AsByte()
                    : Vector256.Narrow(a, b)).StoreUnsafe(ref destination, i);
                (global::System.Runtime.Intrinsics.X86.Avx2.IsSupported
                    ? global::System.Runtime.Intrinsics.X86.Avx2.Permute4x64(global::System.Runtime.Intrinsics.X86.Avx2.PackUnsignedSaturate(c.AsInt16(), d.AsInt16()).AsUInt64(), 0xD8).AsByte()
                    : Vector256.Narrow(c, d)).StoreUnsafe(ref destination, i + 32);
                i += 64;
            }
            if (i == length)
                return true;
            nuint last = length - 32;
            while (i < last)
            {
                var a = Vector256.LoadUnsafe(ref source, i);
                var b = Vector256.LoadUnsafe(ref source, i + 16);
                if (((a | b) & high) != Vector256<ushort>.Zero)
                    return false;
                (global::System.Runtime.Intrinsics.X86.Avx2.IsSupported
                    ? global::System.Runtime.Intrinsics.X86.Avx2.Permute4x64(global::System.Runtime.Intrinsics.X86.Avx2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16()).AsUInt64(), 0xD8).AsByte()
                    : Vector256.Narrow(a, b)).StoreUnsafe(ref destination, i);
                i += 32;
            }
            {
                var a = Vector256.LoadUnsafe(ref source, last);
                var b = Vector256.LoadUnsafe(ref source, last + 16);
                if (((a | b) & high) != Vector256<ushort>.Zero)
                    return false;
                (global::System.Runtime.Intrinsics.X86.Avx2.IsSupported
                    ? global::System.Runtime.Intrinsics.X86.Avx2.Permute4x64(global::System.Runtime.Intrinsics.X86.Avx2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16()).AsUInt64(), 0xD8).AsByte()
                    : Vector256.Narrow(a, b)).StoreUnsafe(ref destination, last);
            }
            return true;
        }

        // The original 128-bit fallback retains complete bounded loads and the same scalar callers.
        {
            var high = Vector128.Create((ushort)0xFF00);
            nuint last = length - 16;
            while (i < last)
            {
                var a = Vector128.LoadUnsafe(ref source, i);
                var b = Vector128.LoadUnsafe(ref source, i + 8);
                if (((a | b) & high) != Vector128<ushort>.Zero)
                    return false;
                Vector128.Narrow(a, b).StoreUnsafe(ref destination, i);
                i += 16;
            }
            {
                var a = Vector128.LoadUnsafe(ref source, last);
                var b = Vector128.LoadUnsafe(ref source, last + 8);
                if (((a | b) & high) != Vector128<ushort>.Zero)
                    return false;
                Vector128.Narrow(a, b).StoreUnsafe(ref destination, last);
            }
            return true;
        }
    }
#endif

    /// <summary>The shared one-character strings of every Latin-1 code unit.</summary>
    private static readonly string[] Latin1Singles = CreateLatin1Singles();

    /// <summary>Creates the shared one-character strings.</summary>
    /// <returns>256 strings indexed by their code unit.</returns>
    private static string[] CreateLatin1Singles()
    {
        var singles = new string[256];
        for (int i = 0; i < singles.Length; i++)
            singles[i] = ((char)i).ToString();
        return singles;
    }

    /// <summary>Decodes or reuses a validated nonempty Latin-1 payload at an inlined record site.</summary>
    /// <param name="payload">The first payload byte.</param>
    /// <param name="length">The positive character count, equal to the payload width.</param>
    /// <param name="current">The immutable storage available for reuse.</param>
    /// <returns>The existing equal string or a fresh one.</returns>
    /// <remarks>Only 1 to 16 characters are handled inline; other lengths use the outlined decoder.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ReadLatin1Payload(ref byte payload, int length, string? current)
    {
#if NET8_0_OR_GREATER
        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(length - 1) <= 2)
        {
            // One to three bytes are read exactly; the widened characters end with zero code units, which equal the
            // terminating null of a same-length string, so four or eight bytes compare and store whole.
            // A one-character record resolves to the shared immutable string of its code unit, as char.ToString does.
            if (length == 1)
                return current is not null && current.Length == 1 && current[0] == payload ? current : Latin1Singles[payload];
            uint bytes = LuminPackReader.Read<ushort>(ref payload);
            if (length == 3)
                bytes |= (uint)Unsafe.Add(ref payload, 2) << 16;
            ulong wide = WidenLatin1(bytes);
            if (current is not null && current.Length == length)
            {
                ref byte code = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(current.AsSpan()));
                if (length == 3 ? LuminPackReader.Read<ulong>(ref code) == wide : LuminPackReader.Read<uint>(ref code) == (uint)wide)
                    return current;
            }

            string small = FastString.Allocate(length);
            ref byte target = ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in small.GetPinnableReference()));
            if (length == 3)
                LuminPackWriter.Write(ref target, wide);
            else
                LuminPackWriter.Write(ref target, (uint)wide);
            return small;
        }

        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(length - 4) <= 12)
        {
            int back = length - (length >= 8 ? 8 : 4);
            Vector128<ushort> first, last;
            if (length >= 8)
            {
                first = Vector128.WidenLower(Vector128.CreateScalarUnsafe(LuminPackReader.Read<ulong>(ref payload)).AsByte());
                last = Vector128.WidenLower(Vector128.CreateScalarUnsafe(LuminPackReader.Read<ulong>(ref Unsafe.Add(ref payload, (nint)(uint)back))).AsByte());
            }
            else
            {
                first = Vector128.CreateScalarUnsafe(WidenLatin1(LuminPackReader.Read<uint>(ref payload))).AsUInt16();
                last = Vector128.CreateScalarUnsafe(WidenLatin1(LuminPackReader.Read<uint>(ref Unsafe.Add(ref payload, (nint)(uint)back)))).AsUInt16();
            }

            if (current is not null && current.Length == length)
            {
                ref ushort chars = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(current.AsSpan()));
                bool equal = length >= 8
                    ? Vector128.EqualsAll(first, Vector128.LoadUnsafe(ref chars)) && Vector128.EqualsAll(last, Vector128.LoadUnsafe(ref chars, (nuint)(uint)back))
                    : first.AsUInt64().ToScalar() == LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref chars)) &&
                      last.AsUInt64().ToScalar() == LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref chars, (nint)(uint)back)));
                if (equal)
                    return current;
            }

            string result = FastString.Allocate(length);
            ref ushort destination = ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in result.GetPinnableReference()));
            if (length >= 8)
            {
                first.StoreUnsafe(ref destination);
                last.StoreUnsafe(ref destination, (nuint)(uint)back);
            }
            else
            {
                LuminPackWriter.Write(ref Unsafe.As<ushort, byte>(ref destination), first.AsUInt64().ToScalar());
                LuminPackWriter.Write(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destination, (nint)(uint)back)), last.AsUInt64().ToScalar());
            }

            return result;
        }
#endif
        return ReadLatin1PayloadSlow(ref payload, length, current);
    }

    /// <summary>Decodes or reuses a validated nonempty Latin-1 payload outside inlined record sites.</summary>
    /// <param name="payload">The first payload byte.</param>
    /// <param name="length">The positive character count.</param>
    /// <param name="current">The immutable storage available for reuse.</param>
    /// <returns>The existing equal string or a fresh one.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ReadLatin1PayloadSlow(ref byte payload, int length, string? current)
    {
#if NET8_0_OR_GREATER
        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(length - 17) <= 47 && (length <= 32 || Vector256.IsHardwareAccelerated))
        {
            if (current is not null && current.Length == length &&
                Latin1Medium(ref payload, ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(current.AsSpan())), length, compare: true))
                return current;
            string result = FastString.Allocate(length);
            Latin1Medium(ref payload, ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in result.GetPinnableReference())), length, compare: false);
            return result;
        }
#endif
        return ReadLatin1PayloadCore(ref payload, length, current);
    }

#if NET8_0_OR_GREATER
    /// <summary>Widens or compares 17 to 64 Latin-1 bytes with a leading and an overlapping trailing block.</summary>
    /// <param name="source">The first of <paramref name="length"/> readable payload bytes.</param>
    /// <param name="chars">The first character of the candidate or of the unpublished destination string.</param>
    /// <param name="length">The character count, from 17 to 32, or up to 64 when 256-bit vectors are accelerated.</param>
    /// <param name="compare">Whether to compare with <paramref name="chars"/> instead of storing into it.</param>
    /// <returns>Whether every character equals its widened byte; always <see langword="true"/> when storing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Latin1Medium(ref byte source, ref ushort chars, int length, bool compare)
    {
        if (length <= 32)
        {
            nuint back = (nuint)(uint)(length - 16);
            var (a, b) = Vector128.Widen(Vector128.LoadUnsafe(ref source));
            var (c, d) = Vector128.Widen(Vector128.LoadUnsafe(ref source, back));
            if (compare)
                return ((a ^ Vector128.LoadUnsafe(ref chars)) | (b ^ Vector128.LoadUnsafe(ref chars, 8)) |
                    (c ^ Vector128.LoadUnsafe(ref chars, back)) | (d ^ Vector128.LoadUnsafe(ref chars, back + 8))) == Vector128<ushort>.Zero;
            a.StoreUnsafe(ref chars);
            b.StoreUnsafe(ref chars, 8);
            c.StoreUnsafe(ref chars, back);
            d.StoreUnsafe(ref chars, back + 8);
            return true;
        }

        nuint tail = (nuint)(uint)(length - 32);
        var (e, f) = Vector256.Widen(Vector256.LoadUnsafe(ref source));
        var (g, h) = Vector256.Widen(Vector256.LoadUnsafe(ref source, tail));
        if (compare)
            return ((e ^ Vector256.LoadUnsafe(ref chars)) | (f ^ Vector256.LoadUnsafe(ref chars, 16)) |
                (g ^ Vector256.LoadUnsafe(ref chars, tail)) | (h ^ Vector256.LoadUnsafe(ref chars, tail + 16))) == Vector256<ushort>.Zero;
        e.StoreUnsafe(ref chars);
        f.StoreUnsafe(ref chars, 16);
        g.StoreUnsafe(ref chars, tail);
        h.StoreUnsafe(ref chars, tail + 16);
        return true;
    }
#endif

    /// <summary>Decodes or reuses a validated nonempty Latin-1 payload.</summary>
    /// <param name="payload">The first payload byte.</param>
    /// <param name="length">The positive character count.</param>
    /// <param name="current">The immutable storage available for reuse.</param>
    /// <returns>The existing equal string or a fresh one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ReadLatin1PayloadCore(ref byte payload, int length, string? current) =>
        current is not null && current.Length == length && MatchesLatin1(ref payload, current) ? current : CreateLatin1String(ref payload, length);

    /// <summary>Allocates a string from a validated Latin-1 payload.</summary>
    /// <param name="payload">The first payload byte.</param>
    /// <param name="length">The positive character count, equal to the payload width.</param>
    /// <returns>A fresh string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string CreateLatin1String(ref byte payload, int length)
    {
#if NET8_0_OR_GREATER
        string result = FastString.Allocate(length);
        WidenLatin1(ref payload, ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in result.GetPinnableReference())), length);
        return result;
#else
        string result = new string('\0', length);
        fixed (char* destination = result)
        {
#if NETSTANDARD2_1
            if (LuminPackBurst.TryWidenLatin1(ref payload, ref *destination, length)) return result;
#endif
            for (int i = 0; i < length; i++)
                destination[i] = (char)Unsafe.Add(ref payload, (nint)(uint)i);
        }

        return result;
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>Widens a Latin-1 payload into an unpublished string.</summary>
    /// <param name="source">The first payload byte.</param>
    /// <param name="destination">The first character of the string.</param>
    /// <param name="length">The positive character count.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WidenLatin1(ref byte source, ref ushort destination, int length)
    {
        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && length >= 8)
        {
            if (length > 16)
            {
                WidenLatin1Long(ref source, ref destination, (nuint)(uint)length);
                return;
            }

            // 8 to 16 bytes: the first eight and the last eight.
            var first = Vector128.CreateScalarUnsafe(LuminPackReader.Read<ulong>(ref source)).AsByte();
            var last = Vector128.CreateScalarUnsafe(LuminPackReader.Read<ulong>(ref Unsafe.Add(ref source, (nint)(uint)(length - 8)))).AsByte();
            Vector128.WidenLower(first).StoreUnsafe(ref destination);
            Vector128.WidenLower(last).StoreUnsafe(ref destination, (nuint)(uint)(length - 8));
            return;
        }

        if (Unsafe.SizeOf<IntPtr>() == 8 && (uint)(length - 4) < 4)
        {
            // 4 to 7 bytes: the first four and the last four.
            ulong head = WidenLatin1(LuminPackReader.Read<uint>(ref source));
            ulong tail = WidenLatin1(LuminPackReader.Read<uint>(ref Unsafe.Add(ref source, (nint)(uint)(length - 4))));
            LuminPackWriter.Write(ref Unsafe.As<ushort, byte>(ref destination), head);
            LuminPackWriter.Write(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destination, (nint)(uint)(length - 4))), tail);
            return;
        }

        for (int i = 0; i < length; i++)
            Unsafe.Add(ref destination, (nint)(uint)i) = Unsafe.Add(ref source, (nint)(uint)i);
    }

    /// <summary>Widens four Latin-1 bytes to four characters.</summary>
    /// <param name="bytes">Four bytes in order.</param>
    /// <returns>The four characters in order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong WidenLatin1(uint bytes)
    {
        ulong value = bytes;
        value = (value | (value << 16)) & 0x0000FFFF0000FFFFUL;
        return (value | (value << 8)) & 0x00FF00FF00FF00FFUL;
    }

    /// <summary>Widens more than sixteen Latin-1 bytes.</summary>
    /// <param name="source">The first payload byte.</param>
    /// <param name="destination">The first character of the string.</param>
    /// <param name="length">The character count, above sixteen.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WidenLatin1Long(ref byte source, ref ushort destination, nuint length)
    {
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && length >= 32)
        {
            while (true)
            {
                nuint at = i + 32 <= length ? i : length - 32;
                var (lower, upper) = Vector256.Widen(Vector256.LoadUnsafe(ref source, at));
                lower.StoreUnsafe(ref destination, at);
                upper.StoreUnsafe(ref destination, at + 16);
                if (at + 32 == length)
                    return;
                i = at + 32;
            }
        }

        while (true)
        {
            nuint at = i + 16 <= length ? i : length - 16;
            var (lower, upper) = Vector128.Widen(Vector128.LoadUnsafe(ref source, at));
            lower.StoreUnsafe(ref destination, at);
            upper.StoreUnsafe(ref destination, at + 8);
            if (at + 16 == length)
                return;
            i = at + 16;
        }
    }
#endif

    /// <summary>Compares a validated Latin-1 payload with a same-length candidate.</summary>
    /// <param name="source">The first of <c>text.Length</c> readable payload bytes.</param>
    /// <param name="text">The nonempty candidate string.</param>
    /// <returns>Whether every character equals its widened byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchesLatin1(ref byte source, string text)
    {
#if NETSTANDARD2_1
        if (LuminPackBurst.TryLatin1Equal(ref source, text.AsSpan(), out bool equal)) return equal;
#endif
        int length = text.Length;
        ref ushort chars = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(text.AsSpan()));
#if NET8_0_OR_GREATER
        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && length >= 8)
        {
            if (length > 16)
                return MatchesLatin1Long(ref source, ref chars, (nuint)(uint)length);
            var first = Vector128.WidenLower(Vector128.CreateScalarUnsafe(LuminPackReader.Read<ulong>(ref source)).AsByte());
            var last = Vector128.WidenLower(Vector128.CreateScalarUnsafe(LuminPackReader.Read<ulong>(ref Unsafe.Add(ref source, (nint)(uint)(length - 8)))).AsByte());
            return Vector128.EqualsAll(first, Vector128.LoadUnsafe(ref chars)) &&
                Vector128.EqualsAll(last, Vector128.LoadUnsafe(ref chars, (nuint)(uint)(length - 8)));
        }

        if (Unsafe.SizeOf<IntPtr>() == 8 && (uint)(length - 4) < 4)
        {
            // 4 to 7 characters: the first four and the last four.
            ref byte code = ref Unsafe.As<ushort, byte>(ref chars);
            return ((WidenLatin1(LuminPackReader.Read<uint>(ref source)) ^ LuminPackReader.Read<ulong>(ref code)) |
                (WidenLatin1(LuminPackReader.Read<uint>(ref Unsafe.Add(ref source, (nint)(uint)(length - 4)))) ^
                LuminPackReader.Read<ulong>(ref Unsafe.Add(ref code, (nint)(uint)((length - 4) * sizeof(char)))))) == 0;
        }
#endif
        for (int i = 0; i < length; i++)
        {
            if (Unsafe.Add(ref chars, (nint)(uint)i) != Unsafe.Add(ref source, (nint)(uint)i))
                return false;
        }

        return true;
    }

#if NET8_0_OR_GREATER
    /// <summary>Compares more than sixteen Latin-1 bytes with their candidate characters.</summary>
    /// <param name="source">The first payload byte.</param>
    /// <param name="chars">The first candidate character.</param>
    /// <param name="length">The character count, above sixteen.</param>
    /// <returns>Whether every character equals its widened byte.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MatchesLatin1Long(ref byte source, ref ushort chars, nuint length)
    {
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && length >= 32)
        {
            while (true)
            {
                nuint at = i + 32 <= length ? i : length - 32;
                var (lower, upper) = Vector256.Widen(Vector256.LoadUnsafe(ref source, at));
                if (((lower ^ Vector256.LoadUnsafe(ref chars, at)) | (upper ^ Vector256.LoadUnsafe(ref chars, at + 16))) != Vector256<ushort>.Zero)
                    return false;
                if (at + 32 == length)
                    return true;
                i = at + 32;
            }
        }

        while (true)
        {
            nuint at = i + 16 <= length ? i : length - 16;
            var (lower, upper) = Vector128.Widen(Vector128.LoadUnsafe(ref source, at));
            if (((lower ^ Vector128.LoadUnsafe(ref chars, at)) | (upper ^ Vector128.LoadUnsafe(ref chars, at + 8))) != Vector128<ushort>.Zero)
                return false;
            if (at + 16 == length)
                return true;
            i = at + 16;
        }
    }
#endif
}
