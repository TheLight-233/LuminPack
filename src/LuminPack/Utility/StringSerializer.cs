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

    private static unsafe OperationStatus SerializeAsUtf8(
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
            OperationStatus status = OperationStatus.Done;
            int remaining = source.Length;
                
            while (remaining > 0)
            {
                int chunkSize = Math.Min(remaining, destination.Length - (int)(pOutput - pDest));
                if (chunkSize <= 0) break;
                    
                status = TranscodeToUtf8(
                    pInput, chunkSize,
                    pOutput, destination.Length - (int)(pOutput - pDest),
                    out char* pInputRemaining,
                    out byte* pOutputRemaining);
                    
                int processed = (int)(pInputRemaining - pInput);
                remaining -= processed;
                    
                if (status > OperationStatus.DestinationTooSmall && 
                    (status != OperationStatus.NeedMoreData || isFinalBlock))
                {
                    if (!replaceInvalidSequences)
                    {
                        status = OperationStatus.InvalidData;
                        break;
                    }
                        
                    // 跳过无效字符
                    pInput++;
                    remaining--;
                    pOutput[0] = 0xEF;
                    pOutput[1] = 0xBF;
                    pOutput[2] = 0xBD;
                    pOutput += 3;
                }
                else
                {
                    pInput = pInputRemaining;
                    pOutput = pOutputRemaining;
                        
                    if (status == OperationStatus.DestinationTooSmall || 
                        status == OperationStatus.NeedMoreData)
                    {
                        break;
                    }
                }
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

    private static unsafe OperationStatus DeserializeFromUtf8(
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
            OperationStatus status = OperationStatus.Done;
            int remaining = source.Length;
            
            while (remaining > 0)
            {
                int chunkSize = Math.Min(remaining, destination.Length - (int)(pOutput - pDest));
                if (chunkSize <= 0) break;
                    
                status = TranscodeToUtf16(
                    pInput, chunkSize, 
                    pOutput, destination.Length - (int)(pOutput - pDest),
                    out byte* pInputRemaining, 
                    out char* pOutputRemaining);
                    
                int processed = (int)(pInputRemaining - pInput);
                remaining -= processed;
                    
                if (status > OperationStatus.DestinationTooSmall && 
                    (status != OperationStatus.NeedMoreData || isFinalBlock))
                {
                    if (!replaceInvalidSequences)
                    {
                        status = OperationStatus.InvalidData;
                        break;
                    }
                        
                    // 跳过无效字节
                    pInput++;
                    remaining--;
                    *pOutput = '\uFFFD';
                    pOutput++;
                }
                else
                {
                    pInput = pInputRemaining;
                    pOutput = pOutputRemaining;
                        
                    if (status == OperationStatus.DestinationTooSmall || 
                        status == OperationStatus.NeedMoreData)
                    {
                        break;
                    }
                }
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
            
        while (pInputBuffer < inputEnd && pOutputBuffer < outputEnd)
        {
            uint firstByte = *pInputBuffer;
                
            // 1字节序列 (ASCII)
            if (firstByte < 0x80)
            {
                *pOutputBuffer = (char)firstByte;
                pInputBuffer++;
                pOutputBuffer++;
                continue;
            }
                
            // 2字节序列
            if ((firstByte & 0xE0) == 0xC0)
            {
                if (pInputBuffer + 2 > inputEnd)
                {
                    break; // 需要更多数据
                }
                    
                uint secondByte = pInputBuffer[1];
                if ((secondByte & 0xC0) != 0x80)
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.InvalidData;
                }
                    
                uint codePoint = ((firstByte & 0x1F) << 6) | (secondByte & 0x3F);
                if (codePoint < 0x80)
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.InvalidData;
                }
                    
                *pOutputBuffer = (char)codePoint;
                pInputBuffer += 2;
                pOutputBuffer++;
                continue;
            }
                
            // 3字节序列
            if ((firstByte & 0xF0) == 0xE0)
            {
                if (pInputBuffer + 3 > inputEnd)
                {
                    break; // 需要更多数据
                }
                    
                uint secondByte = pInputBuffer[1];
                uint thirdByte = pInputBuffer[2];
                    
                if ((secondByte & 0xC0) != 0x80 || (thirdByte & 0xC0) != 0x80)
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.InvalidData;
                }
                    
                uint codePoint = ((firstByte & 0x0F) << 12) | 
                                 ((secondByte & 0x3F) << 6) | 
                                 (thirdByte & 0x3F);
                    
                if (codePoint < 0x800 || (codePoint >= 0xD800 && codePoint <= 0xDFFF))
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.InvalidData;
                }
                    
                *pOutputBuffer = (char)codePoint;
                pInputBuffer += 3;
                pOutputBuffer++;
                continue;
            }
                
            // 4字节序列 (代理对)
            if ((firstByte & 0xF8) == 0xF0)
            {
                if (pInputBuffer + 4 > inputEnd)
                {
                    break; // 需要更多数据
                }
                    
                if (pOutputBuffer + 2 > outputEnd)
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.DestinationTooSmall;
                }
                    
                uint secondByte = pInputBuffer[1];
                uint thirdByte = pInputBuffer[2];
                uint fourthByte = pInputBuffer[3];
                    
                if ((secondByte & 0xC0) != 0x80 || 
                    (thirdByte & 0xC0) != 0x80 || 
                    (fourthByte & 0xC0) != 0x80)
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.InvalidData;
                }
                    
                uint codePoint = ((firstByte & 0x07) << 18) | 
                                 ((secondByte & 0x3F) << 12) | 
                                 ((thirdByte & 0x3F) << 6) | 
                                 (fourthByte & 0x3F);
                    
                if (codePoint < 0x10000 || codePoint > 0x10FFFF)
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.InvalidData;
                }
                    
                codePoint -= 0x10000;
                pOutputBuffer[0] = (char)((codePoint >> 10) + 0xD800);
                pOutputBuffer[1] = (char)((codePoint & 0x3FF) + 0xDC00);
                    
                pInputBuffer += 4;
                pOutputBuffer += 2;
                continue;
            }
                
            // 无效的首字节
            pInputBufferRemaining = pInputBuffer;
            pOutputBufferRemaining = pOutputBuffer;
            return OperationStatus.InvalidData;
        }
            
        pInputBufferRemaining = pInputBuffer;
        pOutputBufferRemaining = pOutputBuffer;
            
        return pInputBuffer == inputEnd ? 
            OperationStatus.Done : 
            OperationStatus.NeedMoreData;
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
            
        while (pInputBuffer < inputEnd && pOutputBuffer < outputEnd)
        {
            uint codePoint = *pInputBuffer;
                
            // ASCII字符 (1字节)
            if (codePoint < 0x80)
            {
                *pOutputBuffer = (byte)codePoint;
                pInputBuffer++;
                pOutputBuffer++;
                continue;
            }
                
            // 2字节序列
            if (codePoint < 0x800)
            {
                if (pOutputBuffer + 2 > outputEnd)
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.DestinationTooSmall;
                }
                    
                pOutputBuffer[0] = (byte)(0xC0 | (codePoint >> 6));
                pOutputBuffer[1] = (byte)(0x80 | (codePoint & 0x3F));
                    
                pInputBuffer++;
                pOutputBuffer += 2;
                continue;
            }
                
            // 代理对处理
            if (codePoint >= 0xD800 && codePoint <= 0xDFFF)
            {
                // 高代理项
                if (codePoint <= 0xDBFF)
                {
                    if (pInputBuffer + 2 > inputEnd)
                    {
                        break; // 需要更多数据
                    }
                        
                    uint lowSurrogate = pInputBuffer[1];
                    if (lowSurrogate < 0xDC00 || lowSurrogate > 0xDFFF)
                    {
                        pInputBufferRemaining = pInputBuffer;
                        pOutputBufferRemaining = pOutputBuffer;
                        return OperationStatus.InvalidData;
                    }
                        
                    codePoint = 0x10000 + ((codePoint - 0xD800) << 10) + (lowSurrogate - 0xDC00);
                }
                else // 无效的低代理项
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.InvalidData;
                }
            }
                
            // 3字节序列
            if (codePoint < 0x10000)
            {
                if (pOutputBuffer + 3 > outputEnd)
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.DestinationTooSmall;
                }
                    
                pOutputBuffer[0] = (byte)(0xE0 | (codePoint >> 12));
                pOutputBuffer[1] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                pOutputBuffer[2] = (byte)(0x80 | (codePoint & 0x3F));
                    
                pInputBuffer += (codePoint > 0xFFFF) ? 2 : 1;
                pOutputBuffer += 3;
                continue;
            }
                
            // 4字节序列
            if (codePoint <= 0x10FFFF)
            {
                if (pOutputBuffer + 4 > outputEnd)
                {
                    pInputBufferRemaining = pInputBuffer;
                    pOutputBufferRemaining = pOutputBuffer;
                    return OperationStatus.DestinationTooSmall;
                }
                    
                pOutputBuffer[0] = (byte)(0xF0 | (codePoint >> 18));
                pOutputBuffer[1] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
                pOutputBuffer[2] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                pOutputBuffer[3] = (byte)(0x80 | (codePoint & 0x3F));
                    
                pInputBuffer += (codePoint > 0xFFFF) ? 2 : 1;
                pOutputBuffer += 4;
                continue;
            }
                
            // 无效的Unicode码点
            pInputBufferRemaining = pInputBuffer;
            pOutputBufferRemaining = pOutputBuffer;
            return OperationStatus.InvalidData;
        }
            
        pInputBufferRemaining = pInputBuffer;
        pOutputBufferRemaining = pOutputBuffer;
            
        return pInputBuffer == inputEnd ? 
            OperationStatus.Done : 
            OperationStatus.NeedMoreData;
    }
}