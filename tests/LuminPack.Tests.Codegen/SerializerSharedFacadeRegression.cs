using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks shared convenience bodies, concrete adapters and incremental support selection.</summary>
internal static class SerializerSharedFacadeRegression
{
    /// <summary>Compiles modern and compatibility facades with identical APIs and operation semantics.</summary>
    /// <param name="references">The current platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (bool modern in new[] { true, false }) Verify(references, modern);
        Console.WriteLine("Passed shared facade signatures, direct adapters, scalar results, async/profile behavior, raw sequences and retained/fresh selection.");
    }

    /// <summary>Exercises one target's complete and pruned facade generation.</summary>
    /// <param name="references">The current platform and runtime references.</param>
    /// <param name="modern">Whether scoped state APIs and modern async templates are selected.</param>
    private static void Verify(MetadataReference[] references, bool modern)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose, preprocessorSymbols: modern
            ? ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"] : ["UNITY_2022_3_OR_NEWER"]);
        var settings = CSharpSyntaxTree.ParseText("[assembly: LuminPack.Attribute.LuminPackGeneratorOptions(LuminPack.Attribute.LuminPackGenerationMode.Full)]", parse, "Settings.cs");
        var models = CSharpSyntaxTree.ParseText("""
            using System;
            using System.Buffers;
            using System.IO;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            [LuminPackable] public sealed class A { public int Id; public string Text = ""; }
            [LuminPackable] public sealed class B { public int Id; public string Note = ""; }
            [LuminPackable] public sealed class C { public int Id; public bool Flag; }
            public static class Entry
            {
                public static void Run()
                {
                    var value = new A { Id = 17, Text = "\u00E9\u4E2D" };
                    var bytes = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(value);
                    var target = new A();
                    var original = target;
                    LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(bytes.AsSpan(), ref target);
                    if (!ReferenceEquals(original, target) || target.Id != 17 || target.Text != value.Text) throw new Exception("Profile reuse changed.");
                    string json = LuminPackSerializer.SerializeJson(value);
                    target = null;
                    LuminPackSerializer.DeserializeJson(json, ref target);
                    if (target.Id != 17 || target.Text != value.Text) throw new Exception("JSON changed.");
                    using var stream = new MemoryStream();
                    LuminPackSerializer.SerializeAsync<LuminPackUtf16LengthOptions>(stream, value).GetAwaiter().GetResult();
                    if (!stream.ToArray().SequenceEqual(bytes)) throw new Exception("Async profile write changed.");
                    stream.Position = 0;
                    target = LuminPackSerializer.DeserializeAsync<LuminPackUtf16LengthOptions>(stream, value).GetAwaiter().GetResult();
                    if (ReferenceEquals(target, value) || target.Id != 17 || !stream.CanRead) throw new Exception("Async fresh read or ownership changed.");
                    var fixedValue = new C { Id = 5, Flag = true };
                    var fixedBytes = LuminPackSerializer.Serialize(fixedValue);
                    C fixedCopy = null;
                    if (LuminPackSerializer.Deserialize(fixedBytes.AsSpan(), ref fixedCopy) != fixedBytes.Length || fixedCopy.Id != 5 || !fixedCopy.Flag) throw new Exception("Fixed record round trip changed.");
                    byte tag = 7;
                    byte[] data = LuminPackSerializer.Serialize(tag);
                    byte[] array = LuminPackSerializer.Serialize(data);
                    byte[] restored = null;
                    LuminPackSerializer.Deserialize(array.AsSpan(), ref restored);
                    if (data.Length != 1 || data[0] != tag || !restored.SequenceEqual(data)) throw new Exception("Byte-array API result changed.");
                    int scalar = 0;
                    var sequence = new ReadOnlySequence<byte>(LuminPackSerializer.Serialize(23));
                    if (LuminPackSerializer.Deserialize(sequence, ref scalar) != 4 || scalar != 23) throw new Exception("Raw sequence changed.");
                    var tuple = (17, 19);
                    var tupleBytes = LuminPackSerializer.Serialize(tuple);
                    var tupleTarget = (0, 0);
                    LuminPackSerializer.Deserialize(tupleBytes.AsSpan(), ref tupleTarget);
                    if (tupleTarget != tuple) throw new Exception("Existing tuple fallback changed.");
                }
            }
            """, parse, "Models.cs");
        var input = CSharpCompilation.Create("SharedFacade_" + modern, [settings, models], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        var driver = NewDriver(parse).RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        CheckErrors(output, diagnostics);
        var methods = FacadeMethods(driver, parse);
        var facade = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" }).ToArray();
        foreach (string type in new[] { "global::A", "global::B" })
        {
            var concrete = facade.Where(method => method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == "value" && parameter.Type?.ToString() == type)).ToArray();
            Require(concrete.Count(method => method.TypeParameterList == null) == 15 && concrete.Count(method => method.TypeParameterList?.Parameters.Count == 1) == 15,
                "Sharing changed the complete concrete signatures for " + type);
            Require(concrete.Where(method => method.TypeParameterList != null).All(method => method.Body!.Statements.Count == 1 && method.Body.ToString().Contains("__LuminFacade_", StringComparison.Ordinal)),
                "A managed full facade retained repeated profile bodies.");
            var adapters = methods.Where(method => method.Parent is StructDeclarationSyntax && method.ParameterList.Parameters.LastOrDefault()?.Type?.ToString() == type).ToArray();
            // Only direct records evaluate an exact width, so the other adapters reject it instead of dispatching.
            Require(adapters.Length == 5 && adapters.Where(method => method.Identifier.ValueText != "Size").All(method =>
                method.ExpressionBody?.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } } &&
                generic.TypeArgumentList.Arguments.Count == 1), "Concrete adapters added generic payload dispatch.");
            Require(adapters.All(method => method.ParameterList.Parameters.All(parameter => parameter.Modifiers.Any(SyntaxKind.ScopedKeyword) == modern)),
                "Adapter ref-safety annotations differ from the target state APIs.");
        }
        // A primitive-only object has a constant width: modern targets write it directly; pointer-cursor targets keep the pooled entry.
        var fixedWrite = facade.Single(method => method.Identifier.ValueText == "Serialize" && method.TypeParameterList?.Parameters.Count == 1 &&
            method.ReturnType.ToString() == "byte[]" && method.ParameterList.Parameters.Count == 1 && method.ParameterList.Parameters[0].Type?.ToString() == "global::C");
        string fixedBody = fixedWrite.Body!.ToString();
        Require(modern
                ? fixedBody.Contains("new byte[", StringComparison.Ordinal) && !fixedBody.Contains("LuminBufferWriterPool", StringComparison.Ordinal)
                : fixedBody.Contains("__LuminFacade_", StringComparison.Ordinal) || fixedBody.Contains("LuminBufferWriterPool", StringComparison.Ordinal),
            "A fixed record changed its allocating entry selection.");
        Require(facade.Any(method => method.Identifier.ValueText.StartsWith("__LuminFacade_", StringComparison.Ordinal) && method.TypeParameterList?.Parameters.Count == 3),
            "No generic shared body was emitted.");
        Require(facade.Any(method => method.Identifier.ValueText == "Deserialize" && method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "int") &&
            method.Body!.ToString().Contains("__luminSequenceValue", StringComparison.Ordinal)), "Sharing replaced architecture-specific raw sequence staging.");
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var context = new AssemblyLoadContext("SharedFacade", isCollectible: true);
        image.Position = 0;
        context.LoadFromStream(image).GetType("Entry")!.GetMethod("Run")!.Invoke(null, null);
        context.Unload();

        var minimal = input.ReplaceSyntaxTree(settings, CSharpSyntaxTree.ParseText(settings.ToString().Replace(".Full", ".Minimal", StringComparison.Ordinal), parse, "Settings.cs"));
        var retained = driver.RunGeneratorsAndUpdateCompilation(minimal, out var minimalOutput, out diagnostics);
        CheckErrors(minimalOutput, diagnostics);
        Require(Text(retained) == Text(NewDriver(parse).RunGenerators(minimal)), "Removing complete API roots retained stale shared support.");
        Require(!Text(retained).Contains("__LuminFacadeOperations", StringComparison.Ordinal), "Pruned APIs acquired unused full-facade adapter support.");
        var restoredDriver = retained.RunGeneratorsAndUpdateCompilation(input, out var restoredOutput, out diagnostics);
        CheckErrors(restoredOutput, diagnostics);
        Require(Text(restoredDriver) == Text(driver), "Restoring complete API roots lost shared operations.");
    }

    /// <summary>Creates a fresh production driver for one conditional target.</summary>
    /// <param name="parse">The target language and framework symbols.</param>
    /// <returns>The two production generators with no retained state.</returns>
    private static GeneratorDriver NewDriver(CSharpParseOptions parse)
        => CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Reads facade declarations, including their private support methods.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <param name="parse">The current parse settings.</param>
    /// <returns>The methods from all facade partitions.</returns>
    private static MethodDeclarationSyntax[] FacadeMethods(GeneratorDriver driver, CSharpParseOptions parse)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Where(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal))
            .SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();

    /// <summary>Captures deterministic generated values for retained/fresh comparisons.</summary>
    /// <param name="driver">The completed production driver.</param>
    /// <returns>Hint names and exact source values in canonical order.</returns>
    private static string Text(GeneratorDriver driver)
        => string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).OrderBy(file => file.HintName, StringComparer.Ordinal).Select(file => file.HintName + "\n" + file.SourceText));

    /// <summary>Rejects generator or generated-compilation errors.</summary>
    /// <param name="output">The generated consumer compilation.</param>
    /// <param name="diagnostics">The generator diagnostics.</param>
    private static void CheckErrors(Compilation output, IEnumerable<Diagnostic> diagnostics)
    {
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(item => item.ToString())));
    }

    /// <summary>Fails a concrete sharing invariant without changing production diagnostics.</summary>
    /// <param name="condition">The expected condition.</param>
    /// <param name="message">The invariant description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
