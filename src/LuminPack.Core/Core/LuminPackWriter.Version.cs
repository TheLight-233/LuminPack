using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Option;

namespace LuminPack.Core;

public ref partial struct LuminPackWriter
{
    /// <summary>Writes the complete descriptor table or references its ordinal in the current operation.</summary>
    /// <typeparam name="TOptions">The selected binary string profile.</typeparam>
    /// <param name="schema">The generated identity of the descriptor table.</param>
    /// <param name="descriptors">The complete descriptor table in wire order.</param>
    /// <param name="cache">The generated owner of this immutable table.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVersionRecord<TOptions>(ulong schema, scoped ReadOnlySpan<byte> descriptors, LuminPackVersionMapCache cache)
        where TOptions : struct, ILuminPackStringCodec
    {
        var state = OptionState;
        int ordinal = state.VersionRegistry.Find(schema, cache, descriptors);
        byte profile = default(TOptions).BinaryProfile;
        if (ordinal < 0)
        {
            WriteFullVersionRecord(schema, descriptors, cache, state, profile);
            return;
        }

        if (ordinal >= 128)
        {
            WriteExtendedVersionRecord(ordinal, profile);
            return;
        }
        EnsureAdditionalCapacity(3);
        ref byte destination = ref Current;
        destination = LuminPackVersionRecord.Marker;
        Unsafe.Add(ref destination, 1) = (byte)(LuminPackVersionRecord.Short | profile);
        Unsafe.Add(ref destination, 2) = (byte)ordinal;
        AdvanceProven(3);
    }

    /// <summary>Writes and registers a complete schema when it has not appeared in scope.</summary>
    /// <param name="schema">The generated table identity.</param>
    /// <param name="descriptors">The immutable table bytes.</param>
    /// <param name="cache">The generated table's owner.</param>
    /// <param name="state">The current operation's writer state.</param>
    /// <param name="profile">The binary string profile identifier.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteFullVersionRecord(ulong schema, scoped ReadOnlySpan<byte> descriptors, LuminPackVersionMapCache cache,
        LuminPackWriterOptionalState state, byte profile)
    {
        int count = descriptors.Length / sizeof(ulong);
        if (count > LuminPackVersionRecord.MaximumFields || descriptors.Length % sizeof(ulong) != 0 || profile > 5)
            LuminPackExceptionHelper.ThrowFormatException("Invalid version-tolerant record header.");

        int length = 3 + sizeof(ulong) + descriptors.Length;
        EnsureAdditionalCapacity(length);
        var entry = new LuminPackVersionRegistration(schema, cache, cache.GetModelDescriptors(descriptors), null);
        state.VersionRegistry.Add(in entry, indexBySchema: true);
        ref byte destination = ref Current;
        destination = LuminPackVersionRecord.Marker;
        Unsafe.Add(ref destination, 1) = (byte)(LuminPackVersionRecord.Full | profile);
        Unsafe.Add(ref destination, 2) = (byte)count;
        LuminPackWriter.Write(ref Unsafe.Add(ref destination, 3), schema);
        LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref destination, 3 + sizeof(ulong)),
            ref MemoryMarshal.GetReference(descriptors), (uint)descriptors.Length);
        AdvanceProven(length);
    }

    /// <summary>Writes a short header for an ordinal encoded in multiple base-128 bytes.</summary>
    /// <param name="ordinal">The schema registration number.</param>
    /// <param name="profile">The binary string profile identifier.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteExtendedVersionRecord(int ordinal, byte profile)
    {
        int ordinalWidth = ordinal < 1 << 14 ? 2 : ordinal < 1 << 21 ? 3 : ordinal < 1 << 28 ? 4 : 5;
        EnsureAdditionalCapacity(ordinalWidth + 2);
        ref byte destination = ref Current;
        destination = LuminPackVersionRecord.Marker;
        Unsafe.Add(ref destination, 1) = (byte)(LuminPackVersionRecord.Short | profile);

        uint remaining = (uint)ordinal;
        for (int i = 2; i <= ordinalWidth; i++)
        {
            Unsafe.Add(ref destination, i) = (byte)(remaining | 128);
            remaining >>= 7;
        }
        Unsafe.Add(ref destination, ordinalWidth + 1) = (byte)remaining;
        AdvanceProven(ordinalWidth + 2);
    }

    /// <summary>Writes one complete version-tolerant string with the selected codec.</summary>
    /// <typeparam name="TOptions">The selected binary string profile.</typeparam>
    /// <param name="value">The nullable string field value.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVersionString<TOptions>(string? value) where TOptions : struct, ILuminPackStringCodec
    {
#if NET8_0_OR_GREATER
        if (IntPtr.Size == 8 &&
            (typeof(TOptions) == typeof(LuminPackDefaultOptions) ||
             typeof(TOptions) == typeof(LuminPackUtf16LengthOptions) ||
             typeof(TOptions) == typeof(LuminPackUtf16LengthStrings)))
        {
            if (FitsRecord(Utf16RecordBound(value)))
                AdvanceProven(WriteReservedUtf16Record(ref Current, value));
            else
                WriteVersionUtf16StringSlow(value);
            return;
        }
#endif
        var codec = default(TOptions);
        int bound = codec.RecordBound(value);
        if (FitsRecord(bound))
        {
            ref byte cursor = ref Current;
            ref byte end = ref codec.WriteRecord(ref this, ref cursor, value);
            CommitRecords(ref end);
        }
        else
        {
            codec.Write(ref this, value);
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>Continues through the ordinary UTF-16 length writer after a short version record window.</summary>
    /// <param name="value">The nullable string field value.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteVersionUtf16StringSlow(string? value) => WriteUtf16LengthString(value);
#endif

    /// <summary>Reserves and writes one fixed-width version-tolerant field.</summary>
    /// <typeparam name="T">The unmanaged field storage type.</typeparam>
    /// <param name="value">The complete fixed-width field value.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVersionValue<T>(in T value) where T : unmanaged
    {
        int width = Unsafe.SizeOf<T>();
        EnsureAdditionalCapacity(width);
        Write(ref Current, in value);
        AdvanceProven(width);
    }

    /// <summary>Writes the zero padding of a fixed-width, absent nullable field.</summary>
    /// <param name="byteCount">The number of bytes reserved for the absent payload.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PadVersionField(int byteCount)
    {
        int position = Position;
        Reserve(position, byteCount).Clear();
        AdvanceProven(byteCount);
    }

    /// <summary>Captures the active descriptor registry extent before a length-prefixed field is written.</summary>
    /// <returns>The extent to restore when the field ends.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EnterVersionScope() => Options?.VersionRegistry.Count ?? 0;

    /// <summary>Discards schemas added within a length-prefixed field, preserving the enclosing registrations.</summary>
    /// <param name="count">The extent captured before entering this field.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitVersionScope(int count)
    {
        var state = Options;
        if (state is not null && state.VersionRegistry.Count != count)
            state.VersionRegistry.Truncate(count);
    }
}
