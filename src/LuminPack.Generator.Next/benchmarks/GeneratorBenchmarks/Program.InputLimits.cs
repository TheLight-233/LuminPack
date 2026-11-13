using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Pipeline;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    /// <summary>Executes checked-copy contracts through trusted inline descendants under frozen root limits.</summary>
    /// <param name="references">The framework and runtime metadata.</param>
    /// <param name="parse">The consumer parse options.</param>
    /// <param name="coreDll">The runtime implementation used by the generated fixture.</param>
    /// <returns>Zero when every profile preserves checked reads, reuse and ordinary wire bytes.</returns>
    private static int VerifyInputLimits(MetadataReference[] references, CSharpParseOptions parse, string coreDll)
    {
        var tree = CSharpSyntaxTree.ParseText(InputLimitFixture, parse, "InputLimits.cs");
        var input = CSharpCompilation.Create("NextInputLimitContracts", new[] { tree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        foreach (GeneratorRunResult result in driver.GetRunResult().Results)
            if (result.Exception != null)
                return LuminPackExceptionHelper.ThrowInvalidOperation(result.Exception.ToString());
        foreach (Diagnostic diagnostic in diagnostics.Concat(output.GetDiagnostics()))
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                return LuminPackExceptionHelper.ThrowInvalidOperation(diagnostic.ToString());
        string generated = string.Concat(driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources)
            .Select(static file => file.SourceText.ToString()));
        if (!generated.Contains("LuminInputReads_", StringComparison.Ordinal))
            return LuminPackExceptionHelper.ThrowInvalidOperation("The fixture did not exercise a root-owned checked copy.");
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        if (!emitted.Success)
            return LuminPackExceptionHelper.ThrowInvalidOperation(string.Join(Environment.NewLine, emitted.Diagnostics));
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(coreDll));
        Assembly assembly = Assembly.Load(image.ToArray());
        int checks = (int)assembly.GetType("InputLimitFixture")!.GetMethod("Exercise")!.Invoke(null, null)!;
        foreach (string edited in new[]
        {
            InputLimitFixture.Replace("MaxStringLength = 4", "MaxStringLength = 1"),
            InputLimitFixture.Replace("MaxStringLength = 4, MaxCollectionLength = 4", "UntrustedData = false"),
            InputLimitFixture,
        })
        {
            var changed = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(edited, parse, "InputLimits.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(changed, out var retainedOutput, out var retainedDiagnostics);
            GeneratorDriver fresh = CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: parse)
                .RunGeneratorsAndUpdateCompilation(changed, out var freshOutput, out var freshDiagnostics);
            var actual = Sources(driver);
            var expected = Sources(fresh);
            RequireIncremental(actual.Count == expected.Count, "Input-limit edits changed the retained file count.");
            foreach (var file in expected)
                RequireIncremental(actual.TryGetValue(file.Key, out string? text) && text == file.Value,
                    "Input-limit edits retained stale checked source: " + file.Key);
            RequireIncremental(DiagnosticSnapshot(retainedOutput, retainedDiagnostics) == DiagnosticSnapshot(freshOutput, freshDiagnostics),
                "Input-limit edits retained stale diagnostics.");
            foreach (Diagnostic diagnostic in retainedDiagnostics.Concat(retainedOutput.GetDiagnostics()))
                RequireIncremental(diagnostic.Severity != DiagnosticSeverity.Error, diagnostic.ToString());
        }
        Console.WriteLine($"Next input-limit contracts: {checks} checks passed across six string profiles.");
        Console.WriteLine("Changed, disabled and restored input limits match fresh-driver source and diagnostics.");
        return 0;
    }

    private const string InputLimitFixture = """
        using System;
        using System.IO;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class TrustedLeaf
        {
            public int Number;
            public string Text;
            public bool Flag;
            public bool[] Bits;
        }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class StandaloneFlag { public bool Flag; }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class TrustedMiddle { public TrustedLeaf Leaf; }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class TrustedContainer { public TrustedMiddle Middle; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public sealed class LimitedRoot { public TrustedContainer Child { get; set; } }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public sealed class LimitedListRoot { public System.Collections.Generic.List<TrustedContainer> Children; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public sealed class LimitedArrayRoot { public TrustedContainer[] Children; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public sealed class LimitedDictionaryRoot { public System.Collections.Generic.Dictionary<int, TrustedContainer> Children; }
        [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(MaxStringLength = 4)]
        public partial class LimitedVersion { public string Text { get; set; } }
        [LuminPackable(GeneratorType.VersionTolerant)]
        public partial class EarlierVersion { public string Text { get; set; } public int Removed; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public partial struct LimitedStruct { public string Text; public int[] Values; }
        [LuminPackable(GeneratorType.CircleReference), LuminPackOption(MaxStringLength = 4)]
        public partial class LimitedCircle { [LuminPackOrder(0)] public string Text { get; set; } }
        [LuminPackable(GeneratorType.CircleReference), LuminPackOption(MaxDepth = 2)]
        public partial class LimitedCycle { [LuminPackOrder(0)] public LimitedCycle Next; }
        [LuminPackable, LuminPackOption(MaxCollectionLength = 4)]
        public partial class LimitedCompressed { [LuminPackCompress] public int[] Values; }
        [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(MaxCollectionLength = 4)]
        public partial class LimitedVersionCompressed { [LuminPackCompress] public int[] Values; }
        [LuminPackable(GeneratorType.VersionTolerant)]
        public partial class EarlierVersionCompressed { [LuminPackCompress] public int[] Values; public int Removed; }
        [LuminPackable, LuminPackOption(MaxInputBytes = 5)]
        public partial class LimitedStream { public int Value; }
        [LuminPackable, LuminPackOption(UntrustedData = true)]
        public partial struct RejectedStruct { public object Value; }
        [LuminPackable, LuminPackUnion(0, typeof(RejectedCase)), LuminPackOption(UntrustedData = true)]
        public abstract partial class RejectedUnion { }
        [LuminPackable]
        public sealed partial class RejectedCase : RejectedUnion { public object Value; }
        [LuminPackable, LuminPackUnion(0, typeof(TrustedFlagCase)), LuminPackOption(UntrustedData = true)]
        public abstract partial class CheckedFlagUnion { }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed partial class TrustedFlagCase : CheckedFlagUnion { public bool Flag; }
        public static class InputLimitFixture
        {
            public static int Exercise() => Check<LuminPackUtf16LengthOptions>() + Check<LuminPackUtf8LengthOptions>() +
                Check<LuminPackUtf16VarIntOptions>() + Check<LuminPackUtf8VarIntOptions>() +
                Check<LuminPackUtf16TokenOptions>() + Check<LuminPackUtf8TokenOptions>() + CheckAdditionalRoots();

            private static int CheckAdditionalRoots()
            {
                var version = new LimitedVersion { Text = "ok" };
                var structure = new LimitedStruct { Text = "ok", Values = new[] { 1, 2 } };
                var circle = new LimitedCircle { Text = "ok" };
                var compressed = new LimitedCompressed { Values = new[] { 1, 2 } };
                var earlier = new EarlierVersion { Text = "old", Removed = 42 };
                if (LuminPackSerializer.Deserialize<LimitedVersion>(LuminPackSerializer.Serialize(version)).Text != "ok" ||
                    LuminPackSerializer.Deserialize<LimitedVersion>(LuminPackSerializer.Serialize(earlier)).Text != "old" ||
                    LuminPackSerializer.Deserialize<LimitedStruct>(LuminPackSerializer.Serialize(structure)).Values[1] != 2 ||
                    LuminPackSerializer.Deserialize<LimitedCircle>(LuminPackSerializer.Serialize(circle)).Text != "ok" ||
                    LuminPackSerializer.Deserialize<LimitedCompressed>(LuminPackSerializer.Serialize(compressed)).Values[1] != 2)
                    throw new Exception("A checked root changed valid wire reads.");

                version.Text = structure.Text = circle.Text = "oversized";
                compressed.Values = new int[5];
                byte[] versionBytes = LuminPackSerializer.Serialize(version);
                byte[] structBytes = LuminPackSerializer.Serialize(structure);
                byte[] circleBytes = LuminPackSerializer.Serialize(circle);
                byte[] compressedBytes = LuminPackSerializer.Serialize(compressed);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedVersion>(versionBytes));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedStruct>(structBytes));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedCircle>(circleBytes));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedCompressed>(compressedBytes));
                earlier.Text = "oversized";
                byte[] earlierBytes = LuminPackSerializer.Serialize(earlier);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedVersion>(earlierBytes));
                structure.Text = "ok";
                structure.Values = new int[5];
                byte[] oversizedStruct = LuminPackSerializer.Serialize(structure);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedStruct>(oversizedStruct));
                var earlierCompressed = new EarlierVersionCompressed { Values = new[] { 1, 2 }, Removed = 42 };
                if (LuminPackSerializer.Deserialize<LimitedVersionCompressed>(LuminPackSerializer.Serialize(earlierCompressed)).Values[1] != 2)
                    throw new Exception("The evolved schema changed a valid compressed read.");
                earlierCompressed.Values = new int[5];
                byte[] oversizedCompressed = LuminPackSerializer.Serialize(earlierCompressed);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedVersionCompressed>(oversizedCompressed));

                var cycle = new LimitedCycle { Next = new LimitedCycle() };
                cycle.Next.Next = cycle;
                var cycleCopy = LuminPackSerializer.Deserialize<LimitedCycle>(LuminPackSerializer.Serialize(cycle));
                if (!ReferenceEquals(cycleCopy, cycleCopy.Next.Next))
                    throw new Exception("A back reference incorrectly consumed another level of depth.");
                cycle.Next.Next = new LimitedCycle();
                byte[] deepCycle = LuminPackSerializer.Serialize(cycle);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedCycle>(deepCycle));

                byte[] bytes = LuminPackSerializer.Serialize(new LimitedStream { Value = 42 });
                using var exact = new MemoryStream(bytes);
                if (LuminPackSerializer.DeserializeAsync<LimitedStream>(exact).GetAwaiter().GetResult().Value != 42)
                    throw new Exception("The stream read rejected its exact limit.");
                Array.Resize(ref bytes, bytes.Length + 4);
                using var oversized = new MemoryStream(bytes);
                RejectRead(() => LuminPackSerializer.DeserializeAsync<LimitedStream>(oversized).GetAwaiter().GetResult());
                if (oversized.Position != 6)
                    throw new Exception("The stream reader consumed more than the limit and one lookahead byte.");
                var rejected = new RejectedStruct { Value = 42 };
                RejectedUnion rejectedCase = new RejectedCase { Value = 42 };
                RejectContract(() => LuminPackSerializer.Serialize(rejected));
                RejectContract(() => LuminPackSerializer.Deserialize<RejectedStruct>(new byte[] { 1 }));
                RejectContract(() => LuminPackSerializer.SerializeJson(rejected));
                RejectContract(() => LuminPackSerializer.DeserializeJson<RejectedStruct>("{}"));
                RejectContract(() => LuminPackSerializer.Serialize(rejectedCase));
                RejectContract(() => LuminPackSerializer.Deserialize<RejectedUnion>(new byte[] { 0 }));
                RejectContract(() => LuminPackSerializer.SerializeJson(rejectedCase));
                RejectContract(() => LuminPackSerializer.DeserializeJson<RejectedUnion>("{}"));
                CheckedFlagUnion flag = new TrustedFlagCase { Flag = true };
                byte[] flagBytes = LuminPackSerializer.Serialize(flag);
                if (!((TrustedFlagCase)LuminPackSerializer.Deserialize<CheckedFlagUnion>(flagBytes)).Flag)
                    throw new Exception("A checked fixed Union case changed its valid value.");
                flagBytes[flagBytes.Length - 1] = 2;
                RejectRead(() => LuminPackSerializer.Deserialize<CheckedFlagUnion>(flagBytes));
                return 28;
            }

            private static void RejectContract(Action action)
            {
                try { action(); }
                catch (NotSupportedException error) when (error.Message.Contains("untrusted-input option")) { return; }
                throw new Exception("An explicitly untrusted contract accepted an input-selected type.");
            }

            private static void RejectRead(Action read)
            {
                try { read(); }
                catch (InvalidDataException) { return; }
                throw new Exception("A root bypassed its declared input limit.");
            }

            private static int Check<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var value = new LimitedRoot { Child = NewContainer() };
                byte[] bytes = LuminPackSerializer.Serialize<LimitedRoot, TProfile>(value);
                byte[] trusted = LuminPackSerializer.Serialize<TrustedContainer, TProfile>(value.Child);
                if (!bytes.AsSpan(1).SequenceEqual(trusted))
                    throw new Exception("Checked copies changed ordinary wire bytes.");
                LimitedRoot copy = null;
                int consumed = LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref copy);
                if (consumed != bytes.Length || copy.Child.Middle.Leaf.Number != 17 || !copy.Child.Middle.Leaf.Flag || copy.Child.Middle.Leaf.Text != "ok" ||
                    copy.Child.Middle.Leaf.Bits.Length != 2 || copy.Child.Middle.Leaf.Bits[0])
                    throw new Exception("The checked copy changed a valid read.");
                var container = copy.Child;
                var middle = container.Middle;
                var leaf = middle.Leaf;
                var text = leaf.Text;
                LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref copy);
                if (!ReferenceEquals(container, copy.Child) || !ReferenceEquals(middle, copy.Child.Middle) ||
                    !ReferenceEquals(leaf, copy.Child.Middle.Leaf) || !ReferenceEquals(text, copy.Child.Middle.Leaf.Text))
                    throw new Exception("The checked copy lost object or equal-string reuse.");
                for (int iteration = 0; iteration < 16; iteration++)
                    LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref copy);
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int iteration = 0; iteration < 128; iteration++)
                    LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref copy);
                if (GC.GetAllocatedBytesForCurrentThread() != allocated)
                    throw new Exception("A warmed checked-copy read allocated while reusing its complete target.");
                foreach (int offset in new[] { 2, 3, bytes.Length - 7, bytes.Length - 1 })
                {
                    byte[] forged = (byte[])bytes.Clone();
                    forged[offset] = offset < 4 ? (byte)0 : (byte)2;
                    Reject<TProfile>(forged, "A trusted inline descendant bypassed a checked-copy marker or Boolean domain at " + offset);
                }
                var current = value.Child.Middle.Leaf;
                current.Text = "oversized";
                Reject<TProfile>(LuminPackSerializer.Serialize<LimitedRoot, TProfile>(value), "An inline string bypassed its root limit.");
                current.Text = "ok";
                current.Bits = new bool[5];
                Reject<TProfile>(LuminPackSerializer.Serialize<LimitedRoot, TProfile>(value), "An inline array bypassed its root limit.");
                current.Bits = new[] { false, true };
                value.Child.Middle = null;
                LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(LuminPackSerializer.Serialize<LimitedRoot, TProfile>(value), ref copy);
                if (copy.Child.Middle != null)
                    throw new Exception("A null inline record did not clear reused storage.");
                byte[] uncheckedBytes = (byte[])trusted.Clone();
                uncheckedBytes[uncheckedBytes.Length - 7] = 2;
                uncheckedBytes[uncheckedBytes.Length - 1] = 2;
                TrustedContainer uncheckedCopy = null;
                LuminPackSerializer.Deserialize<TrustedContainer, TProfile>(uncheckedBytes, ref uncheckedCopy);
                byte[] flagBytes = LuminPackSerializer.Serialize<StandaloneFlag, TProfile>(new StandaloneFlag { Flag = true });
                StandaloneFlag flagCopy = null;
                if (LuminPackSerializer.Deserialize<StandaloneFlag, TProfile>(flagBytes, ref flagCopy) != flagBytes.Length || !flagCopy.Flag)
                    throw new Exception("The trusted scalar reader did not advance through its Boolean.");
                return 13 + CheckCollections<TProfile>();
            }

            private static TrustedContainer NewContainer() => new TrustedContainer { Middle = new TrustedMiddle { Leaf =
                new TrustedLeaf { Number = 17, Text = "ok", Flag = true, Bits = new[] { false, true } } } };

            private static int CheckCollections<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var child = NewContainer();
                var list = new LimitedListRoot { Children = new System.Collections.Generic.List<TrustedContainer> { child } };
                var array = new LimitedArrayRoot { Children = new[] { child } };
                var dictionary = new LimitedDictionaryRoot { Children = new System.Collections.Generic.Dictionary<int, TrustedContainer> { [17] = child } };
                byte[] listBytes = LuminPackSerializer.Serialize<LimitedListRoot, TProfile>(list);
                byte[] arrayBytes = LuminPackSerializer.Serialize<LimitedArrayRoot, TProfile>(array);
                byte[] dictionaryBytes = LuminPackSerializer.Serialize<LimitedDictionaryRoot, TProfile>(dictionary);
                LimitedListRoot listCopy = null;
                LimitedArrayRoot arrayCopy = null;
                LimitedDictionaryRoot dictionaryCopy = null;
                if (LuminPackSerializer.Deserialize<LimitedListRoot, TProfile>(listBytes, ref listCopy) != listBytes.Length || !listCopy.Children[0].Middle.Leaf.Flag)
                    throw new Exception("A checked list changed a valid element read.");
                if (LuminPackSerializer.Deserialize<LimitedArrayRoot, TProfile>(arrayBytes, ref arrayCopy) != arrayBytes.Length || !arrayCopy.Children[0].Middle.Leaf.Flag)
                    throw new Exception("A checked array changed a valid element read.");
                if (LuminPackSerializer.Deserialize<LimitedDictionaryRoot, TProfile>(dictionaryBytes, ref dictionaryCopy) != dictionaryBytes.Length || !dictionaryCopy.Children[17].Middle.Leaf.Flag)
                    throw new Exception("A checked dictionary changed a valid element read.");
                var existingList = listCopy.Children;
                var existingArray = arrayCopy.Children;
                var existingDictionary = dictionaryCopy.Children;
                LuminPackSerializer.Deserialize<LimitedListRoot, TProfile>(listBytes, ref listCopy);
                LuminPackSerializer.Deserialize<LimitedArrayRoot, TProfile>(arrayBytes, ref arrayCopy);
                LuminPackSerializer.Deserialize<LimitedDictionaryRoot, TProfile>(dictionaryBytes, ref dictionaryCopy);
                if (!ReferenceEquals(existingList, listCopy.Children) || !ReferenceEquals(existingArray, arrayCopy.Children) ||
                    !ReferenceEquals(existingDictionary, dictionaryCopy.Children))
                    throw new Exception("Checked collection copies lost existing container reuse.");
                byte[][] payloads = new[] { listBytes, arrayBytes, dictionaryBytes };
                for (int kind = 0; kind < payloads.Length; kind++)
                {
                    byte[] payload = payloads[kind];
                    foreach (int offset in new[] { kind == 2 ? 11 : 7, payload.Length - 7, payload.Length - 1 })
                    {
                        byte[] forged = (byte[])payload.Clone();
                        forged[offset] = offset < 12 ? (byte)0 : (byte)2;
                        RejectCollection<TProfile>(forged, kind);
                    }
                }
                child.Middle.Leaf.Text = "oversized";
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedListRoot, TProfile>(list), 0);
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedArrayRoot, TProfile>(array), 1);
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedDictionaryRoot, TProfile>(dictionary), 2);
                child.Middle.Leaf.Text = "ok";
                child.Middle.Leaf.Bits = new bool[5];
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedListRoot, TProfile>(list), 0);
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedArrayRoot, TProfile>(array), 1);
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedDictionaryRoot, TProfile>(dictionary), 2);
                list.Children[0] = null;
                array.Children[0] = null;
                dictionary.Children[17] = null;
                LuminPackSerializer.Deserialize<LimitedListRoot, TProfile>(LuminPackSerializer.Serialize<LimitedListRoot, TProfile>(list), ref listCopy);
                LuminPackSerializer.Deserialize<LimitedArrayRoot, TProfile>(LuminPackSerializer.Serialize<LimitedArrayRoot, TProfile>(array), ref arrayCopy);
                LuminPackSerializer.Deserialize<LimitedDictionaryRoot, TProfile>(LuminPackSerializer.Serialize<LimitedDictionaryRoot, TProfile>(dictionary), ref dictionaryCopy);
                if (listCopy.Children[0] != null || arrayCopy.Children[0] != null || dictionaryCopy.Children[17] != null)
                    throw new Exception("A checked collection did not clear a reused null element.");
                return 20;
            }

            private static void RejectCollection<TProfile>(byte[] bytes, int kind) where TProfile : struct, ILuminPackOptions
            {
                try
                {
                    if (kind == 0)
                    {
                        LimitedListRoot list = null;
                        LuminPackSerializer.Deserialize<LimitedListRoot, TProfile>(bytes, ref list);
                    }
                    else if (kind == 1)
                    {
                        LimitedArrayRoot array = null;
                        LuminPackSerializer.Deserialize<LimitedArrayRoot, TProfile>(bytes, ref array);
                    }
                    else
                    {
                        LimitedDictionaryRoot dictionary = null;
                        LuminPackSerializer.Deserialize<LimitedDictionaryRoot, TProfile>(bytes, ref dictionary);
                    }
                }
                catch (InvalidDataException) { return; }
                throw new Exception("A shared collection owner bypassed a checked inline descendant for kind " + kind);
            }

            private static void Reject<TProfile>(byte[] bytes, string message) where TProfile : struct, ILuminPackOptions
            {
                LimitedRoot target = null;
                try { LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref target); }
                catch (InvalidDataException) { return; }
                throw new Exception(message);
            }
        }
        """;
}
