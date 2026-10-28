using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks first-build fluent roots and exact overload selection without unrelated formatter roots.</summary>
internal static class SerializerAdvancingApiRegression
{
    /// <summary>Compiles and executes minimal scalar and portable collection consumers.</summary>
    /// <param name="references">The platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string members in new[]
        {
            """
            [LuminPackable] public struct Record<T> where T : unmanaged { public byte Tag; public T Number; }
            public static class Entry
            {
                public static void Check()
                {
                    var value = new Record<int> { Tag = 17, Number = 1729 };
                    Span<byte> storage = new byte[64]; var writer = new LuminPackWriter(ref storage);
                    writer.Write(value); writer.Write((byte)91);
                    if (writer.Position != 9 || storage[0] != 17 || BitConverter.ToInt32(storage[4..]) != 1729)
                        throw new Exception("An open generic formatter intercepted a concrete raw operation.");
                    ReadOnlySpan<byte> payload = storage[..writer.Position]; var reader = new LuminPackReader(ref payload);
                    Record<int> result = default; reader.Read(ref result);
                    if (result.Tag != 17 || result.Number != 1729 || reader.Read<byte>() != 91 || reader.Position != 9)
                        throw new Exception("A concrete raw reader changed storage width or the following record.");
                }
            }
            """,
            """
            [LuminPackable] public sealed class Record { public int Id; public List<string> Values; }
            public static class Entry
            {
                public static void Check()
                {
                    using var output = new LuminPack.Utility.LuminBufferWriter(true);
                    var writer = new LuminPackJsonWriter(output, output.WriterState);
                    writer.Write(new Record { Id = 1729, Values = new List<string> { "first", "second" } });
                    ReadOnlySpan<byte> payload = writer.GetSpan();
                    using var state = new LuminPack.Option.LuminPackReaderOptionalState();
                    var reader = new LuminPackJsonReader(ref payload, state);
                    Record result = null; reader.Read(ref result); reader.EnsureEndOfDocument();
                    if (result.Id != 1729 || result.Values[1] != "second" || reader.Position != payload.Length)
                        throw new Exception("A fluent-only JSON root or its collection contract changed.");
                }
            }
            """,
            """
            [LuminPackable] public sealed class Record { public int Id; public long Number; }
            public static class Entry
            {
                public static void Check()
                {
                    Span<byte> storage = new byte[64]; var writer = new LuminPackWriter(ref storage);
                    writer.Write(new Record { Id = 1729, Number = 9876543210 }); writer.Write((byte)91);
                    if (writer.Position != 14 || storage[0] != 2 || BitConverter.ToInt32(storage[1..]) != 1729)
                        throw new Exception("A byte header selected a wider numeric extension.");
                    ReadOnlySpan<byte> payload = storage[..writer.Position]; var reader = new LuminPackReader(ref payload);
                    Record value = null; reader.Read(ref value);
                    if (value.Id != 1729 || value.Number != 9876543210 || reader.Read<byte>() != 91 || reader.Position != 14)
                        throw new Exception("A fluent-only root lost its generated operation.");
                }
            }
            """,
            """
            [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed), StructLayout(LayoutKind.Sequential)]
            public struct Portable { public byte Tag; public long Number; }
            public static class Entry
            {
                public static void Check()
                {
                    var array = new[] { new Portable { Tag = 17, Number = 1729 }, new Portable { Tag = 19, Number = 23 } };
                    var list = new List<Portable>(array);
                    Span<byte> storage = new byte[128]; var writer = new LuminPackWriter(ref storage);
                    writer.Write(array); writer.Write(list); writer.Write((byte)91);
                    if (writer.Position != 45) throw new Exception("A portable collection selected raw span storage.");
                    ReadOnlySpan<byte> payload = storage[..writer.Position]; var reader = new LuminPackReader(ref payload);
                    Portable[] restored = null; List<Portable> restoredList = null;
                    reader.Read(ref restored); reader.Read(ref restoredList);
                    if (restored[0].Number != 1729 || restoredList[1].Tag != 19 || reader.Read<byte>() != 91 || reader.Position != 45)
                        throw new Exception("Portable collection fields or following records changed.");
                }
            }
            """
        })
        {
            const string header = "using System; using System.Collections.Generic; using System.Runtime.InteropServices; using LuminPack; using LuminPack.Core; using LuminPack.Attribute; [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)] ";
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var input = CSharpCompilation.Create("AdvancingConsumer" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(header + members, parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            using var image = new MemoryStream();
            var result = output.Emit(image);
            if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Concat(result.Diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
        }
        VerifyJsonRoots(references);
        VerifyAccessibleFields(references);
        Console.WriteLine("Passed minimal fluent roots, scalar overloads, portable collection selection, and concrete JSON roots in all four generation tiers.");
    }

    /// <summary>Requires accessible fields to avoid storage aliases while private members retain their views.</summary>
    /// <param name="references">The platform and runtime references.</param>
    private static void VerifyAccessibleFields(MetadataReference[] references)
    {
        const string source = """
            using System; using LuminPack; using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
            [LuminPackable] public sealed class Ordinary { public int Id; [LuminPackInclude] internal string Text; }
            [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Versioned { public int Id; [LuminPackInclude] internal string Text; }
            [LuminPackable(GeneratorType.CircleReference)] public sealed class Tracked { [LuminPackOrder(0)] public int Id; [LuminPackOrder(1), LuminPackInclude] internal string Text; }
            [LuminPackable] public sealed class Hidden { [LuminPackInclude] private int Id; public int GetId() => Id; }
            public static class Entry
            {
                public static void Check()
                {
                    Check(new Ordinary { Id = 17, Text = "ordinary" });
                    Check(new Versioned { Id = 19, Text = "versioned" });
                    Check(new Tracked { Id = 23, Text = "tracked" });
                    foreach (int? value in new int?[] { 42, null })
                        if (LuminPackSerializer.Deserialize<int?>(LuminPackSerializer.Serialize<int?>(value)) != value)
                            throw new Exception("Generic nullable dispatch bypassed its formatter contract.");
                }
                private static void Check<T>(T value)
                {
                    var bytes = LuminPackSerializer.Serialize(value);
                    var result = LuminPackSerializer.Deserialize<T>(bytes);
                    if (LuminPackSerializer.SerializeJson(value) != LuminPackSerializer.SerializeJson(result))
                        throw new Exception("Direct field access changed a serialization contract.");
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var input = CSharpCompilation.Create("DirectFields", [CSharpSyntaxTree.ParseText(source, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        foreach (string type in new[] { "Ordinary", "Versioned", "Tracked" })
        {
            var bodies = methods.Where(method => method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().TrimEnd('?') == "global::" + type));
            if (bodies.Any(method => method.ToString().Contains("LuminPackMarshal.As<global::" + type, StringComparison.Ordinal)))
                throw new InvalidOperationException(type + " retained a storage alias for accessible fields.");
        }
        if (!methods.Any(method => method.ToString().Contains("LuminPackMarshal.As<global::Hidden", StringComparison.Ordinal)))
            throw new InvalidOperationException("Private included storage lost its Local access mechanism.");
        using var image = new MemoryStream();
        var result = output.Emit(image);
        if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Requires concrete JSON state shells to target roots in every supported generation tier.</summary>
    /// <param name="references">The platform and runtime references.</param>
    private static void VerifyJsonRoots(MetadataReference[] references)
    {
        const string source = """
            using System; using System.Collections.Generic; using LuminPack; using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed class Root { public List<Leaf> Items = new(); }
            [LuminPackable] public sealed class Leaf { public int Id; public string Name; }
            [LuminPackable] public sealed class Unused { public long Id; }
            public static class Entry
            {
                public static void Check()
                {
                    var value = new Root { Items = new() { new Leaf { Id = 17, Name = "root" } } };
                    var binary = LuminPackSerializer.Serialize(value);
                    var json = LuminPackSerializer.SerializeJson(value);
                    Root result = null;
                    LuminPackSerializer.DeserializeJson(json, ref result);
                    if (result.Items[0].Id != 17 || result.Items[0].Name != "root") throw new Exception("JSON root round trip failed.");
                }
            }
            """;
        foreach (string tier in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var input = CSharpCompilation.Create("JsonRoot" + tier,
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", tier, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
            foreach (string operation in new[] { "SerializeJson", "DeserializeJson" })
            {
                var shells = methods.Where(method => method.Identifier.ValueText == operation && method.ParameterList.Parameters.Count == 2 &&
                    method.ParameterList.Parameters[0].Type?.ToString().Contains("LuminPackJson", StringComparison.Ordinal) == true &&
                    method.ParameterList.Parameters[1].Type?.ToString().TrimEnd('?') == "global::Root").ToArray();
                if (shells.Length != 2 || shells.Any(shell => shell.Body?.Statements.Count != 1 ||
                    !shell.Body.Statements[0].ToString().Contains(operation == "SerializeJson" ? "RootWriteJson" : "RootReadJson", StringComparison.Ordinal)))
                    throw new InvalidOperationException(tier + " retained JSON dispatch or an intermediate state forwarding hop for Root.");
            }
            bool unused = methods.Any(method => method.Identifier.ValueText == "RootWriteJson" &&
                method.ParameterList.Parameters.Last().Type?.ToString().TrimEnd('?') == "global::Unused");
            if (unused != (tier != "Minimal")) throw new InvalidOperationException(tier + " changed annotated root retention.");
            using var image = new MemoryStream();
            var emit = output.Emit(image);
            if (!emit.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
        }
    }
}
