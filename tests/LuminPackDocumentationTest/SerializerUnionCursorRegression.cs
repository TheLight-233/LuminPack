using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects fixed-cursor qualification, original fallback boundaries and incremental invalidation.</summary>
internal static class SerializerUnionCursorRegression
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using LuminPack;
        using LuminPack.Attribute;
        [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable, LuminPackUnion(1, typeof(Fixed)), LuminPackUnion(2, typeof(Text)),
         LuminPackUnion(3, typeof(Private)), LuminPackUnion(4, typeof(Callback)),
         LuminPackUnion(5, typeof(Specialized)), LuminPackUnion(6, typeof(Property))]
        public abstract partial class Root { }
        [LuminPackable] public sealed partial class Text : Root { public string Value; }
        [LuminPackable] public sealed partial class Private : Root { [LuminPackInclude] private int Number = 7; }
        [LuminPackable] public sealed partial class Callback : Root
        {
            public int Number;
            [LuminPackOnSerializing] public static void Before() { }
        }
        [LuminPackable] public sealed partial class Property : Root { public int Number { get; set; } }
        [LuminPackable(GeneratorType.VersionTolerant)] public partial struct Versioned { [LuminPackOrder(1)] public int Number; }
        [LuminPackable] public sealed partial class Specialized : Root { public Versioned Value; }
        [LuminPackable, LuminPackWideTag, LuminPackUnion(60000, typeof(WideCase))] public abstract partial class Wide { }
        [LuminPackable] public sealed partial class WideCase : Wide { public int Number; }
        [LuminPackable, LuminPackUnion(1, typeof(GenericCase<int>))] public abstract partial class GenericRoot<T> { }
        [LuminPackable] public sealed partial class GenericCase<T> : GenericRoot<T> { public T Value; }
        [LuminPackable, LuminPackUnion(1, typeof(UnusedCase))] public abstract partial class UnusedRoot { }
        [LuminPackable] public sealed partial class UnusedCase : UnusedRoot { public int Number; }
        [LuminPackable] public sealed class Graph { public Dictionary<int, Root> Values; }
        public static class Entry
        {
            public static void Check()
            {
                var graph = new Graph { Values = new Dictionary<int, Root> { [1] = new Fixed { Number = 11 }, [2] = new Text { Value = "text" } } };
                byte[] bytes = LuminPackSerializer.Serialize(graph);
                var copy = LuminPackSerializer.Deserialize<Graph>(bytes);
                if (((Fixed)copy.Values[1]).Number != 11 || ((Text)copy.Values[2]).Value != "text" ||
                    !LuminPackSerializer.Serialize(copy).AsSpan().SequenceEqual(bytes)) throw new Exception("Mixed Union wire or field data changed.");
                _ = LuminPackSerializer.Serialize(new Dictionary<int, Wide> { [1] = new WideCase { Number = 19 } });
                _ = LuminPackSerializer.Serialize(new Dictionary<int, GenericRoot<int>> { [1] = new GenericCase<int> { Value = 23 } });
            }
        }
        """;

    /// <summary>Checks all pruning modes and the registration-free unsafe-disabled configuration.</summary>
    /// <param name="references">Current platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Light", "Minimal" })
        {
            var before = Compilation(references, mode, true);
            var driver = Driver().RunGeneratorsAndUpdateCompilation(before, out var output, out var diagnostics);
            Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
            var shared = methods.Single(method => method.Identifier.ValueText == "SerializeUnionAt" &&
                method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::Fixed");
            Require(shared.ReturnType.ToString() == "int" && shared.GetLeadingTrivia().ToFullString().Contains("absolute next byte index", StringComparison.Ordinal),
                "The shared body lost its absolute-cursor XML contract.");
            Require(!methods.Any(method => method.Identifier.ValueText == "SerializeUnionAt" &&
                method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') is "global::Private" or "global::Callback" or "global::Specialized" or "global::Property"),
                "A private, callback, property or versioned payload entered the raw cursor body.");
            var cursorSlots = methods.Where(method => method.Identifier.ValueText.StartsWith("__LuminPackUnionSerializeCursor_", StringComparison.Ordinal)).ToArray();
            Require(cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Root" } && method.Modifiers.Any(SyntaxKind.VirtualKeyword)) &&
                cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Fixed" } && method.Modifiers.Any(SyntaxKind.OverrideKeyword)),
                "The fixed path omitted its virtual root or exact case override.");
            Require(!cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax parent && parent.Identifier.ValueText is
                "Wide" or "WideCase" or "GenericRoot" or "GenericCase"),
                "A wide/generic root acquired a cursor slot.");
            foreach (var forwarding in cursorSlots.Where(method => method.Parent is ClassDeclarationSyntax parent &&
                parent.Identifier.ValueText is "Private" or "Callback" or "Specialized" or "Property" or "Text"))
                Require(forwarding.Body!.Statements.Count == 3 && forwarding.Body.Statements[0].ToString().Contains("writer.CurrentIndex = initialIndex", StringComparison.Ordinal) &&
                    forwarding.Body.Statements[1].ToString().Contains("__LuminPackUnionSerialize_", StringComparison.Ordinal) &&
                    forwarding.Body.Statements[2].ToString().Contains("writer.GetCurrentSpanIndex()", StringComparison.Ordinal),
                    "A non-fixed case bypassed publication or its original virtual operation.");
            Require(methods.Any(method => method.Body?.ToString().Contains("__LuminPackUnionSerializeCursor_", StringComparison.Ordinal) == true &&
                method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" }), "The retained dictionary did not reach the fixed virtual protocol.");
            if (mode == "Minimal") Require(!cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax parent && parent.Identifier.ValueText is "UnusedRoot" or "UnusedCase"),
                "Minimal retained an unused Union cursor family.");
            Execute(output);

            var leaf = before.SyntaxTrees.Single(tree => tree.FilePath == "Fixed.cs");
            var after = before.ReplaceSyntaxTree(leaf, CSharpSyntaxTree.ParseText(leaf.ToString().Replace("public int Number", "public volatile int Number"),
                new CSharpParseOptions(LanguageVersion.CSharp13), "Fixed.cs"));
            var retained = driver.RunGeneratorsAndUpdateCompilation(after, out var updated, out diagnostics);
            var fresh = Driver().RunGenerators(after);
            Require(Files(retained).SequenceEqual(Files(fresh)), "A fixed-to-volatile edit retained stale nested Union cursor output.");
            Require(!retained.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Any(source =>
                source.SourceText.ToString().Contains("__LuminPackUnionSerializeCursor_", StringComparison.Ordinal) && source.SourceText.ToString().Contains("globalRoot_", StringComparison.Ordinal)),
                "The last fixed case became volatile but its root cursor protocol survived.");
            Execute(updated);
        }
        var safe = Driver().RunGeneratorsAndUpdateCompilation(Compilation(references, "Minimal", false), out var safeOutput, out _);
        Require(!safe.GetRunResult().Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "Unsafe-disabled cursor generation failed.");
        Execute(safeOutput);
        Console.WriteLine("Passed fixed Union cursor XML, public/private/property/callback/versioned guards, wide/generic fallback, pruning modes, unsafe-disabled output and retained/fresh volatility edits.");
    }

    /// <summary>Creates independent root and leaf trees so nested dependency guards must observe edits.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="mode">The actual generation mode.</param>
    /// <param name="allowUnsafe">Whether manual registration is available.</param>
    /// <returns>The unchanged mixed Union fixture.</returns>
    private static CSharpCompilation Compilation(MetadataReference[] references, string mode, bool allowUnsafe)
        => CSharpCompilation.Create("UnionCursor_" + mode + "_" + Guid.NewGuid().ToString("N"), new[]
        {
            CSharpSyntaxTree.ParseText(Source.Replace("MODE", mode), new CSharpParseOptions(LanguageVersion.CSharp13), "Root.cs"),
            CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public sealed partial class Fixed : Root { public int Number; }", new CSharpParseOptions(LanguageVersion.CSharp13), "Fixed.cs")
        }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe, optimizationLevel: OptimizationLevel.Release));

    /// <summary>Creates a fresh pair of incremental generators under the fixture's language version.</summary>
    /// <returns>A driver that can be retained across a leaf edit.</returns>
    private static GeneratorDriver Driver() => CSharpGeneratorDriver.Create(new[]
    {
        new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()
    }, parseOptions: new CSharpParseOptions(LanguageVersion.CSharp13));

    /// <summary>Orders the exact generated files for retained-versus-fresh comparisons.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>Stable file identities and complete source text.</returns>
    private static string[] Files(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    /// <summary>Compiles and executes the actual generated mixed-case round trip.</summary>
    /// <param name="compilation">The completed consumer output.</param>
    private static void Execute(Compilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Reports a structural, documentation or runtime regression.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure explanation.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
