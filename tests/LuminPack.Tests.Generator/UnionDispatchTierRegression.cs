using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks tiered Union writes and per-element dispatch for contiguous collections.</summary>
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
        Require(direct.Contains("Unsafe.As<global::Only>(value).LuminPackUnionSerialize_") && !direct.Contains("case global::Only"),
            "A closed single-case root without registration must call its case directly.", direct);
        string legacySingle = Compiled(Generate(single, register: false, modern: false), "Root");
        Require(legacySingle.Contains("case global::Only ") && legacySingle.Contains("default:") &&
            !legacySingle.Contains("Unsafe.As<global::Only>"), "A portable single-case root must use its exact pattern.", legacySingle);

        // Registration keeps the virtual fallback behind an exact guard.
        string guardedSingle = Compiled(Generate(single, register: true), "Root");
        Require(guardedSingle.Contains("case global::Only ") && guardedSingle.Contains("value.LuminPackUnionSerialize_"),
            "A registrable single-case root must guard its case before the virtual fallback.", guardedSingle);

        // Another assembly can derive from a public root with a public constructor, so it keeps the guard.
        string open = Compiled(Generate(single.Replace("internal abstract", "public abstract").Replace("internal sealed", "public sealed"), register: false), "Root");
        Require(open.Contains("case global::Only "), "An externally derivable root must keep the exact guard.", open);

        // SingleImplementation closes a public root.
        string declared = Compiled(Generate(single.Replace("internal abstract", "[LuminPackOption(SingleImplementation = true)] public abstract")
            .Replace("internal sealed", "public sealed"), register: false), "Root");
        Require(declared.Contains("Unsafe.As<global::Only>") && !declared.Contains("case global::Only"), "SingleImplementation must allow the direct call.", declared);

        // A second, unpackable derived type in the compilation keeps the guard.
        string derived = Compiled(Generate(single + "\ninternal sealed class Other : Root { }", register: false), "Root");
        Require(derived.Contains("case global::Only "), "Another derived source type must keep the exact guard.", derived);
        VerifyClosureEdit(single);

        string nonsealed = Compiled(Generate(single.Replace("sealed partial", "partial"), register: true), "Root");
        Require(nonsealed.Contains("case global::Only caseValue0 when ((object)value).GetType() == typeof(global::Only):"),
            "An assignable subtype must pass through the existing virtual fallback.", nonsealed);

        string cases(int count) => "using LuminPack.Attribute;\n[LuminPackable] public abstract partial class Many { public int Id; }\n" +
            string.Concat(Enumerable.Range(0, count).Select(index => $"[LuminPackable] public sealed partial class Case{index} : Many {{ public int Value; }}\n")) +
            "[LuminPackable] public partial class Holder { public System.Collections.Generic.List<Many> Items = new(); public Many[] ArrayItems; public System.Collections.Immutable.ImmutableArray<Many> ImmutableItems; }\n";
        string four = Compiled(Generate(cases(4), register: true), "Many");
        Require(Enumerable.Range(0, 4).All(index => four.Split('\n').Any(line =>
            line.Contains($"case global::Case{index} ") && line.Contains("(ref writer); return;"))),
            "Four cases must use one exact pattern and call per line.", four);
        string five = Compiled(Generate(cases(5), register: true), "Many");
        Require(!five.Contains("case global::Case") && five.Contains("value.LuminPackUnionSerialize_"),
            "More than four cases on .NET 8+ must keep the single virtual call.", five);
        string portableFive = Compiled(Generate(cases(5), register: true, modern: false), "Many");
        Require(Enumerable.Range(0, 5).All(index => portableFive.Contains($"case global::Case{index} ")),
            "All portable cases must use exact patterns.", portableFive);

        string collections = Generate(cases(5), register: true);
        Require(!collections.Contains("LuminPackUnionPartition") && !collections.Contains("LuminUnionPartitionMap"),
            "Fixed Union lists, arrays and immutable arrays must retain per-element dispatch.", collections);

        string ordinaryAliases = Generate(single, register: false);
        Require(ordinaryAliases.Contains("using Unsafe = global::System.Runtime.CompilerServices.Unsafe;"),
            "A qualified compiler helper should use its generated alias.", ordinaryAliases);
        // A declaration in another namespace reserves the spelling without changing what unqualified generated names bind to.
        string collision = Generate(single + "\nnamespace Other { public sealed class Unsafe { } }", register: false);
        Require(!collision.Contains("using Unsafe =") && collision.Contains("global::System.Runtime.CompilerServices.Unsafe.As<global::Only>"),
            "A consumer declaration must reserve its spelling before aliases are added.", collision);
    }

    /// <summary>Runs both generators over one source file.</summary>
    /// <param name="source">The consumer source.</param>
    /// <param name="register">Whether registration APIs are generated.</param>
    /// <param name="modern">Whether the target defines the .NET 8 or later feature symbol.</param>
    /// <returns>All generated sources.</returns>
    private static string Generate(string source, bool register, bool modern = true)
    {
        // Assembly attributes follow the source's using directive.
        if (!register) source = DisableRegistration(source);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols(modern ? "NET8_0_OR_GREATER" : "NETSTANDARD2_1");
        var compilation = CSharpCompilation.Create("UnionDispatchTiers", new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            CompatibilityCompilationReferences.Create(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        return GeneratedText(Driver(parseOptions).RunGenerators(compilation));
    }

    /// <summary>Checks that an existing unannotated class opening a root invalidates retained output.</summary>
    /// <param name="single">The model with a closed single-case root.</param>
    private static void VerifyClosureEdit(string single)
    {
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("NET8_0_OR_GREATER");
        var original = CSharpSyntaxTree.ParseText("internal sealed class Other { }", options, "Other.cs");
        var model = CSharpSyntaxTree.ParseText(DisableRegistration(single) + """

            [LuminPackable] internal sealed class Holder
            {
                public System.Collections.Generic.Dictionary<int, Root> Values = new();
            }
            """, options, "Models.cs");
        var before = CSharpCompilation.Create("UnionClosureEdit", new[] { model, original }, CompatibilityCompilationReferences.Create(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var initial = Driver(options).RunGenerators(before);
        var after = before.ReplaceSyntaxTree(original, CSharpSyntaxTree.ParseText("internal sealed class Other : Root { }", options, "Other.cs"));
        string retained = GeneratedText(initial.RunGenerators(after));
        string fresh = GeneratedText(Driver(options).RunGenerators(after));
        Require(retained == fresh && Compiled(retained, "Root").Contains("case global::Only "),
            "Opening the root in another file must replace its retained direct calls with exact patterns.", Compiled(retained, "Root"));
    }

    private static string DisableRegistration(string source) => source.Replace("using LuminPack.Attribute;",
        "using LuminPack.Attribute;\n[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]");

    private static GeneratorDriver Driver(CSharpParseOptions options) => CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() },
        parseOptions: options);

    private static string GeneratedText(GeneratorDriver driver)
    {
        var result = driver.GetRunResult();
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
        string marker = "LuminPackUnionSerialize_global" + root + "_";
        var methods = CSharpSyntaxTree.ParseText(generated).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText.StartsWith(marker, StringComparison.Ordinal) &&
                (method.Identifier.ValueText.EndsWith("_Compiled", StringComparison.Ordinal) ||
                 method.Identifier.ValueText.EndsWith("_Dispatch", StringComparison.Ordinal))).ToArray();
        if (methods.Length == 0) throw new InvalidOperationException("Missing the compiled write slot of " + root + ".");
        return string.Join("\n", methods.Select(method => method.ToFullString()));
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
