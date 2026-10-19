using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.Unicode;
#endif
using LuminPack.Option;

namespace LuminPack.Core;

public unsafe ref partial struct LuminPackReader
{
    /// <summary>Reads a string record, retaining the supplied immutable string when its text matches.</summary>
    /// <param name="index">The absolute input cursor, advanced only after a successful read.</param>
    /// <param name="current">The string available for reuse.</param>
    /// <returns>The existing string when equal, or the normally decoded value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? ReadStringAndAdvance(ref int index, string? current)
        => ReadStringAndAdvance<LuminPackDefaultOptions>(ref index, current);

    /// <summary>Reads a compiled-profile string record without allocating an unchanged string.</summary>
    /// <typeparam name="TOptions">The stateless binary string profile.</typeparam>
    /// <param name="index">The absolute input cursor, advanced only after a successful read.</param>
    /// <param name="current">The string available for reuse.</param>
    /// <returns>The existing string when equal, or the normally decoded value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? ReadStringAndAdvance<TOptions>(ref int index, string? current)
        where TOptions : struct, ILuminPackOptions
    {
        if (current is null) return ReadStringAndAdvance<TOptions>(ref index);
#if NET8_0_OR_GREATER
        if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 &&
            default(TOptions).StringRecording == LuminPackStringRecording.Length)
        {
            int start = index;
            int available = _bufferReference.Length;
            if ((uint)start <= (uint)available && available - start >= sizeof(int) * 2)
            {
                ReadUnmanagedWithoutSizeReturn(ref start, out int bytes, out int characters);
                if (bytes > 0 && bytes <= available - start - sizeof(int) * 2)
                {
                    if (characters > 0 && characters != current.Length) return ReadStringAndAdvance<TOptions>(ref index);
                    ref byte payloadStart = ref Unsafe.Add(ref _bufferStart, (nint)(uint)start + sizeof(int) * 2);
                    bool matches;
                    if (bytes == current.Length)
                    {
                        // Short ASCII records dominate reuse traffic. The framework helper's fixed cost
                        // (span construction plus a cross-assembly vector setup) outweighs its work at these
                        // lengths, so compare short records in-assembly and keep the helper for longer ones.
                        matches = bytes <= 16
                            ? MatchesAsciiRecord(ref payloadStart, current)
                            : Ascii.Equals(MemoryMarshal.CreateReadOnlySpan(ref payloadStart, bytes), current.AsSpan());
                    }
                    else matches = MatchesUtf8(MemoryMarshal.CreateReadOnlySpan(ref payloadStart, bytes), current);
                    if (matches)
                    {
                        // Publish only a validated match; failed reads retain their original cursor.
                        index = start + sizeof(int) * 2 + bytes;
                        return current;
                    }
                    // The validated payload was compared once; a mismatch needs the original decoder only.
                    return ReadStringAndAdvance<TOptions>(ref index);
                }
            }
        }
#endif
        return ReadExistingStringAndAdvance<TOptions>(ref index, current);
    }

    /// <summary>Isolates fallback comparison work from the original fresh-string reader.</summary>
    /// <typeparam name="TOptions">The stateless binary string profile.</typeparam>
    /// <param name="index">The absolute record cursor.</param>
    /// <param name="current">The non-null candidate string.</param>
    /// <returns>The reused or normally decoded string.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private string? ReadExistingStringAndAdvance<TOptions>(ref int index, string current)
        where TOptions : struct, ILuminPackOptions
    {
        if (MatchesStringRecord<TOptions>(index, current, out int end))
        {
            index = end;
            return current;
        }

        return ReadStringAndAdvance<TOptions>(ref index);
    }

    /// <summary>Compares validated string payload bytes without publishing a cursor or creating text.</summary>
    /// <typeparam name="TOptions">The stateless binary string profile.</typeparam>
    /// <param name="index">The original record cursor.</param>
    /// <param name="current">The candidate immutable string.</param>
    /// <param name="end">Receives the complete record end after a match.</param>
    /// <returns>Whether the ordinary reader would produce the candidate text.</returns>
    private bool MatchesStringRecord<TOptions>(int index, string current, out int end)
        where TOptions : struct, ILuminPackOptions
    {
        end = index;
        bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
        bool token = default(TOptions).StringRecording == LuminPackStringRecording.Token;
        int header = token ? 0 : utf8 ? 8 : 4;
        int available = _bufferReference.Length;
        if ((uint)index > (uint)available || (uint)header > (uint)(available - index)) return false;
        ReadStringLength<TOptions>(ref index, out int length);
        // Null, empty and malformed records retain the original reader's normalization and failures.
        if (length <= 0) return false;
        if ((uint)length > (uint)(available - index - header)) return false;
        ReadOnlySpan<byte> payload = _bufferReference.Slice(index + header, length);
        bool matches;
        if (utf8)
        {
#if !NETSTANDARD2_1
            if (!token)
            {
                int characterIndex = index + 4;
                ReadUnmanagedWithoutSizeReturn(ref characterIndex, out int characters);
                if (characters > 0 && characters != current.Length) return false;
            }
#endif
            matches = MatchesUtf8(payload, current);
        }
        else
        {
            matches = payload.SequenceEqual(MemoryMarshal.AsBytes(current.AsSpan()));
            // Token decoding uses Encoding.Unicode, which replaces malformed surrogate sequences.
            if (matches && token)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    if (!char.IsSurrogate(current[i])) continue;
                    if (!char.IsHighSurrogate(current[i]) || ++i == current.Length || !char.IsLowSurrogate(current[i]))
                        return false;
                }
            }
        }

        if (matches) end = index + header + length + (token ? utf8 ? 1 : 2 : 0);
        return matches;
    }

    /// <summary>Compares canonical UTF-8 scalar values against UTF-16 without temporary storage.</summary>
    /// <param name="bytes">The complete, bounds-validated UTF-8 payload.</param>
    /// <param name="text">The candidate UTF-16 text.</param>
    /// <returns>Whether valid UTF-8 decodes to exactly the candidate text.</returns>
    private static bool MatchesUtf8(ReadOnlySpan<byte> bytes, string text)
    {
#if NET8_0_OR_GREATER
        if (bytes.Length == text.Length) return Ascii.Equals(bytes, text.AsSpan());
        if (bytes.Length > 64) return MatchesUtf8Chunks(bytes, text.AsSpan());
#endif
        int position = 0;
        for (int i = 0; i < bytes.Length;)
        {
            uint scalar = bytes[i++];
            if (scalar >= 0x80)
            {
                int trailing;
                uint minimum;
                if (scalar >= 0xC2 && scalar <= 0xDF) { trailing = 1; minimum = 0x80; scalar &= 0x1F; }
                else if (scalar >= 0xE0 && scalar <= 0xEF) { trailing = 2; minimum = 0x800; scalar &= 0x0F; }
                else if (scalar >= 0xF0 && scalar <= 0xF4) { trailing = 3; minimum = 0x10000; scalar &= 7; }
                else return false;
                if (trailing > bytes.Length - i) return false;
                for (int j = 0; j < trailing; j++)
                {
                    uint next = bytes[i++];
                    if ((next & 0xC0) != 0x80) return false;
                    scalar = (scalar << 6) | (next & 0x3F);
                }
                if (scalar < minimum || scalar > 0x10FFFF || scalar >= 0xD800 && scalar <= 0xDFFF) return false;
            }
            if (scalar >= 0x10000)
            {
                scalar -= 0x10000;
                if (position >= text.Length || text[position++] != (char)(0xD800 + (scalar >> 10))) return false;
                scalar = 0xDC00 + (scalar & 0x3FF);
            }
            if (position >= text.Length || text[position++] != (char)scalar) return false;
        }
        return position == text.Length;
    }

#if NET8_0_OR_GREATER
    /// <summary>Compares a short equal-length ASCII record against the candidate text inside this assembly.</summary>
    /// <param name="source">The first payload byte; the record is <c>text.Length</c> bytes long.</param>
    /// <param name="text">The candidate immutable text whose length equals the record's byte count.</param>
    /// <returns>Whether every payload byte equals the matching character and every character is ASCII.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesAsciiRecord(ref byte source, string text)
    {
        int length = text.Length;
        // Reading a payload window wider than the record would leave the validated range, so the
        // overlapping-window form below is used only where the two windows stay inside it.
        if (!Vector128.IsHardwareAccelerated || Unsafe.SizeOf<IntPtr>() != 8 || length < 4)
        {
            for (int i = 0; i < length; i++)
            {
                if (text[i] > 0x7F || (byte)text[i] != Unsafe.Add(ref source, i)) return false;
            }

            return true;
        }

        ref ushort characters = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(text.AsSpan()));
        if (length < 8)
        {
            // 4 to 7 characters: the first four and the last four cover the record.
            var chars = Vector128.Create(
                Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref characters)),
                Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref characters, length - 4)))).AsUInt16();
            if (Vector128.GreaterThanAny(chars, Vector128.Create((ushort)0x7F))) return false;
            ulong payload = Unsafe.ReadUnaligned<uint>(ref source) |
                ((ulong)Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, length - 4)) << 32);
            return Vector128.Narrow(chars, chars).AsUInt64().GetElement(0) == payload;
        }

        // 8 to 16 characters: the first eight and the last eight cover the record.
        var first = Vector128.LoadUnsafe(ref characters);
        var last = Vector128.LoadUnsafe(ref characters, (nuint)(length - 8));
        if (Vector128.GreaterThanAny(first | last, Vector128.Create((ushort)0x7F))) return false;
        var record = Vector128.Create(
            Unsafe.ReadUnaligned<ulong>(ref source),
            Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, length - 8))).AsByte();
        return Vector128.EqualsAll(Vector128.Narrow(first, last), record);
    }

    /// <summary>Compares longer Unicode payloads using bounded stack storage and the runtime transcoder.</summary>
    /// <param name="bytes">The complete UTF-8 payload.</param>
    /// <param name="text">The candidate UTF-16 text.</param>
    /// <returns>Whether every decoded code unit matches and both inputs finish together.</returns>
    private static bool MatchesUtf8Chunks(ReadOnlySpan<byte> bytes, ReadOnlySpan<char> text)
    {
        Span<char> decoded = stackalloc char[256];
        while (!bytes.IsEmpty)
        {
            OperationStatus status = Utf8.ToUtf16(bytes, decoded, out int consumed, out int written,
                replaceInvalidSequences: false, isFinalBlock: true);
            if (status is not (OperationStatus.Done or OperationStatus.DestinationTooSmall) ||
                written > text.Length || !decoded.Slice(0, written).SequenceEqual(text.Slice(0, written))) return false;
            bytes = bytes.Slice(consumed);
            text = text.Slice(written);
        }
        return text.IsEmpty;
    }
#endif
}
