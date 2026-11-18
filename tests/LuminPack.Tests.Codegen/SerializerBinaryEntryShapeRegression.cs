extern alias ModelBasedAnalyzer;

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks that binary entries shortcut only the storage and operation state their formatter graph permits.</summary>
internal static class SerializerBinaryEntryShapeRegression
{
    /// <summary>Compiles a Next consumer and checks the root routes and their wire behavior.</summary>
    /// <param name="references">The framework and runtime metadata.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            using LuminPack.Utility;

            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
            public struct NativeCell { public long X; public int Y; }

            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
            public struct PackedCell { public byte X; public long Y; }

            [LuminPackable] public sealed class PlainGraph { public List<string> Text; public PackedCell? Cell; }
            [LuminPackable(GeneratorType.VersionTolerant)] public sealed class VersionCell { public int Value; }
            [LuminPackable] public sealed class VersionGraph { public List<VersionCell> Cells; }

            [LuminPackable(GeneratorType.Custom)] public struct CustomCell
            {
                public int Value;
                public static void Serialize(ref LuminPackWriter writer, in CustomCell value) => writer.Write(value.Value);
                public static void Deserialize(ref LuminPackReader reader, ref CustomCell value)
                {
                    if (reader.Options == null) throw new Exception("Custom read lost its explicit operation state.");
                    value.Value = reader.Read<int>();
                }
            }
            [LuminPackable] public sealed class CustomGraph { public CustomCell Cell; }

            public static class Cases
            {
                public static void Run()
                {
                    using var buffer = new LuminBufferWriter(true);
                    var raw = new NativeCell { X = -321, Y = 412 };
                    var rawBytes = LuminPackSerializer.Serialize(raw);
                    LuminPackSerializer.Serialize(raw, buffer);
                    if (!buffer.GetSpan().SequenceEqual(rawBytes)) throw new Exception("Raw root bytes changed.");
                    var result = LuminPackSerializer.Deserialize<NativeCell>(rawBytes);
                    if (result.X != raw.X || result.Y != raw.Y) throw new Exception("Raw read changed fields.");
                    bool failed = false;
                    try { LuminPackSerializer.Deserialize(rawBytes.AsSpan(1), ref result); }
                    catch { failed = true; }
                    if (!failed || result.X != raw.X || result.Y != raw.Y) throw new Exception("Raw read published a truncated value.");

                    PackedCell? packed = new PackedCell { X = 23, Y = -4096 };
                    byte[] packedBytes = LuminPackSerializer.Serialize(packed.Value);
                    byte[] nullableBytes = LuminPackSerializer.Serialize(packed);
                    buffer.ResetForReuse();
                    LuminPackSerializer.Serialize(packed, buffer);
                    if (packedBytes.Length != 9 || nullableBytes.Length != 10 ||
                        !nullableBytes.AsSpan(1).SequenceEqual(packedBytes) || !nullableBytes.AsSpan().SequenceEqual(buffer.GetSpan()))
                        throw new Exception("Packed nullable width or marker changed.");
                    var received = LuminPackSerializer.Deserialize<PackedCell?>(nullableBytes);
                    if (received?.X != 23 || received?.Y != -4096) throw new Exception("Packed nullable read changed fields.");

                    var plain = new PlainGraph { Text = new List<string> { "abc" }, Cell = packed };
                    byte[] plainBytes = LuminPackSerializer.Serialize<PlainGraph, LuminPackUtf8TokenOptions>(plain);
                    PlainGraph reused = new PlainGraph { Text = new List<string>(), Cell = null };
                    if (LuminPackSerializer.Deserialize<PlainGraph, LuminPackUtf8TokenOptions>(plainBytes, ref reused) != plainBytes.Length ||
                        reused.Text.Count != 1 || reused.Text[0] != "abc" || reused.Cell?.Y != -4096)
                        throw new Exception("Explicit-profile read changed data or consumed width.");
                    var version = new VersionGraph { Cells = new List<VersionCell> { new VersionCell { Value = 17 }, new VersionCell { Value = 31 } } };
                    var versionBytes = LuminPackSerializer.Serialize(version);
                    if (LuminPackSerializer.Deserialize<VersionGraph>(versionBytes).Cells[1].Value != 31)
                        throw new Exception("Nested version read changed data.");
                    var customBytes = LuminPackSerializer.Serialize(new CustomGraph { Cell = new CustomCell { Value = 71 } });
                    if (LuminPackSerializer.Deserialize<CustomGraph>(customBytes).Cell.Value != 71)
                        throw new Exception("Nested Custom read changed data.");
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview,
            preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"])
            .WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        var compilation = CSharpCompilation.Create("BinaryEntryShapeCases",
            [CSharpSyntaxTree.ParseText(source, parse, "BinaryEntryShapeCases.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var methods = driver.GetRunResult().GeneratedTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Identifier.ValueText.StartsWith("LuminIntercept_", StringComparison.Ordinal)).ToArray();
        var native = methods.Where(method => method.Identifier.ValueText.StartsWith("LuminIntercept_NativeCell_", StringComparison.Ordinal)).ToArray();
        var plain = methods.Single(method => method.Identifier.ValueText.StartsWith("LuminIntercept_PlainGraph_", StringComparison.Ordinal) &&
            method.ParameterList.Parameters[0].Type!.ToString() == "ReadOnlySpan<byte>");
        if (!native.Any(method => method.ToString().Contains("LuminPackReader.ReadComplete", StringComparison.Ordinal)) ||
            !native.Any(method => method.ToString().Contains("AllocateUninitializedArray<byte>", StringComparison.Ordinal)) ||
            !plain.ToString().Contains("new LuminPackReader(ref buffer)", StringComparison.Ordinal))
            throw new InvalidOperationException("A proven binary root did not select its direct entry.");
        foreach (string type in new[] { "VersionGraph", "CustomGraph" })
        {
            var stateful = methods.Single(method => method.Identifier.ValueText.StartsWith("LuminIntercept_" + type + "_", StringComparison.Ordinal) &&
                method.ParameterList.Parameters[0].Type!.ToString() == "ReadOnlySpan<byte>");
            if (!stateful.ToString().Contains("LuminPackReaderOptionalState.Lease", StringComparison.Ordinal))
                throw new InvalidOperationException(type + " lost its binary operation state.");
        }

        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        if (diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error) || !emitted.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Concat(emitted.Diagnostics)
                .Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("Cases")!.GetMethod("Run")!.Invoke(null, null);
        Console.WriteLine("Passed raw, portable, nullable, stateless and pooled binary root entry contracts.");
    }
}
