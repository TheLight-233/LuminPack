using System;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Utility;
/// <summary>Decodes complete tokens without temporary fallback objects on the legacy UTF-8 runtime.</summary>
/// <remarks>Modern and unrecognized default encoding implementations retain their platform decoder.</remarks>
internal static class Utf8PlatformDecoder
{
    /// <summary>Preserves platform replacement and capacity failures while avoiding legacy fallback buffers.</summary>
    /// <param name = "source">The complete encoded token.</param>
    /// <param name = "destination">The bounded character destination.</param>
    /// <returns>The number of decoded characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Decode(ReadOnlySpan<byte> source, Span<char> destination)
    {
#if NETSTANDARD2_1
        // Reference-source runtimes expose the base UTF8Encoding as their default; newer BCLs use a specialized subclass.
        // Keep this check stateless: a new cached-policy type adds a first-use allocation on Mono.
        if (Encoding.UTF8.GetType() == typeof(UTF8Encoding))
        {
            if (destination.Length >= source.Length)
                return DecodeUtf8LegacyComplete(source, destination);
            int written = TryDecodeBounded(source, destination);
            if (written >= 0)
                return written;
        }

#endif
        return Encoding.UTF8.GetChars(source, destination);
    }

    /// <summary>Decodes a complete token only when its characters fit the supplied destination.</summary>
    /// <param name = "source">The complete encoded token.</param>
    /// <param name = "destination">The bounded character destination.</param>
    /// <param name = "written">The character count on success; unspecified on a capacity miss.</param>
    /// <returns>Whether decoding completed; a capacity miss leaves the destination unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryDecode(ReadOnlySpan<byte> source, Span<char> destination, out int written)
    {
        if (source.Length <= destination.Length)
        {
            written = Decode(source, destination);
            return true;
        }

#if NET8_0_OR_GREATER
        // An ASCII prefix longer than the destination proves a miss without decoding into scratch.
        if (System.Text.Ascii.IsValid(source.Slice(0, destination.Length + 1)))
        {
            written = -1;
            return false;
        }
#endif
        if (source.Length <= 768)
        {
            written = TryDecodeBounded(source, destination);
            return written >= 0;
        }

        written = Encoding.UTF8.GetCharCount(source);
        if (written > destination.Length)
            return false;
        written = Encoding.UTF8.GetChars(source, destination);
        return true;
    }

#if NETSTANDARD2_1
    /// <summary>Decodes a final legacy UTF-8 block into storage proven to hold at least one character per input byte.</summary>
    /// <param name = "source">The complete encoded token.</param>
    /// <param name = "destination">Storage whose length is at least the input byte count.</param>
    /// <returns>The number of decoded characters.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int DecodeUtf8LegacyComplete(ReadOnlySpan<byte> source, Span<char> destination)
    {
        ref byte inputStart = ref MemoryMarshal.GetReference(source);
        ref char outputStart = ref MemoryMarshal.GetReference(destination);
        int inputIndex = 0;
        int outputIndex = 0;
        int length = source.Length;
        if (source.Length >= 128 && BitConverter.IsLittleEndian && IntPtr.Size == 8)
        {
            if ((uint)(Unsafe.Add(ref inputStart, inputIndex) - 0xc2) <= 0x1d)
            {
                int count = DecodeTwoBytePrefix(ref inputStart, length, ref outputStart);
                inputIndex += count * 2;
                outputIndex += count;
            }
            else if ((uint)(Unsafe.Add(ref inputStart, inputIndex) - 0xe1) <= 11)
            {
                int count = DecodeThreeBytePrefix(ref inputStart, length, ref outputStart);
                inputIndex += count * 3;
                outputIndex += count;
            }
        }

        if (BitConverter.IsLittleEndian && IntPtr.Size == 8)
            while (length - inputIndex >= 8)
            {
                ulong word = Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref inputStart, inputIndex)).Value;
                if ((word & 0x8080808080808080UL) == 0)
                {
                    Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = SpreadAscii4((uint)word);
                    Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex + 4)).Value = SpreadAscii4((uint)(word >> 32));
                    inputIndex += 8;
                    outputIndex += 8;
                    continue;
                }

                uint low = (uint)word;
                if ((low & 128) == 0)
                {
                    if ((low & 0x80808080u) == 0)
                    {
                        Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = SpreadAscii4(low);
                        inputIndex += 4;
                        outputIndex += 4;
                        continue;
                    }

                    if ((low & 0x8080u) == 0)
                    {
                        Unsafe.As<char, Utf8PackedWord32>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = (low & 255) | ((low & 0xff00u) << 8);
                        inputIndex += 2;
                        outputIndex += 2;
                        continue;
                    }

                    Unsafe.Add(ref outputStart, outputIndex++) = (char)(byte)low;
                    inputIndex++;
                    continue;
                }

                if ((low & 0xc0e0u) == 0x80c0u)
                {
                    uint code = ((low & 31) << 6) | ((low >> 8) & 63);
                    if (code < 128)
                        goto InvalidWindow;
                    Unsafe.Add(ref outputStart, outputIndex++) = (char)code;
                    inputIndex += 2;
                    continue;
                }

                if ((low & 0x00c0c0f0u) == 0x008080e0u)
                {
                    uint code = ((low & 15) << 12) | ((low >> 2) & 0xfc0u) | ((low >> 16) & 63);
                    if (code < 0x800 || (code & 0xf800) == 0xd800)
                        goto InvalidWindow;
                    Unsafe.Add(ref outputStart, outputIndex++) = (char)code;
                    inputIndex += 3;
                    continue;
                }

                if ((low & 0xc0c0c0f8u) == 0x808080f0u)
                {
                    uint code = (((low & 7) << 18) | ((low << 4) & 0x3f000u) | ((low >> 10) & 0xfc0u) | ((low >> 24) & 63)) - 0x10000;
                    if (code > 0xfffff)
                        goto InvalidWindow;
                    Unsafe.As<char, Utf8PackedWord32>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = (0xd800 + (code >> 10)) | ((0xdc00 + (code & 1023)) << 16);
                    outputIndex += 2;
                    inputIndex += 4;
                    continue;
                }

                InvalidWindow:
                    inputIndex += LegacyInvalidPrefixLength(low);
                Unsafe.Add(ref outputStart, outputIndex++) = '\ufffd';
            }

        while (inputIndex < length)
        {
            uint b = Unsafe.Add(ref inputStart, inputIndex++);
            if (b < 128)
            {
                if (BitConverter.IsLittleEndian && IntPtr.Size == 8 && length - inputIndex >= 7)
                {
                    ulong word = Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref inputStart, inputIndex - 1)).Value;
                    if ((word & 0x8080808080808080UL) == 0)
                    {
                        Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = SpreadAscii4((uint)word);
                        Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex + 4)).Value = SpreadAscii4((uint)(word >> 32));
                        inputIndex += 7;
                        outputIndex += 8;
                        continue;
                    }
                }

                Unsafe.Add(ref outputStart, outputIndex++) = (char)b;
                while (inputIndex < length && Unsafe.Add(ref inputStart, inputIndex) < 128)
                    Unsafe.Add(ref outputStart, outputIndex++) = (char)Unsafe.Add(ref inputStart, inputIndex++);
                continue;
            }

            if (b < 0xc2 || b > 0xf4 || inputIndex == length || (Unsafe.Add(ref inputStart, inputIndex) & 0xc0) != 0x80)
                goto Replacement;
            uint second = Unsafe.Add(ref inputStart, inputIndex++);
            if (b < 0xe0)
            {
                Unsafe.Add(ref outputStart, outputIndex++) = (char)(((b & 31) << 6) | (second & 63));
                continue;
            }

            // The legacy decoder consumes the second continuation before rejecting restricted scalar ranges.
            if ((b == 0xe0 && second < 0xa0) || (b == 0xed && second >= 0xa0) || (b == 0xf0 && second < 0x90) || (b == 0xf4 && second > 0x8f))
                goto Replacement;
            if (inputIndex == length || (Unsafe.Add(ref inputStart, inputIndex) & 0xc0) != 0x80)
                goto Replacement;
            uint third = Unsafe.Add(ref inputStart, inputIndex++);
            if (b < 0xf0)
            {
                Unsafe.Add(ref outputStart, outputIndex++) = (char)(((b & 15) << 12) | ((second & 63) << 6) | (third & 63));
                continue;
            }

            if (inputIndex == length || (Unsafe.Add(ref inputStart, inputIndex) & 0xc0) != 0x80)
                goto Replacement;
            uint scalar = (((b & 7) << 18) | ((second & 63) << 12) | ((third & 63) << 6) | (Unsafe.Add(ref inputStart, inputIndex++) & 63u)) - 0x10000;
            Unsafe.Add(ref outputStart, outputIndex) = (char)(0xd800 + (scalar >> 10));
            Unsafe.Add(ref outputStart, outputIndex + 1) = (char)(0xdc00 + (scalar & 1023));
            outputIndex += 2;
            continue;
            Replacement:
                Unsafe.Add(ref outputStart, outputIndex++) = '\ufffd';
        }

        return outputIndex;
    }

    /// <summary>Counts the legacy invalid prefix after a complete lookahead rejected its first scalar.</summary>
    /// <param name = "window">The first four bytes of the already validated input window, in little-endian order.</param>
    /// <returns>The one- to three-byte prefix replaced by one U+FFFD.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LegacyInvalidPrefixLength(uint window)
    {
        uint lead = window & 255;
        if (lead < 0xe0 || lead > 0xf4 || (window & 0xc000u) != 0x8000u)
            return 1;
        if (lead < 0xf0)
            return 2;
        uint second = (window >> 8) & 255;
        if ((lead == 0xf0 && second < 0x90) || (lead == 0xf4 && second > 0x8f))
            return 2;
        return (window & 0xc00000u) == 0x800000u ? 3 : 2;
    }

    /// <summary>Widens pairs of validated two-byte scalars from a long little-endian input prefix.</summary>
    /// <param name = "inputStart">The bounded source start.</param>
    /// <param name = "length">The available source bytes.</param>
    /// <param name = "outputStart">Storage proven to hold at least one character per input byte.</param>
    /// <returns>The decoded character count; twice this count is the consumed byte count.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int DecodeTwoBytePrefix(ref byte inputStart, int length, ref char outputStart)
    {
        int inputIndex = 0;
        int outputIndex = 0;
        while (length - inputIndex >= 4)
        {
            uint word = Unsafe.As<byte, Utf8PackedWord32>(ref Unsafe.Add(ref inputStart, inputIndex)).Value;
            if ((word & 0xc0e0c0e0u) != 0x80c080c0u || (word & 0x001e0000u) == 0 || (word & 0x1eu) == 0)
                break;
            Unsafe.As<char, Utf8PackedWord32>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = ((word & 0x001f001fu) << 6) | ((word >> 8) & 0x003f003fu);
            inputIndex += 4;
            outputIndex += 2;
        }

        return outputIndex;
    }

    /// <summary>Widens pairs of three-byte scalars whose leading bytes exclude overlong and surrogate ranges.</summary>
    /// <param name = "inputStart">The bounded source start.</param>
    /// <param name = "length">The available source bytes, including each complete eight-byte lookahead.</param>
    /// <param name = "outputStart">Storage proven to hold at least one character per input byte.</param>
    /// <returns>The decoded character count; three times this count is the consumed byte count.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int DecodeThreeBytePrefix(ref byte inputStart, int length, ref char outputStart)
    {
        int inputIndex = 0;
        int outputIndex = 0;
        while (length - inputIndex >= 8)
        {
            ulong word = Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref inputStart, inputIndex)).Value;
            if ((word & 0x0000c0c0f0c0c0f0UL) != 0x00008080e08080e0UL || (uint)((byte)word - 0xe1) > 11 || (uint)((byte)(word >> 24) - 0xe1) > 11)
                break;
            Unsafe.As<char, Utf8PackedWord32>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = ((uint)word & 15) << 12 | ((uint)(word >> 2) & 0xfc0u) | ((uint)(word >> 16) & 63u) | ((uint)(word << 4) & 0xf0000000u) | ((uint)(word >> 10) & 0x0fc00000u) | ((uint)(word >> 24) & 0x003f0000u);
            inputIndex += 6;
            outputIndex += 2;
        }

        return outputIndex;
    }

#endif
    /// <summary>Stages a short capacity-constrained decode without the BCL counting pass's fallback objects.</summary>
    /// <param name = "source">The complete encoded token.</param>
    /// <param name = "destination">The bounded final destination.</param>
    /// <returns>The decoded character count, or minus one without destination writes when staging or capacity is insufficient.</returns>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TryDecodeBounded(ReadOnlySpan<byte> source, Span<char> destination)
    {
        if (source.Length <= 768)
        {
            Span<char> scratch = source.Length <= 8 ? stackalloc char[8] : stackalloc char[768];
            int written = Decode(source, scratch);
            if (written <= destination.Length)
            {
                scratch.Slice(0, written).CopyTo(destination);
                return written;
            }
        }

        return -1;
    }

#if NETSTANDARD2_1
    /// <summary>Widens four validated ASCII bytes to four little-endian UTF-16 code units.</summary>
    /// <param name = "value">Four ASCII bytes.</param>
    /// <returns>The widened word.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SpreadAscii4(uint value) => (value & 0xffUL) | ((value & 0xff00UL) << 8) | ((value & 0xff0000UL) << 16) | ((value & 0xff000000UL) << 24);
    /// <summary>Provides an explicitly unaligned 64-bit word used only on 64-bit targets.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Utf8PackedWord64
    {
        /// <summary>The word value.</summary>
        public ulong Value;
    }

    /// <summary>Provides an explicitly unaligned four-byte word.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Utf8PackedWord32
    {
        /// <summary>The word value.</summary>
        public uint Value;
    }
#endif
}
