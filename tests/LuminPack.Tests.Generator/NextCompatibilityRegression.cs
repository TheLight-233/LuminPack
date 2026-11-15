using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using LuminPack.Build;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;

/// <summary>Verifies Next against the pointer runtime with the Roslyn 4.3 compiler.</summary>
internal static class NextCompatibilityRegression
{
    /// <summary>Compiles C# 9 and 10 fixtures and executes their IL-rewritten and registered entries.</summary>
    internal static void Run()
    {
        MetadataReference[] references = CompatibilityCompilationReferences.Create();
        string[] paths = Array.ConvertAll(references, reference => ((PortableExecutableReference)reference).FilePath!);
        string runtime = Array.Find(paths, path => Path.GetFileName(path) == "LuminPack.Core.dll")!;
        var context = new AssemblyLoadContext("NextPortableCompatibility", isCollectible: true);
        try
        {
            context.LoadFromAssemblyPath(runtime);
            foreach (LanguageVersion language in new[] { LanguageVersion.CSharp9, LanguageVersion.CSharp10 })
            foreach (bool rewrite in new[] { false, true })
            {
                var parse = new CSharpParseOptions(language);
                if (rewrite)
                    parse = parse.WithPreprocessorSymbols("LUMINPACK_IL_REWRITE");
                string name = "NextPortable_" + language + (rewrite ? "_IL" : "_Registered");
                string source = Fixture.Replace("REGISTRATION", rewrite ? "Disabled" : "Auto");
                var tree = CSharpSyntaxTree.ParseText(source, parse, name + ".cs", Encoding.UTF8);
                var input = CSharpCompilation.Create(name, new[] { tree }, references,
                    new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
                GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { NextAnalyzer.Create() }, parseOptions: parse)
                    .RunGeneratorsAndUpdateCompilation(input, out Compilation output, out var diagnostics);
                foreach (Diagnostic diagnostic in diagnostics)
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                        throw new InvalidOperationException(name + ": " + diagnostic);
                foreach (GeneratorRunResult result in driver.GetRunResult().Results)
                {
                    if (result.Exception != null)
                        throw new InvalidOperationException(name, result.Exception);
                    foreach (GeneratedSourceResult file in result.GeneratedSources)
                        if (file.HintName.IndexOf('/') >= 0)
                            throw new InvalidOperationException(name + ": a portable source hint retained a directory separator.");

                    var hints = result.GeneratedSources.Select(file => file.HintName).ToHashSet(StringComparer.Ordinal);
                    if (!hints.Contains("LuminPack.SourceGenerator.LuminPackSourceGenerator.LuminPackGen.LuminPackSerializer.g.cs") ||
                        !hints.Contains("LuminPack.SourceGenerator.LuminPackJsonGenerator.LuminPackGen.LuminPackSerializer.g.cs") ||
                        !hints.Contains("LuminPack.SourceGenerator.LuminPackFormatterGenerator.LuminPackGen.FormatterDispatch.g.cs"))
                        throw new InvalidOperationException(name + ": a portable source hint is missing its legacy category prefix.");
                }
                VerifyScalarArrayReservation(driver, name);
                using var pe = new MemoryStream();
                using var pdb = new MemoryStream();
                var emitted = output.Emit(pe, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
                if (!emitted.Success)
                    throw new InvalidOperationException(name + ": " + string.Join(Environment.NewLine, emitted.Diagnostics));
                byte[] image = pe.ToArray(), symbols = pdb.ToArray();
                var rewritten = StaticCallAssembly.Rewrite(image, symbols, paths);
                if (rewrite ? rewritten is not { Calls: 4, Pdb.Length: > 0 } : rewritten != null)
                    throw new InvalidOperationException(name + ": unexpected static rewrite.");
                using var execution = new MemoryStream(rewritten?.Pe ?? image);
                var assembly = context.LoadFromStream(execution);
                if (assembly.GetType("Entry")!.GetMethod("Exercise", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null) is not true)
                    throw new InvalidOperationException(name + ": binary or JSON round trip failed.");
            }
        }
        finally
        {
            context.Unload();
        }
        Console.WriteLine("Next portable compatibility: four C# 9/10 and Roslyn 4.3 backend combinations passed.");
    }

    /// <summary>Checks that scalar arrays consume the same reservation in the portable record body.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <param name="name">The fixture configuration.</param>
    private static void VerifyScalarArrayReservation(GeneratorDriver driver, string name)
    {
        var write = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Where(file => file.HintName.Contains("Payload", StringComparison.Ordinal))
            .SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Select(method => method.Body?.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(call => call.Expression is MemberAccessExpressionSyntax).ToArray())
            .FirstOrDefault(calls => calls?.Any(call => Name(call) == "ReserveRecord") == true);
        if (write == null || write.Count(call => Name(call) == "WriteReservedSpanInline") != 2 ||
            write.Any(call => Name(call) == "WriteRecordSpan") ||
            !write.Any(call => Name(call) == "ReserveRecord" &&
                call.ArgumentList.Arguments[1].Expression.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                    .Count(term => term.Name.Identifier.ValueText == "Bound") == 2))
            throw new InvalidOperationException(name + ": the scalar arrays lost their shared reservation." + Environment.NewLine +
                (write == null ? "No reservation was emitted." : string.Join(Environment.NewLine, write.Select(call => call.ToString()))));

        static string Name(InvocationExpressionSyntax call) => ((MemberAccessExpressionSyntax)call.Expression).Name.Identifier.ValueText;
    }

    private const string Fixture = """
        using System;
        using System.Linq;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        [assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.REGISTRATION)]

        [LuminPackable]
        public sealed class Payload
        {
            public long Wide;
            public int[] Numbers;
            public bool[] Flags;
            public int? Optional;
            public string Text;
        }

        public static class Entry
        {
            public static void Main() => Exercise();

            public static bool Exercise()
            {
                foreach (int[] numbers in new[] { null, Array.Empty<int>(), Enumerable.Range(0, 20000).ToArray() })
                {
                    var value = new Payload { Wide = 0x102030405060708L, Numbers = numbers, Flags = new[] { true, false }, Optional = 23, Text = "\u4E2D" };
                    byte[] bytes = LuminPackSerializer.Serialize<Payload, LuminPackUtf8VarIntOptions>(value);
                    var binary = LuminPackSerializer.Deserialize<Payload, LuminPackUtf8VarIntOptions>(bytes);
                    string text = LuminPackSerializer.SerializeJson<Payload, LuminPackUtf8LengthOptions>(value);
                    var json = LuminPackSerializer.DeserializeJson<Payload, LuminPackUtf8LengthOptions>(text);
                    if (binary.Wide != value.Wide || binary.Optional != 23 || binary.Text != value.Text ||
                        (numbers == null ? binary.Numbers != null : !numbers.SequenceEqual(binary.Numbers)) ||
                        !value.Flags.SequenceEqual(binary.Flags) ||
                        json.Wide != value.Wide || json.Optional != 23 || json.Text != value.Text)
                        return false;
                }
                return true;
            }
        }
        """;
}
