using System.Reflection;
using System.Text.RegularExpressions;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Executes repeated nested operation expansions and checks that concrete binary entries contain no ordinary formatter calls.</summary>
internal static class SerializerBinaryInliningRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Checks reference lifetimes, independent local names, private mirrors, local helpers, reuse, and generic wire parity.</summary>
    /// <param name="references">Runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Buffers;
            using System.Collections.Generic;
            using System.IO;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using LuminPack.Utility;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
            [LuminPackable] public class Base
            {
                public int _base = 19;
                public int BaseValue() => _base;
            }
            [LuminPackable] public sealed class Child : Base
            {
                [LuminPackInclude] private int _secret = 31;
                public string Text = "same";
                public string Locked { get; private set; } = "default";
                public int Initial { get; init; } = 71;
                public string ReadOnly { get; } = "read-only storage";
                public void SetLocked(string value) => Locked = value;
                public int Secret() => _secret;
            }
            [LuminPackable] public sealed class Payload
            {
                [LuminPackInclude] private int _head = 47;
                public Child First = new();
                public Child Second = new();
                public Dictionary<string, List<Child[]>> Left = new();
                public Dictionary<string, List<Child[]>> Right = new();
                public List<int> Numbers = new();
                public List<int> OtherNumbers = new();
                public int Tail = 173;
                public int __lumin_p0 = 53;
                public int __lumin_v2 = 59;
                public int __lp0_v3 = 61;
                public int Head() => _head;
            }
            [LuminPackable] public struct GenericValue<T> { public T Item; public List<T> Items; }
            [LuminPackable] public sealed class GenericBox<T> { public T Item { get; set; } public List<T> Items { get; set; } }
            public static class Entry
            {
                public static int Check()
                {
                    CheckGenerics();
                    int checkedCases = 0;
                    foreach (bool sparse in new[] { false, true })
                    foreach (int count in new[] { -1, 0, 3 })
                    {
                        var value = new Payload();
                        value.First.SetLocked("first private setter");
                        value.Second.SetLocked("second private setter");
                        if (count == 3) value.First.Text = new string('x', 1400);
                        if (count < 0) { value.Left = null!; value.Right = null!; value.Numbers = null!; }
                        else
                        {
                            for (int i = 0; i < count; i++)
                            {
                                value.Left[i.ToString()] = new() { new Child[] { new() { Text = "left" + i }, new() { Text = "nested" } }, null! };
                                value.Right[i.ToString()] = new() { new Child[] { new() { Text = "right" + i } } };
                                value.Numbers.Add(i * 7);
                                value.OtherNumbers.Add(i * 11);
                            }
                            if (sparse && count > 0) { value.Left.Remove("1"); value.Right.Remove("1"); }
                        }
                        byte[] expected = LuminPackSerializer.Serialize<Payload>(in value);
                        byte[] actual = LuminPackSerializer.Serialize(in value);
                        if (!actual.AsSpan().SequenceEqual(expected)) throw new Exception("Concrete write changed the binary contract.");
                        if (LuminPackSerializer.Sizeof(value) != LuminPackSerializer.Sizeof<Payload>(value))
                            throw new Exception("Concrete size evaluation changed the original generic contract.");
                        var buffer = LuminBufferWriterPool.Rent();
                        try
                        {
                            LuminPackSerializer.Serialize(in value, buffer);
                            if (!buffer.GetSpan().SequenceEqual(expected)) throw new Exception("Buffer write changed the binary contract.");
                            Payload bufferValue = null!;
                            LuminPackSerializer.Deserialize(buffer, ref bufferValue);
                            Validate(value, bufferValue);
                        }
                        finally { LuminBufferWriterPool.Return(buffer); }
                        Payload restored = null!;
                        LuminPackSerializer.Deserialize(actual.AsSpan(), ref restored);
                        Validate(value, restored);
                        LuminPackSerializer.Deserialize(actual.AsSpan(), ref restored);
                        Validate(value, restored);
                        Payload sequenceValue = null!;
                        LuminPackSerializer.Deserialize(new ReadOnlySequence<byte>(actual), ref sequenceValue);
                        Validate(value, sequenceValue);
                        var first = new Segment(actual.AsMemory(0, actual.Length / 2));
                        var last = first.Append(actual.AsMemory(actual.Length / 2));
                        var segmented = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
                        LuminPackSerializer.Deserialize(segmented, ref sequenceValue);
                        Validate(value, sequenceValue);
                        using var stream = new MemoryStream();
                        LuminPackSerializer.SerializeAsync(stream, value).GetAwaiter().GetResult();
                        if (!stream.ToArray().AsSpan().SequenceEqual(expected)) throw new Exception("Async write changed the binary contract.");
                        stream.Position = 0;
                        Validate(value, LuminPackSerializer.DeserializeAsync(stream, new Payload()).GetAwaiter().GetResult());
                        Validate(value, LuminPackSerializer.Deserialize<Payload>(actual));
                        foreach (var encoding in new[] { LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16 })
            {
                if (encoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
                    RunProfile<global::LuminPack.Option.LuminPackUtf8LengthOptions>();
                else
                    RunProfile<global::LuminPack.Option.LuminPackUtf16LengthOptions>();
                void RunProfile<TProfile>()
                    where TProfile : struct, global::LuminPack.Option.ILuminPackOptions
                {
                    string expectedJson = LuminPackSerializer.SerializeJson<Payload, TProfile>(value);
                    string actualJson = LuminPackSerializer.SerializeJson<TProfile>(value);
                    if (actualJson != expectedJson)
                        throw new Exception("Concrete JSON output changed.");
                    Payload jsonValue = null!;
                    int consumedJson = LuminPackSerializer.DeserializeJson<TProfile>(actualJson, ref jsonValue);
                    if (consumedJson != (encoding == LuminPackStringEncoding.UTF8 ? System.Text.Encoding.UTF8 : System.Text.Encoding.Unicode).GetByteCount(actualJson))
                        throw new Exception("JSON string consumed-byte units changed.");
                    Validate(value, jsonValue);
                    LuminPackSerializer.DeserializeJson<TProfile>(actualJson.AsSpan(), ref jsonValue);
                    Validate(value, jsonValue);
                    var bytesJson = (encoding == LuminPackStringEncoding.UTF8 ? System.Text.Encoding.UTF8 : System.Text.Encoding.Unicode).GetBytes(actualJson);
                    LuminPackSerializer.DeserializeJson<TProfile>(bytesJson.AsSpan(), ref jsonValue);
                    Validate(value, jsonValue);
                    var jsonBuffer = LuminBufferWriterPool.Rent();
                    try
                    {
                        LuminPackSerializer.SerializeJson<TProfile>(value, jsonBuffer);
                        if (!jsonBuffer.GetSpan().SequenceEqual(bytesJson))
                            throw new Exception("Buffered JSON output changed.");
                        LuminPackSerializer.DeserializeJson<TProfile>(jsonBuffer, ref jsonValue);
                        Validate(value, jsonValue);
                    }
                    finally
                    {
                        LuminBufferWriterPool.Return(jsonBuffer);
                    }

                    using var document = System.Text.Json.JsonDocument.Parse(actualJson);
                    string reordered = "{\"unknown\":[{\"skip\":42}]," + string.Join(",", document.RootElement.EnumerateObject().Reverse().Select(property => "\"" + property.Name + "\":" + property.Value.GetRawText())) + "}";
                    LuminPackSerializer.DeserializeJson<TProfile>(reordered, ref jsonValue);
                    Validate(value, jsonValue);
                    bool rejected = false;
                    try
                    {
                        LuminPackSerializer.DeserializeJson<TProfile>(actualJson + " true", ref jsonValue);
                    }
                    catch
                    {
                        rejected = true;
                    }

                    if (!rejected)
                        throw new Exception("Concrete JSON entry accepted trailing data.");
                }
            }            checkedCases++;
                    }
                    return checkedCases;
                }
                private static void CheckGenerics()
                {
                    var value = new GenericValue<int> { Item = 37, Items = new() { 3, 5 } };
                    var bytes = LuminPackSerializer.Serialize(in value);
                    if (!bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize<GenericValue<int>>(in value)))
                        throw new Exception("Closing a generic struct changed its original wire decisions.");
                    var restored = LuminPackSerializer.Deserialize<GenericValue<int>>(bytes);
                    if (restored.Item != 37 || restored.Items[1] != 5) throw new Exception("Generic struct fallback read changed.");
                    LuminPackSerializer.Deserialize(bytes.AsSpan(), ref restored);
                    if (restored.Item != 37 || restored.Items[0] != 3) throw new Exception("Generic struct concrete read changed.");
                    var nested = new GenericBox<GenericBox<int>>
                    {
                        Item = new() { Item = 19, Items = new() { 23, 29 } },
                        Items = new() { new() { Item = 31, Items = new() { 41, 43 } } }
                    };
                    bytes = LuminPackSerializer.Serialize(in nested);
                    if (!bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize<GenericBox<GenericBox<int>>>(in nested)))
                        throw new Exception("Closing a nested generic class changed its original wire decisions.");
                    GenericBox<GenericBox<int>> nestedRead = null!;
                    LuminPackSerializer.Deserialize(bytes.AsSpan(), ref nestedRead);
                    if (nestedRead.Item.Item != 19 || nestedRead.Items[0].Items[1] != 43) throw new Exception("Nested generic concrete read changed.");
                    nestedRead = LuminPackSerializer.Deserialize<GenericBox<GenericBox<int>>>(bytes);
                    if (nestedRead.Item.Items[1] != 29 || nestedRead.Items[0].Item != 31) throw new Exception("Nested generic fallback read changed.");
                }
                private static void Validate(Payload source, Payload value)
                {
                    if (value.Head() != 47 || value.Tail != 173 || value.First.Secret() != 31 || value.Second.BaseValue() != 19)
                        throw new Exception("Private or repeated object state changed.");
                    if (value.First.Locked != source.First.Locked || value.Second.Locked != source.Second.Locked ||
                        value.First.Initial != source.First.Initial || value.First.ReadOnly != source.First.ReadOnly)
                        throw new Exception("Nested private-setter, init-only, or getter-only storage changed.");
                    if (value.__lumin_p0 != 53 || value.__lumin_v2 != 59 || value.__lp0_v3 != 61)
                        throw new Exception("A source field sharing a generated naming prefix was renamed.");
                    if ((source.Left?.Count ?? -1) != (value.Left?.Count ?? -1) || (source.Right?.Count ?? -1) != (value.Right?.Count ?? -1))
                        throw new Exception("Repeated dictionary state changed.");
                    if (source.Numbers is null ? value.Numbers is not null : !source.Numbers.SequenceEqual(value.Numbers))
                        throw new Exception("Repeated unmanaged list state changed.");
                    foreach (var pair in source.Left ?? new())
                        if (value.Left[pair.Key][0][0].Text != pair.Value[0][0].Text || value.Left[pair.Key][1] is not null)
                            throw new Exception("Nested managed collection state changed.");
                }
            }
            public sealed class Segment : ReadOnlySequenceSegment<byte>
            {
                public Segment(ReadOnlyMemory<byte> memory) { Memory = memory; }
                public Segment Append(ReadOnlyMemory<byte> memory)
                {
                    var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
                    Next = next;
                    return next;
                }
            }
            """;
        // Extension syntax in the fixture intentionally shares the same using scope as normal consumers.
        var input = CSharpCompilation.Create("RepeatedInlineOperations", [CSharpSyntaxTree.ParseText("using System.Linq;\n" + source, Parse, "Models.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(driver.GetRunResult().Results.All(result => result.Exception == null), string.Join(Environment.NewLine, driver.GetRunResult().Results.Select(result => result.Exception)));
        Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        Require(!output.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            string.Join(Environment.NewLine, output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        int inspected = 0;
        var seen = new List<string>();
        foreach (var tree in output.SyntaxTrees.Where(tree => Path.GetFileName(tree.FilePath).StartsWith("LuminPackSerializer.", StringComparison.Ordinal)))
        {
            var model = output.GetSemanticModel(tree);
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method =>
                method.TypeParameterList == null && method.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeAsync" or "DeserializeAsync" or "Sizeof" or "SerializeJson" or "DeserializeJson" &&
                method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().TrimEnd('?') == "global::Payload")))
            {
                inspected++;
                seen.Add(method.Identifier.ValueText + method.ParameterList);
                var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
                // Convenience entries use the same state overloads as framework-internal calls.
                foreach (var call in calls)
                {
                    var symbol = model.GetSymbolInfo(call).Symbol as IMethodSymbol;
                    Require(symbol == null || !symbol.ContainingType.Name.StartsWith("LuminPackExtensions_", StringComparison.Ordinal),
                        "A concrete " + method.Identifier.ValueText + " entry retained a generated formatter or accessor call: " + call);
                }
                Require(method.DescendantNodes().OfType<LocalFunctionStatementSyntax>().All(local => local.AttributeLists.ToString().Contains("MethodImplOptions.NoInlining", StringComparison.Ordinal)),
                    "An ordinary generated dictionary helper remained behind a local call boundary.");
            }
        }
        // Light keeps the IBufferWriter facades only when a project call selects them, like the reusable-buffer overloads.
        Require(inspected == 14, "Expected every selected concrete binary, asynchronous, JSON, and size payload entry; found " + inspected + ": " + string.Join(" | ", seen));
        var implementations = output.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.TypeParameterList?.Parameters.Count == 1 && method.ParameterList.Parameters.Count == 2 &&
                method.ParameterList.Parameters[1].Type?.ToString() is "global::Payload" or "global::Payload?" &&
                method.ParameterList.Parameters[0].Type?.ToString().Contains("LuminPack", StringComparison.Ordinal) == true &&
                method.Identifier.ValueText is "RootWrite" or "RootRead" or "RootWriteJson" or "RootReadJson" or "Sizeof").ToArray();
        Require(implementations.Length == 5, "Expected one state implementation for each payload operation.");
        VerifyJsonIntegerFallback(implementations.Single(method => method.Identifier.ValueText == "RootWriteJson"));
        var sharedJsonCalls = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);
        foreach (var method in implementations)
        {
            var model = output.GetSemanticModel(method.SyntaxTree);
            VerifyRootBindings(method, model);
            Require(method.DescendantNodes().OfType<LocalFunctionStatementSyntax>().All(local => local.AttributeLists.ToString().Contains("MethodImplOptions.NoInlining", StringComparison.Ordinal)),
                "A state implementation retained an ordinary local formatter helper.");
            foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbol = model.GetSymbolInfo(call).Symbol as IMethodSymbol;
                if (method.Identifier.ValueText.EndsWith("Json", StringComparison.Ordinal) &&
                    SerializerExecutionAssertions.IsConcreteJsonStateCall(symbol))
                {
                    var target = symbol!.OriginalDefinition;
                    sharedJsonCalls[target] = sharedJsonCalls.TryGetValue(target, out int count) ? count + 1 : 1;
                    continue;
                }
                if (method.Identifier.ValueText == "Sizeof" && symbol?.ContainingType.Name == "LuminPackSerializer" && symbol.Name == "Sizeof")
                    continue;
                Require((symbol?.ContainingType.Name != "LuminPackSerializer" || symbol.MethodKind == MethodKind.LocalFunction) &&
                    !(symbol?.ContainingType.Name.StartsWith("LuminPackExtensions_", StringComparison.Ordinal) ?? false),
                    "An ordinary payload field retained a serializer or extension call instead of expanding: " + call);
            }
        }
        Require(sharedJsonCalls.Values.Any(count => count > 1),
            "Repeated large JSON fields no longer share one concrete state implementation.");
        var bridges = output.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Identifier.ValueText is "WriteValue" or "ReadValue" or "CalculateOffset" &&
                method.Parent is ClassDeclarationSyntax owner && owner.Identifier.ValueText.StartsWith("LuminPackExtensions_", StringComparison.Ordinal)).ToArray();
        Require(bridges.Length == 1 && bridges.All(method => method.Identifier.ValueText == "CalculateOffset" &&
                method.TypeParameterList?.Parameters.Count == 1 && method.ExpressionBody != null),
            "Only the evaluator may retain its generic bridge; Writer and Reader must use canonical operations.");
        // The property-name cache stays owned by the per-type Json_<Type> helper: it is declared
        // once for the whole compilation, never lifted and copied into each concrete entry.
        string[] generated = output.SyntaxTrees.Select(tree => tree.ToString()).ToArray();
        Require(!generated.Any(text => Regex.IsMatch(text, "__LuminLayout_[0-9a-f]+_[A-Za-z0-9_]*_utf8_")),
            "A shared JSON property-name cache was copied per concrete root.");
        Require(generated.Sum(text => Regex.Matches(text, @"readonly byte\[\] _utf8_First\b").Count) == 1 &&
                generated.Sum(text => Regex.Matches(text, @"readonly char\[\] _utf16_First\b").Count) == 1,
            "The shared JSON cache array for Payload.First was declared more than once across all generated files.");
        Require(generated.Sum(text => Regex.Matches(text, @"_utf8Hash_First\b").Count) == 1,
            "The shared JSON cache hash symbol for Payload.First must appear exactly once across all generated files.");
        if (Environment.GetEnvironmentVariable("LUMINPACK_MECHANISM_CAPTURE") is { Length: > 0 } capture)
        {
            Directory.CreateDirectory(capture);
            foreach (var generatedSource in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources))
                File.WriteAllText(Path.Combine(capture, generatedSource.HintName), generatedSource.SourceText.ToString());
        }
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(image.ToArray());
        Require((int)assembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! == 6, "Nested collection execution did not cover every case.");
        Console.WriteLine("Passed shared binary roots, closed collection calls, repeated local identities, private inherited layouts, reuse, and generic wire parity.");
    }

    /// <summary>Requires the state body to use its own root bindings without a second expansion frame.</summary>
    /// <param name="method">One concrete state implementation with nested operations already expanded.</param>
    /// <param name="model">The model distinguishing actual parameter references from matching user member names.</param>
    private static void VerifyRootBindings(MethodDeclarationSyntax method, SemanticModel model)
    {
        var payload = model.GetDeclaredSymbol(method.ParameterList.Parameters[1]);
        var aliases = method.DescendantNodes().OfType<VariableDeclaratorSyntax>().Where(variable =>
            variable.Initializer?.Value is RefExpressionSyntax reference &&
            SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(reference.Expression).Symbol, payload));
        Require(!aliases.Any(), "A root operation introduced a redundant payload ref alias.");
        Require(method.Body!.Statements.FirstOrDefault() is not BlockSyntax,
            "A root operation retained the nested-expansion wrapper scope.");
        if (!method.Identifier.ValueText.StartsWith("RootWrite", StringComparison.Ordinal)) return;
        var snapshot = method.Body.Statements.OfType<LocalDeclarationStatementSyntax>().SelectMany(statement => statement.Declaration.Variables)
            .Single(variable => variable.Initializer != null &&
                SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(variable.Initializer.Value).Symbol, payload));
        var snapshotSymbol = model.GetDeclaredSymbol(snapshot);
        Require(!method.Body.Statements.OfType<LocalDeclarationStatementSyntax>().SelectMany(statement => statement.Declaration.Variables)
                .Any(variable => variable.Initializer != null &&
                    SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(variable.Initializer.Value).Symbol, snapshotSymbol)),
            "A root writer copied its complete payload snapshot a second time.");
    }

    /// <summary>Preserves the fused integer path without re-emitting its unreachable literal branch in the fallback.</summary>
    /// <param name="writer">The fully expanded JSON state implementation.</param>
    private static void VerifyJsonIntegerFallback(MethodDeclarationSyntax writer)
    {
        var fused = writer.DescendantNodes().OfType<IfStatementSyntax>().Where(statement =>
            statement.Statement is ExpressionStatementSyntax
            {
                Expression: InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "WriteGeneratedInteger" }
                }
            } && statement.Condition.IsKind(SyntaxKind.LogicalAndExpression)).ToArray();
        Require(fused.Length != 0, "The JSON fixture no longer exercises a fused integer property.");
        foreach (var branch in fused)
        {
            var condition = (BinaryExpressionSyntax)branch.Condition;
            Require(condition.Right.ToString() == "global::System.BitConverter.IsLittleEndian" &&
                branch.Else?.Statement is BlockSyntax, "The fused integer guard no longer establishes the fallback's architecture.");
            var fallback = ((BlockSyntax)branch.Else!.Statement).Statements.OfType<IfStatementSyntax>().First();
            Require(SyntaxFactory.AreEquivalent(condition.Left, fallback.Condition) &&
                fallback.Statement is ExpressionStatementSyntax
                {
                    Expression: InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "WriteRaw" }
                    }
                } && fallback.Statement.ToString().Contains("Utf8_", StringComparison.Ordinal) && fallback.Else != null,
                "The known big-endian UTF-8 fallback duplicated the fused literal path or omitted the UTF-16 alternative.");
        }
    }

    /// <summary>Reports a generated-code contract failure.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
