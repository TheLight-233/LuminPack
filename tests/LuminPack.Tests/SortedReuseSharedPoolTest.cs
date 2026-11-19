using System.Buffers;
using System.Collections.Immutable;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Proves mutable and immutable collection readers borrow the same framework element pool.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class SortedReuseSharedPoolTest
{
    /// <summary>Verifies that probing a captured write run never invokes a consumer's profile getters.</summary>
    [Fact]
    public void CapturedRunsLeaveCustomProfileToTheOriginalWrites()
    {
        SharedCollectionPoolOptions.Expected = ["alpha"];
        SharedCollectionPoolOptions.SawPopulatedRental = false;
        try
        {
            Span<byte> storage = new byte[64];
            var writer = new global::LuminPack.Core.LuminPackWriter(ref storage);
            KeyValuePair<string, string?>[] pairs = [new("k", "v")];
            Assert.Equal(0, writer.WriteStringPairPrefix<SharedCollectionPoolOptions>(pairs));
            Assert.Equal(0, writer.WriteStringPrefix<SharedCollectionPoolOptions>(["k", "v"]));
            Assert.Equal(0, writer.Position);
            Assert.False(SharedCollectionPoolOptions.SawPopulatedRental);
            writer.Write<SharedCollectionPoolOptions>(pairs[0].Key);
            Assert.True(SharedCollectionPoolOptions.SawPopulatedRental);
        }
        finally
        {
            SharedCollectionPoolOptions.Expected = null;
            SharedCollectionPoolOptions.SawPopulatedRental = false;
        }
    }

    /// <summary>Observes one physical string rental in both set reuse and immutable-list materialization.</summary>
    [Fact]
    public void DifferentCollectionReadersShareTheSameStringRental()
    {
        string[] values = { "alpha", "beta", "gamma" };
        byte[] payload = LuminPackSerializer.Serialize<string[], LuminPackUtf16LengthOptions>(values);
        string[] expected = global::LuminPack.Utility.LuminStaging<string>.Rent(16);
        global::LuminPack.Utility.LuminStaging<string>.Return(expected, expected.Length);
        string[]? returned = null;
        try
        {
            SharedCollectionPoolOptions.Expected = expected;
            SharedCollectionPoolOptions.SawPopulatedRental = false;
            var set = new SortedSet<string> { "old" };
            var original = set;
            Assert.Equal(payload.Length, LuminPackSerializer.Deserialize<SortedSet<string>, SharedCollectionPoolOptions>(payload, ref set));
            Assert.Same(original, set);
            Assert.True(set.SetEquals(values));
            Assert.True(SharedCollectionPoolOptions.SawPopulatedRental);
            Assert.All(expected, value => Assert.Null(value));

            SharedCollectionPoolOptions.SawPopulatedRental = false;
            ImmutableList<string>? list = null;
            Assert.Equal(payload.Length, LuminPackSerializer.Deserialize<ImmutableList<string>, SharedCollectionPoolOptions>(payload, ref list));
            Assert.Equal(values, list);
            Assert.True(SharedCollectionPoolOptions.SawPopulatedRental);
            Assert.All(expected, value => Assert.Null(value));
            returned = global::LuminPack.Utility.LuminStaging<string>.Rent(16);
            Assert.Same(expected, returned);
        }
        finally
        {
            SharedCollectionPoolOptions.Expected = null;
            SharedCollectionPoolOptions.SawPopulatedRental = false;
            if (returned is not null) global::LuminPack.Utility.LuminStaging<string>.Return(returned, returned.Length);
            // Restore a captured rental if an assertion exposed the former reader losing it.
            if (returned is not null && !ReferenceEquals(returned, expected)) global::LuminPack.Utility.LuminStaging<string>.Return(expected, expected.Length);
        }
    }
}

/// <summary>Observes shared reference storage while returning the unchanged UTF-16 length profile.</summary>
public readonly struct SharedCollectionPoolOptions : ILuminPackOptions
{
    private global::LuminPack.Option.LuminPackUtf16LengthStrings ObservedCodec
    {
        get
        {
            _ = StringEncoding;
            return default;
        }
    }

    /// <inheritdoc/>
    public int RecordBound(string? value) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).RecordBound(value);

    /// <inheritdoc/>
    public ref byte WriteRecord(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
        ref ObservedCodec.WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    public void Write(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
        ObservedCodec.Write(ref writer, value);

    /// <inheritdoc/>
    public string? Read(scoped ref global::LuminPack.Core.LuminPackReader reader, string? current) =>
        ObservedCodec.Read(ref reader, current);

    /// <inheritdoc/>
    public void ReadPair(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        ObservedCodec.ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    public void Skip(scoped ref global::LuminPack.Core.LuminPackReader reader, int maxStringLength) =>
        default(LuminPackUtf16LengthStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    public void ReadSlots(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped global::System.Span<string?> slots)
    {
        foreach (ref string? slot in slots)
            reader.Read<SharedCollectionPoolOptions>(ref slot);
    }

    /// <inheritdoc/>
    public global::System.Buffers.OperationStatus Encode(global::System.ReadOnlySpan<char> source, global::System.Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    public int Measure(string? value) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Measure(value);

    /// <inheritdoc/>
    public void WriteReserved(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
        ObservedCodec.WriteReserved(ref writer, value);

    /// <inheritdoc/>
    public void WriteText(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<char> text) =>
        ObservedCodec.WriteText(ref writer, text);

    /// <inheritdoc/>
    public global::System.Buffers.OperationStatus Decode(global::System.ReadOnlySpan<byte> source, global::System.Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    public bool SupportsUtf16Runs() =>
        false;

    /// <inheritdoc/>
    public void WriteSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
        ObservedCodec.WriteSlots(ref writer, values);

    /// <inheritdoc/>
    public void WriteCheckedSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
        ObservedCodec.WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    public void WritePairs(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values) =>
        ObservedCodec.WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    public void WritePairRuns(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values, bool allowRuns) =>
        ObservedCodec.WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    public ref byte WriteAtCursor(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
        ref ObservedCodec.WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    public int WriteSlotPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    public int WritePairPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, string?>> pairs) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    public bool TryPeekCharacterCount(scoped ref global::LuminPack.Core.LuminPackReader reader, out int count) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    public bool TryPeekUtf16Payload(scoped ref global::LuminPack.Core.LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public global::LuminPack.Option.LuminPackStringEncoding Encoding => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encoding;

    /// <inheritdoc/>
    public global::LuminPack.Option.LuminPackStringRecording Recording => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Recording;

    /// <inheritdoc/>
    public byte BinaryProfile => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).BinaryProfile;

    public static string[]? Expected;
    public static bool SawPopulatedRental;

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        get
        {
            if (Expected is { Length: > 0 } values && values[0] == "alpha") SawPopulatedRental = true;
            return LuminPackStringEncoding.UTF16;
        }
    }

    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
    /// <inheritdoc/>
    public StandardFormat StandardFormat => new('G');
    /// <inheritdoc/>
    public int MaxJsonDepth => 1024;
    /// <inheritdoc/>
    public bool ValidateJsonSyntax => false;
}
