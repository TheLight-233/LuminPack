using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Option;
using System.Runtime.CompilerServices;

namespace LuminPack.Core;

public unsafe ref partial struct LuminPackReader
{
    /// <summary>Reads a named version-tolerant header and rejects unrelated record formats.</summary>
    /// <returns>The member count, or minus one for a null object.</returns>
    public int ReadVersionHeader() => ReadVersionHeader<LuminPackDefaultOptions>();

    /// <summary>Reads a named header and rejects incompatible format revisions or binary string profiles.</summary>
    /// <typeparam name="TOptions">The compiled receiving profile.</typeparam>
    /// <returns>The member count, or minus one for a null object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadVersionHeader<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        ValidateCollectionPayload(_currentIndex, 1);
        if (_bufferReference[_currentIndex] == 255) { _currentIndex++; return -1; }
        return ReadNonNullVersionHeader<TOptions>();
    }

    /// <summary>Decodes an evolved non-null header outside the common matching-schema native body.</summary>
    /// <typeparam name="TOptions">The compiled receiving profile.</typeparam>
    /// <returns>The validated member count.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ReadNonNullVersionHeader<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        byte marker = _bufferReference[_currentIndex++];
        if (marker != 251) LuminPackExceptionHelper.ThrowFormatException("Expected a named version-tolerant record.");
        ValidateCollectionPayload(_currentIndex, 3);
        byte revision = _bufferReference[_currentIndex++];
        byte profile = _bufferReference[_currentIndex++];
        int count = _bufferReference[_currentIndex++];
        if (revision != 1 || count > 249) LuminPackExceptionHelper.ThrowFormatException("Unsupported version-tolerant record header.");
        if (profile != (((int)default(TOptions).StringEncoding << 1) | (int)default(TOptions).StringRecording))
            LuminPackExceptionHelper.ThrowFormatException("The version-tolerant binary string profile does not match the input.");
        return count;
    }

    /// <summary>Maps and validates a complete dynamic member table before reading any payload.</summary>
    /// <param name="fields">The generated receiving schema.</param>
    /// <param name="slots">Receives local member indices, or minus one for unknown fields.</param>
    /// <param name="lengths">Receives payload lengths in table order.</param>
    /// <param name="present">Receives the set of local fields supplied by the input.</param>
    public void ReadVersionTable(LuminPackVersionField[] fields, scoped Span<int> slots, scoped Span<int> lengths, scoped Span<byte> present)
    {
        present.Clear();
        for (int i = 0; i < slots.Length; i++)
        {
            int slot;
            if (i < fields.Length && fields[i].Prefix.Length <= _bufferReference.Length - _currentIndex &&
                _bufferReference.Slice(_currentIndex, fields[i].Prefix.Length).SequenceEqual(fields[i].Prefix))
            {
                _currentIndex += fields[i].Prefix.Length;
                slot = i;
            }
            else
            {
                var name = ReadVersionMetadata();
                var contract = ReadVersionMetadata();
                slot = LuminPackVersionField.Find(fields, name, i);
                if (slot >= 0 && !contract.SequenceEqual(fields[slot].Contract))
                    LuminPackExceptionHelper.ThrowFormatException("The version-tolerant member type or binary profile has changed.");
            }
            slots[i] = slot;
            if (slot >= 0)
            {
                if (present[slot] != 0) LuminPackExceptionHelper.ThrowFormatException("Duplicate version-tolerant member identity.");
                present[slot] = 1;
            }
        }
        ReadVersionLengths(lengths);
    }

    /// <summary>Matches a complete cached schema without decoding or mapping individual member names.</summary>
    /// <param name="metadata">The receiving model's complete immutable metadata.</param>
    /// <returns>Whether the complete table matched and was consumed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadVersionMetadata(scoped ReadOnlySpan<byte> metadata)
    {
        if ((uint)_currentIndex > (uint)_bufferReference.Length || metadata.Length > _bufferReference.Length - _currentIndex ||
            !System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref GetSpanReference(_currentIndex), metadata.Length).SequenceEqual(metadata)) return false;
        _currentIndex += metadata.Length;
        return true;
    }

    /// <summary>Matches a complete header and metadata block with the default binary profile.</summary>
    /// <param name="metadata">The generated complete name and type table.</param>
    /// <param name="count">The exact receiving member count.</param>
    /// <returns>Whether the complete matching prefix was consumed.</returns>
    public bool TryReadVersionSchema(scoped ReadOnlySpan<byte> metadata, byte count) => TryReadVersionSchema<LuminPackDefaultOptions>(metadata, count);

    /// <summary>Matches a complete schema prefix before entering the known-order read path.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    /// <param name="metadata">The generated complete name and type table.</param>
    /// <param name="count">The exact receiving member count.</param>
    /// <returns>Whether the matching header and table were consumed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadVersionSchema<TOptions>(scoped ReadOnlySpan<byte> metadata, byte count) where TOptions : struct, ILuminPackOptions
    {
        int available = _bufferReference.Length - _currentIndex;
        if ((uint)_currentIndex > (uint)_bufferReference.Length || available < 4 || metadata.Length > available - 4) return false;
        ReadUnmanagedWithoutSizeReturn(ref _currentIndex, out uint header);
        if (!BitConverter.IsLittleEndian) header = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(header);
        uint profile = ((uint)default(TOptions).StringEncoding << 1) | (uint)default(TOptions).StringRecording;
        uint expected = 251u | (1u << 8) | (profile << 16) | ((uint)count << 24);
        if (header != expected || !System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref GetSpanReference(_currentIndex + 4), metadata.Length).SequenceEqual(metadata)) return false;
        _currentIndex += 4 + metadata.Length;
        return true;
    }

    /// <summary>Matches a cached complete default-profile prefix or the selected alternate profile.</summary>
    /// <param name="metadata">The exact name and type table.</param>
    /// <param name="count">The receiving member count.</param>
    /// <param name="defaultPrefix">The cached default header and metadata block.</param>
    /// <returns>Whether the complete matching prefix was consumed.</returns>
    public bool TryReadVersionSchema(scoped ReadOnlySpan<byte> metadata, byte count, scoped ReadOnlySpan<byte> defaultPrefix)
        => TryReadVersionSchema<LuminPackDefaultOptions>(metadata, count, defaultPrefix);

    /// <summary>Fuses the common header and metadata comparison into one constant-length operation.</summary>
    /// <typeparam name="TOptions">The compiled receiving profile.</typeparam>
    /// <param name="metadata">The exact name and type table.</param>
    /// <param name="count">The receiving member count.</param>
    /// <param name="defaultPrefix">The cached UTF-8 length-profile header and metadata.</param>
    /// <returns>Whether the matching header and metadata were consumed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadVersionSchema<TOptions>(scoped ReadOnlySpan<byte> metadata, byte count, scoped ReadOnlySpan<byte> defaultPrefix)
        where TOptions : struct, ILuminPackOptions
        => default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && default(TOptions).StringRecording == LuminPackStringRecording.Length
            ? TryReadVersionMetadata(defaultPrefix)
            : TryReadVersionSchema<TOptions>(metadata, count);

    /// <summary>Reads a complete payload-length table and validates its combined input boundary.</summary>
    /// <param name="lengths">Receives lengths in wire order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadVersionLengths(scoped Span<int> lengths)
    {
        // Every length has at least one byte. Extended records revalidate the remaining table.
        ValidateCollectionPayload(_currentIndex, lengths.Length);
        long total = 0;
        for (int i = 0; i < lengths.Length; i++)
        {
            byte first = GetSpanReference(_currentIndex);
            int length;
            if (first <= 127) { _currentIndex++; length = first; }
            else
            {
                length = ReadVersionExtendedLength();
                ValidateCollectionPayload(_currentIndex, lengths.Length - i - 1);
            }
            lengths[i] = length;
            total += length;
        }
        if (total > int.MaxValue) LuminPackExceptionHelper.ThrowFormatException("Version-tolerant payload is too large.");
        ValidateCollectionPayload(_currentIndex, (int)total);
    }

    /// <summary>Validates a generated table's minimum width or its complete payload boundary.</summary>
    /// <param name="length">The aggregate byte count, calculated without narrowing arithmetic.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ValidateVersionPayloadSize(long length)
    {
        if ((ulong)length > int.MaxValue) LuminPackExceptionHelper.ThrowFormatException("Version-tolerant payload is too large.");
        ValidateCollectionPayload(_currentIndex, (int)length);
    }

    /// <summary>Reads a known-schema length after the complete table's minimum width has been validated.</summary>
    /// <param name="remainingEntries">The minimum byte count still required by subsequent table entries.</param>
    /// <returns>The nonnegative payload length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadVersionLength(int remainingEntries)
    {
        byte first = GetSpanReference(_currentIndex);
        if (first <= 127) { _currentIndex++; return first; }
        int length = ReadVersionExtendedLength();
        ValidateCollectionPayload(_currentIndex, remainingEntries);
        return length;
    }

    /// <summary>Validates and reads the uncommon extended payload length.</summary>
    /// <returns>The nonnegative payload length.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ReadVersionExtendedLength()
    {
        sbyte code = (sbyte)_bufferReference[_currentIndex];
        int width = code switch
        {
            VarIntCodes.Byte or VarIntCodes.SByte => 2,
            VarIntCodes.UInt16 or VarIntCodes.Int16 => 3,
            VarIntCodes.UInt32 or VarIntCodes.Int32 => 5,
            VarIntCodes.UInt64 or VarIntCodes.Int64 => 9,
            _ => 1
        };
        ValidateCollectionPayload(_currentIndex, width);
        int length = ReadVarIntInt32();
        if (length < 0) LuminPackExceptionHelper.ThrowFormatException("Negative version-tolerant payload length.");
        return length;
    }

    /// <summary>Reads one bounded metadata byte string without allocating a managed string.</summary>
    /// <returns>The input metadata bytes.</returns>
    private ReadOnlySpan<byte> ReadVersionMetadata()
    {
        uint length = 0;
        for (int shift = 0; ; shift += 7)
        {
            ValidateCollectionPayload(_currentIndex, 1);
            byte next = _bufferReference[_currentIndex++];
            if (shift == 28 && next > 7) LuminPackExceptionHelper.ThrowFormatException("Invalid version-tolerant metadata length.");
            length |= (uint)(next & 127) << shift;
            if (next < 128) break;
        }
        ValidateCollectionPayload(_currentIndex, (int)length);
        var result = _bufferReference.Slice(_currentIndex, (int)length);
        _currentIndex += (int)length;
        return result;
    }

    /// <summary>Restricts dynamic input to one field without changing the reader or operation-state identity.</summary>
    /// <param name="length">The validated field payload length.</param>
    /// <returns>The previous input length, which must be restored in a finally block.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EnterVersionField(int length)
    {
        ValidateCollectionPayload(_currentIndex, length);
        int previousLength = _bufferReference.Length;
        _bufferReference = _bufferReference.Slice(0, _currentIndex + length);
        return previousLength;
    }

    /// <summary>Restores the exact enclosing input boundary after a generated field operation.</summary>
    /// <param name="previousLength">The length returned by the matching EnterVersionField call.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitVersionField(int previousLength)
    {
        _bufferReference = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref GetSpanReference(0), previousLength);
    }

    /// <summary>Checks a known field's completed read against its table length.</summary>
    /// <param name="actual">The completed absolute cursor.</param>
    /// <param name="expected">The expected absolute end.</param>
    public static void ValidateVersionFieldEnd(int actual, int expected)
    {
        if (actual != expected) LuminPackExceptionHelper.ThrowFormatException("Version-tolerant field length does not match its payload.");
    }
}
