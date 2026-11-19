using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Option;

namespace LuminPack.Core;

/// <summary>The input boundary and schema extent of an entered version-tolerant field.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct LuminPackVersionFieldScope
{
    internal readonly int End;
    internal readonly int PreviousLength;
    internal readonly int SchemaCount;

    /// <summary>Captures the bounds and schema extent of a known field.</summary>
    /// <param name="end">The absolute position after the field.</param>
    /// <param name="previousLength">The enclosing input window length.</param>
    /// <param name="schemaCount">The number of schemas present before the field.</param>
    internal LuminPackVersionFieldScope(int end, int previousLength, int schemaCount)
    {
        End = end;
        PreviousLength = previousLength;
        SchemaCount = schemaCount;
    }
}

public ref partial struct LuminPackReader
{
    /// <summary>Reads and resolves the framing of one positional or mapped version-tolerant record.</summary>
    /// <typeparam name="TOptions">The selected binary string profile.</typeparam>
    /// <param name="schema">The generated identity of the receiving model.</param>
    /// <param name="descriptors">The complete receiving descriptor table.</param>
    /// <param name="cache">The receiving model's descriptor cache.</param>
    /// <returns>The framing decision and any map required for the payload.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminPackVersionRecord ReadVersionRecord<TOptions>(ulong schema, scoped ReadOnlySpan<byte> descriptors,
        LuminPackVersionMapCache cache)
        where TOptions : struct, ILuminPackStringCodec
    {
        if (!IsOutOfRange(Position, 3, _buffer.Length))
        {
            ref byte source = ref Current;
            byte form = Unsafe.Add(ref source, 1);
            if (source == LuminPackVersionRecord.Marker && (uint)(form - LuminPackVersionRecord.Short) <= 5)
            {
                if (form != (byte)(LuminPackVersionRecord.Short | default(TOptions).BinaryProfile))
                    ThrowVersionHeader();
                uint ordinal = Unsafe.Add(ref Current, 2);
                if (ordinal < 128 && _options is { } state && state.VersionRegistry.IsPositional(ordinal, cache))
                {
                    AdvanceValidated(3);
                    return default;
                }
                return ReadShortVersionRecord(schema, descriptors, cache);
            }
        }
        return ReadVersionRecordSlow<TOptions>(schema, descriptors, cache);
    }

    /// <summary>Resolves full, mapped, extended, legacy and invalid versioned headers.</summary>
    /// <typeparam name="TOptions">The selected binary string profile.</typeparam>
    /// <param name="schema">The generated identity of the receiving model.</param>
    /// <param name="descriptors">The complete receiving descriptor table.</param>
    /// <param name="cache">The receiving model's descriptor cache.</param>
    /// <returns>The resolved framing decision.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private LuminPackVersionRecord ReadVersionRecordSlow<TOptions>(ulong schema, scoped ReadOnlySpan<byte> descriptors,
        LuminPackVersionMapCache cache)
        where TOptions : struct, ILuminPackStringCodec
    {
        EnsureReadable(Position, 1);
        byte marker = Current;
        if (marker == LuminPackCode.NullObject)
        {
            AdvanceValidated(1);
            return new LuminPackVersionRecord(null, isNull: true);
        }

        EnsureReadable(Position, 2);
        byte form = Unsafe.Add(ref Current, 1);
        if (marker != LuminPackVersionRecord.Marker)
            ThrowVersionHeader();
        if (form == 1)
            return new LuminPackVersionRecord(null, isLegacy: true);
        if ((form & 15) > 5)
            ThrowVersionHeader();

        byte profile = default(TOptions).BinaryProfile;
        if (form == (byte)(LuminPackVersionRecord.Short | profile))
            return ReadShortVersionRecord(schema, descriptors, cache);
        if (form == (byte)(LuminPackVersionRecord.Full | profile))
            return ReadFullVersionRecord(schema, descriptors, cache);

        ThrowVersionHeader();
        return default;
    }

    /// <summary>Validates and registers a complete incoming descriptor table before reading its payload.</summary>
    /// <param name="schema">The receiving model's schema.</param>
    /// <param name="descriptors">The receiving model's immutable descriptors.</param>
    /// <param name="cache">The receiving model's descriptor cache.</param>
    /// <returns>The positional or mapped framing for the record.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private LuminPackVersionRecord ReadFullVersionRecord(ulong schema, scoped ReadOnlySpan<byte> descriptors,
        LuminPackVersionMapCache cache)
    {
        const int HeaderWidth = 3 + sizeof(ulong);
        int start = Position;
        EnsureReadable(start, HeaderWidth);
        byte count = Unsafe.Add(ref Current, 2);
        if (count > LuminPackVersionRecord.MaximumFields)
            ThrowVersionHeader();

        int length = count * LuminPackVersionRecord.DescriptorWidth;
        EnsureReadable(start, HeaderWidth + length);
        ulong senderSchema = LuminPackReader.Read<ulong>(ref Unsafe.Add(ref Current, 3));
        var incoming = _buffer.Slice(start + HeaderWidth, length);
        LuminPackVersionRegistration entry;
        if (senderSchema == schema && incoming.SequenceEqual(descriptors))
        {
            entry = new LuminPackVersionRegistration(senderSchema, cache, cache.GetModelDescriptors(descriptors), null);
        }
        else
        {
            var mapped = cache.Resolve(senderSchema, incoming);
            entry = new LuminPackVersionRegistration(senderSchema, cache, mapped.Descriptors, mapped.Map);
        }
        OptionState.VersionRegistry.Add(in entry, indexBySchema: false);
        AdvanceValidated(HeaderWidth + length);
        return new LuminPackVersionRecord(entry.Map);
    }

    /// <summary>Resolves a short record in the active descriptor registry.</summary>
    /// <param name="schema">The receiving model's schema.</param>
    /// <param name="descriptors">The receiving model's immutable descriptors.</param>
    /// <param name="cache">The receiving model's descriptor cache.</param>
    /// <returns>The positional or mapped framing for the record.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private LuminPackVersionRecord ReadShortVersionRecord(ulong schema, scoped ReadOnlySpan<byte> descriptors,
        LuminPackVersionMapCache cache)
    {
        int start = Position;
        EnsureReadable(start, 3);
        byte first = Unsafe.Add(ref Current, 2);
        int consumed = 3;
        uint ordinal = first < 128 ? first : ReadExtendedVersionOrdinal(start, out consumed);
        var state = _options;
        if (state is null)
            ThrowMissingSchema();

        var entry = state!.VersionRegistry.Get(ordinal);
        LuminPackDescriptorMap? map;
        if (ReferenceEquals(entry.Owner, cache))
            map = entry.Map;
        else if (entry.Schema == schema && descriptors.SequenceEqual(entry.Descriptors))
            map = null;
        else
            map = cache.Resolve(entry.Schema, entry.Descriptors).Map;

        AdvanceValidated(consumed);
        return new LuminPackVersionRecord(map);
    }

    /// <summary>Decodes a canonical multi-byte schema ordinal within the current input boundary.</summary>
    /// <param name="start">The absolute short record position.</param>
    /// <param name="consumed">Receives the complete short header width.</param>
    /// <returns>The registry ordinal.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private uint ReadExtendedVersionOrdinal(int start, out int consumed)
    {
        uint value = 0;
        int offset = start + 2;
        for (int shift = 0; shift <= 28; shift += 7)
        {
            EnsureReadable(offset, 1);
            byte next = _buffer[offset++];
            if (shift == 28 && next > 3)
                break;
            value |= (uint)(next & 127) << shift;
            if (next < 128)
            {
                if (next == 0)
                    break;
                consumed = offset - start;
                return value;
            }
        }
        LuminPackExceptionHelper.ThrowFormatException("Invalid version-tolerant schema ordinal.");
        consumed = 0;
        return 0;
    }

    /// <summary>Consumes an unknown version-tolerant field using its recorded framing.</summary>
    /// <typeparam name="TOptions">The binary string profile of this record.</typeparam>
    /// <param name="widthCode">The field's validated descriptor width code.</param>
    /// <param name="maxStringLength">The maximum permitted UTF-16 character count for an unframed string field.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void SkipVersionField<TOptions>(byte widthCode, int maxStringLength = int.MaxValue)
        where TOptions : struct, ILuminPackStringCodec
    {
        if (widthCode <= 250)
            Advance(widthCode);
        else if (widthCode == 254)
            default(TOptions).Skip(ref this, maxStringLength);
        else if (widthCode == 255)
            Advance(ReadVersionPayloadLength());
        else
            ThrowVersionHeader();
    }

    /// <summary>Restricts reads to one length-prefixed member and starts a nested schema scope.</summary>
    /// <returns>The boundary and extent to restore when this field ends.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminPackVersionFieldScope EnterVersionField()
    {
        ReadVersionPayloadLength(out int end);
        int previousLength = _buffer.Length;
        // The length reader has already proved the whole field lies inside this input window.
        Buffer = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(_buffer), end);
        return new LuminPackVersionFieldScope(end, previousLength, EnterVersionScope());
    }

    /// <summary>Restricts reads to a known payload length from a revision-1 field table.</summary>
    /// <param name="length">The validated number of field bytes.</param>
    /// <returns>The input boundary and schema extent to restore when the field ends.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminPackVersionFieldScope EnterLegacyVersionField(int length)
    {
        int count = EnterVersionScope();
        int previousLength = EnterVersionField(length);
        return new LuminPackVersionFieldScope(_buffer.Length, previousLength, count);
    }

    /// <summary>Checks whether the complete bounded field was consumed.</summary>
    /// <param name="scope">The current version-tolerant field scope.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ValidateVersionFieldEnd(LuminPackVersionFieldScope scope) => ValidateVersionFieldEnd(Position, scope.End);

    /// <summary>Restores the enclosing input window and discards schema registrations made by the current field.</summary>
    /// <param name="scope">The scope returned when the field was entered.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitVersionField(LuminPackVersionFieldScope scope)
    {
        ExitVersionField(scope.PreviousLength);
        RollbackVersionScope(scope.SchemaCount);
    }

    /// <summary>Captures the descriptor registry extent before a record or field is read.</summary>
    /// <returns>The extent to restore after a failed record or an exited field.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EnterVersionScope() => _options?.VersionRegistry.Count ?? 0;

    /// <summary>Discards schemas added after the saved registry extent.</summary>
    /// <param name="count">The extent captured before entering the record or field.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RollbackVersionScope(int count)
    {
        if (_options is { } state && state.VersionRegistry.Count != count)
            state.VersionRegistry.Truncate(count);
    }

    /// <summary>Rejects framing outside the supported version-tolerant record formats.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowVersionHeader() => LuminPackExceptionHelper.ThrowFormatException("Unsupported version-tolerant record header.");

    /// <summary>Rejects a short record without an enclosing schema registration.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMissingSchema() => LuminPackExceptionHelper.ThrowFormatException("Unknown version-tolerant schema ordinal.");
}
