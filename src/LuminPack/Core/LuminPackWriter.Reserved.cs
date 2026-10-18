using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Utility;

namespace LuminPack.Core;

public unsafe ref partial struct LuminPackWriter
{
    /// <summary>Writes the declared null or empty string record inside a complete existing reservation.</summary>
    /// <typeparam name="TOptions">The stateless compiled string profile.</typeparam>
    /// <param name="index">The original record position; the caller retains cursor advancement.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReservedNullStringHeader<TOptions>(ref int index) where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token)
        {
            GetSpanReference(index) = 0;
            if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF16) GetSpanReference(index + 1) = 0;
        }
        else if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
            WriteReservedUtf8Header(index, LuminPackCode.NullCollection, LuminPackCode.NullCollection);
        else
        {
            int marker = LuminPackCode.NullCollection;
            Unsafe.CopyBlockUnaligned(ref GetSpanReference(index), ref Unsafe.As<int, byte>(ref marker), sizeof(int));
        }
    }

    /// <summary>Writes a reserved null string and returns its unchanged header or token width.</summary>
    /// <typeparam name="TOptions">The stateless compiled string profile.</typeparam>
    /// <param name="index">The original record position.</param>
    /// <param name="offset">Receives the actual record width.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReservedNullStringHeader<TOptions>(ref int index, out int offset) where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteReservedNullStringHeader<TOptions>(ref index);
        offset = StringRecordLength<TOptions>();
    }

    /// <summary>Writes the default null record inside a complete existing reservation.</summary>
    /// <param name="index">The original record position.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReservedNullStringHeader(ref int index) => WriteReservedNullStringHeader<global::LuminPack.Option.LuminPackDefaultOptions>(ref index);

    /// <summary>Writes the default reserved null record and returns its original width.</summary>
    /// <param name="index">The original record position.</param>
    /// <param name="offset">Receives the default record width.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReservedNullStringHeader(ref int index, out int offset) => WriteReservedNullStringHeader<global::LuminPack.Option.LuminPackDefaultOptions>(ref index, out offset);
    /// <summary>Gets the maximum payload bytes per UTF-16 code unit for the default compiled encoding.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int StringCapacityScale
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetStringCapacityScale<global::LuminPack.Option.LuminPackDefaultOptions>();
    }

    /// <summary>Reserves a statically selected collection upper bound without changing the write cursor.</summary>
    /// <param name="index">The absolute next payload position, including any unpublished local offset.</param>
    /// <param name="byteCount">The computed upper bound of the remaining payload.</param>
    /// <returns>False when the bound cannot fit a supported contiguous buffer; the caller must use its checked loop.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReserveCollection(int index, long byteCount)
    {
        const int maximumBufferLength = 0x7fffffc7;
        if (index < 0 || byteCount < 0 || byteCount > (long)maximumBufferLength - index) return false;
        if (_writerBuffer is null && byteCount > (long)_bufferLength - index) return false;
        EnsureCapacity(index, (int)byteCount);
        return true;
    }

    /// <summary>Reserves one complete container element when the aggregate upper bound is unrepresentable.</summary>
    /// <param name="index">The actual, possibly unpublished next element position.</param>
    /// <param name="byteCount">The statically selected complete element bound.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCollectionCapacity(int index, int byteCount) => EnsureCapacity(index, byteCount);

    /// <summary>Preserves owned-buffer headroom after a guaranteed record while retaining valid caller-span writes.</summary>
    /// <remarks>The complete record has already been reserved. An unowned span requires no owner publication.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompleteReservedRecord()
    {
        if (_writerBuffer is not null) CheckBufferPosition(_currentIndex);
    }

    /// <summary>Writes a complete string record inside capacity already reserved by generated code.</summary>
    /// <param name="value">The string written using the library's default compiled profile.</param>
    /// <returns>The payload byte count, excluding the existing record header or token width.</returns>
    /// <remarks>The caller must reserve the complete record. This support API performs no capacity checks or growth.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteReservedString(string? value) { return WriteReservedString<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

    /// <summary>Writes a complete string record at an unpublished cursor in an already reserved region.</summary>
    /// <param name="index">The absolute record position; the caller retains cursor advancement.</param>
    /// <param name="value">The string, including null and empty values.</param>
    /// <returns>The actual payload byte count, excluding the record header or terminator.</returns>
    /// <remarks>Uses the original encoding, normalization and raw UTF-16 contract without capacity checks.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteReservedString(ref int index, string? value)
        { return WriteReservedString<global::LuminPack.Option.LuminPackDefaultOptions>(ref index, value); }

    /// <summary>Keeps encoding state out of the caller without making its cursor escape by reference.</summary>
    /// <param name="index">The copied absolute record position.</param>
    /// <param name="value">The string whose complete capacity is already reserved.</param>
    /// <returns>The actual payload bytes, excluding the existing header or terminator.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int WriteReservedStringCore(int index, string? value)
    { return WriteReservedStringCore<global::LuminPack.Option.LuminPackDefaultOptions>(index, value); }

    /// <summary>Writes the common UTF-8 length record without carrying token or UTF-16 state.</summary>
    /// <param name="index">The reserved record position.</param>
    /// <param name="value">The original string, including null or empty values.</param>
    /// <returns>The encoded payload byte count.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int WriteReservedUtf8Length(int index, string? value)
    {
        if (value is null)
        {
            WriteReservedUtf8Header(index, LuminPackCode.NullCollection, LuminPackCode.NullCollection);
            return 0;
        }
        int characters = value.Length;
        if (characters == 0)
        {
            WriteReservedUtf8Header(index, 0, 0);
            return 0;
        }
        var source = value.AsSpan();
        var destination = _bufferReference.Slice(index + sizeof(int) * 2);
#if NET8_0_OR_GREATER
        if (ShortAsciiTranscoder.TryEncode(source, destination, escapeJson: false))
        {
            WriteReservedUtf8Header(index, characters, characters);
            return characters;
        }
#endif
        return WriteReservedUtf8LengthSlow(index, source, destination);
    }

    /// <summary>Preserves strict general UTF-8 conversion outside the short ASCII record frame.</summary>
    /// <param name="index">The original record start.</param>
    /// <param name="source">The complete UTF-16 source.</param>
    /// <param name="destination">The already reserved UTF-8 payload range.</param>
    /// <returns>The actual encoded payload bytes.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int WriteReservedUtf8LengthSlow(int index, ReadOnlySpan<char> source, Span<byte> destination)
    {
        var status = StringSerializer.Serialize(source, destination, out _, out int bytesWritten, replaceInvalidSequences: false);
        if (status != OperationStatus.Done) LuminPackExceptionHelper.ThrowFailedEncoding(status);
        WriteReservedUtf8Header(index, bytesWritten, source.Length);
        return bytesWritten;
    }

    /// <summary>Writes the original two-word UTF-8 length header with the required architecture-specific access.</summary>
    /// <param name="index">The reserved header position.</param>
    /// <param name="bytes">The payload byte count or original null marker.</param>
    /// <param name="characters">The UTF-16 code-unit count or original null marker.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteReservedUtf8Header(int index, int bytes, int characters)
    {
        long header = (long)(uint)bytes | ((long)characters << 32);
        if (Unsafe.SizeOf<IntPtr>() == 8) Unsafe.WriteUnaligned(ref GetSpanReference(index), header);
        else Unsafe.CopyBlockUnaligned(ref GetSpanReference(index), ref Unsafe.As<long, byte>(ref header), 8);
    }

    /// <summary>Writes the existing strict UTF-8 token representation without growth or truncation.</summary>
    /// <param name="index">The reserved payload position.</param>
    /// <param name="value">The complete source string.</param>
    /// <returns>The encoded byte count before the terminator.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int WriteReservedUtf8Token(int index, string? value)
    {
        int bytesWritten = 0;
        if (!string.IsNullOrEmpty(value))
        {
            var status = StringSerializer.Serialize(value.AsSpan(), _bufferReference.Slice(index), out _, out bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done) LuminPackExceptionHelper.ThrowFailedEncoding(status);
        }
        GetSpanReference(index + bytesWritten) = 0;
        return bytesWritten;
    }

    /// <summary>Copies raw UTF-16 code units without carrying UTF-8 conversion or retry state.</summary>
    /// <param name="index">The reserved record position.</param>
    /// <param name="value">The complete source, including raw unpaired surrogates.</param>
    /// <param name="token">Whether the existing mode records a two-byte terminator.</param>
    /// <returns>The actual payload bytes excluding its header or terminator.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int WriteReservedUtf16(int index, string? value, bool token)
    {
        int byteLength = checked((value?.Length ?? 0) * sizeof(char));
        int payloadIndex = index;
        if (!token)
        {
            int length = value is null ? LuminPackCode.NullCollection : byteLength;
            // The established UTF-16 length record stores bytes, not UTF-16 code-unit count.
            Unsafe.CopyBlockUnaligned(ref GetSpanReference(index), ref Unsafe.As<int, byte>(ref length), sizeof(int));
            payloadIndex += sizeof(int);
        }
        if (byteLength != 0)
        {
            // The size was computed from this complete string, so the source cannot be null.
#if NET8_0_OR_GREATER
            CopyUtf16Payload(ref GetSpanReference(payloadIndex), ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in value!.GetPinnableReference())), (uint)byteLength);
#else
            CopyUtf16Payload(ref GetSpanReference(payloadIndex), ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)byteLength);
#endif
        }
        if (token)
        {
            ushort terminator = 0;
            Unsafe.CopyBlockUnaligned(ref GetSpanReference(payloadIndex + byteLength), ref Unsafe.As<ushort, byte>(ref terminator), sizeof(ushort));
        }
        return byteLength;
    }
}
