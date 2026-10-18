using System.Reflection;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks independent local identities across repeated collection and wrapper operation shapes.</summary>
internal static class SerializerCollectionInliningRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Compiles and executes concrete binary, JSON, and size entries for repeated managed element graphs.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        var shapes = new (string Type, string Value)[]
        {
            ("Child[,]", "new Child[,] { { new Child(), null! }, { new Child(), new Child() } }"),
            ("Tuple<Child, Child>", "Tuple.Create(new Child(), new Child())"),
            ("KeyValuePair<string, Child>", "new KeyValuePair<string, Child>(\"key\", new Child())"),
            ("Memory<Child>", "new Child[] { new Child(), null!, new Child() }.AsMemory()"),
            ("ReadOnlyMemory<Child>", "new Child[] { new Child(), null!, new Child() }"),
            ("IReadOnlyDictionary<string, Child>", "new Dictionary<string, Child> { [\"key\"] = new Child() }"),
            ("Queue<Child>", "new Queue<Child>(new[] { new Child(), new Child() })"),
            ("Stack<Child>", "new Stack<Child>(new[] { new Child(), new Child() })"),
            ("ConcurrentDictionary<string, Child>", "new ConcurrentDictionary<string, Child>(new Dictionary<string, Child> { [\"key\"] = new Child() })"),
            ("ImmutableDictionary<string, Child>", "ImmutableDictionary<string, Child>.Empty.Add(\"key\", new Child())"),
            ("FrozenDictionary<string, Child>", "new Dictionary<string, Child> { [\"key\"] = new Child() }.ToFrozenDictionary()"),
            ("Lazy<Child>", "new Lazy<Child>(new Child())"),
            ("ManagedValue?", "new ManagedValue { Text = \"nested\", Items = new[] { new Child(), new Child() } }"),
            ("PriorityQueue<Child, int>", "CreatePriorityQueue()")
        };
        var source = new StringBuilder("""
            using System;
            using System.Linq;
            using System.Text;
            using System.Collections.Generic;
            using System.Collections.Concurrent;
            using System.Collections.Immutable;
            using System.Collections.Frozen;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
            [LuminPackable] public sealed class Child
            {
                [LuminPackInclude] private int _value = 71;
                public string Text = "child";
                public int __lumin_v2 = 73;
                public int __lp0_p1 = 79;
            }
            [LuminPackable] public struct ManagedValue { public string Text; public Child[] Items; }
            """);
        for (int index = 0; index < shapes.Length; index++)
        {
            source.Append("[LuminPackable] public sealed class Model").Append(index).Append(" { public ").Append(shapes[index].Type)
                .Append(" First; public ").Append(shapes[index].Type).AppendLine(" Second; public int Tail = 83; }");
        }
        source.AppendLine("""
            public static class Entry
            {
                private static Exception Failure(Func<int> action)
                {
                    try { action(); }
                    catch (Exception exception) { return exception; }
                    throw new Exception("An unsupported evaluator unexpectedly succeeded.");
                }
                private static void CompareFailure(Func<int> expected, Func<int> actual)
                {
                    var first = Failure(expected);
                    var second = Failure(actual);
                    if (first.GetType() != second.GetType() || first.Message != second.Message)
                        throw new Exception("The existing evaluator fallback contract changed.");
                }
                private static PriorityQueue<Child, int> CreatePriorityQueue()
                {
                    var value = new PriorityQueue<Child, int>();
                    value.Enqueue(new Child(), 3);
                    value.Enqueue(new Child(), 5);
                    return value;
                }
                public static int Check()
                {
            """);
        for (int index = 0; index < shapes.Length; index++) AppendCase(source, index, shapes[index].Type, shapes[index].Value);
        source.Append("return ").Append(shapes.Length).AppendLine("; } }");

        var input = CSharpCompilation.Create("RepeatedCollectionOperations", [CSharpSyntaxTree.ParseText(source.ToString(), Parse, "Models.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: Parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(driver.GetRunResult().Results.All(result => result.Exception == null), string.Join(Environment.NewLine, driver.GetRunResult().Results.Select(result => result.Exception)));
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(diagnostic => diagnostic.ToString() +
            (diagnostic.Location.SourceTree == null ? "" : "\n" + diagnostic.Location.SourceTree.GetText().Lines[diagnostic.Location.GetLineSpan().StartLinePosition.Line]))));
        int inspected = 0;
        var signatures = new List<string>();
        foreach (var tree in output.SyntaxTrees.Where(tree => Path.GetFileName(tree.FilePath).StartsWith("LuminPackSerializer.", StringComparison.Ordinal)))
        {
            var model = output.GetSemanticModel(tree);
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method => method.TypeParameterList == null &&
                method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().StartsWith("global::Model", StringComparison.Ordinal) == true)))
            {
                inspected++;
                if (method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().TrimEnd('?') == "global::Model0"))
                    signatures.Add(method.Identifier.ValueText + method.ParameterList);
                // Contract: JSON entries are now fully expanded exactly like binary and size entries,
                // so no concrete collection entry may retain a shared per-type formatter call.
                foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var symbol = model.GetSymbolInfo(call).Symbol as IMethodSymbol;
                    if (symbol is { IsGenericMethod: true, Name: "CalculateOffset" } &&
                        IsMissingEvaluator(model.GetTypeInfo(call.ArgumentList.Arguments.Last().Expression).Type)) continue;
                    Require(symbol == null || !symbol.ContainingType.Name.StartsWith("LuminPackExtensions_", StringComparison.Ordinal),
                        "An ordinary collection entry retained a generated formatter call in " + method.ParameterList + ": " + call + " -> " + symbol?.ToDisplayString());
                }
            }
        }
        Require(inspected == shapes.Length * 5, "The repeated collection fixture did not select every expected entry; found " + inspected + ". " + string.Join("; ", signatures));
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(image.ToArray());
        Require((int)assembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! == shapes.Length, "Collection execution omitted an operation shape.");
        Console.WriteLine("Passed 14 repeated collection/wrapper shapes, 70 concrete entries, independent locals, and binary/JSON/size fallback parity.");
    }

    /// <summary>Appends one executable typed call site and comparisons against the existing generic formatter path.</summary>
    /// <param name="source">The consumer fixture source builder.</param>
    /// <param name="index">The concrete model identity.</param>
    /// <param name="type">The collection or wrapper shape.</param>
    /// <param name="initializer">The repeated managed graph initializer.</param>
    private static void AppendCase(StringBuilder source, int index, string type, string initializer)
    {
        string name = "Model" + index;
        source.AppendLine("{").Append("var value = new ").Append(name).Append(" { First = ").Append(initializer).Append(", Second = ").Append(initializer).AppendLine(" };");
        source.Append("var expected = LuminPackSerializer.Serialize<").Append(name).AppendLine(">(in value);");
        source.AppendLine("var actual = LuminPackSerializer.Serialize(in value);");
        source.Append("if (!actual.AsSpan().SequenceEqual(expected)) throw new Exception(\"Binary write: ").Append(name).AppendLine("\");");
        if (type is "Child[,]" or "Tuple<Child, Child>" or "Memory<Child>" or "ReadOnlyMemory<Child>" or "Lazy<Child>" or "PriorityQueue<Child, int>")
            source.Append("CompareFailure(() => LuminPackSerializer.Sizeof<").Append(name).AppendLine(">(value), () => LuminPackSerializer.Sizeof(value));");
        else
            source.Append("if (LuminPackSerializer.Sizeof(value) != LuminPackSerializer.Sizeof<").Append(name).Append(">(value)) throw new Exception(\"Size: ").Append(name).AppendLine("\");");
        source.Append(name).Append(" generic = null!, direct = null!;").AppendLine();
        source.Append("LuminPackSerializer.Deserialize<").Append(name).AppendLine(">(expected.AsSpan(), ref generic);");
        source.AppendLine("LuminPackSerializer.Deserialize(expected.AsSpan(), ref direct);");
        AppendRestoredComparison(source, name, "Binary read");
        source.Append("var expectedJson = LuminPackSerializer.SerializeJson<").Append(name).AppendLine(">(value);");
        source.Append("if (LuminPackSerializer.SerializeJson(value) != expectedJson) throw new Exception(\"JSON write: ").Append(name).AppendLine("\");");
        source.AppendLine("var json = Encoding.UTF8.GetBytes(expectedJson);");
        if (type is "KeyValuePair<string, Child>" or "FrozenDictionary<string, Child>")
        {
            // These existing readers reject their own separator/object shape. Preserve
            // their failure contracts while checking that expansion adds no new failure.
            source.Append("CompareFailure(() => { generic = LuminPackSerializer.DeserializeJson<").Append(name)
                .AppendLine(">(json.AsSpan()); return 0; }, () => LuminPackSerializer.DeserializeJson(json.AsSpan(), ref direct));");
        }
        else
        {
            source.Append("generic = LuminPackSerializer.DeserializeJson<").Append(name).AppendLine(">(json.AsSpan());");
            source.AppendLine("int directConsumed = LuminPackSerializer.DeserializeJson(json.AsSpan(), ref direct);");
            source.Append("if (directConsumed != json.Length) throw new Exception(\"JSON consumed bytes: ").Append(name).AppendLine("\");");
            AppendRestoredComparison(source, name, "JSON read");
        }
        source.AppendLine("}");
    }

    /// <summary>Compares restored content through the same existing serializer operation on both values.</summary>
    /// <param name="source">The executable fixture source.</param>
    /// <param name="name">The concrete model type.</param>
    /// <param name="operation">The operation named in a failure.</param>
    private static void AppendRestoredComparison(StringBuilder source, string name, string operation)
        => source.Append("if (!LuminPackSerializer.Serialize<").Append(name).Append(">(in generic).AsSpan().SequenceEqual(LuminPackSerializer.Serialize<")
            .Append(name).Append(">(in direct))) throw new Exception(\"").Append(operation).Append(": ").Append(name).AppendLine("\");");

    /// <summary>Identifies shapes whose evaluator already uses the runtime registration fallback.</summary>
    /// <param name="type">The bound evaluator argument type.</param>
    /// <returns>Whether the existing emitter registry has no evaluator for this shape.</returns>
    private static bool IsMissingEvaluator(ITypeSymbol? type)
        => type is IArrayTypeSymbol { Rank: > 1 } || type is INamedTypeSymbol named &&
            named.OriginalDefinition.ToDisplayString() is "System.Tuple<T1, T2>" or "System.Memory<T>" or "System.ReadOnlyMemory<T>" or
                "System.Lazy<T>" or "System.Collections.Generic.PriorityQueue<TElement, TPriority>";

    /// <summary>Reports a concrete operation contract failure.</summary>
    /// <param name="condition">The invariant required by this regression.</param>
    /// <param name="message">The observed failure.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
