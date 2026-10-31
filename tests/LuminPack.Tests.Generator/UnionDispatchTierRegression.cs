using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks the tiered Union write dispatch and the adaptive partition eligibility of List members.</summary>
internal static class UnionDispatchTierRegression
{
    /// <summary>Runs every tier scenario.</summary>
    public static void Run()
    {
        const string single = """
            using LuminPack.Attribute;
            [LuminPackable] internal abstract partial class Root { public int Id; }
            [LuminPackable] internal sealed partial class Only : Root { public int Value; }
            """;
        // Without registration, a closed root with one sealed case calls the case directly.
        string direct = Compiled(Generate(single, register: false), "Root");
        Require(direct.Contains("Unsafe.As<global::Only>(value).__LuminPackUnionSerialize_") && !direct.Contains("value is global::Only"),
            "A closed single-case root without registration must call its case directly.", direct);

        // Registration keeps the virtual fallback behind an exact guard.
        string guardedSingle = Compiled(Generate(single, register: true), "Root");
        Require(guardedSingle.Contains("if (value is global::Only)") && guardedSingle.Contains("value.__LuminPackUnionSerialize_"),
            "A registrable single-case root must guard its case before the virtual fallback.", guardedSingle);

        // Another assembly can derive from a public root with a public constructor, so it keeps the guard.
        string open = Compiled(Generate(single.Replace("internal abstract", "public abstract").Replace("internal sealed", "public sealed"), register: false), "Root");
        Require(open.Contains("if (value is global::Only)"), "An externally derivable root must keep the exact guard.", open);

        // SingleImplementation closes a public root.
        string declared = Compiled(Generate(single.Replace("internal abstract", "[LuminPackOption(SingleImplementation = true)] public abstract")
            .Replace("internal sealed", "public sealed"), register: false), "Root");
        Require(!declared.Contains("value is global::Only"), "SingleImplementation must allow the direct call.", declared);

        // A second, unpackable derived type in the compilation keeps the guard.
        string derived = Compiled(Generate(single + "\ninternal sealed class Other : Root { }", register: false), "Root");
        Require(derived.Contains("if (value is global::Only)"), "Another derived source type must keep the exact guard.", derived);

        string cases(int count) => "using LuminPack.Attribute;\n[LuminPackable] public abstract partial class Many { public int Id; }\n" +
            string.Concat(Enumerable.Range(0, count).Select(index => $"[LuminPackable] public sealed partial class Case{index} : Many {{ public int Value; }}\n")) +
            "[LuminPackable] public partial class Holder { public System.Collections.Generic.List<Many> Items = new(); }\n";
        string four = Compiled(Generate(cases(4), register: true), "Many");
        Require(Enumerable.Range(0, 4).All(index => four.Contains($"if (value is global::Case{index})")), "Four cases must all be guarded.", four);
        string five = Compiled(Generate(cases(5), register: true), "Many");
        Require(!five.Contains("value is global::Case"), "More than four cases must keep the single virtual call.", five);

        // Portable targets keep per-element List dispatch; modern-target partition output and its records are covered by
        // UnionPartitionTest in a real build, which also binds the inlined entry operations this harness cannot bind.
        string portable = Generate(cases(5), register: true);
        Require(!portable.Contains("__LuminPackUnionPartition"), "netstandard2.1 must keep per-element List dispatch.", "");
    }

    /// <summary>Runs both generators over one source file.</summary>
    /// <param name="source">The consumer source.</param>
    /// <param name="register">Whether registration APIs are generated.</param>
    /// <returns>All generated sources.</returns>
    private static string Generate(string source, bool register)
    {
        // Assembly attributes follow the source's using directive.
        if (!register) source = source.Replace("using LuminPack.Attribute;",
            "using LuminPack.Attribute;\n[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Full, LuminPackRegisterMode.Disabled)]");
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("NETSTANDARD2_1");
        var compilation = CSharpCompilation.Create("UnionDispatchTiers", new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            CompatibilityCompilationReferences.Create(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var result = CSharpGeneratorDriver.Create(new[] { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() },
            parseOptions: parseOptions).RunGenerators(compilation).GetRunResult();
        if (result.Results.FirstOrDefault(item => item.Exception is not null).Exception is { } exception)
            throw new InvalidOperationException("Union tier generation failed.", exception);
        var errors = result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(diagnostic => diagnostic.ToString())));
        if (Environment.GetEnvironmentVariable("LUMINPACK_MECHANISM_CAPTURE") is { Length: > 0 } capture)
        {
            Directory.CreateDirectory(capture);
            foreach (var file in result.Results.SelectMany(item => item.GeneratedSources))
                File.WriteAllText(Path.Combine(capture, file.HintName), file.SourceText.ToString());
        }
        return string.Join("\n", result.Results.SelectMany(item => item.GeneratedSources).Select(item => item.SourceText.ToString())).Replace("\r\n", "\n");
    }

    /// <summary>Extracts the compiled binary write slot of a root.</summary>
    /// <param name="generated">All generated sources.</param>
    /// <param name="root">The root's simple name.</param>
    /// <returns>The compiled slot's text.</returns>
    private static string Compiled(string generated, string root)
    {
        string marker = "public static void __LuminPackUnionSerialize_global" + root + "_";
        int start = generated.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("Missing the compiled write slot of " + root + ".");
        int end = generated.IndexOf("ThrowInvalidOperationException", start, StringComparison.Ordinal);
        return generated.Substring(start, end - start);
    }

    /// <summary>Fails with the inspected text.</summary>
    /// <param name="condition">The checked condition.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="text">The inspected generated text.</param>
    private static void Require(bool condition, string message, string text)
    {
        if (!condition) throw new InvalidOperationException(message + Environment.NewLine + text);
    }
}
