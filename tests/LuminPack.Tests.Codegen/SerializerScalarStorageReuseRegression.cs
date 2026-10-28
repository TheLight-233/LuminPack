using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects scalar reuse selection and retained generation across element-contract edits.</summary>
internal static class SerializerScalarStorageReuseRegression
{
    /// <summary>Compiles the selected roots in every generation tier and verifies restoration after an exclusion edit.</summary>
    /// <param name="references">The platform and runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using Element = System.Int32;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
            public struct PortableElement { public byte Tag; public long Value; }
            public static class ScalarReuseEntry
            {
                public static int Sequence(ReadOnlySpan<byte> bytes, ref IEnumerable<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Collection(ReadOnlySpan<byte> bytes, ref ICollection<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int List(ReadOnlySpan<byte> bytes, ref IList<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ReadOnlyList(ReadOnlySpan<byte> bytes, ref IReadOnlyList<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ReadOnlyCollection(ReadOnlySpan<byte> bytes, ref IReadOnlyCollection<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Memory(ReadOnlySpan<byte> bytes, ref Memory<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Segment(ReadOnlySpan<byte> bytes, ref ArraySegment<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int Matrix(ReadOnlySpan<byte> bytes, ref Element[,] value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static int ReadOnlyMemory(ReadOnlySpan<byte> bytes, ref ReadOnlyMemory<Element> value) => LuminPackSerializer.Deserialize(bytes, ref value);
                public static void Check()
                {
                    var array = new Element[3];
                    IEnumerable<Element> source = array;
                    byte[] bytes = LuminPackSerializer.Serialize(source);
                    IEnumerable<Element> target = new Element[3];
                    object original = target;
                    if (Sequence(bytes, ref target) != bytes.Length || !ReferenceEquals(original, target)) throw new Exception("Scalar sequence reuse changed.");
                    Memory<Element> view = array.AsMemory(1, 1);
                    bytes = LuminPackSerializer.Serialize(view);
                    if (Memory(bytes, ref view) != bytes.Length || !MemoryMarshal.TryGetArray((ReadOnlyMemory<Element>)view, out var segment) || !ReferenceEquals(array, segment.Array) || segment.Offset != 1) throw new Exception("Scalar slice reuse changed.");
                    var matrix = new Element[2, 3];
                    bytes = LuminPackSerializer.Serialize(matrix);
                    original = matrix;
                    if (Matrix(bytes, ref matrix) != bytes.Length || !ReferenceEquals(original, matrix)) throw new Exception("Scalar matrix reuse changed.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            string text = source.Replace("MODE", mode, StringComparison.Ordinal);
            var compilation = CSharpCompilation.Create("ScalarReuse" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(text, parse, "Shape.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckSelection(driver, scalar: true);
            Assembly.Load(Emit(output)).GetType("ScalarReuseEntry")!.GetMethod("Check")!.Invoke(null, null);

            string excluded = text.Replace("using Element = System.Int32;", "using Element = PortableElement;", StringComparison.Ordinal);
            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(excluded, parse, "Shape.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var editedOutput, out diagnostics);
            CheckSelection(driver, scalar: false);
            Emit(editedOutput);
            GeneratorDriver fresh = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            fresh = fresh.RunGenerators(edited);
            Require(Trees(driver).SequenceEqual(Trees(fresh)), "A scalar-to-portable edit retained stale storage reuse.");
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out diagnostics);
            CheckSelection(driver, scalar: true);
            fresh = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse).RunGenerators(compilation);
            Require(Trees(driver).SequenceEqual(Trees(fresh)), "Restoring scalar storage retained a stale exclusion.");
            Assembly.Load(Emit(output)).GetType("ScalarReuseEntry")!.GetMethod("Check")!.Invoke(null, null);
        }
        Console.WriteLine("Passed scalar storage reuse across four tiers and retained/fresh scalar-portable edits.");
    }

    /// <summary>Checks reuse in each selected family while excluding read-only memory and portable storage.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="scalar">Whether the alias selects the known framework scalar.</param>
    private static void CheckSelection(GeneratorDriver driver, bool scalar)
    {
        int selected = 0;
        var selectedTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                     .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                     .Where(method => method.Identifier.ValueText == "RootRead" && method.ParameterList.Parameters.Count == 2))
        {
            string type = method.ParameterList.Parameters[1].Type!.ToString();
            bool sequence = type.Contains(".IEnumerable<", StringComparison.Ordinal) || type.Contains(".ICollection<", StringComparison.Ordinal) ||
                type.Contains(".IList<", StringComparison.Ordinal) || type.Contains(".IReadOnlyList<", StringComparison.Ordinal) || type.Contains(".IReadOnlyCollection<", StringComparison.Ordinal);
            bool view = type.Contains(".Memory<", StringComparison.Ordinal) || type.Contains(".ArraySegment<", StringComparison.Ordinal);
            bool matrix = method.ParameterList.Parameters[1].Type is ArrayTypeSyntax array &&
                array.RankSpecifiers.Any(rank => rank.Rank == 2);
            if (type.Contains(".ReadOnlyMemory<", StringComparison.Ordinal))
                Require(!method.ToString().Contains("__storage.Count == length", StringComparison.Ordinal), "Read-only memory acquired mutable storage reuse.");
            if (!sequence && !view && !matrix) continue;
            string marker = sequence ? "value.GetType() == typeof(" : view ? "__storage.Count == length" : "value.GetLowerBound(0)";
            Require(method.ToString().Contains(marker, StringComparison.Ordinal) == scalar, "Scalar reuse qualification differs: " + type);
            selected++;
            selectedTypes.Add(type);
        }
        Require(selectedTypes.Count == 8, "The scalar reuse fixture did not generate all eight selected roots: " + selected + ": " + string.Join(", ", selectedTypes));
    }

    /// <summary>Compiles a selected consumer and exposes compiler failures.</summary>
    /// <param name="output">The complete generated compilation.</param>
    /// <returns>The compiled consumer image.</returns>
    private static byte[] Emit(Compilation output)
    {
        using var image = new MemoryStream();
        var result = output.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        return image.ToArray();
    }

    /// <summary>Captures complete generated text with its deterministic hint name.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The sorted generated-source snapshot.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a failed focused invariant.</summary>
    /// <param name="condition">Whether the invariant holds.</param>
    /// <param name="message">The precise failure.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
