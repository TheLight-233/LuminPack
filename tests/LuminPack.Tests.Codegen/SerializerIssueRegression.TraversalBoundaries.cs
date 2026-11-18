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
            [LuminPackable] public sealed class ScalarPrefix { public short Id; public List<TraversalParent> Items; }
            [LuminPackable] public sealed class FieldRunPrefix { public int A; public int B; public List<TraversalParent> Items; }
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
                    CheckJsonHashTraversal();
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
