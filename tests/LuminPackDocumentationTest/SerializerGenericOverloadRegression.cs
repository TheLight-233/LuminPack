using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks complete generic overload coverage when no concrete serializer entry is emitted.</summary>
internal static class SerializerGenericOverloadRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Executes independent consumers and checks roots supplied by another source generator.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string models = """
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
            [LuminPackable] public sealed class GenericPayload { public int Value; public string Text = ""; }
            [LuminPackable] public sealed class UnusedPayload { public long Value; }
            """;
        var input = CSharpCompilation.Create("GenericOverloadLibrary", [CSharpSyntaxTree.ParseText(models, Parse, "Models.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var library, out _);
        CheckAppendEntries(driver);
        Require(ConcreteMethods(driver).Length == 0, "A library without concrete calls retained concrete serializer entries.");
        byte[] image = Emit(library);
        const string consumer = """
            using System;
            using System.IO;
            using System.Text;
            using System.Threading;
            using LuminPack;
            using LuminPack.Option;
            using LuminPack.Utility;
            public static class Consumer
            {
                public static int Check()
                {
                    int cases = 0;
                    foreach (var encoding in new[] { LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16 })
            {
                if (encoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
                    RunProfile<global::LuminPack.Option.LuminPackDefaultOptions>();
                else
                    RunProfile<global::LuminPack.Option.LuminPackUtf16LengthOptions>();
                void RunProfile<TProfile>()
                    where TProfile : struct, global::LuminPack.Option.ILuminPackOptions
                {
                    var value = new GenericPayload
                    {
                        Value = 97,
                        Text = new string ('\u03bb', 1400)
                    };
                    byte[] expected = LuminPackSerializer.Serialize<GenericPayload, TProfile>(in value);
                    string json = LuminPackSerializer.SerializeJson<GenericPayload, TProfile>(value);
                    byte[] jsonBytes = (encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(json);
                    var buffer = LuminBufferWriterPool.Rent();
                    try
                    {
                        LuminPackSerializer.Serialize<GenericPayload, TProfile>(in value, buffer);
                        if (!buffer.GetSpan().SequenceEqual(expected))
                            throw new Exception("Buffer-owned binary fallback ignored its configuration.");
                        GenericPayload restored = null!;
                        LuminPackSerializer.Deserialize<GenericPayload, TProfile>(buffer, ref restored);
                        Validate(value, restored);
                        buffer.ResetCore();
                        LuminPackSerializer.SerializeJson<GenericPayload, TProfile>(value, buffer);
                        if (!buffer.GetSpan().SequenceEqual(jsonBytes))
                            throw new Exception("Buffer-owned JSON fallback ignored its encoding.");
                        if (LuminPackSerializer.DeserializeJson<GenericPayload, TProfile>(buffer, ref restored) != jsonBytes.Length)
                            throw new Exception("Buffer JSON consumed-byte count changed.");
                        Validate(value, restored);
                        if (typeof(LuminBufferWriter).GetProperty("Option") != null)
                            throw new Exception("The buffer retained mutable configuration.");
                        if (LuminPackSerializer.DeserializeJson<GenericPayload, TProfile>(json, ref restored) != jsonBytes.Length)
                            throw new Exception("String JSON consumed-byte count changed.");
                        Validate(value, restored);
                        if (LuminPackSerializer.DeserializeJson<GenericPayload, TProfile>(json.AsSpan(), ref restored) != jsonBytes.Length)
                            throw new Exception("Character JSON consumed-byte count changed.");
                        Validate(value, restored);
                        if (LuminPackSerializer.DeserializeJson<GenericPayload, TProfile>(jsonBytes.AsSpan(), ref restored) != jsonBytes.Length)
                            throw new Exception("Byte JSON consumed-byte count changed.");
                        Validate(value, restored);
                    }
                    finally
                    {
                        LuminBufferWriterPool.Return(buffer);
                    }

                    using var stream = new MemoryStream();
                    LuminPackSerializer.SerializeAsync<GenericPayload, TProfile>(stream, value).GetAwaiter().GetResult();
                    if (!stream.ToArray().AsSpan().SequenceEqual(expected))
                        throw new Exception("Async generic fallback changed the binary payload.");
                    stream.Position = 0;
                    Validate(value, LuminPackSerializer.DeserializeAsync<GenericPayload, TProfile>(stream).GetAwaiter().GetResult());
                    using var cancelled = new MemoryStream();
                    try
                    {
                        LuminPackSerializer.SerializeAsync<GenericPayload, TProfile>(cancelled, value, new CancellationToken(true)).GetAwaiter().GetResult();
                        throw new Exception("Async cancellation was ignored.");
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    using var recovered = new MemoryStream();
                    LuminPackSerializer.SerializeAsync<GenericPayload, TProfile>(recovered, value).GetAwaiter().GetResult();
                    if (!recovered.ToArray().AsSpan().SequenceEqual(expected))
                        throw new Exception("Cancelled write leaked pooled operation state.");
                    cases++;
                }
            }        return cases;
                }
                private static void Validate(GenericPayload expected, GenericPayload actual)
                {
                    if (actual.Value != expected.Value || actual.Text != expected.Text) throw new Exception("Generic fallback lost payload data.");
                }
            }
            """;
        var consumerInput = CSharpCompilation.Create("GenericOverloadConsumer", [CSharpSyntaxTree.ParseText(consumer, Parse)], references.Append(MetadataReference.CreateFromImage(image)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var loadContext = new AssemblyLoadContext("GenericOverloadConsumers", isCollectible: true);
        try
        {
            loadContext.LoadFromStream(new MemoryStream(image));
            var consumerAssembly = loadContext.LoadFromStream(new MemoryStream(Emit(consumerInput)));
            Require((int)consumerAssembly.GetType("Consumer")!.GetMethod("Check")!.Invoke(null, null)! == 2, "Generic overload consumers omitted an encoding.");
        }
        finally { loadContext.Unload(); }

        const string source = """
            public static class Entry
            {
                public static string Check()
                {
                    var value = new GenericPayload { Value = 103, Text = "other generator" };
                    foreach (var copy in new[] { value, OtherContext.Value })
                    {
                        string json = LuminPack.LuminPackSerializer.SerializeJson(copy);
                        LuminPack.LuminPackSerializer.DeserializeJson(OtherContext.Json, ref value);
                    }
                    return value.Text;
                }
            }
            """;
        var dependent = input.WithAssemblyName("OtherGeneratorOverloadRoots").AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, Parse, "Entry.cs"));
        var otherDriver = NewDriver(new OtherContextGenerator()).RunGeneratorsAndUpdateCompilation(dependent, out var completed, out _);
        Require(ConcreteMethods(otherDriver).Length == 0, "An unresolved contextual type retained unrelated concrete overloads.");
        var completedAssembly = Assembly.Load(Emit(completed));
        Require((string)completedAssembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! == "from generated context",
            "A later generator's contextual type did not bind through the generic fallback.");
        Console.WriteLine("Passed complete external generic overloads, UTF-8/UTF-16 ref reads, buffer contexts, cancellation recovery, and cross-generator API pruning.");
    }

    /// <summary>Creates independent serializer generators and an optional contextual source generator.</summary>
    /// <param name="additional">An optional source generator supplying contextual types after root analysis.</param>
    /// <returns>A driver using the same framework settings as the fixture.</returns>
    private static GeneratorDriver NewDriver(ISourceGenerator? additional = null)
    {
        var generators = new List<ISourceGenerator> { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator() };
        if (additional != null) generators.Add(additional);
        return CSharpGeneratorDriver.Create(generators, parseOptions: Parse);
    }

    /// <summary>Checks caller-buffer ownership and exceptional cursor detachment in shared compiled-profile entries.</summary>
    /// <param name="driver">The completed generator driver.</param>
    private static void CheckAppendEntries(GeneratorDriver driver)
    {
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        var entries = methods.Where(method => method.Identifier.ValueText is "Serialize" or "SerializeJson" &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "LuminBufferWriter")).ToArray();
        Require(entries.Length >= 4, "Shared binary/JSON default and compiled-profile append entries are missing.");
        foreach (var entry in entries)
            Require(!entry.ToString().Contains("ResetCore(", StringComparison.Ordinal) &&
                !entry.ToString().Contains("ResetOperationState(", StringComparison.Ordinal) &&
                !entry.DescendantNodes().OfType<CatchClauseSyntax>().Any(),
                "A caller-buffer write entry still clears or catches the caller's session.");
        var boundEntries = entries.Where(method => method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Any(creation => creation.Type.ToString() is "LuminPackWriter" or "LuminPackJsonWriter")).ToArray();
        Require(boundEntries.Length >= 2 && boundEntries.All(method => method.DescendantNodes().OfType<FinallyClauseSyntax>()
            .Any(clause => clause.ToString().Contains("CompleteWrite("))),
            "Shared append entries no longer detach their temporary cursor on exceptional exit.");
    }

    /// <summary>Obtains concrete public serializer declarations without counting shared generic cores.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>Concrete generated facade methods.</returns>
    private static MethodDeclarationSyntax[] ConcreteMethods(GeneratorDriver driver)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Where(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal))
            .SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword) && method.TypeParameterList == null &&
                method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() is "global::GenericPayload" or "global::UnusedPayload")).ToArray();

    /// <summary>Compiles a complete fixture and reports every actual compiler error.</summary>
    /// <param name="compilation">The library or consumer compilation.</param>
    /// <returns>The emitted managed assembly image.</returns>
    private static byte[] Emit(Compilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        return image.ToArray();
    }

    /// <summary>Reports a generic overload or API-root contract failure.</summary>
    /// <param name="condition">The expected invariant.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>Supplies contextual expressions that another generator cannot see during initial root analysis.</summary>
    private sealed class OtherContextGenerator : ISourceGenerator
    {
        /// <summary>Registers no syntax analysis for this fixed contextual fixture.</summary>
        /// <param name="context">The generator initialization context.</param>
        public void Initialize(GeneratorInitializationContext context) { }

        /// <summary>Produces strongly typed properties that repair the original contextual binding.</summary>
        /// <param name="context">The current generation context.</param>
        public void Execute(GeneratorExecutionContext context)
            => context.AddSource("OtherContext.g.cs", """
                public static class OtherContext
                {
                    public static GenericPayload Value => new GenericPayload();
                    public static string Json => "{\"Value\":107,\"Text\":\"from generated context\"}";
                }
                """);
    }
}
