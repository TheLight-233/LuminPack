#if NETSTANDARD2_1
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Utility;

public static partial class StringSerializer
{
    /// <summary>Completes a declined native decode in its unpublished result using the established runtime decoder.</summary>
    /// <param name = "source">The first byte of the validated payload window.</param>
    /// <param name = "length">The complete encoded byte count.</param>
    /// <param name = "result">The unpublished string sized to the recorded UTF-16 count.</param>
    /// <returns>The same string after complete strict decoding.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string DeserializeUtf8AfterDecline(ref byte source, int length, string result)
    {
        try
        {
            int written = StrictUtf8.GetChars(MemoryMarshal.CreateReadOnlySpan(ref source, length),
                MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(result.AsSpan()), result.Length));
            if (written != result.Length)
                LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
            return result;
        }
        catch (ArgumentException)
        {
            // Both malformed UTF-8 and a short recorded destination retain the original framing failure.
            LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
            return null!;
        }
    }

    /// <summary>Encodes a final UTF-16 block into a previously proven worst-case UTF-8 destination without fallback-buffer allocation.</summary>
    /// <param name = "source">The complete input block.</param>
    /// <param name = "destination">Storage with at least three bytes per source code unit.</param>
    /// <param name = "charsRead">The complete input length, or the valid prefix before malformed input in strict mode.</param>
    /// <param name = "bytesWritten">The number of bytes written before completion or rejection.</param>
    /// <param name = "replaceInvalidSequences">Whether each unpaired surrogate is replaced by U+FFFD.</param>
    /// <returns>Done, or InvalidData with the same strict prefix as the bounded encoder.</returns>
    /// <remarks>The caller proves final-block status and capacity. Streaming and smaller windows keep the bounded encoder.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static unsafe OperationStatus SerializeUtf8Complete(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences)
    {
        fixed (char* sourceStart = source)
        fixed (byte* outputStart = destination)
        {
            char* input = sourceStart;
            char* sourceEnd = sourceStart + source.Length;
            byte* output = outputStart;
            while (input < sourceEnd)
            {
                uint codeUnit = *input++;
                if (codeUnit < 0x80)
                {
                    // The current code unit is already consumed; the byte thresholds include only the remaining units.
                    long availableBytes = (long)unchecked((nint)sourceEnd - (nint)input);
                    if (BitConverter.IsLittleEndian && IntPtr.Size == 8 && availableBytes >= 6)
                    {
                        ulong firstWord = ((Utf8PackedWord64*)(input - 1))->Value;
                        if (availableBytes >= 14)
                        {
                            ulong secondWord = ((Utf8PackedWord64*)(input + 3))->Value;
                            if (availableBytes >= 30)
                            {
                                ulong thirdWord = ((Utf8PackedWord64*)(input + 7))->Value, fourthWord = ((Utf8PackedWord64*)(input + 11))->Value;
                                if (((firstWord | secondWord | thirdWord | fourthWord) & 0xff80ff80ff80ff80UL) == 0)
                                {
                                    firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                                    secondWord = (secondWord | (secondWord >> 8)) & 0x0000ffff0000ffffUL;
                                    thirdWord = (thirdWord | (thirdWord >> 8)) & 0x0000ffff0000ffffUL;
                                    fourthWord = (fourthWord | (fourthWord >> 8)) & 0x0000ffff0000ffffUL;
                                    ((Utf8PackedWord64*)output)->Value = (uint)(firstWord | (firstWord >> 16)) | ((ulong)(uint)(secondWord | (secondWord >> 16)) << 32);
                                    ((Utf8PackedWord64*)(output + 8))->Value = (uint)(thirdWord | (thirdWord >> 16)) | ((ulong)(uint)(fourthWord | (fourthWord >> 16)) << 32);
                                    input += 15;
                                    output += 16;
                                    continue;
                                }

                                // Reuse validated prefixes of a failed wide run instead of probing each remaining ASCII character again.
                                if (((firstWord | secondWord | thirdWord) & 0xff80ff80ff80ff80UL) == 0)
                                {
                                    firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                                    secondWord = (secondWord | (secondWord >> 8)) & 0x0000ffff0000ffffUL;
                                    thirdWord = (thirdWord | (thirdWord >> 8)) & 0x0000ffff0000ffffUL;
                                    ((Utf8PackedWord64*)output)->Value = (uint)(firstWord | (firstWord >> 16)) | ((ulong)(uint)(secondWord | (secondWord >> 16)) << 32);
                                    ((Utf8PackedWord32*)(output + 8))->Value = (uint)(thirdWord | (thirdWord >> 16));
                                    input += 11;
                                    output += 12;
                                    goto ScalarAsciiTail;
                                }

                                if (((firstWord | secondWord) & 0xff80ff80ff80ff80UL) == 0)
                                {
                                    firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                                    secondWord = (secondWord | (secondWord >> 8)) & 0x0000ffff0000ffffUL;
                                    ((Utf8PackedWord64*)output)->Value = (uint)(firstWord | (firstWord >> 16)) | ((ulong)(uint)(secondWord | (secondWord >> 16)) << 32);
                                    input += 7;
                                    output += 8;
                                    goto ScalarAsciiTail;
                                }

                                if ((firstWord & 0xff80ff80ff80ff80UL) == 0)
                                {
                                    firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                                    ((Utf8PackedWord32*)output)->Value = (uint)(firstWord | (firstWord >> 16));
                                    input += 3;
                                    output += 4;
                                    goto ScalarAsciiTail;
                                }

                                *output++ = (byte)codeUnit;
                                goto ScalarAsciiTail;
                            }

                            if (((firstWord | secondWord) & 0xff80ff80ff80ff80UL) == 0)
                            {
                                firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                                secondWord = (secondWord | (secondWord >> 8)) & 0x0000ffff0000ffffUL;
                                ((Utf8PackedWord64*)output)->Value = (uint)(firstWord | (firstWord >> 16)) | ((ulong)(uint)(secondWord | (secondWord >> 16)) << 32);
                                input += 7;
                                output += 8;
                                continue;
                            }
                        }

                        if ((firstWord & 0xff80ff80ff80ff80UL) == 0)
                        {
                            firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                            ((Utf8PackedWord32*)output)->Value = (uint)(firstWord | (firstWord >> 16));
                            input += 3;
                            output += 4;
                            continue;
                        }
                    }

                    *output++ = (byte)codeUnit;
                    ScalarAsciiTail:
                        while (input < sourceEnd && *input < 0x80)
                            *output++ = (byte)*input++;
                    continue;
                }

                if (codeUnit < 0x800)
                {
                    output[0] = (byte)(0xc0 | (codeUnit >> 6));
                    output[1] = (byte)(0x80 | (codeUnit & 63));
                    output += 2;
                    continue;
                }

                if (codeUnit >= 0xd800 && codeUnit <= 0xdfff)
                {
                    if (codeUnit <= 0xdbff && input < sourceEnd && (uint)(*input - 0xdc00) <= 0x3ff)
                    {
                        uint scalar = 0x10000 + ((codeUnit - 0xd800) << 10) + (*input++ - 0xdc00u);
                        output[0] = (byte)(0xf0 | (scalar >> 18));
                        output[1] = (byte)(0x80 | ((scalar >> 12) & 63));
                        output[2] = (byte)(0x80 | ((scalar >> 6) & 63));
                        output[3] = (byte)(0x80 | (scalar & 63));
                        output += 4;
                        continue;
                    }

                    if (!replaceInvalidSequences)
                    {
                        charsRead = (int)((long)(unchecked((nint)input - (nint)sourceStart) >> 1)) - 1;
                        bytesWritten = (int)unchecked((nint)output - (nint)outputStart);
                        return OperationStatus.InvalidData;
                    }

                    codeUnit = 0xfffd;
                }

                output[0] = (byte)(0xe0 | (codeUnit >> 12));
                output[1] = (byte)(0x80 | ((codeUnit >> 6) & 63));
                output[2] = (byte)(0x80 | (codeUnit & 63));
                output += 3;
            }

            charsRead = source.Length;
            bytesWritten = (int)unchecked((nint)output - (nint)outputStart);
            return OperationStatus.Done;
        }
    }

    /// <summary>Expresses byte-aligned word access without a generic helper call; used only on 64-bit targets.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Utf8PackedWord64
    {
        public ulong Value;
    }

    /// <summary>Expresses byte-aligned access for the final four-byte ASCII prefix.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Utf8PackedWord32
    {
        public uint Value;
    }
}
#endif

