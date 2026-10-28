using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects executable scopes and metadata during generated-source presentation.</summary>
internal static class SerializerSourcePresentationRegression
{
    /// <summary>Checks scope reduction, preserved storage boundaries, and unchanged Release execution.</summary>
    /// <param name="references">Platform references for compiling the source fixtures.</param>
    internal static void Run(MetadataReference[] references)
    {
        VerifyBlockChains(references);
        VerifyTriviaAndAnnotations();
        SerializerOperationNamesRegression.Run(references);
        Console.WriteLine("Passed generated source presentation: block scopes, storage lifetimes, trivia, annotations, Release IL, and execution.");
    }

    /// <summary>Compiles representative control flow before and after removing redundant wrapper scopes.</summary>
    /// <param name="references">Platform references for the fixture compilation.</param>
    private static void VerifyBlockChains(MetadataReference[] references)
    {
        const string source = """
            using System;
            public static class Entry
            {
                public static int Counter;
                public static int Value(bool choice)
                {
                    {
                        {
                            int value = 3;
                            if (choice) { { value += 4; } }
                            else { { value += 5; } }
                            return value;
                        }
                    }
                }
                public static int NeighborScopes()
                {
                    int value = 0;
                    { int item = 3; value += item; }
                    { int item = 5; value += item; }
                    return value;
                }
                public static int Disposal()
                {
                    {
                        using var lease = new Lease();
                        return lease.Value;
                    }
                }
                public static int References()
                {
                    {
                        Span<int> storage = stackalloc int[1];
                        storage[0] = 7;
                        ref int item = ref storage[0];
                        return item;
                    }
                }
                public static int NestedReferences()
                {
                    {
                        if (Counter >= 0)
                        {
                            scoped Span<int> storage = stackalloc int[1];
                            storage[0] = 9;
                            ref int item = ref storage[0];
                            return item;
                        }
                        return 0;
                    }
                }
                public static int Labels()
                {
                    {
                        int value = 2;
                        goto done;
                        done: return value;
                    }
                }
                public static int Cleanup()
                {
                    try { { return Value(true); } }
                    finally { { Counter += 10; } }
                }
                public static int Run() => Value(true) + Value(false) + NeighborScopes() + Disposal() + References() + NestedReferences() + Labels() + Cleanup() + Counter;
            }
            public sealed class Lease : IDisposable
            {
                public int Value => 5;
                public void Dispose() => Entry.Counter++;
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var original = CSharpSyntaxTree.ParseText(source, parse).GetRoot();
        var rewritten = Rewrite(original);
        Require(rewritten.DescendantNodes().OfType<BlockSyntax>().Count() < original.DescendantNodes().OfType<BlockSyntax>().Count(),
            "Redundant block chains were not removed.");
        foreach (string name in new[] { "NeighborScopes", "Disposal", "References" })
        {
            string before = Method(original, name).NormalizeWhitespace().ToFullString();
            string after = Method(rewritten, name).NormalizeWhitespace().ToFullString();
            Require(before == after, name + " lost a declaration, disposal, or reference-storage scope.");
        }
        var nestedBefore = Method(original, "NestedReferences");
        var nestedAfter = Method(rewritten, "NestedReferences");
        Require(nestedAfter.DescendantNodes().OfType<BlockSyntax>().Count() == nestedBefore.DescendantNodes().OfType<BlockSyntax>().Count() - 1,
            "An empty ancestor of an independently scoped reference block was not removed.");
        string storageBefore = nestedBefore.DescendantNodes().OfType<IfStatementSyntax>().Single().Statement.NormalizeWhitespace().ToFullString();
        string storageAfter = nestedAfter.DescendantNodes().OfType<IfStatementSyntax>().Single().Statement.NormalizeWhitespace().ToFullString();
        Require(storageBefore == storageAfter, "The nested scoped, ref, or stackalloc storage block changed.");
        byte[] originalImage = Compile(original, parse, references);
        byte[] rewrittenImage = Compile(rewritten, parse, references);
        Require(MethodBodies(originalImage).SequenceEqual(MethodBodies(rewrittenImage)),
            "Removing redundant scopes changed Release IL, local signatures, or exception regions.");
        int beforeResult = (int)Assembly.Load(originalImage).GetType("Entry")!.GetMethod("Run")!.Invoke(null, null)!;
        int afterResult = (int)Assembly.Load(rewrittenImage).GetType("Entry")!.GetMethod("Run")!.Invoke(null, null)!;
        Require(beforeResult == 64 && afterResult == beforeResult, "Scope presentation changed control flow or disposal timing.");
    }

    /// <summary>Preserves annotations and rejects braces containing meaningful trivia or directives.</summary>
    private static void VerifyTriviaAndAnnotations()
    {
        var original = SyntaxFactory.ParseCompilationUnit("public static class C { public static int Run() { { return 1; } } }");
        var method = Method(original, "Run");
        var outer = method.Body!;
        var inner = (BlockSyntax)outer.Statements[0];
        var innerMarker = new SyntaxAnnotation("InnerScope");
        var outerMarker = new SyntaxAnnotation("OuterScope");
        var annotated = original.ReplaceNode(outer, outer.WithStatements(SyntaxFactory.SingletonList<StatementSyntax>(
            inner.WithAdditionalAnnotations(innerMarker))).WithAdditionalAnnotations(outerMarker));
        var rewritten = Rewrite(annotated);
        Require(rewritten.GetAnnotatedNodes(innerMarker).Count() == 1 && rewritten.GetAnnotatedNodes(outerMarker).Count() == 1,
            "Scope presentation discarded block annotations.");
        foreach (string source in new[]
        {
            "public static class C { public static int Run() { /* outer */ { return 1; } } }",
            "public static class C { public static int Run() { { /* inner */ return 1; } } }",
            "public static class C { public static int Run() {\n#if true\n{ return 1; }\n#endif\n} }"
        })
        {
            var tree = SyntaxFactory.ParseCompilationUnit(source);
            Require(Rewrite(tree).ToFullString() == tree.ToFullString(), "Scope presentation changed meaningful brace trivia or directives.");
        }
        var braceMarker = new SyntaxAnnotation("BraceScope");
        var markedBrace = original.ReplaceToken(inner.OpenBraceToken, inner.OpenBraceToken.WithAdditionalAnnotations(braceMarker));
        Require(Rewrite(markedBrace).GetAnnotatedTokens(braceMarker).Count() == 1,
            "Scope presentation discarded an annotated brace.");
    }

    /// <summary>Invokes the internal presentation pass without extending the generator's public API.</summary>
    /// <param name="root">The fixture syntax root.</param>
    /// <returns>The transformed syntax root.</returns>
    private static SyntaxNode Rewrite(SyntaxNode root)
        => (SyntaxNode)typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.GeneratedOperationScopes", throwOnError: true)!
            .GetMethod("Rewrite", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [root])!;

    /// <summary>Finds a uniquely named fixture method.</summary>
    /// <param name="root">The fixture root.</param>
    /// <param name="name">The requested method name.</param>
    /// <returns>The matching declaration.</returns>
    private static MethodDeclarationSyntax Method(SyntaxNode root, string name)
        => root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == name);

    /// <summary>Emits an optimized fixture with the same compilation settings and assembly identity.</summary>
    /// <param name="root">The original or presented syntax root.</param>
    /// <param name="parse">The fixture language options.</param>
    /// <param name="references">Platform metadata references.</param>
    /// <returns>The complete emitted assembly.</returns>
    private static byte[] Compile(SyntaxNode root, CSharpParseOptions parse, MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create("PresentationScopes", [CSharpSyntaxTree.Create((CSharpSyntaxNode)root, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true));
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return image.ToArray();
    }

    /// <summary>Captures exact executable bodies and their storage and exception metadata.</summary>
    /// <param name="image">An optimized fixture assembly.</param>
    /// <returns>Comparable method-body records in metadata order.</returns>
    private static string[] MethodBodies(byte[] image)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        return metadata.MethodDefinitions.Select(handle => metadata.GetMethodDefinition(handle)).Where(method => method.RelativeVirtualAddress != 0)
            .Select(method =>
            {
                var body = pe.GetMethodBody(method.RelativeVirtualAddress);
                string locals = body.LocalSignature.IsNil ? "" : Convert.ToHexString(metadata.GetBlobBytes(metadata.GetStandaloneSignature(body.LocalSignature).Signature));
                string exceptions = string.Join(";", body.ExceptionRegions.Select(region =>
                    $"{region.Kind}:{region.TryOffset}:{region.TryLength}:{region.HandlerOffset}:{region.HandlerLength}:{region.FilterOffset}"));
                return metadata.GetString(method.Name) + ":" + Convert.ToHexString(body.GetILBytes()!) + ":" + locals + ":" + exceptions;
            }).ToArray();
    }

    /// <summary>Reports a violated scope-presentation invariant.</summary>
    /// <param name="condition">Whether the required invariant holds.</param>
    /// <param name="message">The explanation reported on failure.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
