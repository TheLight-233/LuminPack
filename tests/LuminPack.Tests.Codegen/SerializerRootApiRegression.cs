using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks first-build overload selection, formatter roots, generic fallback, and incremental invalidation.</summary>
internal static class SerializerRootApiRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
    private const string Models = """
        using System;
        using System.Buffers;
        using System.IO;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Utility;
        using S = LuminPack.LuminPackSerializer;
        using static LuminPack.LuminPackSerializer;
        [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable] public sealed class Payload { public int Value; }
        [LuminPackable] public sealed class Unused { public long Other; }
        """;

    /// <summary>Exercises every concrete overload and the distinct Light and Minimal root rules.</summary>
    /// <param name="references">The platform and unchanged runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        string[] calls =
        [
            "_ = S.Serialize(in value);",
            "S.Serialize(in value, buffer);",
            "_ = S.SerializeJson(value);",
            "S.SerializeJson(value, buffer);",
            "_ = S.SerializeAsync(stream, value);",
            "S.Deserialize(bytes.AsSpan(), ref value);",
            "S.Deserialize(buffer, ref value);",
            "S.Deserialize(sequence, ref value);",
            "S.DeserializeJson(text, ref value);",
            "S.DeserializeJson(bytes.AsSpan(), ref value);",
            "S.DeserializeJson(text.AsSpan(), ref value);",
            "S.DeserializeJson(buffer, ref value);",
            "_ = S.DeserializeAsync(stream, value);",
            "_ = Sizeof(value: value);",
            "S.Serialize(in value, output);",
            "S.SerializeJson(value, output);",
        ];
        for (int index = 0; index < calls.Length; index++)
        {
            string entry = "public static class Entry { public static void Call(Payload value, LuminBufferWriter buffer, Stream stream, byte[] bytes, string text, ReadOnlySequence<byte> sequence, IBufferWriter<byte> output) { " + calls[index] + " } }";
            var input = Create(references, "Minimal", entry).AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                "namespace LuminPack { public static partial class LuminPackSerializer { public static int Helper() => 17; } }", Parse, "Helpers.cs"));
            var driver = Run(input, out var output);
            var concrete = Concrete(driver, "Payload");
            Require(concrete.Length == 1, "Expected exactly one selected concrete overload for case " + index + "; got " + concrete.Length);
            Require(Concrete(driver, "Unused").Length == 0, "Unused concrete APIs survived Minimal pruning.");
            Require(!driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Any(file => file.HintName.Contains("Unused", StringComparison.Ordinal)), "Unused formatter survived Minimal first-build pruning.");
            Require(concrete[0].GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)), "Selected overload lost its XML documentation.");
            var callTree = input.SyntaxTrees.Single(tree => tree.GetRoot().ToString().Contains("public static class Entry", StringComparison.Ordinal));
            var call = callTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
            if (index is 5 or 9 or 10) call = callTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            var selected = output.GetSemanticModel(call.SyntaxTree).GetSymbolInfo(call).Symbol as IMethodSymbol;
            Require(selected is { IsGenericMethod: false }, "Selected call unexpectedly fell back to a generic API for case " + index);
        }

        var light = Run(Create(references, "Light", ""), out _);
        Require(Concrete(light, "Payload").Length == 0 && Concrete(light, "Unused").Length == 0, "Light property roots retained unused concrete APIs.");
        Require(light.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Any(file => file.HintName.Contains("Unused", StringComparison.Ordinal)), "Light must retain annotated formatters for generic fallback.");
        var empty = Run(Create(references, "Minimal", ""), out _);
        Require(!empty.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Any(file => file.HintName.Contains("Unused", StringComparison.Ordinal)), "Empty Minimal roots must not fall back to Light.");
        var full = Run(Create(references, "Full", ""), out _);
        Require(Concrete(full, "Payload").Length == 16 && Concrete(full, "Unused").Length == 16, "Full concrete API compatibility changed.");

        const string forwarding = """
            public static class Entry
            {
                public static byte[] Forward<T>(T value) => S.Serialize<T>(in value);
                public static byte[] Call(Payload value) => Forward(value);
            }
            """;
        var generic = Run(Create(references, "Minimal", forwarding), out _);
        Require(Concrete(generic, "Payload").Length == 0, "Generic forwarding should use shared fallback rather than generate concrete APIs.");
        Require(generic.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Any(file => file.HintName.Contains("Payload", StringComparison.Ordinal)), "Generic forwarding lost its closed formatter root.");
        var forwardedGroup = Run(Create(references, "Minimal", forwarding.Replace("public static byte[] Call(Payload value) => Forward(value);",
            "public static Func<Payload, byte[]> Call = Forward;", StringComparison.Ordinal)), out _);
        Require(Concrete(forwardedGroup, "Payload").Length == 0 && forwardedGroup.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Any(file => file.HintName.Contains("Payload", StringComparison.Ordinal)), "A generic forwarding method group lost its closed formatter root.");

        var group = Run(Create(references, "Minimal", "public static class Entry { public static Func<Payload, int> Group = S.Sizeof; }"), out _);
        Require(Concrete(group, "Payload").Length == 1, "Contextual method groups must retain their selected overload.");
        VerifyTransition(references);
        VerifyUnrelatedEdit(references);
        VerifyBehavior(references);
        VerifyExternalFallback(references);
        VerifyUnmarkedConsumer(references);
        VerifyBclProfiles(references);
        VerifyConcreteScalarPaths(references);
        Console.WriteLine("Passed 16 first-build overload selections, Full/Light/Minimal roots, generic forwarding, method groups, API edits, and external generic fallback.");
    }

    /// <summary>Requires statically typed scalar facades to use the same stateless raw core as the generic fast path.</summary>
    /// <param name="references">The platform and runtime metadata.</param>
    private static void VerifyConcreteScalarPaths(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var input = Create(references, mode, """
                public static class Entry
                {
                    public static int Check()
                    {
                        int value = 731;
                        var bytes = S.Serialize(value);
                        S.Deserialize(bytes.AsSpan(), ref value);
                        return value;
                    }
                }
                """);
            var driver = Run(input, out _);
            var allMethods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
            var methods = allMethods
                .Where(method => method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == "value" && parameter.Type?.ToString() == "int") &&
                    method.TypeParameterList?.Parameters.Count == 1 && method.Identifier.ValueText is "Serialize" or "Deserialize" &&
                    (method.Identifier.ValueText == "Serialize" ? method.ParameterList.Parameters.Count == 1 :
                        method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[0].Type?.ToString() == "ReadOnlySpan<byte>")).ToArray();
            Require(methods.Length == 2, "Concrete scalar facade entries are missing in " + mode + ": " + string.Join("; ", methods.Select(m => m.Identifier.ValueText + m.TypeParameterList + m.ParameterList)) + " / " + string.Join(",", driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName)));
            string Body(MethodDeclarationSyntax method)
            {
                if (method.Body!.Statements is [ReturnStatementSyntax { Expression: InvocationExpressionSyntax { Expression: GenericNameSyntax helper } }] &&
                    helper.Identifier.ValueText.StartsWith("__LuminFacade_", StringComparison.Ordinal))
                    return allMethods.Single(candidate => candidate.Identifier.ValueText == helper.Identifier.ValueText).Body!.ToString();
                return method.Body.ToString();
            }
            Require(methods.All(method => !Body(method).Contains("FormatterDispatch", StringComparison.Ordinal) &&
                !Body(method).Contains("threadStatic", StringComparison.Ordinal) && !Body(method).Contains("Pool.Rent", StringComparison.Ordinal)),
                "A concrete scalar retained formatter dispatch or pooled state in " + mode + ".");
            Require(Body(methods.Single(method => method.Identifier.ValueText == "Serialize")).Contains("LuminPackWriter.Write", StringComparison.Ordinal) &&
                Body(methods.Single(method => method.Identifier.ValueText == "Deserialize")).Contains("LuminPackReader.Read", StringComparison.Ordinal),
                "A concrete scalar bypassed the architecture-aware fixed storage core.");
        }
    }

    /// <summary>Bootstraps primitive-only consumers and retains the unrelated-assembly emission guard.</summary>
    /// <param name="references">The common runtime metadata.</param>
    private static void VerifyUnmarkedConsumer(MetadataReference[] references)
    {
        const string source = """
            using LuminPack;
            public static class Entry
            {
                public static int Check()
                {
                    var bytes = LuminPackSerializer.Serialize(42);
                    return LuminPackSerializer.Deserialize<int>(bytes);
                }
            }
            """;
        var input = CSharpCompilation.Create("UnmarkedConsumer", [CSharpSyntaxTree.ParseText(source, Parse, "Entry.cs")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = Run(input, out var output);
        using var image = new MemoryStream();
        Require(output.Emit(image).Success, "Primitive-only consumer failed to emit.");
        var context = new AssemblyLoadContext("UnmarkedConsumer", isCollectible: true);
        try
        {
            image.Position = 0;
            Require((int)context.LoadFromStream(image).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! == 42,
                "Primitive-only consumer did not round-trip.");
        }
        finally { context.Unload(); }
        var tree = input.SyntaxTrees.Single();
        var unrelated = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText("public static class Entry { public static int Check() => 42; }", Parse, "Entry.cs"));
        driver = driver.RunGeneratorsAndUpdateCompilation(unrelated, out _, out _);
        Require(!driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Any(), "An unrelated consumer acquired serializer output.");
        Require(Sources(driver).SequenceEqual(Sources(Run(unrelated, out _))), "Removing the last API call changed retained/fresh output.");
    }

    /// <summary>Checks primitive children need no caller-authored roots when a BCL operation carries an explicit profile.</summary>
    /// <param name="references">The platform and runtime metadata.</param>
    private static void VerifyBclProfiles(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            string source = $$"""
                using System;
                using LuminPack;
                using LuminPack.Attribute;
                using LuminPack.Option;
                [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.{{mode}})]
                public static class Entry
                {
                    public static int Check()
                    {
                        var value = new Lazy<int>(17);
                        var bytes = LuminPackSerializer.Serialize<Lazy<int>, LuminPackUtf16LengthOptions>(value);
                        var copy = LuminPackSerializer.Deserialize<Lazy<int>, LuminPackUtf16LengthOptions>(bytes);
                        if (copy.Value != 17 || LuminPackSerializer.Sizeof<Lazy<int>, LuminPackUtf16LengthOptions>(value) != bytes.Length)
                            throw new Exception("Lazy primitive profile failed.");
                        int? number = 255;
                        bytes = LuminPackSerializer.Serialize<int?, LuminPackUtf16LengthOptions>(number);
                        if (LuminPackSerializer.Deserialize<int?, LuminPackUtf16LengthOptions>(bytes) != number)
                            throw new Exception("Nullable primitive profile failed.");
                        var tuple = Tuple.Create(13, 19L);
                        bytes = LuminPackSerializer.Serialize<Tuple<int, long>, LuminPackUtf16LengthOptions>(tuple);
                        if (!tuple.Equals(LuminPackSerializer.Deserialize<Tuple<int, long>, LuminPackUtf16LengthOptions>(bytes)))
                            throw new Exception("Tuple primitive profile failed.");
                        return 17;
                    }
                }
                """;
            var input = CSharpCompilation.Create("BclProfiles" + mode, [CSharpSyntaxTree.ParseText(source, Parse, "Entry.cs")],
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            Run(input, out var output);
            using var image = new MemoryStream();
            Require(output.Emit(image).Success, "BCL profile consumer failed to emit.");
            var context = new AssemblyLoadContext("BclProfiles" + mode, isCollectible: true);
            try
            {
                image.Position = 0;
                Require((int)context.LoadFromStream(image).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! == 17,
                    "BCL profile consumer failed to execute.");
            }
            finally { context.Unload(); }
        }
    }

    /// <summary>Requires unchanged facade signatures and call bindings to be reused after an unrelated file edit.</summary>
    /// <param name="references">The common runtime references.</param>
    private static void VerifyUnrelatedEdit(MetadataReference[] references)
    {
        var utility = CSharpSyntaxTree.ParseText("internal static class Utility { internal static int Touch() => 17; }", Parse, "Utility.cs");
        var input = Create(references, "Minimal", "public static class Entry { public static object Call(Payload value) => S.Serialize(in value); }").AddSyntaxTrees(utility);
        var driver = Run(input, out _);
        var metrics = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.IncrementalGenerationMetrics", throwOnError: true)!;
        long Read(string name) => (long)metrics.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
        string[] counters = ["ApiSignatureBuilds", "ApiRootInvocationBindings", "AggregateBuilds", "PackableModelBuilds"];
        var before = counters.ToDictionary(name => name, Read);
        var edited = input.ReplaceSyntaxTree(utility, CSharpSyntaxTree.ParseText(utility.ToString().Replace("17", "18", StringComparison.Ordinal), Parse, utility.FilePath));
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out _);
        CheckErrors(output);
        foreach (string counter in counters) Require(Read(counter) == before[counter], "Unrelated edit repeated " + counter);
    }

    /// <summary>Executes selected binary and JSON entry points with nested dependencies and both text encodings.</summary>
    /// <param name="references">The common platform and unchanged runtime references.</param>
    private static void VerifyBehavior(MetadataReference[] references)
    {
        const string entry = """
            [LuminPack.Attribute.LuminPackable] public sealed class Parent { public Payload Child = new(); }
            public static class Entry
            {
                public static string Check()
                {
                    var value = new Parent { Child = new Payload { Value = 913 } };
                    byte[] bytes = S.Serialize(in value);
                    Parent restored = null!;
                    S.Deserialize(bytes.AsSpan(), ref restored);
                    if (restored.Child.Value != 913 || S.Sizeof(value) != bytes.Length) throw new Exception("Binary or nested formatter parity failed.");
                    foreach (var encoding in new[] { LuminPack.Option.LuminPackStringEncoding.UTF8, LuminPack.Option.LuminPackStringEncoding.UTF16 })
            {
                if (encoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
                    RunProfile<global::LuminPack.Option.LuminPackUtf8LengthOptions>();
                else
                    RunProfile<global::LuminPack.Option.LuminPackUtf16LengthOptions>();
                void RunProfile<TProfile>()
                    where TProfile : struct, global::LuminPack.Option.ILuminPackOptions
                {
                    string json = S.SerializeJson<Parent, TProfile>(value);
                    Parent fromJson = null!;
                    S.DeserializeJson<Parent, TProfile>(json, ref fromJson);
                    if (fromJson.Child.Value != 913)
                        throw new Exception("Selected JSON string/ref or nested formatter parity failed.");
                    S.DeserializeJson<Parent, TProfile>(json.AsSpan(), ref fromJson);
                    if (fromJson.Child.Value != 913)
                        throw new Exception("Selected JSON character/ref parity failed.");
                }
            }        return Convert.ToHexString(bytes);
                }
            }
            """;
        string? expected = null;
        foreach (string mode in new[] { "Full", "Light", "Minimal" })
        {
            Run(Create(references, mode, entry), out var output);
            using var image = new MemoryStream();
            var result = output.Emit(image);
            Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            var context = new AssemblyLoadContext("RootApiBehavior" + mode, isCollectible: true);
            try
            {
                image.Position = 0;
                var assembly = context.LoadFromStream(image);
                string actual = (string)assembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)!;
                expected ??= actual;
                Require(expected == actual, "Call-selected APIs changed the Full binary representation in " + mode);
            }
            finally { context.Unload(); }
        }
    }

    /// <summary>Checks a call-only edit against a fresh driver, with unchanged formatter types.</summary>
    /// <param name="references">The references used by both compilations.</param>
    private static void VerifyTransition(MetadataReference[] references)
    {
        var before = Create(references, "Light", "public static class Entry { public static object Call(Payload value) => S.Serialize(in value); }");
        var tree = before.SyntaxTrees.Last();
        var after = before.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(tree.ToString().Replace("S.Serialize(in value)", "S.Sizeof(value)", StringComparison.Ordinal), Parse, tree.FilePath));
        var retained = Run(before, out _).RunGeneratorsAndUpdateCompilation(after, out var output, out _);
        CheckErrors(output);
        var fresh = Run(after, out _);
        var actual = Concrete(retained, "Payload");
        Require(actual.Length == 1 && actual[0].Identifier.ValueText == "Sizeof", "A call-only edit reused a stale concrete overload mask.");
        Require(Sources(retained).SequenceEqual(Sources(fresh)), "Retained call-edit output differs from fresh generation.");
    }

    /// <summary>Emits a library with no concrete facade roots and executes an independent consumer through generic fallback.</summary>
    /// <param name="references">The common platform and runtime references.</param>
    private static void VerifyExternalFallback(MetadataReference[] references)
    {
        Run(Create(references, "Light", ""), out var library);
        using var libraryImage = new MemoryStream();
        var libraryResult = library.Emit(libraryImage);
        Require(libraryResult.Success, string.Join(Environment.NewLine, libraryResult.Diagnostics));
        const string consumerSource = """
            public static class Consumer
            {
                public static int Check()
                {
                    var value = new Payload { Value = 731 };
                    byte[] bytes = LuminPack.LuminPackSerializer.Serialize(in value);
                    var restored = LuminPack.LuminPackSerializer.Deserialize<Payload>(bytes);
                    string json = LuminPack.LuminPackSerializer.SerializeJson(value);
                    var jsonRestored = LuminPack.LuminPackSerializer.DeserializeJson<Payload>(json);
                    return restored.Value + jsonRestored.Value;
                }
            }
            """;
        var consumer = CSharpCompilation.Create("RootApiConsumer", [CSharpSyntaxTree.ParseText(consumerSource, Parse)],
            references.Append(MetadataReference.CreateFromImage(libraryImage.ToArray())), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        CheckErrors(consumer);
        using var consumerImage = new MemoryStream();
        Require(consumer.Emit(consumerImage).Success, "External fallback consumer could not be emitted.");
        var context = new AssemblyLoadContext("RootApiFallback", isCollectible: true);
        try
        {
            libraryImage.Position = 0;
            context.LoadFromStream(libraryImage);
            consumerImage.Position = 0;
            var assembly = context.LoadFromStream(consumerImage);
            Require((int)assembly.GetType("Consumer")!.GetMethod("Check")!.Invoke(null, null)! == 1462, "External generic binary/JSON fallback lost annotated formatter dependencies.");
        }
        finally { context.Unload(); }
    }

    /// <summary>Creates a first-build input with no generated serializer declaration.</summary>
    /// <param name="references">The common runtime references.</param>
    /// <param name="mode">The existing project generation mode.</param>
    /// <param name="entry">Source containing actual project calls.</param>
    /// <returns>A compilation whose facade calls initially have no declaration to bind.</returns>
    private static CSharpCompilation Create(MetadataReference[] references, string mode, string entry)
        => CSharpCompilation.Create("RootApiLibrary", [CSharpSyntaxTree.ParseText(Models.Replace("MODE", mode, StringComparison.Ordinal), Parse, "Models.cs"),
            CSharpSyntaxTree.ParseText("using System; using System.Buffers; using System.IO; using LuminPack.Utility; using S = LuminPack.LuminPackSerializer; using static LuminPack.LuminPackSerializer; " + entry, Parse, "Entry.cs")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

    /// <summary>Runs the production generator and requires compilable generated code.</summary>
    /// <param name="input">The original source compilation.</param>
    /// <param name="output">Receives the updated compilation.</param>
    /// <returns>The retained production driver.</returns>
    private static GeneratorDriver Run(CSharpCompilation input, out Compilation output)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out output, out var diagnostics);
        Require(driver.GetRunResult().Results.All(result => result.Exception == null), string.Join(Environment.NewLine, driver.GetRunResult().Results.Select(result => result.Exception?.ToString())));
        Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        CheckErrors(output);
        return driver;
    }

    /// <summary>Finds every typed facade overload, including any accidentally retained private entry.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <param name="payload">The payload's simple global type name.</param>
    /// <returns>Concrete overload declarations in generated order.</returns>
    private static MethodDeclarationSyntax[] Concrete(GeneratorDriver driver, string payload)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Where(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal))
            .SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" } && method.TypeParameterList == null &&
                method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "global::" + payload)).ToArray();

    /// <summary>Orders exact source snapshots for retained-driver comparison.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <returns>Stable hint-name and source pairs.</returns>
    private static string[] Sources(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .OrderBy(file => file.HintName, StringComparer.Ordinal).Select(file => file.HintName + "\n" + file.SourceText).ToArray();

    /// <summary>Requires a compilation without source or generated-code errors.</summary>
    /// <param name="compilation">The compilation being checked.</param>
    private static void CheckErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }

    /// <summary>Reports a failed API or formatter contract.</summary>
    /// <param name="condition">The contract condition.</param>
    /// <param name="message">The failure description.</param>
    /// <exception cref="InvalidOperationException">The contract failed.</exception>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
