using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Option;

namespace LuminPack.Core;

public ref partial struct LuminPackWriter
{
    /// <summary>Reserves a complete partition without narrowing an unrepresentable upper bound.</summary>
    /// <param name="byteCount">The remaining payload upper bound.</param>
    /// <param name="hasObjectElements">Whether existing fixed-object writers need their ordinary growth headroom.</param>
    /// <returns>False when the caller must retain its checked element traversal.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnsurePartitionCapacity(long byteCount, bool hasObjectElements)
    {
        const int maximumBufferLength = 0x7fffffc7;
        int position = Position;
        long required = position + byteCount;
        if (position < 0 || byteCount < 0 || required < position || required > maximumBufferLength)
            return false;
        if (hasObjectElements && Owner is not null && RecordDistance() < byteCount)
            required = (required * 8 + 6) / 7 + 1024;
        if (required > maximumBufferLength) return false;
        EnsureAdditionalCapacity((int)(required - position));
        return !hasObjectElements || Owner is null || RecordDistance() >= byteCount;
    }

    /// <summary>Writes raw code units and their ordinary UTF-16 length header inside a proven reservation.</summary>
    /// <param name="destination">The first byte of the complete reserved record.</param>
    /// <param name="value">The nullable string snapshot.</param>
    /// <returns>The exact record width.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteReservedPartitionUtf16Record(ref byte destination, string? value)
    {
        int bytes = value is null ? 0 : checked(value.Length * sizeof(char));
        Write<int>(ref destination, value is null ? -1 : bytes);
        if (bytes != 0)
            global::LuminPack.Utility.StringSerializer.SerializeUtf16(ref Unsafe.Add(ref destination, sizeof(int)),
                ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)bytes);
        return checked(sizeof(int) + bytes);
    }

    /// <summary>Writes a partition string within its already validated profile bound.</summary>
    /// <typeparam name="TOptions">The compiled string profile.</typeparam>
    /// <param name="cursor">The reserved record position.</param>
    /// <param name="value">The nullable string snapshot.</param>
    /// <returns>The cursor after the actual record.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteReservedPartitionRecordString<TOptions>(ref byte cursor, string? value)
        where TOptions : struct, ILuminPackOptions
    {
        if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 &&
            default(TOptions).StringRecording == LuminPackStringRecording.Length)
            return ref Unsafe.Add(ref cursor, WriteReservedPartitionUtf16Record(ref cursor, value));
        return ref WriteReservedRecordString<TOptions>(ref cursor, value);
    }

    /// <summary>Reserves and writes a partition string with raw UTF-16 length records.</summary>
    /// <typeparam name="TOptions">The compiled string profile.</typeparam>
    /// <param name="cursor">The caller-held cursor, published before growth.</param>
    /// <param name="value">The nullable string snapshot.</param>
    /// <returns>The cursor after the actual record.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WritePartitionRecordString<TOptions>(ref byte cursor, string? value)
        where TOptions : struct, ILuminPackOptions
    {
        ref byte destination = ref ReserveRecord(ref cursor, RecordBound<TOptions>(value));
        return ref WriteReservedPartitionRecordString<TOptions>(ref destination, value);
    }
}
