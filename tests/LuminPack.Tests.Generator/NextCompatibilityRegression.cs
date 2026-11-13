using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using LuminPack.Build;
using LuminPack.SourceGenerator.Next.Pipeline;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
                GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: parse)
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
                }
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

    private const string Fixture = """
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        [assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.REGISTRATION)]

        [LuminPackable]
        public sealed class Payload
        {
            public long Wide;
            public int? Optional;
            public string Text;
        }

        public static class Entry
        {
            public static void Main() => Exercise();

            public static bool Exercise()
            {
                var value = new Payload { Wide = 0x102030405060708L, Optional = 23, Text = "\u4E2D" };
                byte[] bytes = LuminPackSerializer.Serialize<Payload, LuminPackUtf8VarIntOptions>(value);
                var binary = LuminPackSerializer.Deserialize<Payload, LuminPackUtf8VarIntOptions>(bytes);
                string text = LuminPackSerializer.SerializeJson<Payload, LuminPackUtf8LengthOptions>(value);
                var json = LuminPackSerializer.DeserializeJson<Payload, LuminPackUtf8LengthOptions>(text);
                return binary.Wide == value.Wide && binary.Optional == 23 && binary.Text == value.Text &&
                    json.Wide == value.Wide && json.Optional == 23 && json.Text == value.Text;
            }
        }
        """;
}
