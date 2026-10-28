using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Restricts stateless interface entries to proven raw-scalar reads, independently of writes.</summary>
internal static class SerializerScalarInterfaceReadRegression
{
    /// <summary>Compiles and executes the targeted interface contracts in every generation tier.</summary>
    /// <param name="references">Platform and runtime compilation references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable(GeneratorType.Custom)] public struct CustomElement
            {
                public int Value;
                public static void Serialize(ref LuminPackWriter writer, in CustomElement value) => writer.Write(value.Value);
                public static void Deserialize(ref LuminPackReader reader, ref CustomElement value) => value.Value = reader.Read<int>();
            }
            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
            public struct PortableElement { public byte Tag; public long Value; }
            [LuminPackable(GeneratorType.CircleReference)] public sealed class ReferenceElement
            {
                [LuminPackOrder(0)] public int Value;
                [LuminPackOrder(1)] public ReferenceElement Next;
            }
            public static class ScalarInterfaceCases
            {
                public static int Sequence(ReadOnlySpan<byte> data, ref IEnumerable<int> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int Collection(ReadOnlySpan<byte> data, ref ICollection<Guid> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int List(ReadOnlySpan<byte> data, ref IReadOnlyList<long> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int Managed(ReadOnlySpan<byte> data, ref IEnumerable<ReferenceElement> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int Text(ReadOnlySpan<byte> data, ref ICollection<string> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int Custom(ReadOnlySpan<byte> data, ref IEnumerable<CustomElement> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int Portable(ReadOnlySpan<byte> data, ref IReadOnlyList<PortableElement> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int Other(ReadOnlySpan<byte> data, ref IList<int> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int Open<T>(ReadOnlySpan<byte> data, ref IEnumerable<T> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static byte[] Write(IEnumerable<int> value) => LuminPackSerializer.Serialize(value);
                public static string Json(IEnumerable<int> value) => LuminPackSerializer.SerializeJson(value);
                public static void ReadJson(string text, ref IEnumerable<int> value) => LuminPackSerializer.DeserializeJson(text, ref value);
                public static void Check()
                {
                    IEnumerable<int> value = new[] { 3, 7, 11 };
                    byte[] bytes = Write(value);
                    value = null;
                    if (Sequence(bytes, ref value) != bytes.Length || string.Join(",", value) != "3,7,11" || !(value is int[]))
                        throw new Exception("Interface entry changed values, width or construction");
                    string json = Json(value);
                    value = null;
                    ReadJson(json, ref value);
                    if (string.Join(",", value) != "3,7,11") throw new Exception("Interface JSON changed");
                    var item = new ReferenceElement { Value = 137 };
                    item.Next = item;
                    IEnumerable<ReferenceElement> source = new[] { item, item };
                    bytes = LuminPackSerializer.Serialize(source);
                    IEnumerable<ReferenceElement> target = null;
                    Managed(bytes, ref target);
                    using var items = target.GetEnumerator();
                    items.MoveNext();
                    var first = items.Current;
                    items.MoveNext();
                    if (!ReferenceEquals(first, items.Current) || !ReferenceEquals(first, first.Next))
                        throw new Exception("A stateful interface lost its shared reference context");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var compilation = CSharpCompilation.Create("ScalarInterface" + mode,
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var (driver, output) = Generate(compilation, parse);
            var methods = Methods(driver);
            var trait = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.CodeEmitters.EmitterTypeTraits", throwOnError: true)!
                .GetMethod("IsStatelessScalarSequenceRead", BindingFlags.Static | BindingFlags.NonPublic)!;
            var open = compilation.GetTypeByMetadataName("ScalarInterfaceCases")!.GetMembers("Open").OfType<IMethodSymbol>().Single();
            Require(!(bool)trait.Invoke(null, [open.Parameters[1].Type])!, "An unresolved interface argument acquired stateless eligibility.");
            foreach (string type in new[] { "global::System.Collections.Generic.IEnumerable<int>", "global::System.Collections.Generic.ICollection<global::System.Guid>",
                         "global::System.Collections.Generic.IReadOnlyList<long>" })
            {
                var body = ReadBody(methods, type);
                Require(!body.ToString().Contains("Lease", StringComparison.Ordinal) && body.ToString().Contains("new LuminPackReader(ref buffer)", StringComparison.Ordinal),
                    "A proven scalar interface read kept unused state in " + mode + ": " + type);
            }
            foreach (string type in new[] { "global::System.Collections.Generic.IEnumerable<global::ReferenceElement>", "global::System.Collections.Generic.ICollection<string>",
                         "global::System.Collections.Generic.IEnumerable<global::CustomElement>", "global::System.Collections.Generic.IReadOnlyList<global::PortableElement>",
                         "global::System.Collections.Generic.IList<int>" })
                Require(ReadBody(methods, type).ToString().Contains("Lease", StringComparison.Ordinal), "An excluded interface lost state: " + type);
            var writer = methods.Single(method => method.Identifier.ValueText == "Serialize" && method.TypeParameterList?.Parameters.Count == 1 &&
                method.ParameterList.Parameters.Count == 1 && method.ParameterList.Parameters[0].Type!.ToString() == "global::System.Collections.Generic.IEnumerable<int>");
            var writeBody = ResolveBody(methods, writer);
            Require(writeBody.ToString().Contains("LuminBufferWriterPool.Rent", StringComparison.Ordinal) &&
                !writeBody.ToString().Contains("directEvaluator", StringComparison.Ordinal), "The read qualification changed iterator writes.");
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("ScalarInterfaceCases")!.GetMethod("Check")!.Invoke(null, null);
            if (mode == "Minimal")
            {
                var compatibilityParse = new CSharpParseOptions(LanguageVersion.CSharp13);
                var compatibility = compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), compatibilityParse));
                var (compatibilityDriver, _) = Generate(compatibility, compatibilityParse);
                Require(ReadBody(Methods(compatibilityDriver), "global::System.Collections.Generic.IEnumerable<int>").ToString().Contains("Lease", StringComparison.Ordinal),
                    "The pointer compatibility metadata acquired the managed stateless entry.");
            }
        }
        Console.WriteLine("Passed scalar interface read eligibility, write/JSON isolation, reference tracking and four generation tiers.");
    }

    /// <summary>Runs the production generator without compiling the whole repository test suite.</summary>
    /// <param name="compilation">The targeted consumer fixture.</param>
    /// <param name="parse">The consumer's language and target symbols.</param>
    /// <returns>The generated syntax and updated consumer.</returns>
    private static (GeneratorDriver Driver, Compilation Output) Generate(CSharpCompilation compilation, CSharpParseOptions parse)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        return (driver, output);
    }

    /// <summary>Gets emitted declarations for exact signature and shared-forwarder inspection.</summary>
    /// <param name="driver">The completed production generator run.</param>
    /// <returns>Every emitted method in the targeted fixture.</returns>
    private static MethodDeclarationSyntax[] Methods(GeneratorDriver driver)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();

    /// <summary>Follows a concrete Span/ref facade into an optional shared body.</summary>
    /// <param name="methods">All generated declarations.</param>
    /// <param name="type">The exact closed destination signature.</param>
    /// <returns>The complete entry body that owns its read state.</returns>
    private static BlockSyntax ReadBody(MethodDeclarationSyntax[] methods, string type)
    {
        var entry = methods.Single(method => method.Identifier.ValueText == "Deserialize" && method.TypeParameterList?.Parameters.Count == 1 &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[0].Type!.ToString().StartsWith("ReadOnlySpan", StringComparison.Ordinal) &&
            method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == type);
        return ResolveBody(methods, entry);
    }

    /// <summary>Finds the shared core behind an optional concrete forwarder.</summary>
    /// <param name="methods">All generated declarations.</param>
    /// <param name="entry">The concrete operation's facade.</param>
    /// <returns>The complete body behind the facade.</returns>
    private static BlockSyntax ResolveBody(MethodDeclarationSyntax[] methods, MethodDeclarationSyntax entry)
    {
        var body = entry.Body!;
        var shared = body.DescendantNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault(call => call.Expression is SimpleNameSyntax name &&
            name.Identifier.ValueText.StartsWith("__LuminFacade", StringComparison.Ordinal));
        return shared?.Expression is SimpleNameSyntax sharedName ? methods.Single(method => method.Identifier.ValueText == sharedName.Identifier.ValueText).Body! : body;
    }

    /// <summary>Reports the exact generator or execution invariant that failed.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
