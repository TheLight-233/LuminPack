using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Executes the storage and evaluation boundaries of expanded binary reader alias removal.</summary>
internal static class SerializerPayloadAliasRegression
{
    /// <summary>Checks stable locals, ref rebinding, side effects, pool storage and user-name collisions.</summary>
    /// <param name="references">Runtime and platform compilation references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            #nullable enable
            public sealed class Holder { public int __lpAlias; }
            public static class Cases
            {
                private static int slot;
                private static int calls;
                private static ref int Rent() { calls++; return ref slot; }
                public static int Stable() { int target = 2; ref int __lpAlias = ref target; __lpAlias += 3; return target; }
                public static int Chain() { int target = 2; ref int __lpAlias = ref target; ref int __lpAlias2 = ref __lpAlias; __lpAlias2 += 5; return target; }
                public static int ReboundAlias() { int target = 2, other = 3; ref int __lpAlias = ref target; __lpAlias = ref other; __lpAlias = 7; return target * 10 + other; }
                public static int ReboundSource() { int first = 2, second = 3; ref int target = ref first; ref int __lpAlias = ref target; target = ref second; __lpAlias = 7; return first * 10 + second; }
                public static int ReboundChain() { int first = 2, second = 3; ref int __lpAlias = ref first; ref int __lpAlias2 = ref __lpAlias; __lpAlias = ref second; __lpAlias2 = 7; return first * 10 + second; }
                public static int StableChainSource() { int first = 2, second = 3; ref int target = ref first; ref int __lpAlias = ref target; ref int __lpAlias2 = ref __lpAlias; target = ref second; __lpAlias2 = 7; return first * 10 + second; }
                public static int RefPool() { slot = 3; calls = 0; int target = 2; ref int __lpAlias = ref target; __lpAlias = ref Rent(); __lpAlias = 7; return target * 100 + slot * 10 + calls; }
                public static int RefReturn() { slot = 3; calls = 0; ref int __lpAlias = ref Rent(); __lpAlias += 2; return slot * 10 + calls; }
                public static int Field() { var target = new Holder { __lpAlias = 2 }; ref int __lpAlias = ref target.__lpAlias; __lpAlias += 3; return target.__lpAlias; }
                public static int Index() { int index = 0; int[] target = { 2, 3 }; ref int __lpAlias = ref target[index++]; __lpAlias += 3; return target[0] * 10 + index; }
                public static int Readonly() { int target = 2; ref readonly int __lpAlias = ref target; return __lpAlias; }
                public static int Observed() { int target = 2; ref int __lpAlias = ref target; return nameof(__lpAlias).Length + __lpAlias; }
                public static int Unmarked() { int target = 2; ref int __lpAlias = ref target; __lpAlias += 3; return target; }
                public static int UnmarkedSource() { int target = 2; ref int __lpAlias = ref target; __lpAlias += 3; return target; }
                public static int PartialUse() { int target = 2; ref int __lpAlias = ref target; __lpAlias += 3; return target; }
                public static int Collision() { int target = 2; var holder = new Holder { __lpAlias = 7 }; ref int __lpAlias = ref target; __lpAlias += 3; return target * 10 + holder.__lpAlias; }
                public static int Suppressed() { string? target = null; ref string __lpAlias = ref target!; __lpAlias = "same"; return __lpAlias!.Length + __lpAlias.Length; }
                public static int Duplicate() { int result = 0; { int target = 2; ref int __lpAlias = ref target; result += __lpAlias; } { int target = 3; ref int __lpAlias = ref target; result += __lpAlias; } return result; }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create("PayloadAliasOriginal", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
        var model = compilation.GetSemanticModel(tree);
        var helper = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.BinaryEntryPayloadAliases", throwOnError: true)!;
        var rewrite = helper.GetMethod("Rewrite", BindingFlags.Static | BindingFlags.NonPublic)!;
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword)).ToArray();
        var replacements = new Dictionary<MethodDeclarationSyntax, MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            string name = method.Identifier.ValueText;
            var body = method.Body!;
            var tokens = body.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken) &&
                (token.Parent is VariableDeclaratorSyntax variable ? model.GetDeclaredSymbol(variable) :
                    token.Parent is IdentifierNameSyntax identifier ? model.GetSymbolInfo(identifier).Symbol : null) is ILocalSymbol).ToArray();
            body = body.ReplaceTokens(tokens, (token, _) =>
            {
                if (name == "Unmarked" || name == "UnmarkedSource" && token.ValueText == "target" ||
                    name == "PartialUse" && token.ValueText == "__lpAlias" && token.Parent is IdentifierNameSyntax) return token;
                var marked = token.WithAdditionalAnnotations(new SyntaxAnnotation("LuminPack.ExpandedIdentity"));
                return token.Parent is VariableDeclaratorSyntax && token.ValueText.StartsWith("__lpAlias", StringComparison.Ordinal)
                    ? marked.WithAdditionalAnnotations(new SyntaxAnnotation("LuminPack.ExpandedPayloadBinding")) : marked;
            });
            var rewritten = (BlockSyntax)rewrite.Invoke(null, [body])!;
            bool removed = name is "Stable" or "Chain" or "Collision" or "Suppressed";
            Require(rewritten.DescendantNodes().OfType<VariableDeclaratorSyntax>().Any(variable => variable.Identifier.ValueText.StartsWith("__lpAlias", StringComparison.Ordinal)) != removed,
                "Payload alias storage proof changed for " + name + ".");
            if (!removed && name != "StableChainSource") Require(rewritten.ToFullString() == body.ToFullString(), "A guarded payload alias changed in " + name + ".");
            if (name == "StableChainSource") Require(rewritten.DescendantNodes().OfType<VariableDeclaratorSyntax>().Count(variable => variable.Identifier.ValueText.StartsWith("__lpAlias", StringComparison.Ordinal)) == 1,
                "A stable alias chain failed to retain the original captured ref binding.");
            if (name == "Collision") Require(rewritten.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.Name.Identifier.ValueText == "__lpAlias"),
                "Payload alias removal renamed a matching user field.");
            replacements.Add(method, method.WithBody(rewritten));
        }
        var changed = tree.WithRootAndOptions(tree.GetRoot().ReplaceNodes(methods, (method, _) => replacements[method]), tree.Options);
        var original = Load(compilation);
        var candidate = Load(compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(changed).WithAssemblyName("PayloadAliasRewritten"));
        foreach (var method in methods)
        {
            string name = method.Identifier.ValueText;
            var expected = original.GetType("Cases")!.GetMethod(name)!.Invoke(null, null);
            var actual = candidate.GetType("Cases")!.GetMethod(name)!.Invoke(null, null);
            Require(Equals(expected, actual), "Payload alias rewriting changed storage or evaluation in " + name + ".");
        }
    }

    /// <summary>Compiles one executable transformation fixture without adding generator behavior.</summary>
    /// <param name="compilation">The original or transformed C# compilation.</param>
    /// <returns>The loaded fixture assembly.</returns>
    private static Assembly Load(CSharpCompilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Require(!result.Diagnostics.Any(diagnostic => diagnostic.Id.StartsWith("CS86", StringComparison.Ordinal)),
            "Payload alias rewriting changed nullable flow: " + string.Join(Environment.NewLine, result.Diagnostics));
        return Assembly.Load(image.ToArray());
    }

    /// <summary>Reports a storage, binding or evaluation regression.</summary>
    /// <param name="condition">The expected invariant.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
