using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks concrete snapshot entry generation, profile propagation and nested operation boundaries.</summary>
internal static class SerializerTimeZoneCacheRegression
{
    /// <summary>Compiles and executes the time-zone contract in each generation tier.</summary>
    /// <param name="references">The runtime and platform compilation references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed class TimeZoneEnvelope { public TimeZoneInfo Zone; public int Tail; }
            public static class TimeZoneCases
            {
                public static byte[] Write(TimeZoneInfo zone) => LuminPackSerializer.Serialize(zone);
                public static int Read(byte[] bytes, ref TimeZoneInfo zone) => LuminPackSerializer.Deserialize(bytes.AsSpan(), ref zone);
                public static void Check()
                {
                    var zone = TimeZoneInfo.CreateCustomTimeZone("Codegen/Zone", TimeSpan.FromMinutes(345), "Display", "Standard");
                    byte[] record = Write(zone);
                    TimeZoneInfo copy = null;
                    if (Read(record, ref copy) != record.Length || copy.ToSerializedString() != zone.ToSerializedString())
                        throw new Exception("Concrete snapshot mismatch");
                    var container = new TimeZoneEnvelope { Zone = zone, Tail = 137 };
                    byte[] nested = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(container);
                    TimeZoneEnvelope result = null;
                    LuminPackSerializer.Deserialize<LuminPackUtf8LengthOptions>(nested.AsSpan(), ref result);
                    if (result.Tail != 137 || result.Zone.ToSerializedString() != zone.ToSerializedString())
                        throw new Exception("Nested UTF8 snapshot mismatch");
                    string json = LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(zone);
                    copy = null;
                    LuminPackSerializer.DeserializeJson<LuminPackUtf8LengthOptions>(json, ref copy);
                    if (copy.ToSerializedString() != zone.ToSerializedString()) throw new Exception("JSON snapshot mismatch");
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview,
            preprocessorSymbols: ["NET8_0_OR_GREATER", "NET10_0_OR_GREATER", "NET11_0_OR_GREATER"]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("TimeZoneCache" + mode,
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
            var writer = methods.Single(method => method.Identifier.ValueText == "Serialize" &&
                method.TypeParameterList?.Parameters.Count == 1 && method.ParameterList.Parameters.Count == 1 &&
                method.ParameterList.Parameters[0].Type!.ToString().TrimEnd('?') == "global::System.TimeZoneInfo");
            Require(writer.Body!.ToString().Contains("writer.WriteTimeZoneText<__LuminProfile>", StringComparison.Ordinal) &&
                writer.Body.ToString().Contains("capacityReserved: true", StringComparison.Ordinal) &&
                writer.Body.ToString().Contains("default(__LuminProfile).StringRecording", StringComparison.Ordinal),
                "The exact snapshot entry lost its common framed core or compiled-profile guard in " + mode);
            var reader = methods.Single(method => method.Identifier.ValueText == "Deserialize" &&
                method.TypeParameterList?.Parameters.Count == 1 && method.ParameterList.Parameters.Count == 2 &&
                method.ParameterList.Parameters[0].Type!.ToString().StartsWith("ReadOnlySpan", StringComparison.Ordinal) &&
                method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::System.TimeZoneInfo");
            var readBody = reader.Body!;
            var sharedRead = readBody.DescendantNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault(call =>
                call.Expression is SimpleNameSyntax name && name.Identifier.ValueText.StartsWith("__LuminFacade", StringComparison.Ordinal));
            if (sharedRead?.Expression is SimpleNameSyntax sharedName)
                readBody = methods.Single(method => method.Identifier.ValueText == sharedName.Identifier.ValueText).Body!;
            Require(!readBody.ToString().Contains("Lease", StringComparison.Ordinal) &&
                readBody.ToString().Contains("new LuminPackReader(ref buffer)", StringComparison.Ordinal),
                "A stateless snapshot read acquired reference-tracking state in " + mode);
            var root = methods.Single(method => method.Identifier.ValueText == "RootRead" &&
                method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::System.TimeZoneInfo");
            Require(root.Body!.ToString().Contains("GetTimeZoneCandidate<__LuminProfile>", StringComparison.Ordinal) &&
                root.Body.ToString().Contains("TryReadTimeZoneInfo<__LuminProfile>", StringComparison.Ordinal) &&
                root.Body.ToString().Contains("LuminPackTimeZoneInfoCache.Parse", StringComparison.Ordinal),
                "A snapshot read lost its complete-content cache or original parse fallback in " + mode);
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("TimeZoneCases")!.GetMethod("Check")!.Invoke(null, null);
        }
        VerifyAsyncPinScope(references);
        Console.WriteLine("Passed time-zone concrete entries, compiled profiles, nested contracts and all four generation tiers.");
    }

    /// <summary>Keeps compatibility-runtime pins inside synchronous scopes of newer async consumers.</summary>
    /// <param name="references">The runtime and platform compilation references.</param>
    private static void VerifyAsyncPinScope(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using LuminPack.Core;
            public static class AsyncPinCases
            {
                public static async ValueTask<int> ReadAsync(ReadOnlyMemory<byte> memory)
                {
                    await Task.Yield();
                    var buffer = memory.Span;
                    var reader = new LuminPackReader(ref buffer);
                    reader.Advance(0);
                    return reader.Position;
                }
                public static async ValueTask<int> ReadLocalAsync(ReadOnlyMemory<byte> memory)
                {
                    await Task.Yield();
                    static int Read(ReadOnlyMemory<byte> input)
                    {
                        var buffer = input.Span;
                        var reader = new LuminPackReader(ref buffer);
                        return reader.Position;
                    }
                    return Read(memory);
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.CSharp13);
        var root = CSharpSyntaxTree.ParseText(source, parse).GetRoot();
        var projection = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.PinnedReaderProjection", throwOnError: true)!;
        var rewriter = (CSharpSyntaxRewriter)Activator.CreateInstance(projection,
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null, [true], culture: null)!;
        var rewritten = rewriter.Visit(root)!;
        Require(rewritten.DescendantNodes().OfType<MethodDeclarationSyntax>().All(method => !method.Modifiers.Any(SyntaxKind.UnsafeKeyword)) &&
            rewritten.DescendantNodes().OfType<UnsafeStatementSyntax>().Count() == 1 &&
            rewritten.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Single().Modifiers.Any(SyntaxKind.UnsafeKeyword),
            "An input pin made an enclosing async method unsafe or lost its synchronous local scope.");
        var compilation = CSharpCompilation.Create("AsyncPinScopes", [CSharpSyntaxTree.Create((CSharpSyntaxNode)rewritten, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
    }

    /// <summary>Reports the exact generated or runtime contract that failed.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
