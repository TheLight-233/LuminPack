using System;
using System.Buffers;
using System.Text;
#if NET8_0_OR_GREATER
using System.Text.Unicode;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace LuminPack.Utility;

public enum SerializeMode : byte
{
    Utf8,
    Utf16
}

/// <summary>
/// Hybrid UTF-16/UTF-8 transcoder. CJK3 / Latin2 use LuminPack SIMD fast paths;
/// ordinary and mixed input uses the platform transcoder when it can preserve
/// the requested buffering and replacement semantics.
/// Portable and bounded fallback: netstandard2.1; SIMD: NET8_0_OR_GREATER.
/// </summary>
public static class StringSerializer
{
    public static OperationStatus Serialize(
        ReadOnlySpan<char> source,
        Span<byte> destination,
        out int charsRead,
        out int bytesWritten,
        SerializeMode mode = SerializeMode.Utf8,
        bool replaceInvalidSequences = true,
        bool isFinalBlock = true)
        => mode == SerializeMode.Utf8
            ? SerializeAsUtf8(source, destination, out charsRead, out bytesWritten,
                replaceInvalidSequences, isFinalBlock)
            : SerializeAsUtf16(source, destination, out charsRead, out bytesWritten,
                replaceInvalidSequences, isFinalBlock);

    public static OperationStatus Deserialize(
        ReadOnlySpan<byte> source,
        Span<char> destination,
        out int bytesRead,
        out int charsWritten,
        SerializeMode mode = SerializeMode.Utf8,
        bool replaceInvalidSequences = true,
        bool isFinalBlock = true)
    {

#if NET8_0_OR_GREATER
        return mode is SerializeMode.Utf8
            ? Utf8.ToUtf16(
                source,
                destination,
                out bytesRead,
                out charsWritten,
                replaceInvalidSequences,
                isFinalBlock)
            : DeserializeFromUtf16(
                source,
                destination,
                out bytesRead,
                out charsWritten,
                replaceInvalidSequences,
                isFinalBlock);
#else
        return mode is SerializeMode.Utf8
            ? DeserializeFromUtf8(
                source,
                destination,
                out bytesRead,
                out charsWritten,
                replaceInvalidSequences,
                isFinalBlock)
            : DeserializeFromUtf16(
                source,
                destination,
                out bytesRead,
                out charsWritten,
                replaceInvalidSequences,
                isFinalBlock);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PreferCustomSimd(ReadOnlySpan<char> source, int destinationLength)
    {
#if NET8_0_OR_GREATER
        if (!BitConverter.IsLittleEndian || !Ssse3.IsSupported ||
            source.Length < 32 || destinationLength < 24)
            return false;

        uint first = source[0];
        if (first < 0x80 || first >= 0xD800 && first <= 0xDFFF)
            return false;

        bool latin = first < 0x800;
        for (int i = 1; i < 16; ++i)
        {
            uint c = source[i];
            if (latin ? (c < 0x80 || c >= 0x800) : !IsBmp3(c))
                return false;
        }
        return true;
#else
        return false;
#endif
    }

    internal static unsafe OperationStatus SerializeAsUtf8(
        ReadOnlySpan<char> source,
        Span<byte> destination,
        out int charsRead,
        out int bytesWritten,
        bool replaceInvalidSequences = true,
        bool isFinalBlock = true)
    {
#if NET8_0_OR_GREATER
        if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(source, destination, escapeJson: false))
        {
            charsRead = source.Length;
            bytesWritten = source.Length;
            return OperationStatus.Done;
        }

        // Native .NET API preserves partial-buffer, invalid-UTF16 and streaming
        // semantics. It also avoids extra SIMD dispatch on ASCII / Emoji / Mixed.
        if (!PreferCustomSimd(source, destination.Length))
            return Utf8.FromUtf16(source, destination, out charsRead, out bytesWritten,
                replaceInvalidSequences, isFinalBlock);
#else
        // netstandard2.1 has no Utf8.FromUtf16. The UTF8Encoding Span API is
        // fast and allocation-free for complete replacement-mode writes, but
        // it has no OperationStatus interface. Only use it when a 3-byte-per-
        // code-unit capacity proof eliminates DestinationTooSmall, and when
        // final-block / replacement settings make its behavior equivalent.
        // Other cases continue through LuminPack's bounded scalar encoder.
        if (replaceInvalidSequences && isFinalBlock &&
            source.Length <= destination.Length / 3)
        {
            bytesWritten = Encoding.UTF8.GetBytes(source, destination);
            charsRead = source.Length;
            return OperationStatus.Done;
        }
#endif
        fixed (char* originalInput = &MemoryMarshal.GetReference(source))
        fixed (byte* originalOutput = &MemoryMarshal.GetReference(destination))
        {
            char* input = originalInput;
            byte* output = originalOutput;
            char* inputEnd = input + source.Length;
            byte* outputEnd = output + destination.Length;
            OperationStatus status;

            while (true)
            {
                status = TranscodeToUtf8(input, (int)(inputEnd - input),
                    output, (int)(outputEnd - output), out input, out output);
                if (status == OperationStatus.Done || status == OperationStatus.DestinationTooSmall)
                    break;
                if (status == OperationStatus.NeedMoreData && !isFinalBlock)
                    break;
                if (!replaceInvalidSequences)
                {
                    status = OperationStatus.InvalidData;
                    break;
                }
                if (outputEnd - output < 3)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }

                // A malformed UTF-16 code unit consumes exactly one char. At the
                // end of a final block the only incomplete sequence is a lone high surrogate.
                ++input;
                output[0] = 0xEF;
                output[1] = 0xBF;
                output[2] = 0xBD;
                output += 3;
            }

            charsRead = (int)(input - originalInput);
            bytesWritten = (int)(output - originalOutput);
            return status;
        }
    }

    internal static unsafe OperationStatus DeserializeFromUtf8(
        ReadOnlySpan<byte> source,
        Span<char> destination,
        out int bytesRead,
        out int charsWritten,
        bool replaceInvalidSequences = true,
        bool isFinalBlock = true)
    {
        fixed (byte* pSource = &MemoryMarshal.GetReference(source))
        fixed (char* pDest = &MemoryMarshal.GetReference(destination))
        {
            byte* pInput = pSource;
            char* pOutput = pDest;
            byte* inputEnd = pSource + source.Length;
            char* outputEnd = pDest + destination.Length;
            OperationStatus status;

            while (true)
            {
                status = TranscodeToUtf16(
                    pInput, (int)(inputEnd - pInput),
                    pOutput, (int)(outputEnd - pOutput),
                    out byte* pInputRemaining,
                    out char* pOutputRemaining);

                pInput = pInputRemaining;
                pOutput = pOutputRemaining;

                if (status is OperationStatus.Done or OperationStatus.DestinationTooSmall)
                    break;

                if (status == OperationStatus.NeedMoreData && !isFinalBlock)
                    break;

                if (!replaceInvalidSequences)
                {
                    status = OperationStatus.InvalidData;
                    break;
                }

                if (pOutput == outputEnd)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }

                int invalidLength = status == OperationStatus.NeedMoreData
                    ? (int)(inputEnd - pInput)
                    : GetInvalidUtf8SequenceLength(pInput, (int)(inputEnd - pInput));

                pInput += invalidLength;
                *pOutput++ = '\uFFFD';
            }

            bytesRead = (int)(pInput - pSource);
            charsWritten = (int)(pOutput - pDest);
            return status;
        }
    }
    
    private static OperationStatus SerializeAsUtf16(
        ReadOnlySpan<char> source, Span<byte> destination,
        out int charsRead, out int bytesWritten,
        bool replaceInvalidSequences = true, bool isFinalBlock = true)
    {
        int count = Math.Min(source.Length, destination.Length / sizeof(char));
        int bytes = count * sizeof(char);
        if (bytes != 0)
            Unsafe.CopyBlockUnaligned(
                ref MemoryMarshal.GetReference(destination),
                ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(source)), (uint)bytes);
        charsRead = count;
        bytesWritten = bytes;
        return count == source.Length ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
    }

    // Keep the original native-UTF16 odd-byte and chunk behavior for compatibility.
    private static OperationStatus DeserializeFromUtf16(
        ReadOnlySpan<byte> source,
        Span<char> destination,
        out int bytesRead,
        out int charsWritten,
        bool replaceInvalidSequences = true,
        bool isFinalBlock = true)
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
                return replaceInvalidSequences ? 
                    OperationStatus.Done : 
                    OperationStatus.InvalidData;
            }
            return OperationStatus.NeedMoreData;
        }
        
        int maxChars = Math.Min(source.Length / sizeof(char), destination.Length);
        int bytesToCopy = maxChars * sizeof(char);
        
        if (bytesToCopy > 0)
        {
            Unsafe.CopyBlockUnaligned(
                ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(destination)),
                ref MemoryMarshal.GetReference(source),
                (uint)bytesToCopy);
        }
        
        bytesRead = bytesToCopy;
        charsWritten = maxChars;
        
        if (source.Length > bytesToCopy)
        {
            return OperationStatus.DestinationTooSmall;
        }
        
        return OperationStatus.Done;
    }
        
        
    internal static unsafe OperationStatus TranscodeToUtf8(
        char* pInputBuffer, int inputLength,
        byte* pOutputBuffer, int outputBytesRemaining,
        out char* pInputBufferRemaining, out byte* pOutputBufferRemaining)
    {
        char* input = pInputBuffer;
        byte* output = pOutputBuffer;
        char* inputEnd = input + inputLength;
        byte* outputEnd = output + outputBytesRemaining;

#if NET8_0_OR_GREATER
        if (BitConverter.IsLittleEndian && inputLength >= 32 && outputBytesRemaining >= 24)
        {
            // Long all-ASCII prefix: branch once, then copy 32 chars at a time.
            // A short or mixed string stays on the low-overhead portable path.
            if (Sse2.IsSupported && inputLength >= 128 && outputBytesRemaining >= 128)
            {
                Vector128<ushort> first = Unsafe.ReadUnaligned<Vector128<ushort>>(input);
                Vector128<ushort> mask = Vector128.Create((ushort)0xFF80);
                Vector128<short> initial = Sse2.CompareEqual(
                    Sse2.And(first, mask).AsInt16(), Vector128<short>.Zero);
                if (Sse2.MoveMask(initial.AsByte()) == 0xFFFF)
                {
                    EncodeAsciiRun(ref input, ref output, inputEnd, outputEnd);
                }
            }

            // The dedicated CJK / Latin SIMD kernels are already proven fast.
            // Re-entry is attempted only after a successful homogeneous run,
            // not for every character in general text.
            if (Ssse3.IsSupported)
            {
                while (inputEnd - input >= 32 && outputEnd - output >= 24)
                {
                    uint lead = *input;
                    if (lead >= 0x80 && (lead < 0x800 || IsBmp3(lead)))
                    {
                        if (TryEncodeVectorRun(ref input, ref output, inputEnd, outputEnd, lead))
                        {
                            // Sparse ASCII delimiter between large CJK runs.
                            // Only consume it here when the next vector-friendly
                            // run is plausibly of the same 3-byte class.
                            if (inputEnd - input >= 33 && outputEnd - output >= 25 &&
                                *input < 0x80 && IsBmp3(input[1]))
                            {
                                *output++ = (byte)*input++;
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
        if (input != pInputBuffer && inputEnd - input >= 64)
        {
            OperationStatus nativeStatus = Utf8.FromUtf16(
                new ReadOnlySpan<char>(input, (int)(inputEnd - input)),
                new Span<byte>(output, (int)(outputEnd - output)),
                out int nativeChars, out int nativeBytes,
                replaceInvalidSequences: false, isFinalBlock: false);
            pInputBufferRemaining = input + nativeChars;
            pOutputBufferRemaining = output + nativeBytes;
            return nativeStatus;
        }
#endif
        OperationStatus status = TranscodeToUtf8Scalar(
            input, (int)(inputEnd - input), output, (int)(outputEnd - output),
            out char* remainingInput, out byte* remainingOutput);
        pInputBufferRemaining = remainingInput;
        pOutputBufferRemaining = remainingOutput;
        return status;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// 32 ASCII chars per iteration, SSE2 (baseline x64 hardware).
    /// Two exact 16-byte stores. No input over-read or output over-store.
    /// The remainder is handled by the shared portable implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe void EncodeAsciiRun(
        ref char* input, ref byte* output, char* inputEnd, byte* outputEnd)
    {
        Vector128<ushort> nonAsciiMask = Vector128.Create((ushort)0xFF80);
        Vector128<short> zero = Vector128<short>.Zero;
        while (inputEnd - input >= 32 && outputEnd - output >= 32)
        {
            Vector128<ushort> a = Unsafe.ReadUnaligned<Vector128<ushort>>(input);
            Vector128<ushort> b = Unsafe.ReadUnaligned<Vector128<ushort>>(input + 8);
            Vector128<ushort> c = Unsafe.ReadUnaligned<Vector128<ushort>>(input + 16);
            Vector128<ushort> d = Unsafe.ReadUnaligned<Vector128<ushort>>(input + 24);
            Vector128<ushort> upper = Sse2.And(
                Sse2.Or(Sse2.Or(a, b), Sse2.Or(c, d)), nonAsciiMask);
            if (Sse2.MoveMask(Sse2.CompareEqual(upper.AsInt16(), zero).AsByte()) != 0xFFFF)
                break;
            Unsafe.WriteUnaligned(output,
                Sse2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16()));
            Unsafe.WriteUnaligned(output + 16,
                Sse2.PackUnsignedSaturate(c.AsInt16(), d.AsInt16()));
            input += 32;
            output += 32;
        }
        while (inputEnd - input >= 16 && outputEnd - output >= 16)
        {
            Vector128<ushort> a = Unsafe.ReadUnaligned<Vector128<ushort>>(input);
            Vector128<ushort> b = Unsafe.ReadUnaligned<Vector128<ushort>>(input + 8);
            Vector128<ushort> upper = Sse2.And(Sse2.Or(a, b), nonAsciiMask);
            if (Sse2.MoveMask(Sse2.CompareEqual(upper.AsInt16(), zero).AsByte()) != 0xFFFF)
                break;
            Unsafe.WriteUnaligned(output,
                Sse2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16()));
            input += 16;
            output += 16;
        }
    }
#endif

    /// <summary>Portable UTF-8 encoder; no SIMD dispatch inside this hot loop.</summary>
    private static unsafe OperationStatus TranscodeToUtf8Scalar(
        char* pInputBuffer, int inputLength,
        byte* pOutputBuffer, int outputBytesRemaining,
        out char* pInputBufferRemaining, out byte* pOutputBufferRemaining)
    {
        char* input = pInputBuffer;
        byte* output = pOutputBuffer;
        char* inputEnd = input + inputLength;
        byte* outputEnd = output + outputBytesRemaining;
        OperationStatus status = OperationStatus.Done;
        bool littleEndian = BitConverter.IsLittleEndian;

        while (input < inputEnd)
        {
            uint c = *input;
            if (c < 0x80)
            {
                // ASCII SWAR: 4 UTF-16 code units -> one exact 32-bit write.
                // This is the portable fast path for netstandard2.1 as well.
                if (littleEndian && inputEnd - input >= 4 && outputEnd - output >= 4)
                {
                    ulong four = Unsafe.ReadUnaligned<ulong>(input);
                    if ((four & 0xFF80FF80FF80FF80UL) == 0)
                    {
                        uint ascii = (uint)(four & 0xFFUL)
                            | (uint)((four >> 8) & 0xFF00UL)
                            | (uint)((four >> 16) & 0xFF0000UL)
                            | (uint)((four >> 24) & 0xFF000000UL);
                        Unsafe.WriteUnaligned(output, ascii);
                        input += 4;
                        output += 4;
                        continue;
                    }
                }
                if (output == outputEnd)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }
                *output++ = (byte)c;
                ++input;
                continue;
            }

            if (c < 0x800)
            {
                // Two adjacent Latin / Cyrillic / Arabic UTF-16 code units can be
                // emitted with a single 32-bit store. No unrelated CJK checks here.
                if (littleEndian && inputEnd - input >= 2 && outputEnd - output >= 4)
                {
                    uint next = input[1];
                    if (next >= 0x80 && next < 0x800)
                    {
                        uint bytes = (0xC0U | (c >> 6))
                            | ((0x80U | (c & 0x3FU)) << 8)
                            | ((0xC0U | (next >> 6)) << 16)
                            | ((0x80U | (next & 0x3FU)) << 24);
                        Unsafe.WriteUnaligned(output, bytes);
                        input += 2;
                        output += 4;
                        continue;
                    }
                }
                if (outputEnd - output < 2)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }
                output[0] = (byte)(0xC0 | (c >> 6));
                output[1] = (byte)(0x80 | (c & 63));
                output += 2;
                ++input;
                continue;
            }

            if (IsBmp3(c))
            {
                // Two BMP characters -> exactly 6 UTF-8 bytes (4 + 2 stores).
                if (littleEndian && inputEnd - input >= 2 && outputEnd - output >= 6)
                {
                    uint next = input[1];
                    if (IsBmp3(next))
                    {
                        uint firstFour = (0xE0U | (c >> 12))
                            | ((0x80U | ((c >> 6) & 0x3FU)) << 8)
                            | ((0x80U | (c & 0x3FU)) << 16)
                            | ((0xE0U | (next >> 12)) << 24);
                        ushort lastTwo = (ushort)((0x80U | ((next >> 6) & 0x3FU))
                            | ((0x80U | (next & 0x3FU)) << 8));
                        Unsafe.WriteUnaligned(output, firstFour);
                        Unsafe.WriteUnaligned(output + 4, lastTwo);
                        input += 2;
                        output += 6;
                        continue;
                    }
                }
                if (outputEnd - output < 3)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }
                output[0] = (byte)(0xE0 | (c >> 12));
                output[1] = (byte)(0x80 | ((c >> 6) & 63));
                output[2] = (byte)(0x80 | (c & 63));
                output += 3;
                ++input;
                continue;
            }

            if (c <= 0xDBFF) // high surrogate
            {
                if (inputEnd - input < 2)
                {
                    status = OperationStatus.NeedMoreData;
                    break;
                }
                uint low = input[1];
                if (low < 0xDC00 || low > 0xDFFF)
                {
                    status = OperationStatus.InvalidData;
                    break;
                }
                if (outputEnd - output < 4)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }
                uint scalar = 0x10000 + ((c - 0xD800) << 10) + (low - 0xDC00);
                output[0] = (byte)(0xF0 | (scalar >> 18));
                output[1] = (byte)(0x80 | ((scalar >> 12) & 63));
                output[2] = (byte)(0x80 | ((scalar >> 6) & 63));
                output[3] = (byte)(0x80 | (scalar & 63));
                input += 2;
                output += 4;
            }
            else // isolated low surrogate
            {
                status = OperationStatus.InvalidData;
                break;
            }
        }

        pInputBufferRemaining = input;
        pOutputBufferRemaining = output;
        return status;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBmp3(uint ch)
        => ch >= 0x0800 && (ch < 0xD800 || ch > 0xDFFF);

    /// <summary>Core decoder: ASCII-vector and portable Unicode validation.</summary>
    internal static unsafe OperationStatus TranscodeToUtf16(
        byte* pInputBuffer,
        int inputLength,
        char* pOutputBuffer,
        int outputCharsRemaining,
        out byte* pInputBufferRemaining,
        out char* pOutputBufferRemaining)
    {
        byte* inputEnd = pInputBuffer + inputLength;
        char* outputEnd = pOutputBuffer + outputCharsRemaining;

        while (pInputBuffer < inputEnd)
        {
            pInputBufferRemaining = pInputBuffer;
            pOutputBufferRemaining = pOutputBuffer;

            uint firstByte = *pInputBuffer;

            if (firstByte < 0x80)
            {
                if (pOutputBuffer == outputEnd)
                    return Complete(OperationStatus.DestinationTooSmall);

                *pOutputBuffer = (char)firstByte;
                pInputBuffer++;
                pOutputBuffer++;
                continue;
            }

            // Only C2..DF are valid two-byte leaders. C0/C1 are overlong.
            if (firstByte is >= 0xC2 and <= 0xDF)
            {
                if (pInputBuffer + 2 > inputEnd)
                    return Complete(OperationStatus.NeedMoreData);

                uint secondByte = pInputBuffer[1];
                if (!IsUtf8ContinuationByte(secondByte))
                    return Complete(OperationStatus.InvalidData);

                if (pOutputBuffer == outputEnd)
                    return Complete(OperationStatus.DestinationTooSmall);

                uint codePoint = ((firstByte & 0x1F) << 6) | (secondByte & 0x3F);
                *pOutputBuffer = (char)codePoint;
                pInputBuffer += 2;
                pOutputBuffer++;
                continue;
            }

            if (firstByte is >= 0xE0 and <= 0xEF)
            {
                if (pInputBuffer + 2 > inputEnd)
                    return Complete(OperationStatus.NeedMoreData);

                uint secondByte = pInputBuffer[1];
                if (!IsUtf8ContinuationByte(secondByte) ||
                    (firstByte == 0xE0 && secondByte < 0xA0) ||
                    (firstByte == 0xED && secondByte >= 0xA0))
                    return Complete(OperationStatus.InvalidData);

                if (pInputBuffer + 3 > inputEnd)
                    return Complete(OperationStatus.NeedMoreData);

                uint thirdByte = pInputBuffer[2];
                if (!IsUtf8ContinuationByte(thirdByte))
                    return Complete(OperationStatus.InvalidData);

                if (pOutputBuffer == outputEnd)
                    return Complete(OperationStatus.DestinationTooSmall);

                uint codePoint = ((firstByte & 0x0F) << 12) | 
                                 ((secondByte & 0x3F) << 6) | 
                                 (thirdByte & 0x3F);
                *pOutputBuffer = (char)codePoint;
                pInputBuffer += 3;
                pOutputBuffer++;
                continue;
            }

            // F0..F4 are the only valid four-byte leaders.
            if (firstByte is >= 0xF0 and <= 0xF4)
            {
                if (pInputBuffer + 2 > inputEnd)
                    return Complete(OperationStatus.NeedMoreData);

                uint secondByte = pInputBuffer[1];
                if (!IsUtf8ContinuationByte(secondByte) ||
                    (firstByte == 0xF0 && secondByte < 0x90) ||
                    (firstByte == 0xF4 && secondByte > 0x8F))
                    return Complete(OperationStatus.InvalidData);

                if (pInputBuffer + 3 > inputEnd)
                    return Complete(OperationStatus.NeedMoreData);

                uint thirdByte = pInputBuffer[2];
                if (!IsUtf8ContinuationByte(thirdByte))
                    return Complete(OperationStatus.InvalidData);

                if (pInputBuffer + 4 > inputEnd)
                    return Complete(OperationStatus.NeedMoreData);

                uint fourthByte = pInputBuffer[3];
                if (!IsUtf8ContinuationByte(fourthByte))
                    return Complete(OperationStatus.InvalidData);

                if (outputEnd - pOutputBuffer < 2)
                    return Complete(OperationStatus.DestinationTooSmall);

                uint codePoint = ((firstByte & 0x07) << 18) | 
                                 ((secondByte & 0x3F) << 12) | 
                                 ((thirdByte & 0x3F) << 6) | 
                                 (fourthByte & 0x3F);

                codePoint -= 0x10000;
                pOutputBuffer[0] = (char)((codePoint >> 10) + 0xD800);
                pOutputBuffer[1] = (char)((codePoint & 0x3FF) + 0xDC00);

                pInputBuffer += 4;
                pOutputBuffer += 2;
                continue;
            }

            return Complete(OperationStatus.InvalidData);
        }

        pInputBufferRemaining = pInputBuffer;
        pOutputBufferRemaining = pOutputBuffer;
        return OperationStatus.Done;

        static OperationStatus Complete(OperationStatus status) => status;
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUtf8ContinuationByte(uint value) => (value & 0xC0) == 0x80;

    private static unsafe int GetInvalidUtf8SequenceLength(byte* input, int remaining)
    {
        if (remaining <= 1) return 1;
        uint b0 = input[0];
        uint b1 = input[1];
        if (b0 >= 0xE0 && b0 <= 0xEF)
        {
            if (!IsUtf8ContinuationByte(b1) || (b0 == 0xE0 && b1 < 0xA0)
                || (b0 == 0xED && b1 >= 0xA0)) return 1;
            return remaining > 2 && !IsUtf8ContinuationByte(input[2]) ? 2 : 1;
        }
        if (b0 >= 0xF0 && b0 <= 0xF4)
        {
            if (!IsUtf8ContinuationByte(b1) || (b0 == 0xF0 && b1 < 0x90)
                || (b0 == 0xF4 && b1 > 0x8F)) return 1;
            if (remaining > 2 && !IsUtf8ContinuationByte(input[2])) return 2;
            if (remaining > 3 && !IsUtf8ContinuationByte(input[3])) return 3;
        }
        return 1;
    }

#if NET8_0_OR_GREATER
    // Masks for converting 8 BMP3 characters into exactly 24 UTF-8 bytes.
    // Retain the already-proven CJK3 path and its exact stores.
    private const byte Skip = 0x80;
    private static readonly Vector128<byte> BmpF0 = Vector128.Create(
        (byte)0, Skip, Skip, (byte)1, Skip, Skip, (byte)2, Skip,
        Skip, (byte)3, Skip, Skip, (byte)4, Skip, Skip, (byte)5);
    private static readonly Vector128<byte> BmpF1 = Vector128.Create(
        Skip, (byte)0, Skip, Skip, (byte)1, Skip, Skip, (byte)2,
        Skip, Skip, (byte)3, Skip, Skip, (byte)4, Skip, Skip);
    private static readonly Vector128<byte> BmpF2 = Vector128.Create(
        Skip, Skip, (byte)0, Skip, Skip, (byte)1, Skip, Skip,
        (byte)2, Skip, Skip, (byte)3, Skip, Skip, (byte)4, Skip);
    private static readonly Vector128<byte> BmpS0 = Vector128.Create(
        Skip, Skip, (byte)6, Skip, Skip, (byte)7, Skip, Skip,
        Skip, Skip, Skip, Skip, Skip, Skip, Skip, Skip);
    private static readonly Vector128<byte> BmpS1 = Vector128.Create(
        (byte)5, Skip, Skip, (byte)6, Skip, Skip, (byte)7, Skip,
        Skip, Skip, Skip, Skip, Skip, Skip, Skip, Skip);
    private static readonly Vector128<byte> BmpS2 = Vector128.Create(
        Skip, (byte)5, Skip, Skip, (byte)6, Skip, Skip, (byte)7,
        Skip, Skip, Skip, Skip, Skip, Skip, Skip, Skip);

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
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe bool TryEncodeVectorRun(
        ref char* input, ref byte* output, char* inputEnd, byte* outputEnd, uint lead)
    {
        char* original = input;
        if (lead < 0x80)
        {
            if (Avx2.IsSupported)
            {
                Vector256<ushort> highMask = Vector256.Create((ushort)0xFF80);
                while (inputEnd - input >= 16 && outputEnd - output >= 16)
                {
                    Vector256<ushort> chars = Unsafe.ReadUnaligned<Vector256<ushort>>(input);
                    Vector256<short> ascii = Avx2.CompareEqual(
                        Avx2.And(chars, highMask).AsInt16(), Vector256<short>.Zero);
                    if (Avx2.MoveMask(ascii.AsByte()) != -1)
                        break;
                    Vector256<byte> packed = Avx2.PackUnsignedSaturate(
                        chars.AsInt16(), chars.AsInt16());
                    Unsafe.WriteUnaligned(output, packed.GetLower().AsUInt64().GetElement(0));
                    Unsafe.WriteUnaligned(output + 8, packed.GetUpper().AsUInt64().GetElement(0));
                    input += 16;
                    output += 16;
                }
            }
            if (Ssse3.IsSupported)
            {
                Vector128<ushort> highMask = Vector128.Create((ushort)0xFF80);
                while (inputEnd - input >= 8 && outputEnd - output >= 8)
                {
                    Vector128<ushort> chars = Unsafe.ReadUnaligned<Vector128<ushort>>(input);
                    Vector128<short> ascii = Sse2.CompareEqual(
                        Sse2.And(chars, highMask).AsInt16(), Vector128<short>.Zero);
                    if (Sse2.MoveMask(ascii.AsByte()) != 0xFFFF)
                        break;
                    Vector128<byte> packed = Sse2.PackUnsignedSaturate(
                        chars.AsInt16(), chars.AsInt16());
                    Unsafe.WriteUnaligned(output, packed.AsUInt64().GetElement(0));
                    input += 8;
                    output += 8;
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
                while (inputEnd - input >= 16 && outputEnd - output >= 32)
                {
                    Vector256<ushort> chars = Unsafe.ReadUnaligned<Vector256<ushort>>(input);
                    Vector256<short> valid = Avx2.And(
                        Avx2.CompareGreaterThan(chars.AsInt16(), min),
                        Avx2.CompareGreaterThan(max, chars.AsInt16()));
                    if (Avx2.MoveMask(valid.AsByte()) != -1)
                        break;
                    Vector256<ushort> low = Avx2.Or(Avx2.ShiftRightLogical(chars, 6), leadBits);
                    Vector256<ushort> high = Avx2.ShiftLeftLogical(
                        Avx2.Or(Avx2.And(chars, sixBits), trailBits), 8);
                    Unsafe.WriteUnaligned(output, Avx2.Or(low, high));
                    input += 16;
                    output += 32;
                }
            }
            if (Ssse3.IsSupported)
            {
                Vector128<short> min = Vector128.Create((short)127);
                Vector128<short> max = Vector128.Create((short)2048);
                Vector128<ushort> sixBits = Vector128.Create((ushort)63);
                Vector128<ushort> leadBits = Vector128.Create((ushort)0xC0);
                Vector128<ushort> trailBits = Vector128.Create((ushort)0x80);
                while (inputEnd - input >= 8 && outputEnd - output >= 16)
                {
                    Vector128<ushort> chars = Unsafe.ReadUnaligned<Vector128<ushort>>(input);
                    Vector128<short> valid = Sse2.And(
                        Sse2.CompareGreaterThan(chars.AsInt16(), min),
                        Sse2.CompareGreaterThan(max, chars.AsInt16()));
                    if (Sse2.MoveMask(valid.AsByte()) != 0xFFFF)
                        break;
                    Vector128<ushort> low = Sse2.Or(Sse2.ShiftRightLogical(chars, 6), leadBits);
                    Vector128<ushort> high = Sse2.ShiftLeftLogical(
                        Sse2.Or(Sse2.And(chars, sixBits), trailBits), 8);
                    Unsafe.WriteUnaligned(output, Sse2.Or(low, high));
                    input += 8;
                    output += 16;
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
                while (inputEnd - input >= 16 && outputEnd - output >= 48)
                {
                    Vector256<ushort> chars = Unsafe.ReadUnaligned<Vector256<ushort>>(input);
                    Vector256<short> top = Avx2.And(chars, topMask).AsInt16();
                    Vector256<short> invalid = Avx2.Or(
                        Avx2.CompareEqual(top, Vector256<short>.Zero),
                        Avx2.CompareEqual(top, surrogate));
                    if (Avx2.MoveMask(invalid.AsByte()) != 0)
                        break;
                    Vector256<ushort> leadBytes = Avx2.Or(Avx2.ShiftRightLogical(chars, 12), e0);
                    Vector256<ushort> middleBytes = Avx2.Or(
                        Avx2.And(Avx2.ShiftRightLogical(chars, 6), sixBits), trail);
                    Vector256<ushort> lastBytes = Avx2.Or(Avx2.And(chars, sixBits), trail);
                    Vector256<byte> a = Avx2.PackUnsignedSaturate(leadBytes.AsInt16(), leadBytes.AsInt16());
                    Vector256<byte> b = Avx2.PackUnsignedSaturate(middleBytes.AsInt16(), middleBytes.AsInt16());
                    Vector256<byte> c = Avx2.PackUnsignedSaturate(lastBytes.AsInt16(), lastBytes.AsInt16());
                    Vector256<byte> first = Avx2.Or(Avx2.Or(
                        Avx2.Shuffle(a, BmpF0x2), Avx2.Shuffle(b, BmpF1x2)),
                        Avx2.Shuffle(c, BmpF2x2));
                    Vector256<byte> second = Avx2.Or(Avx2.Or(
                        Avx2.Shuffle(a, BmpS0x2), Avx2.Shuffle(b, BmpS1x2)),
                        Avx2.Shuffle(c, BmpS2x2));
                    Unsafe.WriteUnaligned(output, first.GetLower());
                    Unsafe.WriteUnaligned(output + 16, second.GetLower().AsUInt64().GetElement(0));
                    Unsafe.WriteUnaligned(output + 24, first.GetUpper());
                    Unsafe.WriteUnaligned(output + 40, second.GetUpper().AsUInt64().GetElement(0));
                    input += 16;
                    output += 48;
                }
            }
            if (Ssse3.IsSupported)
            {
                Vector128<ushort> topMask = Vector128.Create((ushort)0xF800);
                Vector128<short> surrogate = Vector128.Create(unchecked((short)0xD800));
                Vector128<ushort> e0 = Vector128.Create((ushort)0xE0);
                Vector128<ushort> trail = Vector128.Create((ushort)0x80);
                Vector128<ushort> sixBits = Vector128.Create((ushort)63);
                while (inputEnd - input >= 8 && outputEnd - output >= 24)
                {
                    Vector128<ushort> chars = Unsafe.ReadUnaligned<Vector128<ushort>>(input);
                    Vector128<short> top = Sse2.And(chars, topMask).AsInt16();
                    Vector128<short> invalid = Sse2.Or(
                        Sse2.CompareEqual(top, Vector128<short>.Zero),
                        Sse2.CompareEqual(top, surrogate));
                    if (Sse2.MoveMask(invalid.AsByte()) != 0)
                        break;
                    Vector128<ushort> leadBytes = Sse2.Or(Sse2.ShiftRightLogical(chars, 12), e0);
                    Vector128<ushort> middleBytes = Sse2.Or(
                        Sse2.And(Sse2.ShiftRightLogical(chars, 6), sixBits), trail);
                    Vector128<ushort> lastBytes = Sse2.Or(Sse2.And(chars, sixBits), trail);
                    Vector128<byte> a = Sse2.PackUnsignedSaturate(leadBytes.AsInt16(), leadBytes.AsInt16());
                    Vector128<byte> b = Sse2.PackUnsignedSaturate(middleBytes.AsInt16(), middleBytes.AsInt16());
                    Vector128<byte> c = Sse2.PackUnsignedSaturate(lastBytes.AsInt16(), lastBytes.AsInt16());
                    Vector128<byte> first = Sse2.Or(Sse2.Or(
                        Ssse3.Shuffle(a, BmpF0), Ssse3.Shuffle(b, BmpF1)),
                        Ssse3.Shuffle(c, BmpF2));
                    Vector128<byte> second = Sse2.Or(Sse2.Or(
                        Ssse3.Shuffle(a, BmpS0), Ssse3.Shuffle(b, BmpS1)),
                        Ssse3.Shuffle(c, BmpS2));
                    Unsafe.WriteUnaligned(output, first);
                    Unsafe.WriteUnaligned(output + 16, second.AsUInt64().GetElement(0));
                    input += 8;
                    output += 24;
                }
            }
        }
        return input != original;
    }
#endif
}
