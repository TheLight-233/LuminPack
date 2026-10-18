using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Text.Unicode;
#endif
using LuminPack.Option;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Utility;
using LuminPack.Utility.ViewModel;
using static LuminPack.Code.LuminPackMarshal;
using System.ComponentModel;

namespace LuminPack.Core;
public unsafe ref partial struct LuminPackWriter
{
    /// <summary>Gets StringCapacityScale from a compile-time serialization profile.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile.</typeparam>
    /// <returns>The compile-time profile's selected value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int GetStringCapacityScale<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 3 : 2;
    /// <summary>
    /// Calculates the serialized string length.
    /// </summary>
    /// <param name = "value"></param>
    /// <returns></returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int GetStringLength<TOptions>(string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        return (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? LuminPackMarshal.CalculateStringByteCount(value) : checked(value.Length * 2);
    }

    /// <summary>
    /// Calculates the string payload size, including its header or terminator.
    /// </summary>
    /// <param name = "value"></param>
    /// <returns></returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int GetStringWriteLength<TOptions>(string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        return checked(GetStringLength<TOptions>(value) + StringRecordLength<TOptions>());
    }

    /// <summary>Returns the size of the string header used by the current recording mode.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int StringRecordLength<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        // UTF-16 token mode uses a two-byte 0x0000 terminator because individual zero bytes are common within code units.
        return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2) : (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 8 : 4;
    }

    /// <summary>
    /// Writes the null string marker.
    /// </summary>
    /// <param name = "index"></param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteNullStringHeader<TOptions>(ref int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        EnsureCapacity(index, StringRecordLength<TOptions>());
        WriteReservedNullStringHeader<TOptions>(ref index);
    }

    /// <summary>Writes null string header to the current output.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "offset">Receives the offset produced by the operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteNullStringHeader<TOptions>(ref int index, out int offset)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        offset = StringRecordLength<TOptions>();
        EnsureCapacity(index, offset);
        WriteReservedNullStringHeader<TOptions>(ref index);
    }

    /// <summary>
    /// Writes the string terminator.
    /// </summary>
    /// <param name = "index"></param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteStringRecordTokenHeader<TOptions>(ref int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            // UTF-8 token mode uses a single zero-byte terminator.
#if NET8_0_OR_GREATER
            Unsafe.Add(ref _bufferStart, (nint)(uint)index) = 0;
#else
                Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) = 0;
#endif
        }
        else
        {
            // UTF-16 token mode terminates at the two-byte null character 0x0000.
            ushort token = 0;
            Unsafe.CopyBlockUnaligned(ref GetSpanReference(index), ref Unsafe.As<ushort, byte>(ref token), sizeof(ushort));
        }
    }

    /// <summary>
    /// Writes a string using the configured encoding and recording mode.
    /// </summary>
    /// <param name = "value"></param>
    /// <returns>The number of bytes contributed to the payload.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int WriteString<TOptions>(string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
#if NET8_0_OR_GREATER
        byte mode = (byte)((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) |
            (default(TOptions).StringRecording == LuminPackStringRecording.Length ? 1 : 0));
        if (mode == 3) return WriteUtf16WithLength(_currentIndex, value);
        return mode == 1 ? WriteUtf8WithLength(_currentIndex, value) : WriteStringOtherMode<TOptions>(_currentIndex, value);
#else
            var mode = ((byte)((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF16 ? 2 : 0) | (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Length ? 1 : 0)));
            if (mode == 3)
                return WriteUtf16WithLength(_currentIndex, value);
            if (mode == 1)
                return WriteUtf8WithLength(_currentIndex, value);
            return mode == 0
                ? WriteUtf8WithToken<TOptions>(_currentIndex, value)
                : WriteUtf16WithToken<TOptions>(_currentIndex, value);
#endif
    }

    /// <summary>
    /// Writes a string with a known length.
    /// </summary>
    /// <param name = "value"></param>
    /// <param name = "length"></param>
    /// <returns>The number of bytes contributed to the payload.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void WriteString<TOptions>(string? value, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        var mode = ((byte)((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF16 ? 2 : 0) | (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Length ? 1 : 0)));
        if (mode == 3)
            WriteUtf16WithLength(_currentIndex, value, length);
        else if (mode == 1)
            WriteUtf8WithLength(_currentIndex, value, length);
        else if (mode == 0)
            WriteUtf8WithToken<TOptions>(_currentIndex, value, length);
        else
            WriteUtf16WithToken<TOptions>(_currentIndex, value, length);
    }

    /// <summary>
    /// Writes a string using the configured encoding and recording mode.
    /// </summary>
    /// <param name = "index"></param>
    /// <param name = "value"></param>
    /// <returns>The number of bytes contributed to the payload.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int WriteString<TOptions>(ref int index, string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
#if NET8_0_OR_GREATER
        byte mode = (byte)((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) |
            (default(TOptions).StringRecording == LuminPackStringRecording.Length ? 1 : 0));
        if (mode == 3) return WriteUtf16WithLength(index, value);
        return mode == 1 ? WriteUtf8WithLength(index, value) : WriteStringOtherMode<TOptions>(index, value);
#else
            var mode = ((byte)((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF16 ? 2 : 0) | (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Length ? 1 : 0)));
            if (mode == 3)
                return WriteUtf16WithLength(index, value);
            if (mode == 1)
                return WriteUtf8WithLength(index, value);
            return mode == 0
                ? WriteUtf8WithToken<TOptions>(index, value)
                : WriteUtf16WithToken<TOptions>(index, value);
#endif
    }

#if (NET8_0_OR_GREATER)
    /// <summary>Writes string other mode to the current output.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private int WriteStringOtherMode<TOptions>(int index, string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        var mode = ((byte)((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF16 ? 2 : 0) | (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Length ? 1 : 0)));
        if (mode == 3)
            return WriteUtf16WithLength(index, value);
        return mode == 0 ? WriteUtf8WithToken<TOptions>(index, value) : WriteUtf16WithToken<TOptions>(index, value);
    }

#endif
    /// <summary>
    /// Writes a string with a known length.
    /// </summary>
    /// <param name = "index"></param>
    /// <param name = "value"></param>
    /// <param name = "length"></param>
    /// <returns>The number of bytes contributed to the payload.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void WriteString<TOptions>(ref int index, string? value, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        var mode = ((byte)((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF16 ? 2 : 0) | (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Length ? 1 : 0)));
        if (mode == 3)
            WriteUtf16WithLength(index, value, length);
        else if (mode == 1)
            WriteUtf8WithLength(index, value, length);
        else if (mode == 0)
            WriteUtf8WithToken<TOptions>(index, value, length);
        else
            WriteUtf16WithToken<TOptions>(index, value, length);
    }

    /// <summary>
    /// Writes a string from a read-only character span.
    /// </summary>
    /// /// <param name = "index"></param>
    /// <param name = "value"></param>
    /// <returns>The number of bytes contributed to the payload.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public int WriteString<TOptions>(ref int index, ReadOnlySpan<char> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        var mode = ((byte)((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF16 ? 2 : 0) | (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Length ? 1 : 0)));
        if (mode == 3)
            return WriteUtf16WithLength(index, value);
        if (mode == 1)
            return WriteUtf8WithLength(index, value);
        return mode == 0 ? WriteUtf8WithToken<TOptions>(index, value) : WriteUtf16WithToken<TOptions>(index, value);
    }

    /// <summary>Writes utf8 with token to the current output.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int WriteUtf8WithToken<TOptions>(int index, string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (string.IsNullOrEmpty(value) || value.Length is 0)
        {
            EnsureCapacity(index, 1);
            WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf8TokenOptions>(ref index);
            return 0;
        }

        var destination = (uint)index <= (uint)_bufferLength ? _bufferReference.Slice(index) : Span<byte>.Empty;
        var status = StringSerializer.Serialize(value, destination, out var _, out var bytesWritten, replaceInvalidSequences: false);
        if (status != OperationStatus.Done)
        {
            if (status == OperationStatus.DestinationTooSmall)
                bytesWritten = RetryWriteUtf8WithToken(index, value);
            else
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
        }

        EnsureCapacity(index, checked(bytesWritten + 1));
        var tokenIndex = index + bytesWritten;
        WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf8TokenOptions>(ref tokenIndex);
        return bytesWritten;
    }

    /// <summary>Writes utf8 with token to the current output.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteUtf8WithToken<TOptions>(int index, string? value, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (length is 0)
        {
            EnsureCapacity(index, 1);
            WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf8TokenOptions>(ref index);
            return;
        }

        if (length > 0)
            EnsureCapacity(index, checked(length + 1));
        var status = StringSerializer.Serialize(value.AsSpan(), _bufferReference.Slice(index, length), out _, out var bytesWritten, replaceInvalidSequences: false);
        if (status != OperationStatus.Done)
        {
            LuminPackExceptionHelper.ThrowFailedEncoding(status);
        }

        var tokenIndex = index + length;
        WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf8TokenOptions>(ref tokenIndex);
    }

    /// <summary>Writes utf8 with token to the current output.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int WriteUtf8WithToken<TOptions>(int index, ReadOnlySpan<char> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (value.Length is 0)
        {
            EnsureCapacity(index, 1);
            WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf8TokenOptions>(ref index);
            return 0;
        }

        var destination = (uint)index <= (uint)_bufferLength ? _bufferReference.Slice(index) : Span<byte>.Empty;
        var status = StringSerializer.Serialize(value, destination, out _, out var bytesWritten, replaceInvalidSequences: false);
        if (status != OperationStatus.Done)
        {
            if (status == OperationStatus.DestinationTooSmall)
                bytesWritten = RetryWriteUtf8WithToken(index, value);
            else
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
        }

        EnsureCapacity(index, checked(bytesWritten + 1));
        var tokenIndex = index + bytesWritten;
        WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf8TokenOptions>(ref tokenIndex);
        return bytesWritten;
    }

    /// <summary>Writes utf16 with token to the current output.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int WriteUtf16WithToken<TOptions>(int index, string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (string.IsNullOrEmpty(value) || value.Length is 0)
        {
            EnsureCapacity(index, 2);
            WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf16TokenOptions>(ref index);
            return 0;
        }

        int bytesWritten = checked(value.Length * sizeof(char));
        EnsureCapacity(index, checked(bytesWritten + 2));
        Unsafe.CopyBlockUnaligned(ref GetSpanReference(index), ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)bytesWritten);
        var tokenIndex = index + bytesWritten;
        WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf16TokenOptions>(ref tokenIndex);
        return bytesWritten;
    }

    /// <summary>Writes utf16 with token to the current output.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteUtf16WithToken<TOptions>(int index, string? value, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (length is 0)
        {
            EnsureCapacity(index, 2);
            WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf16TokenOptions>(ref index);
            return;
        }

        if (length > 0)
            EnsureCapacity(index, checked(length + 2));
        StringSerializer.Serialize(value, _bufferReference.Slice(index, length), out _, out _, SerializeMode.Utf16, replaceInvalidSequences: false);
        var tokenIndex = index + length;
        WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf16TokenOptions>(ref tokenIndex);
    }

    /// <summary>Writes utf16 with token to the current output.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int WriteUtf16WithToken<TOptions>(int index, ReadOnlySpan<char> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (value.Length is 0)
        {
            EnsureCapacity(index, 2);
            WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf16TokenOptions>(ref index);
            return 0;
        }

        var length = checked(value.Length * 2);
        EnsureCapacity(index, checked(length + 2));
        Unsafe.CopyBlockUnaligned(ref GetSpanReference(index), ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value)), (uint)length);
        var tokenIndex = index + length;
        WriteStringRecordTokenHeader<global::LuminPack.Option.LuminPackUtf16TokenOptions>(ref tokenIndex);
        return length;
    }

    /// <summary>Writes a complete string record inside capacity already reserved by generated code.</summary>
    /// <param name = "value">The string written using the selected compiled profile.</param>
    /// <returns>The payload byte count, excluding the existing record header or token width.</returns>
    /// <remarks>The caller must reserve the complete record. This support API performs no capacity checks or growth.</remarks>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int WriteReservedString<TOptions>(string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => WriteReservedStringCore<TOptions>(_currentIndex, value);
    /// <summary>Writes a complete string record at an unpublished cursor in an already reserved region.</summary>
    /// <param name = "index">The absolute record position; the caller retains cursor advancement.</param>
    /// <param name = "value">The string, including null and empty values.</param>
    /// <returns>The actual payload byte count, excluding the record header or terminator.</returns>
    /// <remarks>Uses the original encoding, normalization and raw UTF-16 contract without capacity checks.</remarks>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int WriteReservedString<TOptions>(ref int index, string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => WriteReservedStringCore<TOptions>(index, value);
    /// <summary>Keeps encoding state out of the caller without making its cursor escape by reference.</summary>
    /// <param name = "index">The copied absolute record position.</param>
    /// <param name = "value">The string whose complete capacity is already reserved.</param>
    /// <returns>The actual payload bytes, excluding the existing header or terminator.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private int WriteReservedStringCore<TOptions>(int index, string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        byte mode = ((byte)((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF16 ? 2 : 0) | (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Length ? 1 : 0)));
        return mode == 1 ? WriteReservedUtf8Length(index, value) : mode == 0 ? WriteReservedUtf8Token(index, value) : WriteReservedUtf16(index, value, mode == 2);
    }
}
