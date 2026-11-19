using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Core;
using System.Buffers;
using System.Text;
#if NET8_0_OR_GREATER
using System.Text.Unicode;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Option;

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

#endif
namespace LuminPack.Utility;
/// <summary>Provides serialize mode functionality.</summary>
public enum SerializeMode : byte
{
    /// <summary>Specifies utf8.</summary>
    Utf8,
    /// <summary>Specifies utf16.</summary>
    Utf16
}

/// <summary>
/// Bounded UTF-16/UTF-8 transcoder with ASCII, mixed Latin1, two-byte and BMP SIMD paths.
/// Other input uses the platform transcoder when it can preserve buffering and replacement semantics.
/// Portable and bounded fallback: netstandard2.1 with 64-bit SWAR ASCII paths; SIMD: NET8_0_OR_GREATER.
/// </summary>
/// <remarks>UTF-16 offsets count code units and UTF-8 offsets count bytes.</remarks>
public static partial class StringSerializer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Checks a complete UTF-8 token without replacement decoding.</summary>
    /// <param name="source">The complete encoded token.</param>
    /// <returns>Whether every byte belongs to a valid Unicode scalar.</returns>
    internal static bool IsValidUtf8(ReadOnlySpan<byte> source)
    {
#if NET8_0_OR_GREATER
        return Utf8.IsValid(source);
#else
        try
        {
            StrictUtf8.GetCharCount(source);
            return true; }
        catch (DecoderFallbackException)
        {
            return false; }
#endif
    }
    /// <summary>Counts UTF-16 code units while validating a complete UTF-8 payload without decoding to storage.</summary>
    /// <param name="source">The bounded payload.</param>
    /// <returns>The exact UTF-16 character count.</returns>
    internal static int GetStrictUtf8CharacterCount(ReadOnlySpan<byte> source)
    {
        try
        {
            return StrictUtf8.GetCharCount(source);
        }
        catch (DecoderFallbackException)
        {
            LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
            return 0;
        }
    }

    /// <summary>Materializes validated native UTF-16 code units without a decoder or temporary character buffer.</summary>
    /// <param name = "source">The complete even-length payload.</param>
    /// <returns>The decoded string.</returns>
    internal static string DeserializeUtf16(ReadOnlySpan<byte> source) => source.IsEmpty ? string.Empty : CreateUtf16String(ref MemoryMarshal.GetReference(source), source.Length);

    /// <summary>Allocates and fills a string from a validated, nonempty, even-length UTF-16 payload.</summary>
    /// <param name = "payload">The first payload byte.</param>
    /// <param name = "bytes">The positive even payload length.</param>
    /// <returns>A fresh string containing the exact code units.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string CreateUtf16String(ref byte payload, int bytes) => CreateUtf16StringCore(ref payload, bytes);

    /// <summary>Allocates and fills a string from a validated, nonempty, even-length UTF-16 payload inside an outlined caller.</summary>
    /// <param name = "payload">The first payload byte.</param>
    /// <param name = "bytes">The positive even payload length.</param>
    /// <returns>A fresh string containing the exact code units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string CreateUtf16StringCore(ref byte payload, int bytes)
    {
#if NET8_0_OR_GREATER
        // Allocate without the span constructor's separate copy call; the exact overlapping lanes
        // below write every code unit before the string escapes.
        string result = FastString.Allocate(bytes >> 1);
        SerializeUtf16(ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in result.GetPinnableReference())), ref payload, (uint)bytes);
        return result;
#else
        return new string (MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, char>(ref payload), bytes / sizeof(char)));
#endif
    }
#if NET8_0_OR_GREATER
    /// <summary>Performs the decode ascii string step for the current operation.</summary>
    /// <param name = "destination">The destination receiving the result.</param>
    /// <param name = "src">The first byte of the validated ASCII input.</param>
    /// <param name = "length">The complete ASCII length.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DecodeAsciiString(Span<char> destination, ref byte src, int length)
    {
        ref byte dst = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(destination));
        uint i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i + 64 <= length; i += 64)
            {
                var data = Vector512.LoadUnsafe(ref Unsafe.Add(ref src, (nint)(uint)(i)));
                Vector512<ushort> lo = Vector512.WidenLower(data);
                Vector512<ushort> hi = Vector512.WidenUpper(data);
                lo.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)(uint)((nint)(i << 1)))));
                hi.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)(uint)((nint)((i + 32) << 1)))));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i + 32 <= length; i += 32)
            {
                var data = Vector256.LoadUnsafe(ref Unsafe.Add(ref src, (nint)(uint)(i)));
                Vector256<ushort> lo = Vector256.WidenLower(data);
                Vector256<ushort> hi = Vector256.WidenUpper(data);
                lo.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)(uint)((nint)(i << 1)))));
                hi.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)(uint)((nint)((i + 16) << 1)))));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i + 16 <= length; i += 16)
            {
                var data = Vector128.LoadUnsafe(ref Unsafe.Add(ref src, (nint)(uint)(i)));
                Vector128<ushort> lo = Vector128.WidenLower(data);
                Vector128<ushort> hi = Vector128.WidenUpper(data);
                lo.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)(uint)((nint)(i << 1)))));
                hi.StoreUnsafe(ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref dst, (nint)(uint)((nint)((i + 8) << 1)))));
            }
        }

        for (; i < length; i++)
        {
            LuminPackWriter.Write(ref Unsafe.Add(ref dst, (nint)(uint)((nint)(i << 1))), Unsafe.Add(ref src, (nint)(uint)((nint)i)));
        }
    }

#endif
#if NET8_0_OR_GREATER
    /// <summary>Reads one to three ASCII bytes without pinning input or preparing a decoder callback.</summary>
    /// <param name = "source">The validated payload's first byte.</param>
    /// <param name = "length">The payload length, from one through three bytes.</param>
    /// <returns>A fresh decoded string, or null when the validating UTF-8 path is required.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string? ReadTinyAsciiString(ref byte source, int length)
    {
        uint first = source;
        if (first >= 0x80)
            return null;
        if (length == 1)
        {
            string single = FastString.Allocate(1);
            Unsafe.AsRef(in single.GetPinnableReference()) = (char)first;
            return single;
        }
        uint last = Unsafe.Add(ref source, (nint)(uint)(length - 1));
        uint middle = length == 3 ? Unsafe.Add(ref source, 1) : 0u;
        if ((last | middle) >= 0x80)
            return null;
        string result = FastString.Allocate(length);
        ref char output = ref Unsafe.AsRef(in result.GetPinnableReference());
        output = (char)first;
        Unsafe.Add(ref output, (nint)(uint)(length - 1)) = (char)last;
        if (length == 3)
            Unsafe.Add(ref output, 1) = (char)middle;
        return result;
    }

#endif
#if NET8_0_OR_GREATER
    /// <summary>Reads short ascii string from the current input.</summary>
    /// <param name = "source">The input data to process.</param>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string? ReadShortAsciiString(ref byte source, int length)
    {
        if (length < 8)
        {
            var shortBytes = Vector128.Create(LuminPackReader.Read<uint>(ref source), LuminPackReader.Read<uint>(ref Unsafe.Add(ref source, (nint)(uint)(length - 4))), 0u, 0u).AsByte();
            if (shortBytes.ExtractMostSignificantBits() != 0)
                return null;
            string shortResult = FastString.Allocate(length);
            ref ushort shortOutput = ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in shortResult.GetPinnableReference()));
            var chars = Vector128.WidenLower(shortBytes);
            LuminPackWriter.Write(ref Unsafe.As<ushort, byte>(ref shortOutput), chars.GetLower());
            LuminPackWriter.Write(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref shortOutput, (nint)(uint)(length - 4))), chars.GetUpper());
            return shortResult;
        }

        // Scalar lane construction avoids stack staging on 64-bit. Retain
        // the existing vector-load path on 32-bit runtimes.
        var bytes = Unsafe.SizeOf<IntPtr>() == 8 ? Vector128.Create(LuminPackReader.Read<ulong>(ref source), LuminPackReader.Read<ulong>(ref Unsafe.Add(ref source, (nint)(uint)(length - 8)))).AsByte() : Vector128.Create(Vector64.LoadUnsafe(ref source), Vector64.LoadUnsafe(ref source, (nuint)(length - 8)));
        // A matching byte/char count is only a hint; invalid UTF-8 must still
        // go through the validating decoder. No string is published yet.
        if (bytes.ExtractMostSignificantBits() != 0)
            return null;
        // This fresh, non-interned string is filled before it escapes. Managed
        // references remain GC-tracked, so no input pin or callback is needed.
        string result = FastString.Allocate(length);
        ref ushort output = ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in result.GetPinnableReference()));
        Vector128.WidenLower(bytes).StoreUnsafe(ref output);
        Vector128.WidenUpper(bytes).StoreUnsafe(ref output, (nuint)(length - 8));
        return result;
    }

#endif
    /// <summary>Compares canonical UTF-8 scalar values against UTF-16 without temporary storage.</summary>
    /// <param name = "bytes">The complete, bounds-validated UTF-8 payload.</param>
    /// <param name = "text">The candidate UTF-16 text.</param>
    /// <returns>Whether valid UTF-8 decodes to exactly the candidate text.</returns>
    internal static bool MatchesUtf8(ReadOnlySpan<byte> bytes, string text)
    {
#if NETSTANDARD2_1
        if (LuminPackBurst.TryUtf8Equal(bytes, text.AsSpan(), out bool equal))
            return equal;
#endif
#if NET8_0_OR_GREATER
        if (bytes.Length == text.Length)
            return Ascii.Equals(bytes, text.AsSpan());
        if (Unsafe.SizeOf<IntPtr>() == 8 && Sse2.IsSupported && text.Length >= 8 &&
            (uint)bytes.Length == (uint)text.Length * 2 && text[0] is >= '\u0080' and <= '\u07FF')
            return Utf8TwoByteComparer.Matches(bytes, text.AsSpan());
        if (bytes.Length > 64)
            return MatchesUtf8Chunks(bytes, text.AsSpan());
        return MatchesShortUtf8(bytes, text);
#else
        int position = 0;
        for (int i = 0; i < bytes.Length;)
        {
            uint scalar = bytes[i++];
            if (scalar >= 0x80)
            {
                int trailing;
                uint minimum;
                if (scalar >= 0xC2 && scalar <= 0xDF)
                {
                    trailing = 1;
                    minimum = 0x80;
                    scalar &= 0x1F;
                }
                else if (scalar >= 0xE0 && scalar <= 0xEF)
                {
                    trailing = 2;
                    minimum = 0x800;
                    scalar &= 0x0F;
                }
                else if (scalar >= 0xF0 && scalar <= 0xF4)
                {
                    trailing = 3;
                    minimum = 0x10000;
                    scalar &= 7;
                }
                else
                    return false;
                if (trailing > bytes.Length - i)
                    return false;
                for (int j = 0; j < trailing; j++)
                {
                    uint next = bytes[i++];
                    if ((next & 0xC0) != 0x80)
                        return false;
                    scalar = (scalar << 6) | (next & 0x3F);
                }

                if (scalar < minimum || scalar > 0x10FFFF || scalar >= 0xD800 && scalar <= 0xDFFF)
                    return false;
            }

            if (scalar >= 0x10000)
            {
                scalar -= 0x10000;
                if (position >= text.Length || text[position++] != (char)(0xD800 + (scalar >> 10)))
                    return false;
                scalar = 0xDC00 + (scalar & 0x3FF);
            }

            if (position >= text.Length || text[position++] != (char)scalar)
                return false;
        }

        return position == text.Length;
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>Compares a short equal-length ASCII record against the candidate text inside this assembly.</summary>
    /// <param name = "source">The first payload byte; the record is <c>text.Length</c> bytes long.</param>
    /// <param name = "text">The candidate immutable text whose length equals the record's byte count.</param>
    /// <returns>Whether every payload byte equals the matching character and every character is ASCII.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchesAsciiRecord(ref byte source, string text)
    {
        int length = text.Length;
        // Reading a payload window wider than the record would leave the validated range, so the
        // overlapping-window form below is used only where the two windows stay inside it.
        if (!Vector128.IsHardwareAccelerated || Unsafe.SizeOf<IntPtr>() != 8 || length < 4)
        {
            for (int i = 0; i < length; i++)
            {
                if (text[i] > 0x7F || (byte)text[i] != Unsafe.Add(ref source, (nint)(uint)(i)))
                    return false;
            }

            return true;
        }

        ref ushort characters = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(text.AsSpan()));
        if (length < 8)
        {
            // 4 to 7 characters: the first four and the last four cover the record.
            var chars = Vector128.Create(LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref characters)), LuminPackReader.Read<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref characters, (nint)(uint)(length - 4))))).AsUInt16();
            if ((chars & Vector128.Create((ushort)0xFF80)) != Vector128<ushort>.Zero)
                return false;
            ulong payload = LuminPackReader.Read<uint>(ref source) | ((ulong)LuminPackReader.Read<uint>(ref Unsafe.Add(ref source, (nint)(uint)(length - 4))) << 32);
            return NarrowValidatedLatin1(chars, chars).AsUInt64().GetElement(0) == payload;
        }

        // 8 to 16 characters: the first eight and the last eight cover the record.
        var first = Vector128.LoadUnsafe(ref characters);
        var last = Vector128.LoadUnsafe(ref characters, (nuint)(length - 8));
        if (((first | last) & Vector128.Create((ushort)0xFF80)) != Vector128<ushort>.Zero)
            return false;
        var record = Vector128.Create(LuminPackReader.Read<ulong>(ref source), LuminPackReader.Read<ulong>(ref Unsafe.Add(ref source, (nint)(uint)(length - 8)))).AsByte();
        return Vector128.EqualsAll(NarrowValidatedLatin1(first, last), record);
    }

#endif
#if NET8_0_OR_GREATER
    /// <summary>Compares a short UTF-16 length record against the candidate text inside this assembly.</summary>
    /// <param name = "source">The first payload byte; the record is <c>text.Length * 2</c> bytes long.</param>
    /// <param name = "text">The candidate immutable text whose code unit bytes are compared.</param>
    /// <returns>Whether every payload byte equals the matching UTF-16 code unit byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchesUtf16Record(ref byte source, string text)
    {
        int bytes = text.Length * sizeof(char);
        ref byte candidate = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(text.AsSpan()));
        // Reading a payload window wider than the record would leave the validated range, so the
        // overlapping-window form below is used only where both windows stay inside it.
        if (!Vector128.IsHardwareAccelerated || Unsafe.SizeOf<IntPtr>() != 8 || bytes < 16)
        {
            if (Unsafe.SizeOf<IntPtr>() == 8 && (uint)(bytes - 8) <= 8)
            {
                // 8 to 16 bytes: the first eight and the last eight cover every byte.
                return ((LuminPackReader.Read<ulong>(ref source) ^ LuminPackReader.Read<ulong>(ref candidate)) |
                    (LuminPackReader.Read<ulong>(ref Unsafe.Add(ref source, (nint)(uint)(bytes - 8))) ^ LuminPackReader.Read<ulong>(ref Unsafe.Add(ref candidate, (nint)(uint)(bytes - 8))))) == 0;
            }

            for (int i = 0; i < bytes; i++)
            {
                if (Unsafe.Add(ref source, (nint)(uint)(i)) != Unsafe.Add(ref candidate, (nint)(uint)(i)))
                    return false;
            }

            return true;
        }

        return MatchesShortUtf16(ref source, ref candidate, (uint)bytes);
    }

    /// <summary>Compares two validated UTF-16 byte ranges with overlapping 16-byte windows.</summary>
    /// <param name="source">The first record byte.</param>
    /// <param name="candidate">The first candidate byte.</param>
    /// <param name="length">The byte length of both ranges, between 16 and 32.</param>
    /// <returns>Whether all bytes match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesShortUtf16(ref byte source, ref byte candidate, uint length)
    {
        // 16 to 32 bytes: the first sixteen and the last sixteen bytes cover the record.
        var recordHead = Vector128.LoadUnsafe(ref source);
        var recordTail = Vector128.LoadUnsafe(ref source, length - 16);
        var candidateHead = Vector128.LoadUnsafe(ref candidate);
        var candidateTail = Vector128.LoadUnsafe(ref candidate, length - 16);
        return Vector128.EqualsAll(recordHead, candidateHead) && Vector128.EqualsAll(recordTail, candidateTail);
    }

#endif
#if NET8_0_OR_GREATER
    /// <summary>Compares a complete validated UTF-16 payload with immutable candidate storage.</summary>
    /// <param name="source">The first byte of exactly <c>text.Length * 2</c> readable bytes.</param>
    /// <param name="text">The same-length candidate.</param>
    /// <returns>Whether every original code unit matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchesUtf16Payload(ref byte source, string text)
    {
        int bytes = text.Length * sizeof(char);
        return MemoryMarshal.CreateReadOnlySpan(ref source, bytes).SequenceEqual(MemoryMarshal.AsBytes(text.AsSpan()));
    }

    /// <summary>Compares a short Unicode payload with one bounded platform decode instead of a scalar loop.</summary>
    /// <param name = "bytes">The complete payload of at most 64 bytes.</param>
    /// <param name = "text">The candidate UTF-16 text.</param>
    /// <returns>Whether valid UTF-8 decodes to exactly the candidate text; malformed input never matches.</returns>
    [SkipLocalsInit]
    private static bool MatchesShortUtf8(ReadOnlySpan<byte> bytes, string text)
    {
        // A UTF-8 payload never decodes to more UTF-16 code units than it has bytes.
        Span<char> decoded = stackalloc char[64];
        OperationStatus status = Utf8.ToUtf16(bytes, decoded, out int consumed, out int written, replaceInvalidSequences: false, isFinalBlock: true);
        return status == OperationStatus.Done && consumed == bytes.Length && written == text.Length &&
            decoded.Slice(0, written).SequenceEqual(text.AsSpan());
    }

    /// <summary>Compares longer Unicode payloads using bounded stack storage and the runtime transcoder.</summary>
    /// <param name = "bytes">The complete UTF-8 payload.</param>
    /// <param name = "text">The candidate UTF-16 text.</param>
    /// <returns>Whether every decoded code unit matches and both inputs finish together.</returns>
    internal static bool MatchesUtf8Chunks(ReadOnlySpan<byte> bytes, ReadOnlySpan<char> text)
    {
        Span<char> decoded = stackalloc char[256];
        while (!bytes.IsEmpty)
        {
            OperationStatus status = Deserialize<LuminPackUtf8LengthOptions>(bytes, decoded, out int consumed, out int written, replaceInvalidSequences: false, isFinalBlock: true);
            if (status is not (OperationStatus.Done or OperationStatus.DestinationTooSmall) || written > text.Length || !decoded.Slice(0, written).SequenceEqual(text.Slice(0, written)))
                return false;
            bytes = bytes.Slice(consumed);
            text = text.Slice(written);
        }

        return text.IsEmpty;
    }

#endif
    /// <summary>Decodes or reuses a complete UTF-16 record without exposing Reader state to allocation.</summary>
    /// <param name = "source">The current record header.</param>
    /// <param name = "available">The complete remaining input window.</param>
    /// <param name = "current">The immutable storage available for reuse.</param>
    /// <param name = "consumed">The validated complete record width.</param>
    /// <returns>The decoded string, or the existing equal string.</returns>
    /// <remarks>Outlined deliberately: generated roots inline only the cursor arithmetic around this call.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string? ReadUtf16Record(ref byte source, int available, string? current, out int consumed)
    {
        if (available < sizeof(int))
            LuminPackExceptionHelper.ThrowSpanOutOfRange(sizeof(int));
        LuminPackReader.Read(ref source, out int header);
        consumed = sizeof(int);
        if (header == -1)
            return null;
        if (header < 0)
            LuminPackExceptionHelper.ThrowInvalidStringLength(header);
        int bytes = Utf16RecordPayload(header);
        if ((uint)bytes > (uint)(available - sizeof(int)))
            LuminPackExceptionHelper.ThrowSpanOutOfRange((long)bytes + sizeof(int));
        consumed += bytes;
        if (bytes == 0)
            return string.Empty;
        ref byte payload = ref Unsafe.Add(ref source, sizeof(int));
        if ((header & 1) != 0)
            return ReadLatin1PayloadCore(ref payload, bytes, current);
        return ReadUtf16PayloadCore(ref payload, bytes, current);
    }

    /// <summary>Decodes a validated nonempty raw UTF-16 payload, retaining an equal string.</summary>
    /// <param name = "payload">The first payload byte.</param>
    /// <param name = "bytes">The positive even payload width.</param>
    /// <param name = "current">The immutable storage available for reuse.</param>
    /// <returns>The existing equal string or a fresh one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ReadUtf16Payload(ref byte payload, int bytes, string? current)
    {
#if NET8_0_OR_GREATER
        if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(bytes - 16) <= 16)
        {
            int length = bytes >> 1;
            if (current is not null && current.Length == length &&
                MatchesShortUtf16(ref payload, ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(current.AsSpan())), (uint)bytes))
                return current;
            string result = FastString.Allocate(length);
            ref byte destination = ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in result.GetPinnableReference()));
            CopyShortUtf16(ref destination, ref payload, (uint)bytes);
            return result;
        }
#endif
        return ReadOtherUtf16Payload(ref payload, bytes, current);
    }

    /// <summary>Decodes raw UTF-16 payloads outside the short vector path.</summary>
    /// <param name="payload">The first validated payload byte.</param>
    /// <param name="bytes">The positive even byte length.</param>
    /// <param name="current">The string available for reuse.</param>
    /// <returns>The decoded string.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ReadOtherUtf16Payload(ref byte payload, int bytes, string? current) => ReadUtf16PayloadCore(ref payload, bytes, current);

    /// <summary>Decodes or reuses a validated nonempty raw UTF-16 payload.</summary>
    /// <param name = "payload">The first payload byte.</param>
    /// <param name = "bytes">The positive even payload width.</param>
    /// <param name = "current">The immutable storage available for reuse.</param>
    /// <returns>The existing equal string or a fresh one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ReadUtf16PayloadCore(ref byte payload, int bytes, string? current)
    {
        if (current is not null && current.Length == bytes / sizeof(char))
        {
#if NET8_0_OR_GREATER
            bool equal = bytes <= 32 ? MatchesUtf16Record(ref payload, current) : MatchesUtf16Payload(ref payload, current);
#else
            var input = MemoryMarshal.CreateReadOnlySpan(ref payload, bytes);
            var candidate = MemoryMarshal.AsBytes(current.AsSpan());
            bool equal = LuminPackBurst.TryEqual(input, candidate, out bool nativeEqual) ? nativeEqual : input.SequenceEqual(candidate);
#endif
            if (equal)
                return current;
        }

        return CreateUtf16StringCore(ref payload, bytes);
    }

    /// <summary>Decodes a validated UTF-8 payload using its recorded character count and shared short-string paths.</summary>
    /// <param name = "source">The first encoded payload byte.</param>
    /// <param name = "length">The complete encoded byte count.</param>
    /// <param name = "utf16Length">The recorded UTF-16 character count.</param>
    /// <returns>The decoded string.</returns>
    internal static string DeserializeUtf8(ref byte source, int length, int utf16Length)
    {
#if NETSTANDARD2_1
        if (utf16Length > 0 && LuminPackBurst.HasUtf8Decode)
        {
            if ((uint)utf16Length > (uint)length)
                LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
            string native = new string('\0', utf16Length);
            if (LuminPackBurst.TryDecodeUtf8(MemoryMarshal.CreateReadOnlySpan(ref source, length),
                MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(native.AsSpan()), utf16Length), false, true,
                out int consumed, out int written, out OperationStatus status))
            {
                if (status != OperationStatus.Done || consumed != length || written != utf16Length)
                    LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
                return native;
            }
            return DeserializeUtf8AfterDecline(ref source, length, native);
        }
#endif
#if NETSTANDARD2_1
        // Unity Mono's implementation of the custom span decoder can report
        // InvalidData for valid four-byte UTF-8 scalar values (for example emoji).
        // The runtime decoder is correct on Unity and this branch is isolated to
        // the Unity-targeted netstandard2.1 build.
        var bytes = LuminPackMarshal.CreateReadOnlySpan(ref source, length);
        if (utf16Length <= 0)
            return Encoding.UTF8.GetString(bytes);
        if ((uint)utf16Length > (uint)length)
            LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
        try
        {
            string decoded = StrictUtf8.GetString(bytes);
            if (decoded.Length != utf16Length)
                LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
            return decoded;
        }
        catch (DecoderFallbackException)
        {
            LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
            return null!;
        }
#else
        if (utf16Length <= 0)
        {
            var src = MemoryMarshal.CreateReadOnlySpan(ref source, length);
            return Encoding.UTF8.GetString(src);
        }
        else
        {
            if ((uint)utf16Length > (uint)length)
                LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
            if (utf16Length == length)
            {
                if ((uint)(length - 1) <= 2)
                {
                    var tinyString = ReadTinyAsciiString(ref source, length);
                    if (tinyString is not null)
                        return tinyString;
                }

                if (Vector128.IsHardwareAccelerated && (uint)(length - 4) <= 28)
                {
                    var shortString = length <= 16 ? ReadShortAsciiString(ref source, length) : ShortAsciiTranscoder.TryDecodeMedium(ref source, length);
                    if (shortString is not null)
                        return shortString;
                }

                if (Vector256.IsHardwareAccelerated && length > 32)
                {
                    // An equal byte/char count is ASCII or malformed. Validate while widening; the unpublished
                    // string is discarded when the validating decoder must report malformed input.
                    string widened = FastString.Allocate(length);
                    if (TryWidenAscii(ref source, length, ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in widened.GetPinnableReference()))))
                        return widened;
                }

                if (Ascii.IsValid(MemoryMarshal.CreateReadOnlySpan(ref source, length)))
                {
                    string ascii = FastString.Allocate(length);
                    DecodeAsciiString(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in ascii.GetPinnableReference()), length), ref source, length);
                    return ascii;
                }
            }

            // Decode straight into the unpublished string; the strict status and count check matches the former callback.
            string decoded = FastString.Allocate(utf16Length);
            var status = Utf8.ToUtf16(MemoryMarshal.CreateReadOnlySpan(ref source, length),
                MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in decoded.GetPinnableReference()), utf16Length), out _, out int charsWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done || charsWritten != utf16Length)
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            return decoded;
        }
#endif
    }

    /// <summary>Copies an already reserved UTF-16 payload using exact overlapping short lanes.</summary>
    /// <param name = "destination">The first reserved destination byte.</param>
    /// <param name = "source">The first immutable character byte.</param>
    /// <param name = "length">The complete even payload length.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SerializeUtf16(ref byte destination, ref byte source, uint length)
    {
#if NETSTANDARD2_1
        if (Unsafe.SizeOf<IntPtr>() == 8 && length >= 16 && LuminPackBurst.TryCopy(ref destination, ref source, length))
            return;
#endif
        if (Unsafe.SizeOf<IntPtr>() == 8 && length <= 64)
        {
#if NET8_0_OR_GREATER
            if (global::System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated && length >= 32)
            {
                LuminPackWriter.Write(ref destination, LuminPackReader.Read<global::System.Runtime.Intrinsics.Vector256<byte>>(ref source));
                LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)((nint)(length - 32))), LuminPackReader.Read<global::System.Runtime.Intrinsics.Vector256<byte>>(ref Unsafe.Add(ref source, (nint)(uint)((nint)(length - 32)))));
                return;
            }

            if (length >= 16 && length <= 32)
            {
                CopyShortUtf16(ref destination, ref source, length);
                return;
            }

#endif
            if (length > 16)
            {
                LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, length);
                return;
            }

            if (length >= 8)
            {
                LuminPackWriter.Write(ref destination, LuminPackReader.Read<ulong>(ref source));
                LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)((nint)(length - 8))), LuminPackReader.Read<ulong>(ref Unsafe.Add(ref source, (nint)(uint)((nint)(length - 8)))));
            }
            else if (length >= 4)
            {
                LuminPackWriter.Write(ref destination, LuminPackReader.Read<uint>(ref source));
                LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)((nint)(length - 4))), LuminPackReader.Read<uint>(ref Unsafe.Add(ref source, (nint)(uint)((nint)(length - 4)))));
            }
            else if (length != 0)
                LuminPackWriter.Write(ref destination, LuminPackReader.Read<ushort>(ref source));
            return;
        }

        LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, length);
    }

#if NET8_0_OR_GREATER
    /// <summary>Copies two bounded, potentially overlapping 16-byte windows on a 64-bit target.</summary>
    /// <param name="destination">The first output byte.</param>
    /// <param name="source">The first input byte.</param>
    /// <param name="length">The validated byte length, between 16 and 32.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyShortUtf16(ref byte destination, ref byte source, uint length)
    {
        nuint tail = length - 16;
        Vector128.LoadUnsafe(ref source).StoreUnsafe(ref destination);
        Vector128.LoadUnsafe(ref source, tail).StoreUnsafe(ref destination, tail);
    }
#endif

    /// <summary>Serializes the supplied value using the selected format and options.</summary>
    /// <param name = "source">The input data to process.</param>
    /// <param name = "destination">The destination receiving the result.</param>
    /// <param name = "charsRead">Receives the chars read produced by the operation.</param>
    /// <param name = "bytesWritten">Receives the bytes written produced by the operation.</param>
    /// <param name = "mode">The mode used by this operation.</param>
    /// <param name = "replaceInvalidSequences">The replace invalid sequences used by this operation.</param>
    /// <param name = "isFinalBlock">The is final block used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OperationStatus Serialize(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, SerializeMode mode = SerializeMode.Utf16, bool replaceInvalidSequences = true, bool isFinalBlock = true) => mode == SerializeMode.Utf8 ? Serialize<LuminPackUtf8LengthOptions>(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock) : Serialize<LuminPackUtf16LengthOptions>(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);
    /// <summary>Encodes characters using a stateless profile whose encoding can be specialized by the runtime compiler.</summary>
    /// <typeparam name = "TOptions">The stateless profile selecting UTF-8 or UTF-16.</typeparam>
    /// <param name = "source">The characters to encode.</param>
    /// <param name = "destination">The bounded destination for encoded bytes.</param>
    /// <param name = "charsRead">Receives the number of consumed characters.</param>
    /// <param name = "bytesWritten">Receives the number of bytes written.</param>
    /// <param name = "replaceInvalidSequences">Whether malformed UTF-16 is replaced when encoding UTF-8.</param>
    /// <param name = "isFinalBlock">Whether the source contains the final input block.</param>
    /// <returns>The conversion status, including partial-buffer and streaming results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OperationStatus Serialize<TOptions>(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
        where TOptions : struct, ILuminPackOptions => default(TOptions).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);
    /// <summary>Deserializes the supplied input using the selected format and options.</summary>
    /// <param name = "source">The input data to process.</param>
    /// <param name = "destination">The destination receiving the result.</param>
    /// <param name = "bytesRead">Receives the bytes read produced by the operation.</param>
    /// <param name = "charsWritten">Receives the chars written produced by the operation.</param>
    /// <param name = "mode">The mode used by this operation.</param>
    /// <param name = "replaceInvalidSequences">The replace invalid sequences used by this operation.</param>
    /// <param name = "isFinalBlock">The is final block used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OperationStatus Deserialize(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, SerializeMode mode = SerializeMode.Utf16, bool replaceInvalidSequences = true, bool isFinalBlock = true) => mode == SerializeMode.Utf8 ? Deserialize<LuminPackUtf8LengthOptions>(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock) : Deserialize<LuminPackUtf16LengthOptions>(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);
    /// <summary>Decodes bytes using a stateless profile whose encoding can be specialized by the runtime compiler.</summary>
    /// <typeparam name = "TOptions">The stateless profile selecting UTF-8 or UTF-16.</typeparam>
    /// <param name = "source">The encoded bytes to consume.</param>
    /// <param name = "destination">The bounded destination for decoded characters.</param>
    /// <param name = "bytesRead">Receives the number of consumed bytes.</param>
    /// <param name = "charsWritten">Receives the number of characters written.</param>
    /// <param name = "replaceInvalidSequences">Whether malformed UTF-8 is replaced when decoding UTF-8.</param>
    /// <param name = "isFinalBlock">Whether the source contains the final input block.</param>
    /// <returns>The conversion status, including partial-buffer and streaming results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OperationStatus Deserialize<TOptions>(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
        where TOptions : struct, ILuminPackOptions =>
        default(TOptions).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <summary>Decodes bytes using a stateless profile whose encoding can be specialized by the runtime compiler.</summary>
    /// <param name = "source">The encoded bytes to consume.</param>
    /// <param name = "destination">The bounded destination for decoded characters.</param>
    /// <param name = "bytesRead">Receives the number of consumed bytes.</param>
    /// <param name = "charsWritten">Receives the number of characters written.</param>
    /// <param name = "replaceInvalidSequences">Whether malformed UTF-8 is replaced when decoding UTF-8.</param>
    /// <param name = "isFinalBlock">Whether the source contains the final input block.</param>
    /// <returns>The conversion status, including partial-buffer and streaming results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OperationStatus DecodeUtf8(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
    {
#if NET8_0_OR_GREATER
        return Utf8.ToUtf16(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);
#else
        return DeserializeFromUtf8(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);
#endif
    }

    /// <summary>Performs the prefer custom simd step for the current operation.</summary>
    /// <param name = "source">The input data to process.</param>
    /// <param name = "destinationLength">The destination length used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PreferCustomSimd(ReadOnlySpan<char> source, int destinationLength)
    {
#if NET8_0_OR_GREATER
        if (!BitConverter.IsLittleEndian || !Ssse3.IsSupported || source.Length < 32 || destinationLength < 24)
            return false;
        uint first = source[0];
        if (first < 0x80 || first >= 0xD800 && first <= 0xDFFF)
            return false;
        bool latin = first < 0x800;
        ref byte start = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(source));
        if (Avx2.IsSupported)
        {
            var chars = Unsafe.ReadUnaligned<Vector256<ushort>>(ref start);
            if (latin)
                return Avx2.MoveMask(Avx2.And(Avx2.CompareGreaterThan(chars.AsInt16(), Vector256.Create((short)127)), Avx2.CompareGreaterThan(Vector256.Create((short)2048), chars.AsInt16())).AsByte()) == -1;
            var high = chars & Vector256.Create((ushort)0xF800);
            var invalid = Vector256.Equals(high, Vector256<ushort>.Zero) | Vector256.Equals(high, Vector256.Create((ushort)0xD800));
            return Avx2.MoveMask(invalid.AsByte()) == 0;
        }

        return IsUniformUtf8Run(Unsafe.ReadUnaligned<Vector128<ushort>>(ref start), latin) && IsUniformUtf8Run(Unsafe.ReadUnaligned<Vector128<ushort>>(ref Unsafe.Add(ref start, 16)), latin);
#else
        return false;
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>Classifies eight bounded code units without a per-character category branch.</summary>
    /// <param name = "chars">The eight code units in the original sixteen-character dispatch sample.</param>
    /// <param name = "latin">Whether the sample must contain only two-byte UTF-8 characters.</param>
    /// <returns>Whether every code unit belongs to the selected homogeneous run.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUniformUtf8Run(Vector128<ushort> chars, bool latin)
    {
        if (latin)
            return Sse2.MoveMask(Sse2.And(Sse2.CompareGreaterThan(chars.AsInt16(), Vector128.Create((short)127)), Sse2.CompareGreaterThan(Vector128.Create((short)2048), chars.AsInt16())).AsByte()) == 0xFFFF;
        var high = chars & Vector128.Create((ushort)0xF800);
        var invalid = Vector128.Equals(high, Vector128<ushort>.Zero) | Vector128.Equals(high, Vector128.Create((ushort)0xD800));
        return Sse2.MoveMask(invalid.AsByte()) == 0;
    }

#endif
    /// <summary>Serializes the supplied value using the selected format and options.</summary>
    /// <param name = "source">The input data to process.</param>
    /// <param name = "destination">The destination receiving the result.</param>
    /// <param name = "charsRead">Receives the chars read produced by the operation.</param>
    /// <param name = "bytesWritten">Receives the bytes written produced by the operation.</param>
    /// <param name = "replaceInvalidSequences">The replace invalid sequences used by this operation.</param>
    /// <param name = "isFinalBlock">The is final block used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OperationStatus SerializeAsUtf8(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
    {
#if NETSTANDARD2_1
        if (LuminPackBurst.TryEncodeUtf8(source, destination, replaceInvalidSequences, isFinalBlock, out charsRead, out bytesWritten, out OperationStatus nativeStatus))
            return nativeStatus;
        if (LuminPackBurst.TryAsciiEncode(source, destination, escapeJson: false))
        {
            charsRead = bytesWritten = source.Length;
            return OperationStatus.Done;
        }
#endif
#if NET8_0_OR_GREATER
        if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(source, destination, escapeJson: false))
        {
            charsRead = source.Length;
            bytesWritten = source.Length;
            return OperationStatus.Done;
        }

        if ((uint)(source.Length - 1) < 3 && destination.Length >= source.Length && TryEncodeTinyAscii(source, destination))
        {
            charsRead = source.Length;
            bytesWritten = source.Length;
            return OperationStatus.Done;
        }

        if (source.Length > 32 && source[0] < 0x80 && Vector256.IsHardwareAccelerated)
            return SerializeAsciiRun(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

        // These inputs cannot enter a custom run. Keep their platform call in
        // the entry so they do not pay for the general encoder's stack frame.
        if (source.Length < 32 || source[0] < 0x80)
            return Utf8.FromUtf16(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);
#endif
        return SerializeAsUtf8Slow(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);
    }

#if NET8_0_OR_GREATER
    /// <summary>Encodes one to three ASCII characters with scalar stores.</summary>
    /// <param name = "source">The one- to three-character input.</param>
    /// <param name = "destination">A destination with room for every character.</param>
    /// <returns>Whether every character was ASCII and has been written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryEncodeTinyAscii(ReadOnlySpan<char> source, Span<byte> destination)
    {
        ref char input = ref MemoryMarshal.GetReference(source);
        uint first = input, middle = source.Length == 3 ? Unsafe.Add(ref input, 1) : 0u, last = Unsafe.Add(ref input, unchecked((nint)(uint)(source.Length - 1)));
        if ((first | middle | last) > 0x7F)
            return false;
        ref byte output = ref MemoryMarshal.GetReference(destination);
        output = (byte)first;
        Unsafe.Add(ref output, unchecked((nint)(uint)(source.Length - 1))) = (byte)last;
        if (source.Length == 3)
            Unsafe.Add(ref output, 1) = (byte)middle;
        return true;
    }

    /// <summary>Narrows a leading ASCII run in 32-character blocks and transcodes any remainder with the platform encoder.</summary>
    /// <param name = "source">The input, longer than 32 characters, whose first character is ASCII.</param>
    /// <param name = "destination">The bounded destination.</param>
    /// <param name = "charsRead">Receives the consumed character count.</param>
    /// <param name = "bytesWritten">Receives the produced byte count.</param>
    /// <param name = "replaceInvalidSequences">Whether malformed input is replaced.</param>
    /// <param name = "isFinalBlock">Whether this is the final input block.</param>
    /// <returns>The same status the platform encoder reports for the complete input.</returns>
    /// <remarks>ASCII encoding is context-free, so continuing from the first non-ASCII block is exact.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OperationStatus SerializeAsciiRun(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten,
        bool replaceInvalidSequences, bool isFinalBlock)
    {
        int length = source.Length;
        ref ushort input = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(source));
        ref byte output = ref MemoryMarshal.GetReference(destination);
        int limit = Math.Min(length, destination.Length);
        var nonAsciiBits = Vector256.Create((ushort)0xFF80);
        int index = 0;
        for (; index <= limit - 128; index += 64)
        {
            var a = Vector256.LoadUnsafe(ref input, (nuint)index);
            var b = Vector256.LoadUnsafe(ref input, (nuint)index + 16);
            var c = Vector256.LoadUnsafe(ref input, (nuint)index + 32);
            var d = Vector256.LoadUnsafe(ref input, (nuint)index + 48);
            if ((((a | b) | (c | d)) & nonAsciiBits) != Vector256<ushort>.Zero)
                break;
            NarrowValidatedLatin1(a, b).StoreUnsafe(ref output, (nuint)index);
            NarrowValidatedLatin1(c, d).StoreUnsafe(ref output, (nuint)index + 32);
        }
        for (; index <= limit - 32; index += 32)
        {
            var lower = Vector256.LoadUnsafe(ref input, (nuint)index);
            var upper = Vector256.LoadUnsafe(ref input, (nuint)index + 16);
            if (((lower | upper) & nonAsciiBits) != Vector256<ushort>.Zero)
                break;
            NarrowValidatedLatin1(lower, upper).StoreUnsafe(ref output, (nuint)index);
        }

        if (index < length && limit == length && length - index < 32)
        {
            // The overlapping final block rewrites already validated bytes with the same values.
            nuint tail = (nuint)(length - 32);
            var lower = Vector256.LoadUnsafe(ref input, tail);
            var upper = Vector256.LoadUnsafe(ref input, tail + 16);
            if (((lower | upper) & nonAsciiBits) == Vector256<ushort>.Zero)
            {
                NarrowValidatedLatin1(lower, upper).StoreUnsafe(ref output, tail);
                index = length;
            }
        }

        if (index == length)
        {
            charsRead = length;
            bytesWritten = length;
            return OperationStatus.Done;
        }

        var status = Utf8.FromUtf16(source.Slice(index), destination.Slice(index), out int read, out int written, replaceInvalidSequences, isFinalBlock);
        charsRead = index + read;
        bytesWritten = index + written;
        return status;
    }

    /// <summary>Narrows two eight-character vectors whose lanes have already been validated as Latin-1 or ASCII.</summary>
    /// <param name="lower">The first eight code units, all in 0..255.</param>
    /// <param name="upper">The next eight code units, all in 0..255.</param>
    /// <returns>The sixteen bytes in input order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<byte> NarrowValidatedLatin1(Vector128<ushort> lower, Vector128<ushort> upper)
    {
        if (Sse2.IsSupported)
            return Sse2.PackUnsignedSaturate(lower.AsInt16(), upper.AsInt16());
        return Vector128.Narrow(lower, upper);
    }

    /// <summary>Narrows two vectors whose lanes have already been validated as Latin-1 or ASCII.</summary>
    /// <param name="lower">The first sixteen code units, all in 0..255.</param>
    /// <param name="upper">The next sixteen code units, all in 0..255.</param>
    /// <returns>The thirty-two bytes in input order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<byte> NarrowValidatedLatin1(Vector256<ushort> lower, Vector256<ushort> upper)
    {
        // Preserve the wider platform lowering; AVX2 otherwise masks each input before truncating it.
        if (Avx512BW.IsSupported)
            return Vector256.Narrow(lower, upper);
        if (Avx2.IsSupported)
            return Avx2.Permute4x64(Avx2.PackUnsignedSaturate(lower.AsInt16(), upper.AsInt16()).AsUInt64(), 0xD8).AsByte();
        return Vector256.Narrow(lower, upper);
    }

    /// <summary>Validates and widens an ASCII payload of more than 32 bytes in one pass.</summary>
    /// <param name = "source">The first payload byte.</param>
    /// <param name = "length">The payload length, greater than 32.</param>
    /// <param name = "destination">The first UTF-16 code unit of an unpublished string of the same length.</param>
    /// <returns>Whether every byte was ASCII and every code unit has been written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryWidenAscii(ref byte source, int length, ref ushort destination)
    {
        int index = 0;
        for (; index <= length - 64; index += 64)
        {
            var first = Vector256.LoadUnsafe(ref source, (nuint)index);
            var second = Vector256.LoadUnsafe(ref source, (nuint)index + 32);
            if ((first | second).ExtractMostSignificantBits() != 0)
                return false;
            Vector256.WidenLower(first).StoreUnsafe(ref destination, (nuint)index);
            Vector256.WidenUpper(first).StoreUnsafe(ref destination, (nuint)index + 16);
            Vector256.WidenLower(second).StoreUnsafe(ref destination, (nuint)index + 32);
            Vector256.WidenUpper(second).StoreUnsafe(ref destination, (nuint)index + 48);
        }
        for (; index <= length - 32; index += 32)
        {
            var bytes = Vector256.LoadUnsafe(ref source, (nuint)index);
            if (bytes.ExtractMostSignificantBits() != 0)
                return false;
            Vector256.WidenLower(bytes).StoreUnsafe(ref destination, (nuint)index);
            Vector256.WidenUpper(bytes).StoreUnsafe(ref destination, (nuint)index + 16);
        }

        if (index < length)
        {
            nuint tail = (nuint)(length - 32);
            var bytes = Vector256.LoadUnsafe(ref source, tail);
            if (bytes.ExtractMostSignificantBits() != 0)
                return false;
            Vector256.WidenLower(bytes).StoreUnsafe(ref destination, tail);
            Vector256.WidenUpper(bytes).StoreUnsafe(ref destination, tail + 16);
        }

        return true;
    }

#endif
    /// <summary>Handles general transcoding outside the short ASCII entry's stack frame.</summary>
    /// <param name = "source">The characters remaining after the bounded short ASCII attempt.</param>
    /// <param name = "destination">The bounded byte destination.</param>
    /// <param name = "charsRead">Receives the consumed character count.</param>
    /// <param name = "bytesWritten">Receives the produced byte count.</param>
    /// <param name = "replaceInvalidSequences">Whether malformed input is replaced.</param>
    /// <param name = "isFinalBlock">Whether this is the final input block.</param>
    /// <returns>The bounded transcoding status.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OperationStatus SerializeAsUtf8Slow(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock)
    {
#if NET8_0_OR_GREATER
        // ASCII-leading input retains the platform path without mixed-Latin1 dispatch.
        if (source.IsEmpty || source[0] < 0x80)
            return Utf8.FromUtf16(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);
        // Preserve the contiguous two-byte and BMP kernels before considering
        // mixed Latin1 compaction, which does more work for homogeneous input.
        if (!PreferCustomSimd(source, destination.Length))
        {
            if (BitConverter.IsLittleEndian && Ssse3.IsSupported && source.Length >= 32 && source[0] is >= '\u0080' and <= '\u00FF')
                return SerializeLatin1AsUtf8(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);
            return Utf8.FromUtf16(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);
        }

#else
        // A final block with three bytes per code unit cannot exhaust the destination.
        // Keep strict prefix/status semantics while avoiding per-character output checks
        // and Encoding.UTF8's heap-allocated fallback buffers on malformed replacement input.
        // Smaller windows and streaming tails retain the bounded status loop below.
        if (isFinalBlock && source.Length <= destination.Length / 3)
        {
            return SerializeUtf8Complete(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences);
        }

#endif
#if !NET && !NETSTANDARD
        // The Burst source projector extracts this bounded fallback region.
        fixed (char* pinnedSource = source)
        {
#endif
        ref char input = ref MemoryMarshal.GetReference(source);
        ref byte output = ref MemoryMarshal.GetReference(destination);
        int inputIndex = 0;
        int outputIndex = 0;
        OperationStatus status;
        while (true)
        {
            status = TranscodeToUtf8(ref Unsafe.Add(ref input, (nint)(uint)inputIndex), source.Length - inputIndex,
                ref Unsafe.Add(ref output, (nint)(uint)outputIndex), destination.Length - outputIndex, out int consumed, out int written);
            inputIndex += consumed;
            outputIndex += written;
            if (status == OperationStatus.Done || status == OperationStatus.DestinationTooSmall)
                break;
            if (status == OperationStatus.NeedMoreData && !isFinalBlock)
                break;
            if (!replaceInvalidSequences)
            {
                status = OperationStatus.InvalidData;
                break;
            }

            if (destination.Length - outputIndex < 3)
            {
                status = OperationStatus.DestinationTooSmall;
                break;
            }

            // A malformed UTF-16 code unit consumes exactly one char. At the
            // end of a final block the only incomplete sequence is a lone high surrogate.
            inputIndex++;
            Unsafe.Add(ref output, (nint)(uint)outputIndex) = 0xEF;
            Unsafe.Add(ref output, (nint)(uint)(outputIndex + 1)) = 0xBF;
            Unsafe.Add(ref output, (nint)(uint)(outputIndex + 2)) = 0xBD;
            outputIndex += 3;
        }

        charsRead = inputIndex;
        bytesWritten = outputIndex;
        return status;
#if !NET && !NETSTANDARD
        }
#endif
    }

    /// <summary>Deserializes the supplied input using the selected format and options.</summary>
    /// <param name = "source">The input data to process.</param>
    /// <param name = "destination">The destination receiving the result.</param>
    /// <param name = "bytesRead">Receives the bytes read produced by the operation.</param>
    /// <param name = "charsWritten">Receives the chars written produced by the operation.</param>
    /// <param name = "replaceInvalidSequences">The replace invalid sequences used by this operation.</param>
    /// <param name = "isFinalBlock">The is final block used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    internal static OperationStatus DeserializeFromUtf8(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
    {
#if NETSTANDARD2_1
        if (LuminPackBurst.TryDecodeUtf8(source, destination, replaceInvalidSequences, isFinalBlock, out bytesRead, out charsWritten, out OperationStatus nativeStatus))
            return nativeStatus;
#endif
        ref byte input = ref MemoryMarshal.GetReference(source);
        ref char output = ref MemoryMarshal.GetReference(destination);
        int inputIndex = 0;
        int outputIndex = 0;
        OperationStatus status;
        while (true)
        {
            status = TranscodeToUtf16(ref Unsafe.Add(ref input, (nint)(uint)inputIndex), source.Length - inputIndex,
                ref Unsafe.Add(ref output, (nint)(uint)outputIndex), destination.Length - outputIndex, out int consumed, out int written);
            inputIndex += consumed;
            outputIndex += written;
            if (status is OperationStatus.Done or OperationStatus.DestinationTooSmall)
                break;
            if (status == OperationStatus.NeedMoreData && !isFinalBlock)
                break;
            if (!replaceInvalidSequences)
            {
                status = OperationStatus.InvalidData;
                break;
            }

            if (outputIndex == destination.Length)
            {
                status = OperationStatus.DestinationTooSmall;
                break;
            }

            int invalidLength = status == OperationStatus.NeedMoreData ? source.Length - inputIndex : GetInvalidUtf8SequenceLength(ref Unsafe.Add(ref input, (nint)(uint)inputIndex), source.Length - inputIndex);
            inputIndex += invalidLength;
            Unsafe.Add(ref output, (nint)(uint)outputIndex++) = '\uFFFD';
        }

        bytesRead = inputIndex;
        charsWritten = outputIndex;
        return status;
    }

    /// <summary>Serializes the supplied value using the selected format and options.</summary>
    /// <param name = "source">The input data to process.</param>
    /// <param name = "destination">The destination receiving the result.</param>
    /// <param name = "charsRead">Receives the chars read produced by the operation.</param>
    /// <param name = "bytesWritten">Receives the bytes written produced by the operation.</param>
    /// <param name = "replaceInvalidSequences">The replace invalid sequences used by this operation.</param>
    /// <param name = "isFinalBlock">The is final block used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    internal static OperationStatus SerializeAsUtf16(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
    {
        int count = Math.Min(source.Length, destination.Length / sizeof(char));
        int bytes = count * sizeof(char);
        if (bytes != 0)
            SerializeUtf16(ref MemoryMarshal.GetReference(destination), ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(source)), (uint)bytes);
        charsRead = count;
        bytesWritten = bytes;
        return count == source.Length ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
    }

    // Keep the original native-UTF16 odd-byte and chunk behavior for compatibility.
    /// <summary>Deserializes the supplied input using the selected format and options.</summary>
    /// <param name = "source">The input data to process.</param>
    /// <param name = "destination">The destination receiving the result.</param>
    /// <param name = "bytesRead">Receives the bytes read produced by the operation.</param>
    /// <param name = "charsWritten">Receives the chars written produced by the operation.</param>
    /// <param name = "replaceInvalidSequences">The replace invalid sequences used by this operation.</param>
    /// <param name = "isFinalBlock">The is final block used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    internal static OperationStatus DeserializeFromUtf16(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
    {
        bytesRead = 0;
        charsWritten = 0;
        if (source.Length % sizeof(char) != 0)
        {
            if (isFinalBlock)
            {
                if (replaceInvalidSequences && destination.Length > 0)
                {
                    destination[0] = '\uFFFD';
                    charsWritten = 1;
                }

                bytesRead = source.Length;
                return replaceInvalidSequences ? OperationStatus.Done : OperationStatus.InvalidData;
            }

            return OperationStatus.NeedMoreData;
        }

        int maxChars = Math.Min(source.Length / sizeof(char), destination.Length);
        int bytesToCopy = maxChars * sizeof(char);
        if (bytesToCopy > 0)
        {
            LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(destination)), ref MemoryMarshal.GetReference(source), (uint)bytesToCopy);
        }

        bytesRead = bytesToCopy;
        charsWritten = maxChars;
        if (source.Length > bytesToCopy)
        {
            return OperationStatus.DestinationTooSmall;
        }

        return OperationStatus.Done;
    }

    /// <summary>Performs the transcode to utf8 step for the current operation.</summary>
    /// <param name = "input">The first input element.</param>
    /// <param name = "inputLength">The input length used by this operation.</param>
    /// <param name = "output">The first output element.</param>
    /// <param name = "outputLength">The available output capacity.</param>
    /// <param name = "charsRead">Receives the number of consumed input elements.</param>
    /// <param name = "bytesWritten">Receives the number of written output elements.</param>
    /// <returns>The result of the operation.</returns>
    internal static OperationStatus TranscodeToUtf8(ref char input, int inputLength, ref byte output, int outputLength, out int charsRead, out int bytesWritten)
    {
        int inputIndex = 0;
        int outputIndex = 0;
#if NET8_0_OR_GREATER
        if (BitConverter.IsLittleEndian && inputLength >= 32 && outputLength >= 24)
        {
            // Long all-ASCII prefix: branch once, then copy 32 chars at a time.
            // A short or mixed string stays on the low-overhead portable path.
            if (Sse2.IsSupported && inputLength >= 128 && outputLength >= 128)
            {
                Vector128<ushort> first = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
                Vector128<ushort> mask = Vector128.Create((ushort)0xFF80);
                Vector128<short> initial = Sse2.CompareEqual(Sse2.And(first, mask).AsInt16(), Vector128<short>.Zero);
                if (Sse2.MoveMask(initial.AsByte()) == 0xFFFF)
                {
                    EncodeAsciiRun(ref input, ref output, inputLength, outputLength, ref inputIndex, ref outputIndex);
                }
            }

            // Dedicated CJK and two-byte kernels consume bounded homogeneous runs.
            // Re-entry is attempted only after a successful homogeneous run,
            // not for every character in general text.
            if (Ssse3.IsSupported)
            {
                while ((inputLength - inputIndex) >= 32 && (outputLength - outputIndex) >= 24)
                {
                    uint lead = Unsafe.Add(ref input, (nint)(uint)inputIndex);
                    if (lead >= 0x80 && (lead < 0x800 || IsBmp3(lead)))
                    {
                        if (TryEncodeVectorRun(ref input, ref output, inputLength, outputLength, ref inputIndex, ref outputIndex, lead))
                        {
                            // Sparse ASCII delimiter between large CJK runs.
                            // Only consume it here when the next vector-friendly
                            // run is plausibly of the same 3-byte class.
                            if ((inputLength - inputIndex) >= 33 && (outputLength - outputIndex) >= 25 && Unsafe.Add(ref input, (nint)(uint)inputIndex) < 0x80 && IsBmp3(Unsafe.Add(ref input, (nint)(uint)(inputIndex + 1))))
                            {
                                Unsafe.Add(ref output, (nint)(uint)outputIndex++) = (byte)Unsafe.Add(ref input, (nint)(uint)inputIndex++);
                                continue;
                            }
                        }
                    }

                    break;
                }
            }
        }

#endif
#if NET8_0_OR_GREATER
        // A homogeneous SIMD prefix can be followed by a large heterogeneous
        // tail (e.g. CJK followed by emoji). Do not punish the entire remainder
        // with the scalar loop. For >= 64 remaining UTF-16 code units, delegate
        // that tail to the native bounded transcoder. `false, false` preserves
        // this low-level method's InvalidData / NeedMoreData behavior; the
        // outer SerializeAsUtf8 loop performs replacement if requested.
        if (inputIndex != 0 && (inputLength - inputIndex) >= 64)
        {
#if NET
            var remainingInput = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref input, (nint)(uint)inputIndex), inputLength - inputIndex);
            var remainingOutput = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)outputIndex), outputLength - outputIndex);
#else
            // Preserve the Burst projection's native span construction.
            fixed (char* inputPointer = &input)
            fixed (byte* outputPointer = &output)
            {
            var remainingInput = new ReadOnlySpan<char>(inputPointer + inputIndex, inputLength - inputIndex);
            var remainingOutput = new Span<byte>(outputPointer + outputIndex, outputLength - outputIndex);
#endif
            OperationStatus nativeStatus = Utf8.FromUtf16(remainingInput, remainingOutput, out int nativeChars, out int nativeBytes,
                replaceInvalidSequences: false, isFinalBlock: false);
            charsRead = inputIndex + nativeChars;
            bytesWritten = outputIndex + nativeBytes;
            return nativeStatus;
#if !NET
            }
#endif
        }

#endif
        OperationStatus status = TranscodeToUtf8Scalar(ref Unsafe.Add(ref input, (nint)(uint)inputIndex), inputLength - inputIndex,
            ref Unsafe.Add(ref output, (nint)(uint)outputIndex), outputLength - outputIndex, out int consumed, out int written);
        charsRead = inputIndex + consumed;
        bytesWritten = outputIndex + written;
        return status;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Bounded ASCII runs use AVX-512BW, AVX2 or SSE2 with exact stores.
    /// No input over-read or output over-store is permitted.
    /// The remainder is handled by the shared portable implementation.
    /// </summary>
    /// <param name = "input">The input data to process.</param>
    /// <param name = "output">The destination receiving the result.</param>
    /// <param name="inputLength">The complete input character count.</param>
    /// <param name="outputLength">The complete output capacity in bytes.</param>
    /// <param name="inputIndex">The number of consumed characters.</param>
    /// <param name="outputIndex">The number of written bytes.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EncodeAsciiRun(ref char input, ref byte output, int inputLength, int outputLength, ref int inputIndex, ref int outputIndex)
    {
        if (Avx512BW.IsSupported)
            EncodeVector512Run(ref input, ref output, inputLength, outputLength, ref inputIndex, ref outputIndex, 0);
        if (Avx2.IsSupported)
        {
            while ((inputLength - inputIndex) >= 32 && (outputLength - outputIndex) >= 32)
            {
                var a = Vector256.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
                var b = Vector256.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)(inputIndex + 16))));
                if (!Vector256.EqualsAll((a | b) & Vector256.Create((ushort)0xFF80), Vector256<ushort>.Zero))
                    break;
                var packed = Avx2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16());
                Avx2.Permute4x64(packed.AsUInt64(), 0xD8).AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)outputIndex));
                inputIndex += 32;
                outputIndex += 32;
            }
        }

        Vector128<ushort> nonAsciiMask = Vector128.Create((ushort)0xFF80);
        Vector128<short> zero = Vector128<short>.Zero;
        while ((inputLength - inputIndex) >= 32 && (outputLength - outputIndex) >= 32)
        {
            Vector128<ushort> a = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
            Vector128<ushort> b = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)(inputIndex + 8))));
            Vector128<ushort> c = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)(inputIndex + 16))));
            Vector128<ushort> d = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)(inputIndex + 24))));
            Vector128<ushort> upper = Sse2.And(Sse2.Or(Sse2.Or(a, b), Sse2.Or(c, d)), nonAsciiMask);
            if (Sse2.MoveMask(Sse2.CompareEqual(upper.AsInt16(), zero).AsByte()) != 0xFFFF)
                break;
            Sse2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16()).AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)outputIndex));
            Sse2.PackUnsignedSaturate(c.AsInt16(), d.AsInt16()).AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)(outputIndex + 16)));
            inputIndex += 32;
            outputIndex += 32;
        }

        while ((inputLength - inputIndex) >= 16 && (outputLength - outputIndex) >= 16)
        {
            Vector128<ushort> a = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
            Vector128<ushort> b = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)(inputIndex + 8))));
            Vector128<ushort> upper = Sse2.And(Sse2.Or(a, b), nonAsciiMask);
            if (Sse2.MoveMask(Sse2.CompareEqual(upper.AsInt16(), zero).AsByte()) != 0xFFFF)
                break;
            Sse2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16()).AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)outputIndex));
            inputIndex += 16;
            outputIndex += 16;
        }
    }

#endif
    /// <summary>Spreads four ASCII bytes from one native word into four UTF-16 code units.</summary>
    /// <param name="value">Four little-endian ASCII bytes.</param>
    /// <returns>Four little-endian UTF-16 code units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SpreadAscii4(uint value) =>
        (value & 0x000000FFUL) |
        ((value & 0x0000FF00UL) << 8) |
        ((value & 0x00FF0000UL) << 16) |
        ((value & 0xFF000000UL) << 24);

    /// <summary>Packs four ASCII UTF-16 code units from one native word into four bytes.</summary>
    /// <param name="value">Four little-endian UTF-16 code units below 0x80.</param>
    /// <returns>Four little-endian ASCII bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackAscii4(ulong value)
    {
        ulong packed = (value | (value >> 8)) & 0x0000FFFF0000FFFFUL;
        return (uint)(packed | (packed >> 16));
    }

    /// <summary>Allocates a string whose characters the caller overwrites completely before publication.</summary>
    /// <param name="length">The positive number of UTF-16 code units.</param>
    /// <returns>A fresh string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string AllocateUninitializedString(int length)
    {
#if NET8_0_OR_GREATER
        return FastString.Allocate(length);
#else
        return new string('\0', length);
#endif
    }

    /// <summary>Decodes a four- to sixteen-byte all-ASCII token into a new string one 64-bit word at a time.</summary>
    /// <param name="source">The first encoded byte.</param>
    /// <param name="length">The token length, four through sixteen.</param>
    /// <returns>The decoded string, or null when the token is not all ASCII or the target is unsupported.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string? TryDecodeShortAscii(ref byte source, int length)
    {
        if (!BitConverter.IsLittleEndian || Unsafe.SizeOf<IntPtr>() != 8 || length < 4 || length > 16)
            return null;

        // Packed fields avoid Mono's bytewise generic unaligned helpers; managed refs survive the result allocation.
        int index = 0;
        for (; index + 8 <= length; index += 8)
#if NETSTANDARD2_1
            if ((Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref source, (nint)(uint)index)).Value & 0x8080808080808080UL) != 0)
#else
            if ((Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, (nint)(uint)index)) & 0x8080808080808080UL) != 0)
#endif
                return null;
        if (length - index >= 4)
        {
#if NETSTANDARD2_1
            if ((Unsafe.As<byte, Utf8PackedWord32>(ref Unsafe.Add(ref source, (nint)(uint)index)).Value & 0x80808080U) != 0)
#else
            if ((Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, (nint)(uint)index)) & 0x80808080U) != 0)
#endif
                return null;
            index += 4;
        }
        for (; index < length; index++)
            if (Unsafe.Add(ref source, (nint)(uint)index) >= 0x80)
                return null;

        string result = AllocateUninitializedString(length);
        ref byte output = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(result.AsSpan()));
        index = 0;
        for (; index + 8 <= length; index += 8)
        {
#if NETSTANDARD2_1
            ulong value = Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref source, (nint)(uint)index)).Value;
            Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref output, (nint)(uint)(index * 2))).Value = SpreadAscii4((uint)value);
            Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref output, (nint)(uint)(index * 2 + 8))).Value = SpreadAscii4((uint)(value >> 32));
#else
            ulong value = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, (nint)(uint)index));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)(index * 2)), SpreadAscii4((uint)value));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)(index * 2 + 8)), SpreadAscii4((uint)(value >> 32)));
#endif
        }
        if (length - index >= 4)
        {
#if NETSTANDARD2_1
            uint value = Unsafe.As<byte, Utf8PackedWord32>(ref Unsafe.Add(ref source, (nint)(uint)index)).Value;
            Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref output, (nint)(uint)(index * 2))).Value = SpreadAscii4(value);
#else
            uint value = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, (nint)(uint)index));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)(index * 2)), SpreadAscii4(value));
#endif
            index += 4;
        }
        for (; index < length; index++)
            Unsafe.Add(ref output, (nint)(uint)(index * 2)) = Unsafe.Add(ref source, (nint)(uint)index);
        return result;
    }
    /// <summary>Portable UTF-8 encoder; no SIMD dispatch inside this hot loop.</summary>
    /// <param name = "input">The first input element.</param>
    /// <param name = "inputLength">The input length used by this operation.</param>
    /// <param name = "output">The first output element.</param>
    /// <param name = "outputLength">The available output capacity.</param>
    /// <param name = "charsRead">Receives the number of consumed input elements.</param>
    /// <param name = "bytesWritten">Receives the number of written output elements.</param>
    /// <returns>The result of the operation.</returns>
    private static OperationStatus TranscodeToUtf8Scalar(ref char input, int inputLength, ref byte output, int outputLength, out int charsRead, out int bytesWritten)
    {
        int inputIndex = 0;
        int outputIndex = 0;
        OperationStatus status = OperationStatus.Done;
        bool littleEndian = BitConverter.IsLittleEndian;
        while (inputIndex < inputLength)
        {
            uint c = Unsafe.Add(ref input, (nint)(uint)inputIndex);
            if (c < 0x80)
            {
#if NETSTANDARD2_1
                if (littleEndian && Unsafe.SizeOf<IntPtr>() == 8 && (inputLength - inputIndex) >= 8 &&
                    (outputLength - outputIndex) >= 8)
                {
                    ulong lower = Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)).Value;
                    ulong upper = Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref input, (nint)(uint)(inputIndex + 4))).Value;
                    if (((lower | upper) & 0xFF80FF80FF80FF80UL) == 0)
                    {
                        Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref output, (nint)(uint)outputIndex)).Value = PackAscii4(lower) | ((ulong)PackAscii4(upper) << 32);
                        inputIndex += 8;
                        outputIndex += 8;
                        continue;
                    }
                }
#endif
                // ASCII SWAR: 4 UTF-16 code units -> one exact 32-bit write.
                // This is the portable fast path for netstandard2.1 as well.
                if (littleEndian && (inputLength - inputIndex) >= 4 && (outputLength - outputIndex) >= 4)
                {
                    // IL2CPP permits typed unaligned values only up to four bytes on 32-bit.
                    ulong four = IntPtr.Size == 8
#if NETSTANDARD2_1
                        ? Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)).Value
#else
                        ? Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)))
#endif
                        : Unsafe.ReadUnaligned<uint>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex))) | ((ulong)Unsafe.ReadUnaligned<uint>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref input, (nint)(uint)(inputIndex + 2)))) << 32);
                    if ((four & 0xFF80FF80FF80FF80UL) == 0)
                    {
                        uint ascii = (uint)(four & 0xFFUL) | (uint)((four >> 8) & 0xFF00UL) | (uint)((four >> 16) & 0xFF0000UL) | (uint)((four >> 24) & 0xFF000000UL);
#if NETSTANDARD2_1
                        if (IntPtr.Size == 8)
                            Unsafe.As<byte, Utf8PackedWord32>(ref Unsafe.Add(ref output, (nint)(uint)outputIndex)).Value = ascii;
                        else
                            Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)outputIndex), ascii);
#else
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)outputIndex), ascii);
#endif
                        inputIndex += 4;
                        outputIndex += 4;
                        continue;
                    }
                }

                if (outputIndex == outputLength)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }

                Unsafe.Add(ref output, (nint)(uint)outputIndex++) = (byte)c;
                ++inputIndex;
                continue;
            }

            if (c < 0x800)
            {
                // Two adjacent Latin / Cyrillic / Arabic UTF-16 code units can be
                // emitted with a single 32-bit store. No unrelated CJK checks here.
                if (littleEndian && (inputLength - inputIndex) >= 2 && (outputLength - outputIndex) >= 4)
                {
                    uint next = Unsafe.Add(ref input, (nint)(uint)(inputIndex + 1));
                    if (next >= 0x80 && next < 0x800)
                    {
                        uint bytes = (0xC0U | (c >> 6)) | ((0x80U | (c & 0x3FU)) << 8) | ((0xC0U | (next >> 6)) << 16) | ((0x80U | (next & 0x3FU)) << 24);
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)outputIndex), bytes);
                        inputIndex += 2;
                        outputIndex += 4;
                        continue;
                    }
                }

                if ((outputLength - outputIndex) < 2)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }

                Unsafe.Add(ref output, (nint)(uint)outputIndex) = (byte)(0xC0 | (c >> 6));
                Unsafe.Add(ref output, (nint)(uint)(outputIndex + 1)) = (byte)(0x80 | (c & 63));
                outputIndex += 2;
                ++inputIndex;
                continue;
            }

            if (IsBmp3(c))
            {
                // Two BMP characters -> exactly 6 UTF-8 bytes (4 + 2 stores).
                if (littleEndian && (inputLength - inputIndex) >= 2 && (outputLength - outputIndex) >= 6)
                {
                    uint next = Unsafe.Add(ref input, (nint)(uint)(inputIndex + 1));
                    if (IsBmp3(next))
                    {
                        uint firstFour = (0xE0U | (c >> 12)) | ((0x80U | ((c >> 6) & 0x3FU)) << 8) | ((0x80U | (c & 0x3FU)) << 16) | ((0xE0U | (next >> 12)) << 24);
                        ushort lastTwo = (ushort)((0x80U | ((next >> 6) & 0x3FU)) | ((0x80U | (next & 0x3FU)) << 8));
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)outputIndex), firstFour);
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref output, (nint)(uint)(outputIndex + 4)), lastTwo);
                        inputIndex += 2;
                        outputIndex += 6;
                        continue;
                    }
                }

                if ((outputLength - outputIndex) < 3)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }

                Unsafe.Add(ref output, (nint)(uint)outputIndex) = (byte)(0xE0 | (c >> 12));
                Unsafe.Add(ref output, (nint)(uint)(outputIndex + 1)) = (byte)(0x80 | ((c >> 6) & 63));
                Unsafe.Add(ref output, (nint)(uint)(outputIndex + 2)) = (byte)(0x80 | (c & 63));
                outputIndex += 3;
                ++inputIndex;
                continue;
            }

            if (c <= 0xDBFF) // high surrogate
            {
                if ((inputLength - inputIndex) < 2)
                {
                    status = OperationStatus.NeedMoreData;
                    break;
                }

                uint low = Unsafe.Add(ref input, (nint)(uint)(inputIndex + 1));
                if (low < 0xDC00 || low > 0xDFFF)
                {
                    status = OperationStatus.InvalidData;
                    break;
                }

                if ((outputLength - outputIndex) < 4)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }

                uint scalar = 0x10000 + ((c - 0xD800) << 10) + (low - 0xDC00);
                Unsafe.Add(ref output, (nint)(uint)outputIndex) = (byte)(0xF0 | (scalar >> 18));
                Unsafe.Add(ref output, (nint)(uint)(outputIndex + 1)) = (byte)(0x80 | ((scalar >> 12) & 63));
                Unsafe.Add(ref output, (nint)(uint)(outputIndex + 2)) = (byte)(0x80 | ((scalar >> 6) & 63));
                Unsafe.Add(ref output, (nint)(uint)(outputIndex + 3)) = (byte)(0x80 | (scalar & 63));
                inputIndex += 2;
                outputIndex += 4;
            }
            else // isolated low surrogate
            {
                status = OperationStatus.InvalidData;
                break;
            }
        }

        charsRead = inputIndex;
        bytesWritten = outputIndex;
        return status;
    }

    /// <summary>Determines whether the supplied value has the bmp3 classification.</summary>
    /// <param name = "ch">The ch used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBmp3(uint ch) => ch >= 0x0800 && (ch < 0xD800 || ch > 0xDFFF);
    /// <summary>Core decoder: ASCII-vector and portable Unicode validation.</summary>
    /// <param name = "input">The first input element.</param>
    /// <param name = "inputLength">The input length used by this operation.</param>
    /// <param name = "output">The first output element.</param>
    /// <param name = "outputLength">The available output capacity.</param>
    /// <param name = "bytesRead">Receives the number of consumed input elements.</param>
    /// <param name = "charsWritten">Receives the number of written output elements.</param>
    /// <returns>The result of the operation.</returns>
    internal static OperationStatus TranscodeToUtf16(ref byte input, int inputLength, ref char output, int outputLength, out int bytesRead, out int charsWritten)
    {
        int inputIndex = 0;
        int outputIndex = 0;
        while (inputIndex < inputLength)
        {
            bytesRead = inputIndex;
            charsWritten = outputIndex;
            uint firstByte = Unsafe.Add(ref input, (nint)(uint)inputIndex);
            if (firstByte < 0x80)
            {
#if NETSTANDARD2_1
                if (BitConverter.IsLittleEndian && Unsafe.SizeOf<IntPtr>() == 8 &&
                    (inputLength - inputIndex) >= 8 && (outputLength - outputIndex) >= 8)
                {
                    ulong eight = Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)).Value;
                    if ((eight & 0x8080808080808080UL) == 0)
                    {
                        Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref output, (nint)(uint)outputIndex)).Value = SpreadAscii4((uint)eight);
                        Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref output, (nint)(uint)(outputIndex + 4))).Value = SpreadAscii4((uint)(eight >> 32));
                        inputIndex += 8;
                        outputIndex += 8;
                        continue;
                    }
                }
#endif
                if (outputIndex == outputLength)
                    return Complete(OperationStatus.DestinationTooSmall);
                Unsafe.Add(ref output, (nint)(uint)outputIndex) = (char)firstByte;
                inputIndex++;
                outputIndex++;
                continue;
            }

            // Only C2..DF are valid two-byte leaders. C0/C1 are overlong.
            if (firstByte is >= 0xC2 and <= 0xDF)
            {
                if (inputLength - inputIndex < 2)
                    return Complete(OperationStatus.NeedMoreData);
                uint secondByte = Unsafe.Add(ref input, (nint)(uint)(inputIndex + 1));
                if (!IsUtf8ContinuationByte(secondByte))
                    return Complete(OperationStatus.InvalidData);
                if (outputIndex == outputLength)
                    return Complete(OperationStatus.DestinationTooSmall);
                uint codePoint = ((firstByte & 0x1F) << 6) | (secondByte & 0x3F);
                Unsafe.Add(ref output, (nint)(uint)outputIndex) = (char)codePoint;
                inputIndex += 2;
                outputIndex++;
                continue;
            }

            if (firstByte is >= 0xE0 and <= 0xEF)
            {
                if (inputLength - inputIndex < 2)
                    return Complete(OperationStatus.NeedMoreData);
                uint secondByte = Unsafe.Add(ref input, (nint)(uint)(inputIndex + 1));
                if (!IsUtf8ContinuationByte(secondByte) || (firstByte == 0xE0 && secondByte < 0xA0) || (firstByte == 0xED && secondByte >= 0xA0))
                    return Complete(OperationStatus.InvalidData);
                if (inputLength - inputIndex < 3)
                    return Complete(OperationStatus.NeedMoreData);
                uint thirdByte = Unsafe.Add(ref input, (nint)(uint)(inputIndex + 2));
                if (!IsUtf8ContinuationByte(thirdByte))
                    return Complete(OperationStatus.InvalidData);
                if (outputIndex == outputLength)
                    return Complete(OperationStatus.DestinationTooSmall);
                uint codePoint = ((firstByte & 0x0F) << 12) | ((secondByte & 0x3F) << 6) | (thirdByte & 0x3F);
                Unsafe.Add(ref output, (nint)(uint)outputIndex) = (char)codePoint;
                inputIndex += 3;
                outputIndex++;
                continue;
            }

            // F0..F4 are the only valid four-byte leaders.
            if (firstByte is >= 0xF0 and <= 0xF4)
            {
                if (inputLength - inputIndex < 2)
                    return Complete(OperationStatus.NeedMoreData);
                uint secondByte = Unsafe.Add(ref input, (nint)(uint)(inputIndex + 1));
                if (!IsUtf8ContinuationByte(secondByte) || (firstByte == 0xF0 && secondByte < 0x90) || (firstByte == 0xF4 && secondByte > 0x8F))
                    return Complete(OperationStatus.InvalidData);
                if (inputLength - inputIndex < 3)
                    return Complete(OperationStatus.NeedMoreData);
                uint thirdByte = Unsafe.Add(ref input, (nint)(uint)(inputIndex + 2));
                if (!IsUtf8ContinuationByte(thirdByte))
                    return Complete(OperationStatus.InvalidData);
                if (inputLength - inputIndex < 4)
                    return Complete(OperationStatus.NeedMoreData);
                uint fourthByte = Unsafe.Add(ref input, (nint)(uint)(inputIndex + 3));
                if (!IsUtf8ContinuationByte(fourthByte))
                    return Complete(OperationStatus.InvalidData);
                if ((outputLength - outputIndex) < 2)
                    return Complete(OperationStatus.DestinationTooSmall);
                uint codePoint = ((firstByte & 0x07) << 18) | ((secondByte & 0x3F) << 12) | ((thirdByte & 0x3F) << 6) | (fourthByte & 0x3F);
                codePoint -= 0x10000;
                Unsafe.Add(ref output, (nint)(uint)outputIndex) = (char)((codePoint >> 10) + 0xD800);
                Unsafe.Add(ref output, (nint)(uint)(outputIndex + 1)) = (char)((codePoint & 0x3FF) + 0xDC00);
                inputIndex += 4;
                outputIndex += 2;
                continue;
            }

            return Complete(OperationStatus.InvalidData);
        }

        bytesRead = inputIndex;
        charsWritten = outputIndex;
        return OperationStatus.Done;
        // <summary>Performs the complete step for the current operation.</summary>
        // <param name="status">The status used by this operation.</param>
        // <returns>The result of the operation.</returns>
        static OperationStatus Complete(OperationStatus status) => status;
    }

    /// <summary>Determines whether the supplied value has the utf8 continuation byte classification.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUtf8ContinuationByte(uint value) => (value & 0xC0) == 0x80;
    /// <summary>Obtains the invalid utf8 sequence length for the supplied input.</summary>
    /// <param name = "input">The input data to process.</param>
    /// <param name = "remaining">The remaining used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static int GetInvalidUtf8SequenceLength(ref byte input, int remaining)
    {
        if (remaining <= 1)
            return 1;
        uint b0 = input;
        uint b1 = Unsafe.Add(ref input, 1);
        if (b0 >= 0xE0 && b0 <= 0xEF)
        {
            if (!IsUtf8ContinuationByte(b1) || (b0 == 0xE0 && b1 < 0xA0) || (b0 == 0xED && b1 >= 0xA0))
                return 1;
            return remaining > 2 && !IsUtf8ContinuationByte(Unsafe.Add(ref input, 2)) ? 2 : 1;
        }

        if (b0 >= 0xF0 && b0 <= 0xF4)
        {
            if (!IsUtf8ContinuationByte(b1) || (b0 == 0xF0 && b1 < 0x90) || (b0 == 0xF4 && b1 > 0x8F))
                return 1;
            if (remaining > 2 && !IsUtf8ContinuationByte(Unsafe.Add(ref input, 2)))
                return 2;
            if (remaining > 3 && !IsUtf8ContinuationByte(Unsafe.Add(ref input, 3)))
                return 3;
        }

        return 1;
    }
#if NET8_0_OR_GREATER
    // Masks for converting 8 BMP3 characters into exactly 24 UTF-8 bytes.
    // Retain the already-proven CJK3 path and its exact stores.
    private const byte Skip = 0x80;
    private static readonly Vector128<byte> BmpF0 = Vector128.Create((byte)0, Skip, Skip, (byte)1, Skip, Skip, (byte)2, Skip, Skip, (byte)3, Skip, Skip, (byte)4, Skip, Skip, (byte)5);
    private static readonly Vector128<byte> BmpF1 = Vector128.Create(Skip, (byte)0, Skip, Skip, (byte)1, Skip, Skip, (byte)2, Skip, Skip, (byte)3, Skip, Skip, (byte)4, Skip, Skip);
    private static readonly Vector128<byte> BmpF2 = Vector128.Create(Skip, Skip, (byte)0, Skip, Skip, (byte)1, Skip, Skip, (byte)2, Skip, Skip, (byte)3, Skip, Skip, (byte)4, Skip);
    private static readonly Vector128<byte> BmpS0 = Vector128.Create(Skip, Skip, (byte)6, Skip, Skip, (byte)7, Skip, Skip, Skip, Skip, Skip, Skip, Skip, Skip, Skip, Skip);
    private static readonly Vector128<byte> BmpS1 = Vector128.Create((byte)5, Skip, Skip, (byte)6, Skip, Skip, (byte)7, Skip, Skip, Skip, Skip, Skip, Skip, Skip, Skip, Skip);
    private static readonly Vector128<byte> BmpS2 = Vector128.Create(Skip, (byte)5, Skip, Skip, (byte)6, Skip, Skip, (byte)7, Skip, Skip, Skip, Skip, Skip, Skip, Skip, Skip);
    private static readonly Vector256<byte> BmpF0x2 = Vector256.Create(BmpF0, BmpF0);
    private static readonly Vector256<byte> BmpF1x2 = Vector256.Create(BmpF1, BmpF1);
    private static readonly Vector256<byte> BmpF2x2 = Vector256.Create(BmpF2, BmpF2);
    private static readonly Vector256<byte> BmpS0x2 = Vector256.Create(BmpS0, BmpS0);
    private static readonly Vector256<byte> BmpS1x2 = Vector256.Create(BmpS1, BmpS1);
    private static readonly Vector256<byte> BmpS2x2 = Vector256.Create(BmpS2, BmpS2);
    /// <summary>
    /// Run a dedicated homogeneous SIMD loop selected only once by the leading
    /// character. Unlike the old design, a Latin2 block doesn't probe CJK3 or
    /// ASCII first on every 8/16 code units, and AVX2 failure does not invoke
    /// three unrelated SSSE3 classifiers.
    /// </summary>
    /// <param name = "input">The input data to process.</param>
    /// <param name = "output">The destination receiving the result.</param>
    /// <param name="inputLength">The complete input character count.</param>
    /// <param name="outputLength">The complete output capacity in bytes.</param>
    /// <param name="inputIndex">The number of consumed characters.</param>
    /// <param name="outputIndex">The number of written bytes.</param>
    /// <param name = "lead">The lead used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryEncodeVectorRun(ref char input, ref byte output, int inputLength, int outputLength, ref int inputIndex, ref int outputIndex, uint lead)
    {
        int original = inputIndex;
        if (Avx512BW.IsSupported)
            EncodeVector512Run(ref input, ref output, inputLength, outputLength, ref inputIndex, ref outputIndex, lead);
        if (lead < 0x80)
        {
            if (Avx2.IsSupported)
            {
                Vector256<ushort> highMask = Vector256.Create((ushort)0xFF80);
                while ((inputLength - inputIndex) >= 16 && (outputLength - outputIndex) >= 16)
                {
                    Vector256<ushort> chars = Vector256.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
                    Vector256<short> ascii = Avx2.CompareEqual(Avx2.And(chars, highMask).AsInt16(), Vector256<short>.Zero);
                    if (Avx2.MoveMask(ascii.AsByte()) != -1)
                        break;
                    Vector256<byte> packed = Avx2.PackUnsignedSaturate(chars.AsInt16(), chars.AsInt16());
                    LuminPackWriter.Write(ref Unsafe.Add(ref output, (nint)(uint)outputIndex), packed.GetLower().AsUInt64().GetElement(0));
                    LuminPackWriter.Write(ref Unsafe.Add(ref output, (nint)(uint)(outputIndex + 8)), packed.GetUpper().AsUInt64().GetElement(0));
                    inputIndex += 16;
                    outputIndex += 16;
                }
            }

            if (Ssse3.IsSupported)
            {
                Vector128<ushort> highMask = Vector128.Create((ushort)0xFF80);
                while ((inputLength - inputIndex) >= 8 && (outputLength - outputIndex) >= 8)
                {
                    Vector128<ushort> chars = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
                    Vector128<short> ascii = Sse2.CompareEqual(Sse2.And(chars, highMask).AsInt16(), Vector128<short>.Zero);
                    if (Sse2.MoveMask(ascii.AsByte()) != 0xFFFF)
                        break;
                    Vector128<byte> packed = Sse2.PackUnsignedSaturate(chars.AsInt16(), chars.AsInt16());
                    LuminPackWriter.Write(ref Unsafe.Add(ref output, (nint)(uint)outputIndex), packed.AsUInt64().GetElement(0));
                    inputIndex += 8;
                    outputIndex += 8;
                }
            }
        }
        else if (lead < 0x800)
        {
            if (Avx2.IsSupported)
            {
                Vector256<short> min = Vector256.Create((short)127);
                Vector256<short> max = Vector256.Create((short)2048);
                Vector256<ushort> sixBits = Vector256.Create((ushort)63);
                Vector256<ushort> leadBits = Vector256.Create((ushort)0xC0);
                Vector256<ushort> trailBits = Vector256.Create((ushort)0x80);
                while ((inputLength - inputIndex) >= 32 && (outputLength - outputIndex) >= 64)
                {
                    var a = Vector256.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
                    var b = Vector256.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)(inputIndex + 16))));
                    var validA = Avx2.And(Avx2.CompareGreaterThan(a.AsInt16(), min), Avx2.CompareGreaterThan(max, a.AsInt16()));
                    var validB = Avx2.And(Avx2.CompareGreaterThan(b.AsInt16(), min), Avx2.CompareGreaterThan(max, b.AsInt16()));
                    if (Avx2.MoveMask(Avx2.And(validA, validB).AsByte()) != -1)
                        break;
                    var lowA = Avx2.Or(Avx2.ShiftRightLogical(a, 6), leadBits);
                    var highA = Avx2.ShiftLeftLogical(Avx2.Or(Avx2.And(a, sixBits), trailBits), 8);
                    var lowB = Avx2.Or(Avx2.ShiftRightLogical(b, 6), leadBits);
                    var highB = Avx2.ShiftLeftLogical(Avx2.Or(Avx2.And(b, sixBits), trailBits), 8);
                    Avx2.Or(lowA, highA).AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)outputIndex));
                    Avx2.Or(lowB, highB).AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)(outputIndex + 32)));
                    inputIndex += 32;
                    outputIndex += 64;
                }

                while ((inputLength - inputIndex) >= 16 && (outputLength - outputIndex) >= 32)
                {
                    Vector256<ushort> chars = Vector256.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
                    Vector256<short> valid = Avx2.And(Avx2.CompareGreaterThan(chars.AsInt16(), min), Avx2.CompareGreaterThan(max, chars.AsInt16()));
                    if (Avx2.MoveMask(valid.AsByte()) != -1)
                        break;
                    Vector256<ushort> low = Avx2.Or(Avx2.ShiftRightLogical(chars, 6), leadBits);
                    Vector256<ushort> high = Avx2.ShiftLeftLogical(Avx2.Or(Avx2.And(chars, sixBits), trailBits), 8);
                    Avx2.Or(low, high).AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)outputIndex));
                    inputIndex += 16;
                    outputIndex += 32;
                }
            }

            if (Ssse3.IsSupported)
            {
                Vector128<short> min = Vector128.Create((short)127);
                Vector128<short> max = Vector128.Create((short)2048);
                Vector128<ushort> sixBits = Vector128.Create((ushort)63);
                Vector128<ushort> leadBits = Vector128.Create((ushort)0xC0);
                Vector128<ushort> trailBits = Vector128.Create((ushort)0x80);
                while ((inputLength - inputIndex) >= 8 && (outputLength - outputIndex) >= 16)
                {
                    Vector128<ushort> chars = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
                    Vector128<short> valid = Sse2.And(Sse2.CompareGreaterThan(chars.AsInt16(), min), Sse2.CompareGreaterThan(max, chars.AsInt16()));
                    if (Sse2.MoveMask(valid.AsByte()) != 0xFFFF)
                        break;
                    Vector128<ushort> low = Sse2.Or(Sse2.ShiftRightLogical(chars, 6), leadBits);
                    Vector128<ushort> high = Sse2.ShiftLeftLogical(Sse2.Or(Sse2.And(chars, sixBits), trailBits), 8);
                    Sse2.Or(low, high).AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)outputIndex));
                    inputIndex += 8;
                    outputIndex += 16;
                }
            }
        }
        else if (IsBmp3(lead))
        {
            if (Avx2.IsSupported)
            {
                Vector256<ushort> topMask = Vector256.Create((ushort)0xF800);
                Vector256<short> surrogate = Vector256.Create(unchecked((short)0xD800));
                Vector256<ushort> e0 = Vector256.Create((ushort)0xE0);
                Vector256<ushort> trail = Vector256.Create((ushort)0x80);
                Vector256<ushort> sixBits = Vector256.Create((ushort)63);
                while ((inputLength - inputIndex) >= 16 && (outputLength - outputIndex) >= 48)
                {
                    Vector256<ushort> chars = Vector256.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
                    Vector256<short> top = Avx2.And(chars, topMask).AsInt16();
                    Vector256<short> invalid = Avx2.Or(Avx2.CompareEqual(top, Vector256<short>.Zero), Avx2.CompareEqual(top, surrogate));
                    if (Avx2.MoveMask(invalid.AsByte()) != 0)
                        break;
                    Vector256<ushort> leadBytes = Avx2.Or(Avx2.ShiftRightLogical(chars, 12), e0);
                    Vector256<ushort> middleBytes = Avx2.Or(Avx2.And(Avx2.ShiftRightLogical(chars, 6), sixBits), trail);
                    Vector256<ushort> lastBytes = Avx2.Or(Avx2.And(chars, sixBits), trail);
                    Vector256<byte> a = Avx2.PackUnsignedSaturate(leadBytes.AsInt16(), leadBytes.AsInt16());
                    Vector256<byte> b = Avx2.PackUnsignedSaturate(middleBytes.AsInt16(), middleBytes.AsInt16());
                    Vector256<byte> c = Avx2.PackUnsignedSaturate(lastBytes.AsInt16(), lastBytes.AsInt16());
                    Vector256<byte> first = Avx2.Or(Avx2.Or(Avx2.Shuffle(a, BmpF0x2), Avx2.Shuffle(b, BmpF1x2)), Avx2.Shuffle(c, BmpF2x2));
                    Vector256<byte> second = Avx2.Or(Avx2.Or(Avx2.Shuffle(a, BmpS0x2), Avx2.Shuffle(b, BmpS1x2)), Avx2.Shuffle(c, BmpS2x2));
                    first.GetLower().AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)outputIndex));
                    LuminPackWriter.Write(ref Unsafe.Add(ref output, (nint)(uint)(outputIndex + 16)), second.GetLower().AsUInt64().GetElement(0));
                    first.GetUpper().AsByte().StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)(outputIndex + 24)));
                    LuminPackWriter.Write(ref Unsafe.Add(ref output, (nint)(uint)(outputIndex + 40)), second.GetUpper().AsUInt64().GetElement(0));
                    inputIndex += 16;
                    outputIndex += 48;
                }
            }

            if (Ssse3.IsSupported)
            {
                Vector128<ushort> topMask = Vector128.Create((ushort)0xF800);
                Vector128<short> surrogate = Vector128.Create(unchecked((short)0xD800));
                Vector128<ushort> e0 = Vector128.Create((ushort)0xE0);
                Vector128<ushort> trail = Vector128.Create((ushort)0x80);
                Vector128<ushort> sixBits = Vector128.Create((ushort)63);
                while ((inputLength - inputIndex) >= 8 && (outputLength - outputIndex) >= 24)
                {
                    Vector128<ushort> chars = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, (nint)(uint)inputIndex)));
                    Vector128<short> top = Sse2.And(chars, topMask).AsInt16();
                    Vector128<short> invalid = Sse2.Or(Sse2.CompareEqual(top, Vector128<short>.Zero), Sse2.CompareEqual(top, surrogate));
                    if (Sse2.MoveMask(invalid.AsByte()) != 0)
                        break;
                    Vector128<ushort> leadBytes = Sse2.Or(Sse2.ShiftRightLogical(chars, 12), e0);
                    Vector128<ushort> middleBytes = Sse2.Or(Sse2.And(Sse2.ShiftRightLogical(chars, 6), sixBits), trail);
                    Vector128<ushort> lastBytes = Sse2.Or(Sse2.And(chars, sixBits), trail);
                    Vector128<byte> a = Sse2.PackUnsignedSaturate(leadBytes.AsInt16(), leadBytes.AsInt16());
                    Vector128<byte> b = Sse2.PackUnsignedSaturate(middleBytes.AsInt16(), middleBytes.AsInt16());
                    Vector128<byte> c = Sse2.PackUnsignedSaturate(lastBytes.AsInt16(), lastBytes.AsInt16());
                    Vector128<byte> first = Sse2.Or(Sse2.Or(Ssse3.Shuffle(a, BmpF0), Ssse3.Shuffle(b, BmpF1)), Ssse3.Shuffle(c, BmpF2));
                    Vector128<byte> second = Sse2.Or(Sse2.Or(Ssse3.Shuffle(a, BmpS0), Ssse3.Shuffle(b, BmpS1)), Ssse3.Shuffle(c, BmpS2));
                    first.StoreUnsafe(ref Unsafe.Add(ref output, (nint)(uint)outputIndex));
                    LuminPackWriter.Write(ref Unsafe.Add(ref output, (nint)(uint)(outputIndex + 16)), second.AsUInt64().GetElement(0));
                    inputIndex += 8;
                    outputIndex += 24;
                }
            }
        }

        return inputIndex != original;
    }
#endif
}
