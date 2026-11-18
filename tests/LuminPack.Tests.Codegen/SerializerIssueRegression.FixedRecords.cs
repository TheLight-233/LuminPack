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

                    using var buffer = new LuminBufferWriter(true);
                    foreach (int length in new[] { 3, 4096 })
                    {
                        var items = new FixedRecord[length];
                        items[0] = record;
                        items[^1] = record;
                        buffer.ResetForReuse();
                        LuminPackSerializer.Serialize(items, buffer);
                        var encoded = buffer.GetSpan();
                        if (encoded.Length != length + 84 || encoded[45] != 255 || !encoded[4..45].SequenceEqual(expected) ||
                            !encoded[^41..].SequenceEqual(expected))
                            throw new Exception("Fixed array window or fallback changed the wire.");
                        FixedRecord[] copy = null;
                        LuminPackSerializer.Deserialize(encoded, ref copy);
                        if (copy[1] != null || copy[^1].Tag != tag)
                            throw new Exception("Fixed array null or non-null element changed.");
                    }

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
                    int tagStart = bytes.AsSpan().IndexOf(tag.ToByteArray());
                    if (tagStart < 4 || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(tagStart - 4)) != 16 ||
                        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(tagStart + 16)) != 8 ||
                        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(tagStart + 28)) != 16 ||
                        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(tagStart + 48)) != 20)
                        throw new Exception("Revision-2 member length prefixes changed.");
                    var versionCopy = LuminPackSerializer.Deserialize<VersionRecord>(bytes);
                    if (versionCopy.Tag != tag || versionCopy.Time.ToBinary() != version.Time.ToBinary() ||
                        !versionCopy.Numbers.AsSpan().SequenceEqual(version.Numbers) || versionCopy.Pairs.Count != 2 ||
                        versionCopy.Pairs[19] != 23 || versionCopy.Text[29] != "Cat")
                        throw new Exception("Named raw or variable payload changed.");
                    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(tagStart - 4), 17);
                    try
                    {
                        _ = LuminPackSerializer.Deserialize<VersionRecord>(bytes);
                        throw new Exception("A mismatched fixed-size payload was accepted.");
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
            string version = files.Single(text => text.Contains("struct VersionRecordFormatter", StringComparison.Ordinal) &&
                text.Contains(".CompleteVersionLength(", StringComparison.Ordinal));
            string reserved = version[..version.IndexOf("static void WriteWithProfile(", StringComparison.Ordinal)];
            if (!reserved.Contains(".WriteReserved<global::System.Guid>", StringComparison.Ordinal) ||
                !reserved.Contains(".WriteReservedSpanRecord(", StringComparison.Ordinal) ||
                !reserved.Contains(".WriteReserved(ref cursor, pairsSnapshot)", StringComparison.Ordinal) ||
                reserved.IndexOf(".CompleteVersionLength(", StringComparison.Ordinal) !=
                reserved.LastIndexOf(".CompleteVersionLength(", StringComparison.Ordinal))
                throw new InvalidOperationException("A known-width named payload took the variable field write path.");
        });
        Console.WriteLine("Passed fixed class and array reservations, callbacks, profile observation order, and revision-2 field lengths.");
    }
}
