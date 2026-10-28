using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks registration-only generic fallbacks and concrete scalar and collection entries.</summary>
internal static class SerializerFormatterDispatchRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    private const string Models = """
        using System.Collections.Generic;
        using LuminPack.Attribute;
        [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable] public sealed class Payload { public int Value; public string Text = ""; }
        [LuminPackable] public sealed class UsedElsewhere { public long Other; }
        public static class Entry
        {
            public static byte[] Scalar() => LuminPack.LuminPackSerializer.Serialize(42);
            public static int ScalarBack(byte[] bytes) { int value = 0; LuminPack.LuminPackSerializer.Deserialize(bytes, ref value); return value; }
            public static byte[] Generic<T>(T value) => LuminPack.LuminPackSerializer.Serialize(value);
            public static T GenericBack<T>(byte[] bytes) => LuminPack.LuminPackSerializer.Deserialize<T>(bytes);
            public static byte[] Closed() => LuminPack.LuminPackSerializer.Serialize(new List<Payload> { new Payload { Value = 7, Text = "seven" } });
            public static int ClosedBack(byte[] bytes) { List<Payload> value = null; LuminPack.LuminPackSerializer.Deserialize(bytes, ref value); return value.Count; }
        }
        """;

    /// <summary>Executes the Full, Light and Minimal fixtures.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Light", "Minimal" })
        {
            var input = CSharpCompilation.Create("DispatchFixture" + mode, [CSharpSyntaxTree.ParseText(Models.Replace("MODE", mode), Parse, "Models.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var generators = new List<ISourceGenerator> { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() };
            var driver = CSharpGeneratorDriver.Create(generators, parseOptions: Parse).RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            var sources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();

            var dispatch = sources.Single(file => file.HintName == "LuminPack.Dispatch.Formatters.g.cs");
            var root = CSharpSyntaxTree.ParseText(dispatch.SourceText, Parse).GetRoot();
            Require(!root.DescendantNodes().OfType<SwitchStatementSyntax>().Any() && !root.DescendantNodes().OfType<SwitchExpressionSyntax>().Any(),
                mode + ": the generic fallback regained a type-id switch.");
            string text = root.ToString();
            Require(!text.Contains("TypeId", StringComparison.Ordinal), mode + ": the generic fallback still reads a compact type id.");
            Require(!text.Contains("Formatters<", StringComparison.Ordinal) && !text.Contains("Make0", StringComparison.Ordinal) &&
                !text.Contains("Payload", StringComparison.Ordinal) && !text.Contains("UsedElsewhere", StringComparison.Ordinal),
                mode + ": the registration-only fallback regained a local type table or factory.");
            Require(text.Contains("TryGetCachedValue<T>", StringComparison.Ordinal), mode + ": the generic fallback lost registered dispatch.");
            int bridges = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Count(method => method.AttributeLists.SelectMany(list => list.Attributes)
                .Any(attribute => attribute.ToString().Contains("NoInlining", StringComparison.Ordinal)) && !method.Identifier.ValueText.EndsWith("Registered", StringComparison.Ordinal));
            Require(bridges <= 5, mode + ": the generic fallback regained per-type bridge methods (" + bridges + ").");

            if (mode != "Full")
            {
                // A concrete scalar call must have its own typed entry beside the generic fallback.
                var facades = sources.Where(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal))
                    .SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                    .Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword) && method.TypeParameterList == null && method.Identifier.ValueText == "Serialize" &&
                        method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() is "int" or "global::System.Int32")).ToArray();
                Require(facades.Length != 0, mode + ": Serialize(42) has no typed int entry.");
            }

            byte[] image = Emit(output);
            var context = new AssemblyLoadContext("Dispatch" + mode, isCollectible: true);
            try
            {
                var assembly = context.LoadFromStream(new MemoryStream(image));
                var entry = assembly.GetType("Entry")!;
                byte[] scalar = (byte[])entry.GetMethod("Scalar")!.Invoke(null, null)!;
                Require((int)entry.GetMethod("ScalarBack")!.Invoke(null, [scalar])! == 42, mode + ": the typed int entry changed the record.");
                byte[] closed = (byte[])entry.GetMethod("Closed")!.Invoke(null, null)!;
                var payloadList = typeof(List<>).MakeGenericType(assembly.GetType("Payload")!);
                Require((int)entry.GetMethod("ClosedBack")!.Invoke(null, [closed])! == 1, mode + ": the concrete collection read failed.");
                var genericCopy = (System.Collections.IList)entry.GetMethod("GenericBack")!.MakeGenericMethod(payloadList).Invoke(null, [closed])!;
                Require(genericCopy.Count == 1, mode + ": the lazy generic collection entry failed.");
            }
            finally { context.Unload(); }
        }
        var weak = ExerciseSafeCache(references);
        for (int attempt = 0; attempt < 12 && weak.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Require(!weak.IsAlive, "A module-local safe generic cache retained its collectible assembly.");
        Console.WriteLine("Passed explicit registration precedence, typed entries and lazy generic collection dispatch.");
    }

    /// <summary>Warms a no-unsafe delegate cache for a noncollectible framework payload before unloading its owner.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <returns>A weak reference to the unloaded owner.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ExerciseSafeCache(MetadataReference[] references)
    {
        var input = CSharpCompilation.Create("SafeDispatchLifetime", [CSharpSyntaxTree.ParseText(Models.Replace("MODE", "Light"), Parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false));
        var driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        var context = new AssemblyLoadContext("SafeDispatchLifetime", isCollectible: true);
        var weak = new WeakReference(context);
        var assembly = context.LoadFromStream(new MemoryStream(Emit(output)));
        var entry = assembly.GetType("Entry")!;
        var bytes = (byte[])entry.GetMethod("Generic")!.MakeGenericMethod(typeof(int)).Invoke(null, [42])!;
        Require((int)entry.GetMethod("GenericBack")!.MakeGenericMethod(typeof(int)).Invoke(null, [bytes])! == 42,
            "The no-unsafe first-use generic entry failed.");
        context.Unload();
        return weak;
    }

    /// <summary>Compiles a complete fixture and reports every actual compiler error.</summary>
    /// <param name="compilation">The library compilation.</param>
    /// <returns>The emitted managed assembly image.</returns>
    private static byte[] Emit(Compilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        return image.ToArray();
    }

    /// <summary>Reports a dispatch contract failure.</summary>
    /// <param name="condition">The expected invariant.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
