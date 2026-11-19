extern alias ModelBasedAnalyzer;

using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NextGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks portable tuple storage, direct field addressing and alias-only incremental edits.</summary>
internal static class SerializerPortableTupleRegression
{
    /// <summary>Checks Full/Minimal generation and modern/compatibility cursor targets.</summary>
    /// <param name="references">Current runtime and framework metadata.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Minimal" })
            foreach (bool modern in new[] { true, false }) Verify(references, mode, modern);
        VerifyRecursivePacked(references);
    }

    /// <summary>Checks an explicitly packed field governs enclosing unmanaged storage under the default Tagged policy.</summary>
    /// <param name="references">Current runtime and framework metadata.</param>
    private static void VerifyRecursivePacked(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Buffers.Binary;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using LuminPack;
            using LuminPack.Attribute;
            namespace RecursivePackedStorage;
            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
            public struct Cell { public byte Tag; public long Number; }
            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Tagged)]
            public struct TaggedParent { public int Prefix; public Cell Item; }
            [LuminPackable]
            public struct Grandparent { public byte Prefix; public TaggedParent Parent; }
            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
            public struct NativeParent { public int Prefix; public Cell Item; }
            [LuminPackable]
            public struct TaggedControl { public byte Tag; public long Number; }
            [LuminPackable]
            public struct StableTagged { public byte Tag; public int Number; }
            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
            public struct StablePacked { public byte Tag; public int Number; }
            [LuminPackable]
            public sealed class MemberOwners
            {
                public (int, Cell)[] Tuples = null!;
                public List<KeyValuePair<int, Cell>> Pairs = null!;
            }
            public static class Entry
            {
                public static void Run()
                {
                    Cell cell = default;
                    Unsafe.InitBlockUnaligned(ref Unsafe.As<Cell, byte>(ref cell), 0xCC, (uint)Unsafe.SizeOf<Cell>());
                    cell.Tag = 7;
                    cell.Number = 0x0102030405060708;
                    int key = 41;
                    var pair = KeyValuePair.Create(key, cell);
                    var tuple = (key, cell);
                    var tagged = new TaggedParent { Prefix = key, Item = cell };
                    var grandparent = new Grandparent { Prefix = 9, Parent = tagged };
                    byte[] expected = new byte[17];
                    BinaryPrimitives.WriteInt32LittleEndian(expected, 1);
                    BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), key);
                    expected[8] = cell.Tag;
                    BinaryPrimitives.WriteInt64LittleEndian(expected.AsSpan(9), cell.Number);
                    byte[] pairBytes = LuminPackSerializer.Serialize(new[] { pair });
                    byte[] tupleBytes = LuminPackSerializer.Serialize(new[] { tuple });
                    byte[] listBytes = LuminPackSerializer.Serialize(new List<KeyValuePair<int, Cell>> { pair });
                    byte[] taggedBytes = LuminPackSerializer.Serialize(new[] { tagged });
                    Check(pairBytes.AsSpan().SequenceEqual(expected) && tupleBytes.AsSpan().SequenceEqual(expected) &&
                        listBytes.AsSpan().SequenceEqual(expected) && taggedBytes.AsSpan().SequenceEqual(expected),
                        $"Packed descendants did not produce {Convert.ToHexString(expected)}. Pair {Convert.ToHexString(pairBytes)}, " +
                        $"tuple {Convert.ToHexString(tupleBytes)}, list {Convert.ToHexString(listBytes)}, struct {Convert.ToHexString(taggedBytes)}.");
                    Check(LuminPackSerializer.Serialize(tuple).AsSpan().SequenceEqual(expected.AsSpan(4)) &&
                        LuminPackSerializer.Serialize(tagged).AsSpan().SequenceEqual(expected.AsSpan(4)), "Packed standalone bytes.");
                    var pairs = default(KeyValuePair<int, Cell>[]);
                    var tuples = default((int, Cell)[]);
                    var list = new List<KeyValuePair<int, Cell>> { default, default };
                    Check(LuminPackSerializer.Deserialize(pairBytes, ref pairs) == expected.Length && pairs![0].Key == key &&
                        pairs[0].Value.Number == cell.Number, "Packed pair read.");
                    Check(LuminPackSerializer.Deserialize(tupleBytes, ref tuples) == expected.Length && tuples![0].Item1 == key &&
                        tuples[0].Item2.Tag == cell.Tag && tuples[0].Item2.Number == cell.Number, "Packed tuple read.");
                    Check(LuminPackSerializer.Deserialize(listBytes, ref list) == expected.Length && list.Count == 1 &&
                        list[0].Value.Number == cell.Number, "Packed pair list reuse.");
                    var members = new MemberOwners { Tuples = new[] { tuple }, Pairs = list };
                    byte[] memberBytes = LuminPackSerializer.Serialize(members);
                    Check(memberBytes.Length == 1 + 2 * expected.Length && memberBytes.AsSpan(1, expected.Length).SequenceEqual(expected) &&
                        memberBytes.AsSpan(1 + expected.Length).SequenceEqual(expected), "Packed nested collection fast paths.");
                    byte[] transitive = LuminPackSerializer.Serialize(new[] { grandparent });
                    Check(transitive.Length == 18 && transitive[4] == grandparent.Prefix &&
                        transitive.AsSpan(5).SequenceEqual(expected.AsSpan(4)), "Packed propagation stopped at the immediate parent.");
                    TaggedParent?[] optional = { tagged, null };
                    byte[] nullable = LuminPackSerializer.Serialize(optional);
                    Check(nullable.Length == 19 && nullable[4] == 0 && nullable[^1] == byte.MaxValue &&
                        nullable.AsSpan(5, 13).SequenceEqual(expected.AsSpan(4)), "Nullable container did not select packed elements.");
                    var native = new NativeParent { Prefix = key, Item = cell };
                    Check(LuminPackSerializer.Serialize(new[] { native }).Length == sizeof(int) + Unsafe.SizeOf<NativeParent>(),
                        "Explicit Native lost its native stride.");
                    var taggedControl = new TaggedControl { Tag = cell.Tag, Number = cell.Number };
                    byte[] tag = LuminPackSerializer.Serialize(new[] { taggedControl });
                    Check(tag.Length == sizeof(int) + 1 + Unsafe.SizeOf<TaggedControl>() && tag[4] != 0,
                        "Tagged without a Packed descendant lost its native layout tag.");
                    Check(LuminPackSerializer.Serialize(taggedControl).Length == 9,
                        "A multi-model Tagged value emitted a tag or native padding outside a collection block.");
                    var stableTagged = new StableTagged { Tag = 5, Number = 0x01020304 };
                    var stablePacked = new StablePacked { Tag = 5, Number = 0x01020304 };
                    Check(LuminPackSerializer.Serialize(stableTagged).Length == 8,
                        "A single-model Tagged value lost its native stride.");
                    Check(LuminPackSerializer.Serialize(stablePacked).AsSpan().SequenceEqual(Convert.FromHexString("0504030201")),
                        "A single-model Packed value included native padding.");
                }
                private static void Check(bool condition, string message)
                {
                    if (!condition) throw new InvalidOperationException(message);
                }
            }
            """;
        CSharpParseOptions parse = new(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, parse, "RecursivePackedStorage.cs");
        CSharpCompilation input = TestCompilation.Create("RecursivePackedStorage", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new NextGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("RecursivePackedStorage.Entry")!.GetMethod("Run")!.Invoke(null, null);

        const string unsupported = """
            namespace RecursivePackedStorage;
            [LuminPack.Attribute.LuminPackable] public struct Unsupported { public Cell Packed; public nint PlatformWidth; }
            public static class InvalidEntry
            {
                public static byte[] Write() => LuminPack.LuminPackSerializer.Serialize(new (nint, Cell)[] { default });
            }
            """;
        var invalid = driver.RunGenerators(input.AddSyntaxTrees(CSharpSyntaxTree.ParseText(unsupported, parse, "Unsupported.cs")));
        string[] errors = invalid.GetRunResult().Diagnostics.Where(item => item.Id == "LuminPack111")
            .Select(item => item.GetMessage()).ToArray();
        Require(errors.Any(message => message.Contains("RecursivePackedStorage.Unsupported", StringComparison.Ordinal)) &&
            errors.Any(message => message.Contains("(nint, RecursivePackedStorage.Cell)", StringComparison.Ordinal)),
            "Recursive Packed with unsupported members did not report LuminPack111 at declaration and call boundaries.");
    }

    /// <summary>Checks component bytes, retained raw roots and freshly rendered output after changing only tuple names.</summary>
    /// <param name="references">Current runtime and framework metadata.</param>
    /// <param name="mode">The generation reachability mode.</param>
    /// <param name="modern">Whether modern reference cursors are available.</param>
    private static void Verify(MetadataReference[] references, string mode, bool modern)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: modern
            ? ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"] : ["UNITY_2022_3_OR_NEWER"]);
        string source = $$"""
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.{{mode}})]
            namespace PortableTupleModels;
            [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
            public struct Packet { public byte Tag; public (int Id, long Count) Pair; }
            [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
            public struct Compact { public (long Value, int Id, int Code) Pair; }
            [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
            public struct LongPacket
            {
                public byte Tag;
                public (int A, int B, int C, int D, int E, int F, int G, int H, int I, int J, int K, int L, int M, int N, int O) Items;
            }
            public static class Entry
            {
                public static int Run()
                {
                    var value = new Packet { Tag = 7, Pair = (123456789, long.MinValue + 17) };
                    var bytes = LuminPackSerializer.Serialize(value);
                    if (bytes.Length != 13 || bytes[0] != value.Tag || BitConverter.ToInt32(bytes, 1) != value.Pair.Id ||
                        BitConverter.ToInt64(bytes, 5) != value.Pair.Count)
                        throw new InvalidOperationException("Tuple aliases changed physical storage width or order.");
                    var copy = global::LuminPackUnitTest.ConcreteReads.Read<Packet>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Packet concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                    if (copy.Tag != value.Tag || copy.Pair != value.Pair) throw new InvalidOperationException("Tuple read failed.");
                    var list = global::LuminPackUnitTest.ConcreteReads.Read<List<Packet>>(LuminPackSerializer.Serialize(new List<Packet> { value, value, value }), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref List<Packet> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                    if (list.Count != 3 || list[2].Pair != value.Pair) throw new InvalidOperationException("Tuple collection read failed.");
                    var compact = new Compact { Pair = (long.MinValue + 1, 123456789, -23) };
                    var compactBytes = LuminPackSerializer.Serialize(compact);
                    if (compactBytes.Length != 16 || BitConverter.ToInt64(compactBytes, 0) != compact.Pair.Value ||
                        BitConverter.ToInt32(compactBytes, 8) != compact.Pair.Id || BitConverter.ToInt32(compactBytes, 12) != compact.Pair.Code ||
                        global::LuminPackUnitTest.ConcreteReads.Read<Compact>(compactBytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Compact concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).Pair != compact.Pair)
                        throw new InvalidOperationException("A compact tuple retained layout-dependent storage.");
                    var longValue = new LongPacket { Tag = 19, Items = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15) };
                    var longBytes = LuminPackSerializer.Serialize(longValue);
                    if (longBytes.Length != 61 || longBytes[0] != longValue.Tag)
                        throw new InvalidOperationException("Flattened tuple fields duplicated Rest storage.");
                    for (int index = 0; index < 15; index++)
                        if (BitConverter.ToInt32(longBytes, 1 + index * 4) != index + 1)
                            throw new InvalidOperationException("Long tuple position order changed.");
                    if (global::LuminPackUnitTest.ConcreteReads.Read<LongPacket>(longBytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref LongPacket concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).Items != longValue.Items)
                        throw new InvalidOperationException("Recursive Rest read failed.");
                    return bytes.Length + compactBytes.Length + longBytes.Length;
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, parse, "PortableTupleModels.cs");
        var input = TestCompilation.Create("PortableTuples_" + mode + "_" + modern, [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CreateDriver(parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        string generated = Text(driver);
        Require(generated.Contains(".Rest.Item1", StringComparison.Ordinal), "Long tuples lost physical Rest traversal.");
        Require(generated.Contains(".Item2", StringComparison.Ordinal), "Tuple operations lost direct component addressing.");
        Require(!output.SyntaxTrees.SelectMany(item => item.GetRoot().DescendantNodes()).OfType<StructDeclarationSyntax>()
            .Any(item => item.Identifier.ValueText.StartsWith("LocalPortable", StringComparison.Ordinal) && item.Members.OfType<FieldDeclarationSyntax>()
                .Any(field => field.Declaration.Variables.Any(variable => variable.Identifier.ValueText == "F7"))),
            "A tuple storage replica retained alias or flattened element slots.");
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Require((int)Assembly.Load(image.ToArray()).GetType("PortableTupleModels.Entry")!.GetMethod("Run")!.Invoke(null, null)! == 90,
            "Incorrect portable tuple wire sizes.");

        var changed = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(
            source.Replace("int Id", "int RenamedId", StringComparison.Ordinal).Replace(".Pair.Id", ".Pair.RenamedId", StringComparison.Ordinal)
                .Replace("long Count", "long RenamedCount", StringComparison.Ordinal).Replace(".Pair.Count", ".Pair.RenamedCount", StringComparison.Ordinal),
            parse, "PortableTupleModels.cs"));
        var retained = driver.RunGenerators(changed);
        var fresh = CreateDriver(parse).RunGenerators(changed);
        Require(Text(retained) == Text(fresh), "Renaming tuple elements retained stale storage analysis.");
    }

    /// <summary>Creates an independent driver for retained-versus-fresh comparisons.</summary>
    /// <param name="parse">The target's language and conditional symbols.</param>
    /// <returns>A fresh driver with both serialization generators.</returns>
    private static GeneratorDriver CreateDriver(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Orders generated sources independently of generator output scheduling.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>The complete source identity and text.</returns>
    private static string Text(GeneratorDriver driver) => string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .OrderBy(source => source.HintName, StringComparer.Ordinal).Select(source => source.HintName + source.SourceText));

    /// <summary>Reports a violated storage or incremental invariant.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
