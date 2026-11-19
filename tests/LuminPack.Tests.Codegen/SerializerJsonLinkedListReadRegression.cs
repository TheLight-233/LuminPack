extern alias ModelBasedAnalyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NextGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks which linked-list element contracts select JSON node refill.</summary>
internal static class SerializerJsonLinkedListReadRegression
{
    /// <summary>Checks node refill and record fallback in all generation modes.</summary>
    /// <param name="references">The platform and runtime compilation references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed class Item { public int Count; }
            public static class Entry
            {
                public static void Numbers(string json, ref LinkedList<int> value) =>
                    LuminPackSerializer.DeserializeJson(json, ref value);
                public static void LongNumbers(string json, ref LinkedList<long> value) =>
                    LuminPackSerializer.DeserializeJson(json, ref value);
                public static void Text(string json, ref LinkedList<string> value) =>
                    LuminPackSerializer.DeserializeJson(json, ref value);
                public static void Objects(string json, ref LinkedList<Item> value) =>
                    LuminPackSerializer.DeserializeJson(json, ref value);
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET10_0_OR_GREATER"])
            .WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        bool shared = false;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var input = CSharpCompilation.Create("LinkedListJson" + mode,
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")],
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new NextGenerator().AsSourceGenerator()], parseOptions: parse);
            driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            var errors = diagnostics.Concat(output.GetDiagnostics()).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            if (errors.Any())
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

            var selected = new HashSet<SpecialType>();
            foreach (var tree in output.SyntaxTrees.Skip(1))
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (method.Identifier.ValueText != "ReadExistingJson")
                    continue;
                var symbol = output.GetSemanticModel(tree).GetDeclaredSymbol(method);
                if (symbol?.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 1 } list ||
                    !selected.Add(list.TypeArguments[0].SpecialType) ||
                    !method.DescendantNodes().OfType<FinallyClauseSyntax>().Any())
                    throw new InvalidOperationException("The generated linked-list reuse body lacks its element contract or cleanup in " + mode);
            }
            if (selected.SetEquals([SpecialType.None, SpecialType.System_String]))
                shared = true;
            else if (!selected.SetEquals([SpecialType.System_Int32, SpecialType.System_Int64, SpecialType.System_String]))
                throw new InvalidOperationException("Unexpected linked-list JSON reuse eligibility in " + mode);
        }
        if (!shared)
            throw new InvalidOperationException("The linked-list JSON fixture did not exercise a shared primitive owner.");
        Console.WriteLine("Passed JSON linked-list node refill and record fallback in four generation modes.");
    }
}
