using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Text.Unicode;
#endif

namespace LuminPack.Utility;

public enum SerializeMode : byte
{
    Utf8,
    Utf16
}

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
    {
#if NET8_0_OR_GREATER
        return mode is SerializeMode.Utf8
            ? Utf8.FromUtf16(
                source,
                destination,
                out charsRead,
                out bytesWritten,
                replaceInvalidSequences,
                isFinalBlock)
            : SerializeAsUtf16(
                source,
                destination,
                out charsRead,
                out bytesWritten,
                replaceInvalidSequences,
                isFinalBlock);
#else
        return mode is SerializeMode.Utf8
            ? SerializeAsUtf8(
                source,
                destination,
                out charsRead,
                out bytesWritten,
                replaceInvalidSequences,
                isFinalBlock)
            : SerializeAsUtf16(
                source,
                destination,
                out charsRead,
                out bytesWritten,
                replaceInvalidSequences,
                isFinalBlock);
#endif
    }

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

    internal static unsafe OperationStatus SerializeAsUtf8(
        ReadOnlySpan<char> source,
        Span<byte> destination,
        out int charsRead,
        out int bytesWritten,
        bool replaceInvalidSequences = true,
        bool isFinalBlock = true)
    {
        fixed (char* pSource = &MemoryMarshal.GetReference(source))
        fixed (byte* pDest = &MemoryMarshal.GetReference(destination))
        {
            char* pInput = pSource;
            byte* pOutput = pDest;
            char* inputEnd = pSource + source.Length;
            byte* outputEnd = pDest + destination.Length;
            OperationStatus status;

            while (true)
            {
                status = TranscodeToUtf8(
                    pInput, (int)(inputEnd - pInput),
                    pOutput, (int)(outputEnd - pOutput),
                    out char* pInputRemaining,
                    out byte* pOutputRemaining);

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

                if (outputEnd - pOutput < 3)
                {
                    status = OperationStatus.DestinationTooSmall;
                    break;
                }

                // Invalid UTF-16 consumes one code unit. An incomplete final
                // high surrogate consumes the entire remaining input.
                pInput = status == OperationStatus.NeedMoreData ? inputEnd : pInput + 1;
                pOutput[0] = 0xEF;
                pOutput[1] = 0xBF;
                pOutput[2] = 0xBD;
                pOutput += 3;
            }

            charsRead = (int)(pInput - pSource);
            bytesWritten = (int)(pOutput - pDest);
            return status;
        }
    }
    
    private static OperationStatus SerializeAsUtf16(
        ReadOnlySpan<char> source,
        Span<byte> destination,
        out int charsRead,
        out int bytesWritten,
        bool replaceInvalidSequences = true,
        bool isFinalBlock = true)
    {
        charsRead = 0;
        bytesWritten = 0;
        
        int requiredBytes = source.Length * sizeof(char);
        
        if (requiredBytes > destination.Length)
        {
            int maxChars = destination.Length / sizeof(char);
            int bytesToCopy = maxChars * sizeof(char);
            
            if (bytesToCopy > 0)
            {
                Unsafe.CopyBlockUnaligned(
                    ref MemoryMarshal.GetReference(destination),
                    ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(source)),
                    (uint)bytesToCopy);
                
                charsRead = maxChars;
                bytesWritten = bytesToCopy;
            }
            
            return OperationStatus.DestinationTooSmall;
        }
        
        Unsafe.CopyBlockUnaligned(
            ref MemoryMarshal.GetReference(destination),
            ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(source)),
            (uint)requiredBytes);
        
        charsRead = source.Length;
        bytesWritten = requiredBytes;
        return OperationStatus.Done;
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
        
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    internal static unsafe OperationStatus TranscodeToUtf8(
        char* pInputBuffer,
        int inputLength,
        byte* pOutputBuffer,
        int outputBytesRemaining,
        out char* pInputBufferRemaining,
        out byte* pOutputBufferRemaining)
    {
        char* inputEnd = pInputBuffer + inputLength;
        byte* outputEnd = pOutputBuffer + outputBytesRemaining;

        while (pInputBuffer < inputEnd)
        {
            pInputBufferRemaining = pInputBuffer;
            pOutputBufferRemaining = pOutputBuffer;

            uint codePoint = *pInputBuffer;

            if (codePoint < 0x80)
            {
                if (pOutputBuffer == outputEnd)
                    return Complete(OperationStatus.DestinationTooSmall);

                *pOutputBuffer = (byte)codePoint;
                pInputBuffer++;
                pOutputBuffer++;
                continue;
            }

            if (codePoint < 0x800)
            {
                if (pOutputBuffer + 2 > outputEnd)
                    return Complete(OperationStatus.DestinationTooSmall);

                pOutputBuffer[0] = (byte)(0xC0 | (codePoint >> 6));
                pOutputBuffer[1] = (byte)(0x80 | (codePoint & 0x3F));

                pInputBuffer++;
                pOutputBuffer += 2;
                continue;
            }

            if (codePoint is >= 0xD800 and <= 0xDBFF)
            {
                if (pInputBuffer + 2 > inputEnd)
                    return Complete(OperationStatus.NeedMoreData);

                uint lowSurrogate = pInputBuffer[1];
                if (lowSurrogate is < 0xDC00 or > 0xDFFF)
                    return Complete(OperationStatus.InvalidData);

                if (pOutputBuffer + 4 > outputEnd)
                    return Complete(OperationStatus.DestinationTooSmall);

                codePoint = 0x10000 + ((codePoint - 0xD800) << 10) + (lowSurrogate - 0xDC00);
                pOutputBuffer[0] = (byte)(0xF0 | (codePoint >> 18));
                pOutputBuffer[1] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
                pOutputBuffer[2] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                pOutputBuffer[3] = (byte)(0x80 | (codePoint & 0x3F));

                pInputBuffer += 2;
                pOutputBuffer += 4;
                continue;
            }

            if (codePoint is >= 0xDC00 and <= 0xDFFF)
                return Complete(OperationStatus.InvalidData);

            if (pOutputBuffer + 3 > outputEnd)
                return Complete(OperationStatus.DestinationTooSmall);

            pOutputBuffer[0] = (byte)(0xE0 | (codePoint >> 12));
            pOutputBuffer[1] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
            pOutputBuffer[2] = (byte)(0x80 | (codePoint & 0x3F));

            pInputBuffer++;
            pOutputBuffer += 3;
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
        if (remaining <= 1)
            return 1;

        uint firstByte = input[0];
        uint secondByte = input[1];

        if (firstByte is >= 0xE0 and <= 0xEF)
        {
            if (!IsUtf8ContinuationByte(secondByte) ||
                (firstByte == 0xE0 && secondByte < 0xA0) ||
                (firstByte == 0xED && secondByte >= 0xA0))
                return 1;

            return remaining > 2 && !IsUtf8ContinuationByte(input[2]) ? 2 : 1;
        }

        if (firstByte is >= 0xF0 and <= 0xF4)
        {
            if (!IsUtf8ContinuationByte(secondByte) ||
                (firstByte == 0xF0 && secondByte < 0x90) ||
                (firstByte == 0xF4 && secondByte > 0x8F))
                return 1;

            if (remaining > 2 && !IsUtf8ContinuationByte(input[2]))
                return 2;

            if (remaining > 3 && !IsUtf8ContinuationByte(input[3]))
                return 3;
        }

        return 1;
    }
}
