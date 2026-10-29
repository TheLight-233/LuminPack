using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks portable tuple storage, direct field addressing and alias-only incremental edits.</summary>
internal static class SerializerPortableTupleRegression
{
    /// <summary>Checks Full/Minimal generation and modern/compatibility cursor targets.</summary>
    /// <param name="references">Current runtime and framework metadata.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Minimal" })
            foreach (bool modern in new[] { true, false }) Verify(references, mode, modern);
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
                    var copy = LuminPackSerializer.Deserialize<Packet>(bytes);
                    if (copy.Tag != value.Tag || copy.Pair != value.Pair) throw new InvalidOperationException("Tuple read failed.");
                    var list = LuminPackSerializer.Deserialize<List<Packet>>(LuminPackSerializer.Serialize(new List<Packet> { value, value, value }));
                    if (list.Count != 3 || list[2].Pair != value.Pair) throw new InvalidOperationException("Tuple collection read failed.");
                    var compact = new Compact { Pair = (long.MinValue + 1, 123456789, -23) };
                    var compactBytes = LuminPackSerializer.Serialize(compact);
                    if (compactBytes.Length != 16 || BitConverter.ToInt64(compactBytes, 0) != compact.Pair.Value ||
                        BitConverter.ToInt32(compactBytes, 8) != compact.Pair.Id || BitConverter.ToInt32(compactBytes, 12) != compact.Pair.Code ||
                        LuminPackSerializer.Deserialize<Compact>(compactBytes).Pair != compact.Pair)
                        throw new InvalidOperationException("A compact tuple retained layout-dependent storage.");
                    var longValue = new LongPacket { Tag = 19, Items = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15) };
                    var longBytes = LuminPackSerializer.Serialize(longValue);
                    if (longBytes.Length != 61 || longBytes[0] != longValue.Tag)
                        throw new InvalidOperationException("Flattened tuple fields duplicated Rest storage.");
                    for (int index = 0; index < 15; index++)
                        if (BitConverter.ToInt32(longBytes, 1 + index * 4) != index + 1)
                            throw new InvalidOperationException("Long tuple position order changed.");
                    if (LuminPackSerializer.Deserialize<LongPacket>(longBytes).Items != longValue.Items)
                        throw new InvalidOperationException("Recursive Rest read failed.");
                    return bytes.Length + compactBytes.Length + longBytes.Length;
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, parse, "PortableTupleModels.cs");
        var input = CSharpCompilation.Create("PortableTuples_" + mode + "_" + modern, [tree], references,
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
