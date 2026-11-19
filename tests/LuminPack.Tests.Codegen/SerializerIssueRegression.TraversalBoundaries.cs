using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerIssueRegression
{
    /// <summary>Checks that compound collection loops forward the original state and only compound nested members are outlined.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunTraversalBoundaries(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Concurrent;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            using LuminPack.Utility;
            [LuminPackable] public sealed class FlatTraversalChild { public int Number; public string Text; }
            [LuminPackable] public sealed class CompoundTraversalChild { public List<string> Items; }
            [LuminPackable] public sealed class DenseTraversalBody
            {
                public int Number;
                public string Text;
                public long Extra;
                public List<int> Items;
                public int Third;
                public string More;
            }
            [LuminPackable] public sealed class WideDenseTraversalBody
            {
                public int First;
                public long Second;
                public int Third;
                public string Text;
                public string More;
                public List<int> Items;
            }
            [LuminPackable, LuminPackUnion(1, typeof(DenseTraversalCase))] public abstract partial class DenseTraversalUnion { }
            [LuminPackable] public sealed partial class DenseTraversalCase : DenseTraversalUnion
            {
                public string Text;
                public List<int> Items;
            }
            [LuminPackable] public sealed class ScalarPrefix { public short Id; public List<TraversalParent> Items; }
            [LuminPackable] public sealed class FieldRunPrefix { public int A; public int B; public List<TraversalParent> Items; }
            public readonly struct ConcurrentUserProfile : ILuminPackOptions
            {
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

                public byte BinaryProfile => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).BinaryProfile;

                public static int EncodingReads;
                public LuminPackStringEncoding StringEncoding { get { EncodingReads++; return LuminPackStringEncoding.UTF16; } }
                public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
                public System.Buffers.StandardFormat StandardFormat => new('G');
                public bool ValidateJsonSyntax => true;
                public int MaxJsonDepth => 32;
            }
            public sealed class DerivedHashSet : HashSet<int> { }
            [LuminPackable] public sealed class TraversalParent
            {
                public FlatTraversalChild Flat;
                public CompoundTraversalChild Compound;
            }
            public static class Entry
            {
                public static void Check()
                {
                    List<TraversalParent> source = new()
                    {
                        new() { Flat = new() { Number = 17, Text = "text" }, Compound = new() { Items = new() { "two", "five" } } },
                        new() { Flat = null, Compound = null },
                    };
                    byte[] bytes = LuminPackSerializer.Serialize(source);
                    var scalar = new ScalarPrefix { Id = 0x1234, Items = source };
                    var scalarBytes = LuminPackSerializer.Serialize(scalar);
                    ScalarPrefix scalarCopy = null;
                    if (scalarBytes[0] != 2 || scalarBytes[1] != 0x34 || scalarBytes[2] != 0x12 ||
                        LuminPackSerializer.Deserialize(scalarBytes, ref scalarCopy) != scalarBytes.Length ||
                        scalarCopy.Id != scalar.Id || scalarCopy.Items.Count != 2)
                        throw new Exception("The first scalar field or the collection following it lost its wire position.");
                    _ = LuminPackSerializer.Serialize(new FieldRunPrefix { A = 1, B = 2, Items = source });
                    List<TraversalParent> copy = null;
                    for (int i = 0; i < 2; ++i)
                    {
                        if (LuminPackSerializer.Deserialize(bytes, ref copy) != bytes.Length ||
                            copy is not [{ Flat: { Number: 17, Text: "text" }, Compound.Items: ["two", "five"] }, { Flat: null, Compound: null }])
                            throw new Exception("Compound collection traversal lost framing or reused member storage.");
                    }
                    var set = new HashSet<int> { 3, 9 };
                    string setJson = LuminPackSerializer.SerializeJson(set);
                    LuminPackSerializer.DeserializeJson(setJson, ref set);
                    Queue<long> queue = null;
                    LuminPackSerializer.DeserializeJson("[9,3000000000]", ref queue);
                    HashSet<string> text = null;
                    LuminPackSerializer.DeserializeJson("[\"text\"]", ref text);
                    if (!set.SetEquals([3, 9]) || queue.Dequeue() != 9 || queue.Dequeue() != 3000000000L || !text.SetEquals(["text"]))
                        throw new Exception("The tiered integer read or the text control changed its values.");

                    CheckConcurrentPairs<LuminPackUtf16LengthOptions>();
                    CheckConcurrentPairs<LuminPackUtf8LengthOptions>();
                    CheckConcurrentIntegerKeys<LuminPackUtf16LengthOptions>();
                    CheckConcurrentIntegerKeys<LuminPackUtf8LengthOptions>();
                    CheckConcurrentIntegerKeys<ConcurrentUserProfile>();
                    _ = LuminPackSerializer.Serialize(new ConcurrentDictionary<bool, string> { [false] = "bool" });
                    CheckJsonHashTraversal();
                    CheckDenseDictionary();
                }

                private static void CheckDenseDictionary()
                {
                    var exact = new Dictionary<string, DenseTraversalBody>
                    {
                        ["key"] = new() { Number = 12, Extra = 100, Third = 3, Text = new string('\u96ea', 1600), More = "mixed", Items = new() { 9, 10 } },
                        ["removed"] = new(),
                        ["null"] = null,
                    };
                    IReadOnlyDictionary<string, DenseTraversalBody> view = exact;
                    RequireSameBytes();
                    exact.Remove("removed");
                    RequireSameBytes();
                    IReadOnlyDictionary<string, FlatTraversalChild> compact = new Dictionary<string, FlatTraversalChild> { ["x"] = new() { Number = 1, Text = "y" } };
                    var wide = new Dictionary<string, WideDenseTraversalBody> { ["x"] = new() { First = 1, Second = 2, Third = 3, Text = "a", More = "b", Items = new() { 7 } } };
                    IReadOnlyDictionary<string, WideDenseTraversalBody> wideView = wide;
                    IReadOnlyDictionary<string, DenseTraversalUnion> tagged = new Dictionary<string, DenseTraversalUnion> { ["x"] = new DenseTraversalCase { Text = "case", Items = new() { 5 } } };
                    _ = LuminPackSerializer.Serialize(compact);
                    if (!LuminPackSerializer.Serialize(wideView).AsSpan().SequenceEqual(LuminPackSerializer.Serialize(wide)))
                        throw new Exception("A wide dictionary value changed the binary dictionary representation.");
                    byte[] wire = LuminPackSerializer.Serialize(tagged);
                    IReadOnlyDictionary<string, DenseTraversalUnion> decoded = null;
                    if (LuminPackSerializer.Deserialize(wire, ref decoded) != wire.Length || decoded["x"] is not DenseTraversalCase { Text: "case", Items: [5] })
                        throw new Exception("The Union dictionary lost its original case framing.");

                    void RequireSameBytes()
                    {
                        byte[] wire = LuminPackSerializer.Serialize(view);
                        if (!wire.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(exact)))
                            throw new Exception("Dense or sparse interface traversal changed the binary dictionary wire.");
                        IReadOnlyDictionary<string, DenseTraversalBody> decoded = null;
                        if (LuminPackSerializer.Deserialize(wire, ref decoded) != wire.Length ||
                            decoded["key"] is not { Number: 12, Extra: 100, Third: 3, More: "mixed", Items: [9, 10] } || decoded["null"] != null)
                            throw new Exception("A dictionary value call boundary lost the original record payload.");
                    }
                }

                private static void CheckConcurrentPairs<TProfile>() where TProfile : struct, ILuminPackOptions
                {
                    var source = new ConcurrentDictionary<string, string>();
                    for (int i = 0; i < 80; i++)
                        source["key" + i] = (i % 4) switch
                        {
                            0 => null,
                            1 => "",
                            2 => new string('a', 3900),
                            _ => new string('\u96ea', 1600),
                        };
                    using var expectedBuffer = new LuminBufferWriter(true);
                    var writer = new LuminPackWriter(expectedBuffer);
                    int header = writer.BeginCollection();
                    foreach (var pair in source)
                    {
                        writer.CheckBuffer();
                        writer.Write<TProfile>(pair.Key);
                        writer.CheckBuffer();
                        writer.Write<TProfile>(pair.Value);
                    }
                    writer.EndCollection(header, source.Count);
                    using var actual = new LuminBufferWriter(true);
                    LuminPackSerializer.Serialize<ConcurrentDictionary<string, string>, TProfile>(source, actual);
                    if (!actual.GetSpan().SequenceEqual(writer.GetSpan()))
                        throw new Exception("Concurrent string pairs changed bytes across output windows.");
                }

                private static void CheckJsonHashTraversal()
                {
                    var sparse = new HashSet<int> { 1, 2, 3, 4 };
                    sparse.Remove(2);
                    HashSet<int> derived = new DerivedHashSet { 4, 1, 9 };
                    foreach (var values in new[] { sparse, derived, new HashSet<int>() })
                    {
                        string expected = "[" + string.Join(",", values) + "]";
                        if (LuminPackSerializer.SerializeJson(values) != expected)
                            throw new Exception("HashSet JSON differs from framework enumeration.");
                    }
                }
                private static void CheckConcurrentIntegerKeys<TProfile>() where TProfile : struct, ILuminPackOptions
                {
                    var source = new ConcurrentDictionary<int, string>();
                    for (int index = 0; index < 48; index++)
                        source[index - 24] = (index % 4) switch
                        {
                            0 => null,
                            1 => "",
                            2 => new string('a', 3100),
                            _ => new string('\u96ea', 1700),
                        };
                    using var expected = new LuminBufferWriter(true);
                    var writer = new LuminPackWriter(expected);
                    int header = writer.BeginCollection();
                    foreach (var pair in source)
                    {
                        writer.CheckBuffer();
                        writer.EnsureAdditionalCapacity(sizeof(int));
                        writer.Write(pair.Key);
                        writer.CheckBuffer();
                        writer.Write<TProfile>(pair.Value);
                    }
                    writer.EndCollection(header, source.Count);
                    using var actual = new LuminBufferWriter(true);
                    ConcurrentUserProfile.EncodingReads = 0;
                    LuminPackSerializer.Serialize<ConcurrentDictionary<int, string>, TProfile>(source, actual);
                    if (!actual.GetSpan().SequenceEqual(writer.GetSpan()))
                        throw new Exception("Concurrent integer keys changed the paired record across writer growth.");
                    if (typeof(TProfile) == typeof(ConcurrentUserProfile) && ConcurrentUserProfile.EncodingReads == 0)
                        throw new Exception("A user profile with the same encoding bypassed its formatter.");
                }
            }
            """;
        Execute(references, source, "TraversalBoundaries", Inspect);
        Console.WriteLine("Passed compact and compound nested member boundaries, collection loop shapes, and fresh and reused reads.");

        static void Inspect(GeneratorDriverRunResult result)
        {
            var owners = result.GeneratedTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()).ToArray();
            foreach (var expected in new[] { ("ScalarPrefixFormatter", true), ("FieldRunPrefixFormatter", false) })
            {
                var body = owners.Where(owner => owner.Identifier.ValueText == expected.Item1).SelectMany(owner => owner.Members)
                    .OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "Write" && method.Body != null);
                if (body.ToString().Contains("writer.WriteObjectHeader(", StringComparison.Ordinal) != expected.Item2)
                    throw new InvalidOperationException("Unexpected scalar header write for " + expected.Item1 + Environment.NewLine + body);
            }
            var parent = owners.Single(owner => owner.Identifier.ValueText == "TraversalParentFormatter" &&
                owner.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == "Write" && method.Body != null));
            foreach (string direction in new[] { "Write", "Read" })
            {
                var operation = parent.Members.OfType<MethodDeclarationSyntax>()
                    .Single(method => method.Identifier.ValueText == direction && method.Body != null);
                var helpers = operation.DescendantNodes().OfType<LocalFunctionStatementSyntax>()
                    .Where(method => method.Identifier.ValueText.StartsWith(direction + "Nested", StringComparison.Ordinal)).ToArray();
                if (helpers.Length != 1 || !helpers[0].ToString().Contains("global::CompoundTraversalChild", StringComparison.Ordinal) ||
                    !helpers[0].AttributeLists.ToString().Contains("NoInlining", StringComparison.Ordinal))
                    throw new InvalidOperationException(direction + " outlined the wrong nested member: " +
                        string.Join(", ", helpers.Select(helper => helper.Identifier.ValueText + helper.ParameterList)) + Environment.NewLine + operation);
            }

            var collection = owners.Single(owner =>
                (owner.Identifier.ValueText == "ListOfTraversalParentFormatter" ||
                    owner.Identifier.ValueText.StartsWith("ListOfElementFormatter", StringComparison.Ordinal)) &&
                owner.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == "Write" && method.Body != null));
            var write = collection.Members.OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "Write" && method.Body != null);
            var read = collection.Members.OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "Read" && method.Body != null);
            if (write.ToString().Contains("loopWriter", StringComparison.Ordinal) || write.DescendantNodes().OfType<TryStatementSyntax>().Any() ||
                read.ToString().Contains("loopReader", StringComparison.Ordinal))
                throw new InvalidOperationException("A compound collection staged the formatter's operation state.");

            var concurrentWrite = owners.Single(owner => owner.Identifier.ValueText == "ConcurrentDictionaryOfStringAndStringFormatter")
                .Members.OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "Write" && method.Body != null);
            string concurrentBody = concurrentWrite.ToString();
            if (!concurrentBody.Contains("EnumerateConcurrentDictionary", StringComparison.Ordinal) ||
                !concurrentBody.Contains("CommitRecords(ref cursor)", StringComparison.Ordinal) ||
                !concurrentBody.Contains("WritePairSlow", StringComparison.Ordinal))
                throw new InvalidOperationException("The concurrent text writer lost its bounded traversal or growth fallback.");

            foreach (var expected in new[] { ("IReadOnlyDictionaryOfStringAndDenseTraversalBodyFormatter", true),
                ("IReadOnlyDictionaryOfStringAndFlatTraversalChildFormatter", false),
                ("IReadOnlyDictionaryOfStringAndWideDenseTraversalBodyFormatter", false),
                ("IReadOnlyDictionaryOfStringAndDenseTraversalUnionFormatter", false) })
            {
                var methods = owners.Where(owner => owner.Identifier.ValueText == expected.Item1).SelectMany(owner => owner.Members)
                    .OfType<MethodDeclarationSyntax>().Where(method => method.Identifier.ValueText == "Write" && method.Body != null).ToArray();
                if (methods.Length != 1)
                    throw new InvalidOperationException("Expected the binary formatter for " + expected.Item1);
                var boundary = methods[0].DescendantNodes().OfType<LocalFunctionStatementSyntax>()
                    .SingleOrDefault(method => method.Identifier.ValueText == "WriteDenseValue");
                if ((boundary != null) != expected.Item2 || boundary != null &&
                    !boundary.AttributeLists.ToString().Contains("NoInlining", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unexpected dense value boundary for " + expected.Item1);
            }

            foreach (var expected in new[] { ("ConcurrentDictionaryOfIntAndStringFormatter", true),
                ("ConcurrentDictionaryOfBoolAndStringFormatter", false) })
            {
                string body = owners.Where(owner => owner.Identifier.ValueText == expected.Item1).SelectMany(owner => owner.Members)
                    .OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "Write" && method.Body != null).ToString();
                if (body.Contains("WriteReservedRecordWidth<TLuminProfile>(ref text", StringComparison.Ordinal) != expected.Item2 ||
                    expected.Item2 && (!body.Contains("CommitRecords(ref cursor)", StringComparison.Ordinal) ||
                        !body.Contains("EnumerateConcurrentDictionary", StringComparison.Ordinal) ||
                        !body.Contains("WritePairSlow", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Unexpected concurrent integer/string pair boundary in " + expected.Item1);
            }

            CheckJsonTier("HashSetOfIntFormatter", "ReadJson", fullOptimization: false);
            CheckJsonTier("QueueOfLongFormatter", "ReadJson", fullOptimization: false);
            CheckJsonTier("HashSetOfIntFormatter", "WriteJson", fullOptimization: true);
            CheckJsonTier("HashSetOfStringFormatter", "ReadJson", fullOptimization: true);
            var hashWrite = owners.Single(owner => owner.Identifier.ValueText == "HashSetOfIntFormatter")
                .Members.OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "WriteJson" && method.Body != null);
            if (!hashWrite.ToString().Contains("TryGetHashSetTraversal", StringComparison.Ordinal) ||
                !hashWrite.ToString().Contains("version != view._version", StringComparison.Ordinal))
                throw new InvalidOperationException("The JSON HashSet writer lost its checked traversal.");

            void CheckJsonTier(string formatter, string operation, bool fullOptimization)
            {
                var method = owners.Where(owner => owner.Identifier.ValueText == formatter).SelectMany(owner => owner.Members)
                    .OfType<MethodDeclarationSyntax>().Single(item => item.Identifier.ValueText == operation && item.Body != null);
                string attributes = method.AttributeLists.ToString();
                if (!attributes.Contains("NoInlining", StringComparison.Ordinal) ||
                    attributes.Contains("AggressiveOptimization", StringComparison.Ordinal) != fullOptimization)
                    throw new InvalidOperationException($"Unexpected JSON tiering for {formatter}.{operation}.");
            }
        }
    }
}
