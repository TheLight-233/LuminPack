using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Verifies that readable generated local names preserve symbol bindings, executable behavior and Release IL.</summary>
internal static class SerializerOperationNamesRegression
{
    /// <summary>Checks role restoration without renaming public members, support declarations or name-observed locals.</summary>
    /// <param name="references">Runtime and platform compilation references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            public sealed class Item { public int offset = 7; public int __lpCollision = 11; }
            public static class Entry
            {
                private static int __LuminLayout0 = 13;
                private static int Calls;
                private static int Touch(ref int value) { Calls++; return ++value; }
                public static int Check()
                {
                    int offset = 3;
                    int __lpOffset = 5;
                    int __lpResult = Touch(ref __lpOffset);
                    int __lpCollision = 17;
                    int __lpKeyword = 19;
                    int __lpFixed = 23;
                    int __lpNamed = 29;
                    int __lpPartial = 31;
                    var item = new Item();
                    ref int __lpAlias = ref __lpResult;
                    __lpAlias += item.offset;
                    if (Calls != 0) goto __lpExit;
                    __lpResult = 0;
                    __lpExit:
                    return offset + __lpOffset + __lpResult + __lpCollision + item.__lpCollision + __lpKeyword + __lpFixed +
                        __lpNamed + nameof(__lpNamed).Length + __lpPartial + __LuminLayout0 + Calls;
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(source, parse);
        var compilation = CSharpCompilation.Create("ReadableNamesOriginal", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
        var model = compilation.GetSemanticModel(tree);
        var roles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__lpOffset"] = "offset", ["__lpResult"] = "result", ["__lpCollision"] = "collision",
            ["__lpKeyword"] = "class", ["__lpFixed"] = "__luminFixedBlock", ["__lpNamed"] = "named",
            ["__lpPartial"] = "partial", ["__lpAlias"] = "result", ["__lpExit"] = "end", ["__LuminLayout0"] = "cache"
        };
        var marked = tree.GetRoot().ReplaceTokens(tree.GetRoot().DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)), (token, _) =>
        {
            ISymbol? symbol = token.Parent switch
            {
                VariableDeclaratorSyntax declaration => model.GetDeclaredSymbol(declaration),
                LabeledStatementSyntax label => model.GetDeclaredSymbol(label),
                IdentifierNameSyntax identifier => model.GetSymbolInfo(identifier).Symbol,
                _ => null
            };
            if (symbol is not (ILocalSymbol or ILabelSymbol) && symbol?.Name != "__LuminLayout0" ||
                !roles.TryGetValue(token.ValueText, out string? role) || token.ValueText == "__lpPartial" && token.Parent is not VariableDeclaratorSyntax)
                return token;
            return token.WithAdditionalAnnotations(new SyntaxAnnotation("LuminPack.GeneratedLocalRole", role));
        });
        var helper = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.GeneratedOperationNames", throwOnError: true)!;
        var rewrite = helper.GetMethod("Rewrite", BindingFlags.Static | BindingFlags.NonPublic)!;
        var rewritten = (SyntaxNode)rewrite.Invoke(null, [marked])!;
        var repeated = (SyntaxNode)rewrite.Invoke(null, [marked])!;
        Require(rewritten.ToFullString() == repeated.ToFullString(), "Generated local names were nondeterministic.");
        var method = rewritten.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(declaration => declaration.Identifier.ValueText == "Check");
        var locals = method.DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(variable => variable.Identifier.ValueText).ToArray();
        Require(locals.Contains("offset2") && locals.Contains("result") && locals.Contains("result2") && locals.Contains("fixedBlock") && locals.Contains("local"),
            "Readable role names, deterministic suffixes or keyword sanitization were omitted.");
        Require(locals.Contains("__lpCollision") && locals.Contains("__lpNamed") && locals.Contains("__lpPartial"),
            "A user-name collision, nameof observation or incomplete compiler identity was renamed.");
        Require(method.DescendantNodes().OfType<LabeledStatementSyntax>().Single().Identifier.ValueText == "end0",
            "A generated exit label did not acquire a short readable name.");
        Require(rewritten.DescendantNodes().OfType<FieldDeclarationSyntax>().SelectMany(field => field.Declaration.Variables).Any(field => field.Identifier.ValueText == "__LuminLayout0"),
            "A support field was renamed by a local presentation pass.");
        var changed = CSharpSyntaxTree.Create((CSharpSyntaxNode)rewritten, parse);
        var original = Load(compilation);
        var candidate = Load(compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(changed).WithAssemblyName("ReadableNamesCandidate"));
        foreach (var type in original.GetTypes())
        {
            var renamedType = candidate.GetType(type.FullName!)!;
            const BindingFlags members = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Require(type.GetFields(members).Select(field => field.Name).SequenceEqual(renamedType.GetFields(members).Select(field => field.Name)),
                "Readable local names changed a field API or support identity.");
            foreach (var originalMethod in type.GetMethods(members))
            {
                var renamedMethod = renamedType.GetMethod(originalMethod.Name, members)!;
                Require(originalMethod.GetMethodBody()!.GetILAsByteArray()!.SequenceEqual(renamedMethod.GetMethodBody()!.GetILAsByteArray()!),
                    "Readable local names changed Release IL for " + originalMethod.Name + ".");
            }
        }
        Require(Equals(original.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null), candidate.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)),
            "Readable generated names changed storage, side effects or state.");
    }

    /// <summary>Compiles one presentation fixture for exact IL and execution comparison.</summary>
    /// <param name="compilation">The original or renamed compilation.</param>
    /// <returns>The loaded fixture assembly.</returns>
    private static Assembly Load(CSharpCompilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return Assembly.Load(image.ToArray());
    }

    /// <summary>Reports a local naming or executable-code regression.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
