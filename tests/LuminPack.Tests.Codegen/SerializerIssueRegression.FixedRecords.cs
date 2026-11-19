using Microsoft.CodeAnalysis;

internal static partial class SerializerIssueRegression
{
    /// <summary>Verifies fixed object reservations and width-preserving named record fields.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunFixedRecordReservations(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Buffers;
            using System.Buffers.Binary;
            using System.Collections.Generic;
            using System.Numerics;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using LuminPack.Utility;

            [LuminPackable] public sealed class FixedRecord
            {
                public int Number;
                public Vector3 Position;
                public long Ticks;
                public Guid Tag;
            }

            [LuminPackable] public sealed class ObservedRecord
            {
                public int Number { get; set; }
                [LuminPackOnSerializing] public void Before() => Number += 1;
                [LuminPackOnSerialized] public void After() => Number += 2;
            }

            [LuminPackable(GeneratorType.VersionTolerant)] public sealed partial class VersionRecord
            {
                [LuminPackOrder(0)] public Guid Tag;
                [LuminPackOrder(1)] public DateTime Time;
                [LuminPackOrder(2)] public int[] Numbers;
                [LuminPackOrder(3)] public Dictionary<int, int> Pairs;
                [LuminPackOrder(4)] public Dictionary<int, string> Text;
            }

            [LuminPackable] public sealed class PositionalRecord
            {
                public string Text { get; set; }
                public List<Vector3> Positions;
            }

            public readonly struct ObservingProfile : ILuminPackOptions
            {
                /// <inheritdoc/>
                public TResult ReadJsonValue<TOperation, TResult>(scoped ref global::LuminPack.Core.LuminPackJsonReader reader)
                    where TOperation : struct, global::LuminPack.Option.ILuminPackJsonReadOperation<TResult> =>
                    ObservedCodec.ReadJsonValue<TOperation, TResult>(ref reader);

                /// <inheritdoc/>
                public void WriteJsonValue<TOperation, TValue>(scoped ref global::LuminPack.Core.LuminPackJsonWriter writer, TValue value)
                    where TOperation : struct, global::LuminPack.Option.ILuminPackJsonWriteOperation<TValue> =>
                    ObservedCodec.WriteJsonValue<TOperation, TValue>(ref writer, value);

                /// <inheritdoc/>
                public int JsonByteCount(int units) =>
                    ObservedCodec.JsonByteCount(units);

                /// <inheritdoc/>
                public bool TryReadJsonUnit(scoped ref global::LuminPack.Core.LuminPackJsonReader reader, out char value) =>
                    ObservedCodec.TryReadJsonUnit(ref reader, out value);

                /// <inheritdoc/>
                public void WriteJsonAscii(scoped ref global::LuminPack.Core.LuminPackJsonWriter writer, byte value) =>
                    ObservedCodec.WriteJsonAscii(ref writer, value);

                /// <inheritdoc/>
                public bool ReadJsonToken<TProfile>(scoped ref global::LuminPack.Core.LuminPackJsonReader reader) where TProfile : struct, global::LuminPack.Option.ILuminPackOptions =>
                    default(global::LuminPack.Option.LuminPackTrustedJson).ReadJsonToken<TProfile>(ref reader);

                /// <inheritdoc/>
                public bool TracksJsonGrammar() =>
                    default(global::LuminPack.Option.LuminPackTrustedJson).TracksJsonGrammar();

                /// <inheritdoc/>
                public void ValidateJsonUtf8(scoped global::System.ReadOnlySpan<byte> text) =>
                    default(global::LuminPack.Option.LuminPackTrustedJson).ValidateJsonUtf8(text);

                /// <inheritdoc/>
                public void CheckJsonDepth(int depth) =>
                    global::LuminPack.Option.LuminPackJsonPolicy.CheckDepth(depth, MaxJsonDepth);

                private global::LuminPack.Option.LuminPackUtf16LengthStrings ObservedCodec
                {
                    get
                    {
                        _ = StringEncoding;
                        return default;
                    }
                }

                public int RecordBound(string? value) =>
                    default(global::LuminPack.Option.LuminPackUtf16LengthStrings).RecordBound(value);

                public ref byte WriteRecord(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
                    ref ObservedCodec.WriteRecord(ref writer, ref cursor, value);

                public void Write(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
                    ObservedCodec.Write(ref writer, value);

                public string? Read(scoped ref global::LuminPack.Core.LuminPackReader reader, string? current) =>
                    ObservedCodec.Read(ref reader, current);

                public void ReadPair(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
                    ObservedCodec.ReadPair(ref reader, ref first, ref second);

                public void Skip(scoped ref global::LuminPack.Core.LuminPackReader reader, int maxStringLength) =>
                    ObservedCodec.Skip(ref reader, maxStringLength);

                public void ReadSlots(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped global::System.Span<string?> slots) =>
                    ObservedCodec.ReadSlots(ref reader, slots);

                public global::System.Buffers.OperationStatus Encode(global::System.ReadOnlySpan<char> source, global::System.Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
                    default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

                public int Measure(string? value) =>
                    default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Measure(value);

                public void WriteReserved(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
                    ObservedCodec.WriteReserved(ref writer, value);

                public void WriteText(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<char> text) =>
                    ObservedCodec.WriteText(ref writer, text);

                public global::System.Buffers.OperationStatus Decode(global::System.ReadOnlySpan<byte> source, global::System.Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
                    default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

                public bool SupportsUtf16Runs() =>
                    false;

                public void WriteSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
                    ObservedCodec.WriteSlots(ref writer, values);

                public void WriteCheckedSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
                    ObservedCodec.WriteCheckedSlots(ref writer, values);

                public void WritePairs(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values) =>
                    ObservedCodec.WritePairs(ref writer, keys, values);

                public void WritePairRuns(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values, bool allowRuns) =>
                    ObservedCodec.WritePairRuns(ref writer, keys, values, allowRuns);

                public ref byte WriteAtCursor(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
                    ref ObservedCodec.WriteAtCursor(ref writer, ref cursor, value);

                public int WriteSlotPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
                    default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteSlotPrefix(ref writer, values);

                public int WritePairPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, string?>> pairs) =>
                    default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairPrefix(ref writer, pairs);

                public bool TryPeekCharacterCount(scoped ref global::LuminPack.Core.LuminPackReader reader, out int count) =>
                    default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekCharacterCount(ref reader, out count);

                public bool TryPeekUtf16Payload(scoped ref global::LuminPack.Core.LuminPackReader reader, int length, out int bytes, out bool latin1) =>
                    default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

                public global::LuminPack.Option.LuminPackStringEncoding Encoding => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encoding;

                public global::LuminPack.Option.LuminPackStringRecording Recording => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Recording;

                public byte BinaryProfile => ObservedCodec.BinaryProfile;

                public static Action Observe;
                public LuminPackStringEncoding StringEncoding
                {
                    get
                    {
                        var observer = Observe;
                        Observe = null;
                        observer?.Invoke();
                        return LuminPackStringEncoding.UTF16;
                    }
                }
                public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
                public StandardFormat StandardFormat => new('G');
                public int MaxJsonDepth => 1024;
                public bool ValidateJsonSyntax => false;
            }

            public static class Entry
            {
                public static void Check()
                {
                    var tag = Guid.Parse("e186a0d6-8d67-4ddb-ab14-05f84307de98");
                    var record = new FixedRecord { Number = 17, Position = new Vector3(1, -2, 3), Ticks = -27, Tag = tag };
                    byte[] bytes = LuminPackSerializer.Serialize(record);
                    Span<byte> expected = stackalloc byte[41];
                    expected[0] = 4;
                    BinaryPrimitives.WriteInt32LittleEndian(expected[1..], 17);
                    BinaryPrimitives.WriteSingleLittleEndian(expected[5..], 1);
                    BinaryPrimitives.WriteSingleLittleEndian(expected[9..], -2);
                    BinaryPrimitives.WriteSingleLittleEndian(expected[13..], 3);
                    BinaryPrimitives.WriteInt64LittleEndian(expected[17..], -27);
                    tag.TryWriteBytes(expected[25..]);
                    if (!bytes.AsSpan().SequenceEqual(expected) ||
                        LuminPackSerializer.Deserialize<FixedRecord>(bytes).Position != record.Position)
                        throw new Exception("Fixed record width or bytes changed.");
                    if (!LuminPackSerializer.Serialize<FixedRecord>(null).AsSpan().SequenceEqual(new byte[] { 255 }))
                        throw new Exception("The allocating fixed root changed the null marker.");

                    using var buffer = new LuminBufferWriter(true);
                    foreach (int length in new[] { 3, 4096 })
                    {
                        var items = new FixedRecord[length];
                        items[0] = record;
                        items[^1] = record;
                        buffer.Reset();
                        LuminPackSerializer.Serialize(items, buffer);
                        var encoded = buffer.GetSpan();
                        if (encoded.Length != length + 84 || encoded[45] != 255 || !encoded[4..45].SequenceEqual(expected) ||
                            !encoded[^41..].SequenceEqual(expected))
                            throw new Exception("Fixed array window or fallback changed the wire.");
                        if (!LuminPackSerializer.Serialize(items).AsSpan().SequenceEqual(encoded))
                            throw new Exception("The allocating array kept its excess null-element room or changed its bytes.");
                        FixedRecord[] copy = null;
                        LuminPackSerializer.Deserialize(encoded, ref copy);
                        if (copy[1] != null || copy[^1].Tag != tag)
                            throw new Exception("Fixed array null or non-null element changed.");
                    }
                    buffer.Reset();
                    var fixedItems = new[] { record, record, record };
                    LuminPackSerializer.Serialize(fixedItems, buffer);
                    if (buffer.CurrentIndex != 127 || !LuminPackSerializer.Serialize(fixedItems).AsSpan().SequenceEqual(buffer.GetSpan()))
                        throw new Exception("The exact allocating array changed the non-null element bytes.");

                    var observed = new ObservedRecord { Number = 31 };
                    var observedCopy = LuminPackSerializer.Deserialize<ObservedRecord>(LuminPackSerializer.Serialize(observed));
                    if (observedCopy.Number != 32 || observed.Number != 34)
                        throw new Exception("Fixed field optimization changed the auto-property callbacks.");

                    var version = new VersionRecord
                    {
                        Tag = tag,
                        Time = new DateTime(635821056000000000, DateTimeKind.Utc),
                        Numbers = [2, 3, 5],
                        Pairs = new() { [7] = 11, [13] = 17, [19] = 23 },
                        Text = new() { [29] = "Cat" },
                    };
                    version.Pairs.Remove(13);
                    bytes = LuminPackSerializer.Serialize(version);
                    const int descriptors = 11;
                    const int payload = descriptors + 5 * 8;
                    if (bytes[0] != 251 || bytes[1] != 0x42 || bytes[2] != 5 ||
                        bytes[descriptors + 7] != 16 || bytes[descriptors + 15] != 8 ||
                        bytes[descriptors + 23] != 255 || bytes[descriptors + 31] != 255 || bytes[descriptors + 39] != 255 ||
                        !bytes.AsSpan(payload, 16).SequenceEqual(tag.ToByteArray()) ||
                        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(payload + 24)) != 16 ||
                        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(payload + 44)) != 20)
                        throw new Exception("Versioned fixed or scoped member framing changed.");
                    var versionCopy = LuminPackSerializer.Deserialize<VersionRecord>(bytes);
                    if (versionCopy.Tag != tag || versionCopy.Time.ToBinary() != version.Time.ToBinary() ||
                        !versionCopy.Numbers.AsSpan().SequenceEqual(version.Numbers) || versionCopy.Pairs.Count != 2 ||
                        versionCopy.Pairs[19] != 23 || versionCopy.Text[29] != "Cat")
                        throw new Exception("Named raw or variable payload changed.");
                    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(payload + 24), 17);
                    try
                    {
                        _ = LuminPackSerializer.Deserialize<VersionRecord>(bytes);
                        throw new Exception("A mismatched scoped payload length was accepted.");
                    }
                    catch (FormatException) { }

                    var positional = new PositionalRecord { Text = "point", Positions = new() { Vector3.UnitX } };
                    ObservingProfile.Observe = () => positional.Positions.Add(Vector3.UnitY);
                    bytes = LuminPackSerializer.Serialize<PositionalRecord, ObservingProfile>(positional);
                    if (!bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(positional)))
                        throw new Exception("A positional span was captured before the custom string profile ran.");

                    ObservingProfile.Observe = () =>
                    {
                        version.Numbers = [7, 11];
                        version.Pairs.Add(31, 37);
                    };
                    bytes = LuminPackSerializer.Serialize<VersionRecord, ObservingProfile>(version);
                    if (!bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(version)))
                        throw new Exception("A versioned collection was captured before the custom schema profile ran.");
                }
            }
            """;
        Execute(references, source, "FixedRecordReservations", result =>
        {
            string[] files = result.Results.SelectMany(item => item.GeneratedSources)
                .Select(item => item.SourceText.ToString()).ToArray();
            if (!files.Any(text => text.Contains("struct FixedRecordFormatter", StringComparison.Ordinal) &&
                    text.Contains("ReserveRecord(ref cursor", StringComparison.Ordinal)) ||
                !files.Any(text => text.Contains("struct FixedRecordArrayFormatter", StringComparison.Ordinal) &&
                    text.Contains("RecordRoom(ref cursor)", StringComparison.Ordinal)))
                throw new InvalidOperationException("The fixed object or array lost its shared capacity check.");
            string versioned = files.Single(text => text.Contains("struct VersionRecordFormatter", StringComparison.Ordinal) &&
                text.Contains("WriteVersionRecord", StringComparison.Ordinal));
            if (!versioned.Contains("WriteVersionRecord", StringComparison.Ordinal) ||
                !versioned.Contains("WriteVersionPayload", StringComparison.Ordinal))
                throw new InvalidOperationException("The versioned formatter lost its outlined descriptor record write.");
            if (versioned.Split("scope = writer.EnterVersionScope();", StringSplitOptions.None).Length != 2)
                throw new InvalidOperationException("Only the profile-dependent string dictionary should open a writer schema scope.");
        });
        Console.WriteLine("Passed fixed class and array reservations, callbacks, profile observation order, and version descriptor widths and scoped lengths.");
    }
}
