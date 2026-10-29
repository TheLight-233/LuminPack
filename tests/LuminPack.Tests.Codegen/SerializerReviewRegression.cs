using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks effective API visibility and source names that overlap generated JSON helpers.</summary>
internal static class SerializerReviewRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Compiles and executes the edge cases under each facade root policy.</summary>
    /// <param name="references">Runtime and platform references used by generated compilations.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Buffers;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed class ReadValue { public int Number; }
            [LuminPackable] public struct Raw { public int Number; public long Stamp; }
            namespace GetJsonPropertyIndexUtf8 { [LuminPackable] public sealed class Payload { public int Number; } }
            internal partial class Container { [LuminPackable] public sealed class Payload { public int Number; } }
            [LuminPackable] internal sealed class Hidden { public int Number; }
            public static class Entry
            {
                public static int Check()
                {
                    var value = new ReadValue { Number = 103 };
                    string json = LuminPackSerializer.SerializeJson(value);
                    if (json != LuminPackSerializer.SerializeJson<ReadValue>(value)) throw new Exception("JSON name collision changed output.");
                    ReadValue restored = null!;
                    LuminPackSerializer.DeserializeJson(json, ref restored);
                    var namespaced = new GetJsonPropertyIndexUtf8.Payload { Number = 107 };
                    string other = LuminPackSerializer.SerializeJson(namespaced);
                    GetJsonPropertyIndexUtf8.Payload otherValue = null!;
                    LuminPackSerializer.DeserializeJson(other, ref otherValue);
                    var nested = new Container.Payload { Number = 109 };
                    byte[] nestedBytes = LuminPackSerializer.Serialize(in nested);
                    Container.Payload nestedValue = null!;
                    LuminPackSerializer.Deserialize(nestedBytes.AsSpan(), ref nestedValue);
                    var items = new List<Hidden> { new() { Number = 113 } };
                    byte[] bytes = LuminPackSerializer.Serialize(in items);
                    List<Hidden> result = null!;
                    LuminPackSerializer.Deserialize(bytes.AsSpan(), ref result);
                    var raw = new Raw { Number = 127, Stamp = 131 };
                    byte[] rawBytes = LuminPackSerializer.Serialize(in raw);
                    Raw rawResult = default;
                    if (LuminPackSerializer.Deserialize(Segment(rawBytes), ref rawResult) != rawBytes.Length || rawResult.Number != 127 || rawResult.Stamp != 131)
                        throw new Exception("Unmanaged sequence specialization changed its bytes or consumed count.");
                    byte[] managedBytes = LuminPackSerializer.Serialize(in value);
                    ReadValue fromSequence = null!;
                    if (LuminPackSerializer.Deserialize(Segment(managedBytes), ref fromSequence) != managedBytes.Length || fromSequence.Number != value.Number)
                        throw new Exception("Managed sequence specialization changed its bytes or consumed count.");
                    return restored.Number + otherValue.Number + nestedValue.Number + result[0].Number;
                }
                private static ReadOnlySequence<byte> Segment(byte[] bytes)
                {
                    var first = new Slice(bytes.AsMemory(0, 1));
                    var last = new Slice(bytes.AsMemory(1));
                    first.Append(last);
                    return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
                }
                private sealed class Slice : ReadOnlySequenceSegment<byte>
                {
                    public Slice(ReadOnlyMemory<byte> memory) { Memory = memory; }
                    public void Append(Slice next) { next.RunningIndex = RunningIndex + Memory.Length; Next = next; }
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Light", "Minimal" })
        {
            var input = CSharpCompilation.Create("ReviewContracts" + mode,
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), Parse, "Models.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            CheckErrors(driver, output);
            foreach (var method in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .Where(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal))
                .SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(method => method.Identifier.ValueText == "Deserialize" && method.TypeParameterList == null &&
                    method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "ReadOnlySequence<byte>")))
            {
                string body = method.Body!.ToString();
                if (method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "global::Raw"))
                    Require(!body.Contains("LuminPackReader", StringComparison.Ordinal), "An unmanaged sequence retained unreachable managed reader code.");
                if (method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "global::ReadValue"))
                    Require(!body.Contains("Unsafe.ReadUnaligned", StringComparison.Ordinal), "A reference sequence retained unreachable unmanaged copy code.");
            }
            using var image = new MemoryStream();
            var emit = output.Emit(image);
            Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
            var context = new AssemblyLoadContext("ReviewContracts" + mode, isCollectible: true);
            try
            {
                image.Position = 0;
                var assembly = context.LoadFromStream(image);
                Require((int)assembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! == 432, "Review fixture changed its payload values.");
                foreach (var method in assembly.GetType("LuminPack.LuminPackSerializer")!.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    Require(method.GetParameters().All(parameter => !parameter.ParameterType.ToString().Contains("Hidden", StringComparison.Ordinal) &&
                        !parameter.ParameterType.ToString().Contains("Container+Payload", StringComparison.Ordinal)), "An internal payload escaped through a public API.");
            }
            finally { context.Unload(); }
        }
        VerifyVisibilityEdit(references);
        VerifyStateRoots(references);
        VerifyCustomBoundary(references);
        Console.WriteLine("Passed JSON helper/type-name collisions, effective nested/generic visibility, retained visibility edits, and managed/unmanaged sequence specialization in Full/Light/Minimal.");
    }

    /// <summary>Retains state-only payload roots without restoring unused convenience overloads.</summary>
    /// <param name="references">Runtime and platform references.</param>
    private static void VerifyStateRoots(MetadataReference[] references)
    {
        const string source = """
            using LuminPack;
            using LuminPack.Core;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            [LuminPackable] public sealed class StateOnly { public int Number; public System.Collections.Generic.List<int> Values; }
            public static class Entry
            {
                public static void Write(ref LuminPackWriter writer, in StateOnly value) => LuminPackSerializer.Serialize(ref writer, in value);
                public static void Read(ref LuminPackReader reader, ref StateOnly value) => LuminPackSerializer.Deserialize(ref reader, ref value);
                public static void JsonWrite(ref LuminPackJsonWriter writer, in StateOnly value) => LuminPackSerializer.SerializeJson(ref writer, in value);
                public static void JsonRead(ref LuminPackJsonReader reader, ref StateOnly value) => LuminPackSerializer.DeserializeJson(ref reader, ref value);
            }
            """;
        var input = CSharpCompilation.Create("StateOnlyRoots", [CSharpSyntaxTree.ParseText(source, Parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        CheckErrors(driver, output);
        var files = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
        Require(files.Any(file => file.HintName == "StateOnly.Extension.g.cs"), "A direct state API failed to retain its Minimal payload root.");
        Require(!files.Any(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal) && file.HintName != "LuminPackSerializer.g.cs"),
            "A state-only root restored unused concrete convenience overloads.");
    }

    /// <summary>Preserves user custom formatter boundaries during call projection.</summary>
    /// <param name="references">Runtime and platform references.</param>
    private static void VerifyCustomBoundary(MetadataReference[] references)
    {
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Core;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            [LuminPackable(GeneratorType.Custom)] public sealed class CustomValue
            {
                public static void Serialize(ref LuminPackWriter writer, in CustomValue value) { Entry.Writes++; }
                public static void Deserialize(ref LuminPackReader reader, ref CustomValue value) { Entry.Reads++; value = new(); }
            }
            public static class CustomOperations
            {
                public static void Write(this ref LuminPackWriter writer, in CustomValue value) { Entry.Writes += 1000; }
                public static void Read(this ref LuminPackReader reader, ref CustomValue value) { Entry.Reads += 1000; value = new(); }
            }
            public static class Entry
            {
                public static int Writes, Reads;
                public static int Check()
                {
                    var value = new CustomValue();
                    var bytes = LuminPackSerializer.Serialize(in value);
                    CustomValue restored = null!;
                    LuminPackSerializer.Deserialize(bytes.AsSpan(), ref restored);
                    return Writes + Reads;
                }
            }
            """;
        var input = CSharpCompilation.Create("CustomStateBoundary", [CSharpSyntaxTree.ParseText(source, Parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        CheckErrors(driver, output);
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(image.ToArray());
        int result = (int)assembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)!;
        Require(result == 2, "A user custom formatter boundary changed: " + result + ".");
    }

    /// <summary>Invalidates the analysis signature universe when only an enclosing type's visibility changes.</summary>
    /// <param name="references">References shared by retained and fresh generation.</param>
    private static void VerifyVisibilityEdit(MetadataReference[] references)
    {
        var input = CSharpCompilation.Create("ReviewVisibilityEdit",
            [CSharpSyntaxTree.ParseText("using LuminPack.Attribute; [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)] public partial class Container { }", Parse, "Outer.cs"),
             CSharpSyntaxTree.ParseText("public partial class Container { [LuminPack.Attribute.LuminPackable] public sealed class Payload { public int Number; } }", Parse, "Payload.cs"),
             CSharpSyntaxTree.ParseText("internal static class Entry { internal static byte[] Save(Container.Payload value) => LuminPack.LuminPackSerializer.Serialize(in value); }", Parse, "Entry.cs")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGenerators(input);
        // Both partial declarations change access together; the call site remains the identical tree.
        var edited = input;
        foreach (var tree in input.SyntaxTrees.Take(2))
            edited = edited.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(tree.ToString().Replace("public partial class Container", "internal partial class Container", StringComparison.Ordinal), Parse, tree.FilePath));
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out _);
        CheckErrors(driver, output);
        var fresh = NewDriver().RunGenerators(edited);
        Require(Files(driver).SequenceEqual(Files(fresh)), "An enclosing visibility edit reused stale API signatures or facade output.");
    }

    /// <summary>Creates an independent retained-capable generator driver.</summary>
    /// <returns>A driver with both production generators.</returns>
    private static GeneratorDriver NewDriver() => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse);

    /// <summary>Captures stable complete output for retained/fresh comparison.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <returns>Hint names and source values in canonical order.</returns>
    private static IEnumerable<string> Files(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .OrderBy(file => file.HintName, StringComparer.Ordinal).Select(file => file.HintName + "\n" + file.SourceText);

    /// <summary>Rejects both generated compilation errors and caught generator diagnostics.</summary>
    /// <param name="driver">The completed generation pass.</param>
    /// <param name="output">The generated compilation.</param>
    private static void CheckErrors(GeneratorDriver driver, Compilation output)
    {
        var errors = output.GetDiagnostics().Concat(driver.GetRunResult().Diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0 && driver.GetRunResult().Results.All(result => result.Exception == null), string.Join(Environment.NewLine, errors));
    }

    /// <summary>Reports an executable review-contract failure.</summary>
    /// <param name="condition">The invariant to preserve.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
