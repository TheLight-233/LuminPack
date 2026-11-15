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
    internal static OperationStatus SerializeUtf8Complete(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences)
    {
        ref char inputStart = ref MemoryMarshal.GetReference(source);
        ref byte outputStart = ref MemoryMarshal.GetReference(destination);
        int inputIndex = 0;
        int outputIndex = 0;
        while (inputIndex < source.Length)
        {
            uint codeUnit = Unsafe.Add(ref inputStart, inputIndex++);
            if (codeUnit < 0x80)
            {
                // The current code unit is already consumed; the byte thresholds include only the remaining units.
                long availableBytes = (long)(source.Length - inputIndex) * sizeof(char);
                if (BitConverter.IsLittleEndian && IntPtr.Size == 8 && availableBytes >= 6)
                {
                    ulong firstWord = Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref inputStart, inputIndex - 1)).Value;
                    if (availableBytes >= 14)
                    {
                        ulong secondWord = Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref inputStart, inputIndex + 3)).Value;
                        if (availableBytes >= 30)
                        {
                            ulong thirdWord = Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref inputStart, inputIndex + 7)).Value, fourthWord = Unsafe.As<char, Utf8PackedWord64>(ref Unsafe.Add(ref inputStart, inputIndex + 11)).Value;
                            if (((firstWord | secondWord | thirdWord | fourthWord) & 0xff80ff80ff80ff80UL) == 0)
                            {
                                firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                                secondWord = (secondWord | (secondWord >> 8)) & 0x0000ffff0000ffffUL;
                                thirdWord = (thirdWord | (thirdWord >> 8)) & 0x0000ffff0000ffffUL;
                                fourthWord = (fourthWord | (fourthWord >> 8)) & 0x0000ffff0000ffffUL;
                                Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = (uint)(firstWord | (firstWord >> 16)) | ((ulong)(uint)(secondWord | (secondWord >> 16)) << 32);
                                Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex + 8)).Value = (uint)(thirdWord | (thirdWord >> 16)) | ((ulong)(uint)(fourthWord | (fourthWord >> 16)) << 32);
                                inputIndex += 15;
                                outputIndex += 16;
                                continue;
                            }

                            // Reuse validated prefixes of a failed wide run instead of probing each remaining ASCII character again.
                            if (((firstWord | secondWord | thirdWord) & 0xff80ff80ff80ff80UL) == 0)
                            {
                                firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                                secondWord = (secondWord | (secondWord >> 8)) & 0x0000ffff0000ffffUL;
                                thirdWord = (thirdWord | (thirdWord >> 8)) & 0x0000ffff0000ffffUL;
                                Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = (uint)(firstWord | (firstWord >> 16)) | ((ulong)(uint)(secondWord | (secondWord >> 16)) << 32);
                                Unsafe.As<byte, Utf8PackedWord32>(ref Unsafe.Add(ref outputStart, outputIndex + 8)).Value = (uint)(thirdWord | (thirdWord >> 16));
                                inputIndex += 11;
                                outputIndex += 12;
                                goto ScalarAsciiTail;
                            }

                            if (((firstWord | secondWord) & 0xff80ff80ff80ff80UL) == 0)
                            {
                                firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                                secondWord = (secondWord | (secondWord >> 8)) & 0x0000ffff0000ffffUL;
                                Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = (uint)(firstWord | (firstWord >> 16)) | ((ulong)(uint)(secondWord | (secondWord >> 16)) << 32);
                                inputIndex += 7;
                                outputIndex += 8;
                                goto ScalarAsciiTail;
                            }

                            if ((firstWord & 0xff80ff80ff80ff80UL) == 0)
                            {
                                firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                                Unsafe.As<byte, Utf8PackedWord32>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = (uint)(firstWord | (firstWord >> 16));
                                inputIndex += 3;
                                outputIndex += 4;
                                goto ScalarAsciiTail;
                            }

                            Unsafe.Add(ref outputStart, outputIndex++) = (byte)codeUnit;
                            goto ScalarAsciiTail;
                        }

                        if (((firstWord | secondWord) & 0xff80ff80ff80ff80UL) == 0)
                        {
                            firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                            secondWord = (secondWord | (secondWord >> 8)) & 0x0000ffff0000ffffUL;
                            Unsafe.As<byte, Utf8PackedWord64>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = (uint)(firstWord | (firstWord >> 16)) | ((ulong)(uint)(secondWord | (secondWord >> 16)) << 32);
                            inputIndex += 7;
                            outputIndex += 8;
                            continue;
                        }
                    }

                    if ((firstWord & 0xff80ff80ff80ff80UL) == 0)
                    {
                        firstWord = (firstWord | (firstWord >> 8)) & 0x0000ffff0000ffffUL;
                        Unsafe.As<byte, Utf8PackedWord32>(ref Unsafe.Add(ref outputStart, outputIndex)).Value = (uint)(firstWord | (firstWord >> 16));
                        inputIndex += 3;
                        outputIndex += 4;
                        continue;
                    }
                }

                Unsafe.Add(ref outputStart, outputIndex++) = (byte)codeUnit;
                ScalarAsciiTail:
                    while (inputIndex < source.Length && Unsafe.Add(ref inputStart, inputIndex) < 0x80)
                        Unsafe.Add(ref outputStart, outputIndex++) = (byte)Unsafe.Add(ref inputStart, inputIndex++);
                continue;
            }

            if (codeUnit < 0x800)
            {
                Unsafe.Add(ref outputStart, outputIndex) = (byte)(0xc0 | (codeUnit >> 6));
                Unsafe.Add(ref outputStart, outputIndex + 1) = (byte)(0x80 | (codeUnit & 63));
                outputIndex += 2;
                continue;
            }

            if (codeUnit >= 0xd800 && codeUnit <= 0xdfff)
            {
                if (codeUnit <= 0xdbff && inputIndex < source.Length && (uint)(Unsafe.Add(ref inputStart, inputIndex) - 0xdc00) <= 0x3ff)
                {
                    uint scalar = 0x10000 + ((codeUnit - 0xd800) << 10) + (Unsafe.Add(ref inputStart, inputIndex++) - 0xdc00u);
                    Unsafe.Add(ref outputStart, outputIndex) = (byte)(0xf0 | (scalar >> 18));
                    Unsafe.Add(ref outputStart, outputIndex + 1) = (byte)(0x80 | ((scalar >> 12) & 63));
                    Unsafe.Add(ref outputStart, outputIndex + 2) = (byte)(0x80 | ((scalar >> 6) & 63));
                    Unsafe.Add(ref outputStart, outputIndex + 3) = (byte)(0x80 | (scalar & 63));
                    outputIndex += 4;
                    continue;
                }

                if (!replaceInvalidSequences)
                {
                    charsRead = inputIndex - 1;
                    bytesWritten = outputIndex;
                    return OperationStatus.InvalidData;
                }

                codeUnit = 0xfffd;
            }

            Unsafe.Add(ref outputStart, outputIndex) = (byte)(0xe0 | (codeUnit >> 12));
            Unsafe.Add(ref outputStart, outputIndex + 1) = (byte)(0x80 | ((codeUnit >> 6) & 63));
            Unsafe.Add(ref outputStart, outputIndex + 2) = (byte)(0x80 | (codeUnit & 63));
            outputIndex += 3;
        }

        charsRead = source.Length;
        bytesWritten = outputIndex;
        return OperationStatus.Done;
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

