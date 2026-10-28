using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Code;

/// <summary>Shares immutable complete time-zone snapshots without retaining generated owners or thread-local entries.</summary>
/// <remarks>The read table retains at most eight snapshots of at most 256 characters each. Its bound excludes parsed
/// rule metadata and the separate weak-key writer table, whose entries live as long as their source instances.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static unsafe class LuminPackTimeZoneInfoCache
{
    private const int MaximumCachedTextLength = 256;
    private static readonly ConditionalWeakTable<TimeZoneInfo, string> Texts = new();
    private static readonly string UtcText = TimeZoneInfo.Utc.ToSerializedString();
    private static readonly Entry?[] Entries = new Entry?[8];
    private static readonly int[] WriterAdmissionFingerprints = new int[64];
    private static int _victim;

    /// <summary>An atomically published complete text and its immutable parsed value.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class Entry
    {
        /// <summary>The complete snapshot text used for ordinal identity checks.</summary>
        public readonly string Text;
        /// <summary>The immutable time zone represented by <see cref="Text"/>.</summary>
        public readonly TimeZoneInfo Value;

        /// <summary>Pairs a successfully parsed zone with its complete snapshot.</summary>
        /// <param name="text">The complete validated text.</param>
        /// <param name="value">The immutable parsed zone.</param>
        internal Entry(string text, TimeZoneInfo value) { Text = text; Value = value; }
    }

    /// <summary>Gets the complete snapshot text while retaining writer keys weakly.</summary>
    /// <param name="value">The immutable source zone.</param>
    /// <returns>The existing complete sender-snapshot representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetText(TimeZoneInfo value)
        => ReferenceEquals(value, TimeZoneInfo.Utc) ? UtcText : GetOtherText(value);

    /// <summary>Looks up repeated writers while avoiding weak-table registration for one-use instances.</summary>
    /// <param name="value">The immutable source zone.</param>
    /// <returns>The complete snapshot for the live weak key.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetOtherText(TimeZoneInfo value)
    {
        if (Texts.TryGetValue(value, out var text)) return text;
        int hash = RuntimeHelpers.GetHashCode(value);
        ref int fingerprint = ref WriterAdmissionFingerprints[hash & 63];
        if (Volatile.Read(ref fingerprint) != hash)
        {
            // The fingerprint controls admission only; a collision never supplies another instance's text.
            Volatile.Write(ref fingerprint, hash);
            return value.ToSerializedString();
        }
        return Texts.GetValue(value, static zone => zone.ToSerializedString());
    }

    /// <summary>Returns the exact width of a snapshot record under the UTF-16 length profile: the object marker and the
    /// string record in its compact or raw form.</summary>
    /// <param name="text">The complete snapshot, or null for a null zone.</param>
    /// <returns>The complete record width.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static int Utf16SnapshotRecordLength(string? text) =>
        text is null ? 1 : checked(1 + sizeof(int) + (text.Length == 0 ? 0 : StringSerializer.Utf16RecordPayloadLength(text)));

    /// <summary>Writes the existing object marker and complete string through the normal string kernel.</summary>
    /// <typeparam name="TOptions">The stateless binary profile.</typeparam>
    /// <param name="writer">The output state.</param>
    /// <param name="text">The complete snapshot, or null for a null zone.</param>
    /// <param name="capacityReserved">Whether the caller already reserved the complete record width.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteTimeZoneText<TOptions>(this ref LuminPackWriter writer, string? text, bool capacityReserved = false)
        where TOptions : struct, ILuminPackOptions
    {
        if (text is null) { writer.WriteNullObjectHeader(); return; }
        writer.WriteObjectHeader(0);
        writer.Write<TOptions>(text, capacityReserved);
    }

    /// <summary>Selects a reuse hint from a complete string-length prefix without changing the input cursor.</summary>
    /// <typeparam name="TOptions">The stateless binary profile.</typeparam>
    /// <param name="reader">The input state positioned at the snapshot string.</param>
    /// <returns>A bounded candidate whose full content still needs validation, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entry? GetTimeZoneCandidate<TOptions>(this ref LuminPackReader reader)
        where TOptions : struct, ILuminPackOptions
    {
        if (default(TOptions).StringRecording != LuminPackStringRecording.Length) return null;
        int available = reader.Buffer.Length - reader.Position;
#if NET8_0_OR_GREATER
        ref byte cursor = ref reader.Cursor;
#else
        ref byte cursor = ref Unsafe.AsRef<byte>(reader.Cursor);
#endif
        if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && available >= sizeof(int))
            return Find(LuminPackReader.Read<int>(ref cursor) >> 1);
        if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && available >= 2 * sizeof(int))
            return Find(LuminPackReader.Read<int>(ref Unsafe.Add(ref cursor, sizeof(int))));
        return null;
    }

    /// <summary>Reads a matching UTF-16 snapshot directly after validating its complete input range and content.</summary>
    /// <typeparam name="TOptions">The stateless binary profile.</typeparam>
    /// <param name="reader">The input state positioned at the snapshot string.</param>
    /// <param name="candidate">The previously selected immutable hint.</param>
    /// <param name="value">Receives the cached value only when all bytes match.</param>
    /// <returns>Whether the complete string was consumed without decoding a new string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadTimeZoneInfo<TOptions>(this ref LuminPackReader reader, Entry? candidate, out TimeZoneInfo? value)
        where TOptions : struct, ILuminPackOptions
    {
        value = null;
        if (default(TOptions).StringEncoding != LuminPackStringEncoding.UTF16 ||
            default(TOptions).StringRecording != LuminPackStringRecording.Length || candidate is null) return false;
        reader.EnsureReadable(reader.Position, sizeof(int));
#if NET8_0_OR_GREATER
        ref byte cursor = ref reader.Cursor;
#else
        ref byte cursor = ref Unsafe.AsRef<byte>(reader.Cursor);
#endif
        int length = candidate.Text.Length;
        int header = LuminPackReader.Read<int>(ref cursor);
        // Snapshots are written through the string kernel, so ASCII text arrives in the compact Latin-1 form.
        bool latin1 = length > 0 && header == StringSerializer.Latin1Header(length);
        int bytes = latin1 ? length : length * sizeof(char);
        if (!latin1 && header != bytes) return false;
        reader.EnsureReadable(reader.Position, bytes + sizeof(int));
        ref byte source = ref Unsafe.Add(ref cursor, sizeof(int));
        if (!Matches(ref source, bytes, latin1, candidate.Text))
        {
            int slot = Slot(length);
            var first = Volatile.Read(ref Entries[slot]);
            var other = ReferenceEquals(first, candidate) ? Volatile.Read(ref Entries[slot + 1]) : first;
            if (other is null || other.Text.Length != length || !Matches(ref source, bytes, latin1, other.Text)) return false;
            candidate = other;
        }
        reader.Advance(bytes + sizeof(int));
        value = candidate.Value;
        return true;
    }

    /// <summary>Compares a validated snapshot payload with a same-length cached text.</summary>
    /// <param name="source">The first payload byte.</param>
    /// <param name="bytes">The payload width.</param>
    /// <param name="latin1">Whether the payload is the compact Latin-1 form.</param>
    /// <param name="text">The cached text.</param>
    /// <returns>Whether every character matches.</returns>
    private static bool Matches(ref byte source, int bytes, bool latin1, string text) =>
        latin1 ? StringSerializer.MatchesLatin1(ref source, text) : MemoryMarshal.CreateReadOnlySpan(ref source, bytes).SequenceEqual(MemoryMarshal.AsBytes(text.AsSpan()));

    /// <summary>Parses a complete snapshot or reuses an ordinally identical immutable cached value.</summary>
    /// <param name="text">The complete decoded snapshot.</param>
    /// <param name="candidate">An optional hint selected from the input header.</param>
    /// <returns>A zone preserving every rule and name in the sender snapshot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeZoneInfo Parse(string text, Entry? candidate = null)
    {
        var cached = candidate ?? Find(text.Length);
        if (cached is not null && string.Equals(cached.Text, text, StringComparison.Ordinal)) return cached.Value;
        int slot = Slot(text.Length);
        var second = Volatile.Read(ref Entries[slot + 1]);
        if (second is not null && string.Equals(second.Text, text, StringComparison.Ordinal)) return second.Value;
        return ParseSlow(text);
    }

    /// <summary>Parses before publishing a bounded immutable entry; failures never change the table.</summary>
    /// <param name="text">The complete decoded snapshot.</param>
    /// <returns>The newly parsed immutable zone.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TimeZoneInfo ParseSlow(string text)
    {
        var value = TimeZoneInfo.FromSerializedString(text);
        if (text.Length <= MaximumCachedTextLength)
        {
            int slot = Slot(text.Length);
            if (Volatile.Read(ref Entries[slot]) is not null)
                slot += Volatile.Read(ref Entries[slot + 1]) is null ? 1 : Interlocked.Increment(ref _victim) & 1;
            Volatile.Write(ref Entries[slot], new Entry(text, value));
        }
        return value;
    }

    /// <summary>Selects one of the four two-way buckets using only the snapshot length.</summary>
    /// <param name="length">The UTF-16 code-unit count.</param>
    /// <returns>The first slot in the selected bucket.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Slot(int length) => (int)(unchecked((uint)length * 0x9E3779B9u) >> 30) * 2;

    /// <summary>Finds a bounded same-length hint whose complete text is checked by its caller.</summary>
    /// <param name="length">The UTF-16 code-unit count.</param>
    /// <returns>A possible immutable snapshot, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Entry? Find(int length)
    {
        int slot = Slot(length);
        var first = Volatile.Read(ref Entries[slot]);
        if (first is not null && first.Text.Length == length) return first;
        var second = Volatile.Read(ref Entries[slot + 1]);
        return second is not null && second.Text.Length == length ? second : null;
    }
}
