using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks assembly-level roots for ordinary and generic referenced types.</summary>
internal static class SerializerAssemblyPackableRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Generates and executes direct and generic APIs for external types in each generation tier.</summary>
    /// <param name="references">Runtime and platform references used by generated compilations.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string librarySource = """
            using LuminPack.Attribute;
            namespace ExternalLibrary;
            public sealed class ExternalModel<T>
            {
                public int Number { get; set; }
                public T Value { get; set; } = default!;
            }
            public class ExternalBase
            {
                private string _baseHidden = "base private storage";
                public System.Collections.Generic.List<int> BaseValues { get; set; } = new();
                public string ReadBaseHidden() => _baseHidden;
            }
            public sealed class ExternalNote : ExternalBase
            {
                private string _hidden = "private storage";
                [LuminPackInclude] private int _secret;
                public string Label { get; set; } = string.Empty;
                public string Locked { get; private set; } = string.Empty;
                public int Computed { get => throw new System.Exception("Computed getter must not run."); set => throw new System.Exception("Computed setter must not run."); }
                public string ReadHidden() => _hidden;
                public int ReadSecret() => _secret;
                public void SetSecret(int value) => _secret = value;
                public void SetLocked(string value) => Locked = value;
            }
            [LuminPackable] public sealed class AnnotatedModel<T>
            {
                public T Value { get; set; } = default!;
            }
            public sealed class ExternalEnvelope
            {
                public ExternalNote Child { get; set; } = new();
                public System.Collections.Generic.List<ExternalNote> Children { get; set; } = new();
                public ExternalNote[] ArrayChildren { get; set; } = [];
            }
            public sealed class ExternalNode
            {
                private string _padding = "node private storage";
                public int Value { get; set; }
                public ExternalNode? Next { get; set; }
            }
            public sealed class MixedStorage
            {
                public int First { get; set; }
                public string Middle = string.Empty;
                public int Last { get; set; }
            }
            """;
        var library = CSharpCompilation.Create("ExternalPackableLibrary",
            [CSharpSyntaxTree.ParseText(librarySource, Parse, "ExternalModel.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var libraryImage = new MemoryStream();
        var libraryEmit = library.Emit(libraryImage);
        Require(libraryEmit.Success, string.Join(Environment.NewLine, libraryEmit.Diagnostics));
        MetadataReference libraryReference = MetadataReference.CreateFromImage(libraryImage.ToArray());

        const string consumerSource = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [assembly: LuminPackOption(StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF16)]
            [assembly: LuminPackable(typeof(ExternalLibrary.ExternalModel<>))]
            [assembly: LuminPackable(typeof(ExternalLibrary.ExternalNote))]
            [assembly: LuminPackable(typeof(ExternalLibrary.AnnotatedModel<>))]
            [assembly: LuminPackable(typeof(ExternalLibrary.ExternalEnvelope))]
            [assembly: LuminPackable(typeof(ExternalLibrary.ExternalNode))]
            [assembly: LuminPackable(typeof(ExternalLibrary.MixedStorage))]
            [LuminPackable] public sealed class LocalHost { public ExternalLibrary.ExternalNote Note = new(); }
            [LuminPackable] public sealed class MixedStorageTwin
            {
                public int First { get; set; }
                public string Middle = string.Empty;
                public int Last { get; set; }
            }
            public static class Entry
            {
                public static int Check()
                {
                    var value = new ExternalLibrary.ExternalModel<int> { Number = 41, Value = 17 };
                    byte[] bytes = LuminPackSerializer.Serialize(value);
                    ExternalLibrary.ExternalModel<int> restored = new();
                    LuminPackSerializer.Deserialize(bytes.AsSpan(), ref restored);
                    if (restored.Number != 41 || restored.Value != 17)
                        throw new Exception("External binary formatter did not preserve all marked members.");
                    string json = LuminPackSerializer.SerializeJson(value);
                    ExternalLibrary.ExternalModel<int> jsonRestored = new();
                    LuminPackSerializer.DeserializeJson(json, ref jsonRestored);
                    if (jsonRestored.Number != 41 || jsonRestored.Value != 17)
                        throw new Exception("External JSON formatter did not preserve all marked members.");

                    var textValue = new ExternalLibrary.ExternalModel<string> { Number = 19, Value = "external" };
                    byte[] textBytes = LuminPackSerializer.Serialize(textValue);
                    ExternalLibrary.ExternalModel<string> textRestored = new();
                    LuminPackSerializer.Deserialize(textBytes.AsSpan(), ref textRestored);
                    string textJson = LuminPackSerializer.SerializeJson(textValue);
                    ExternalLibrary.ExternalModel<string> textJsonRestored = new();
                    LuminPackSerializer.DeserializeJson(textJson, ref textJsonRestored);
                    if (textRestored.Number != 19 || textRestored.Value != "external" ||
                        textJsonRestored.Number != 19 || textJsonRestored.Value != "external")
                        throw new Exception("A second closed external construction was not formatted correctly.");

                    var note = new ExternalLibrary.ExternalNote { Label = "another assembly marker" };
                    note.SetSecret(79);
                    note.SetLocked("private setter");
                    note.BaseValues.Add(83);
                    byte[] noteBytes = LuminPackSerializer.Serialize(note);
                    ExternalLibrary.ExternalNote noteRestored = new();
                    LuminPackSerializer.Deserialize(noteBytes.AsSpan(), ref noteRestored);
                    if (noteRestored.Label != note.Label || noteRestored.ReadHidden() != "private storage" ||
                        noteRestored.ReadBaseHidden() != "base private storage" || noteRestored.ReadSecret() != 79 ||
                        noteRestored.Locked != "private setter" || noteRestored.BaseValues[0] != 83)
                        throw new Exception("A second assembly-level marker was not generated.");
                    var genericNote = RoundTrip(note);
                    if (genericNote.Label != note.Label || genericNote.Locked != note.Locked || genericNote.ReadSecret() != 79)
                        throw new Exception("An ordinary external type failed generic fallback dispatch.");
                    var hostBytes = LuminPackSerializer.Serialize(new LocalHost { Note = note });
                    LocalHost host = new();
                    LuminPackSerializer.Deserialize(hostBytes.AsSpan(), ref host);
                    if (host.Note.Label != note.Label || host.Note.ReadSecret() != 79)
                        throw new Exception("A nested external marked type bypassed its complete Local mirror.");
                    var envelope = new ExternalLibrary.ExternalEnvelope { Child = note };
                    envelope.Children.Add(note);
                    envelope.ArrayChildren = [note];
                    _ = LuminPackSerializer.Serialize(envelope);
                    var envelopeResult = RoundTrip(envelope);
                    if (envelopeResult.Child.ReadSecret() != 79 || envelopeResult.Children[0].Locked != "private setter" ||
                        envelopeResult.ArrayChildren[0].ReadSecret() != 79)
                        throw new Exception("An external member graph lost a nested marker or collection element.");
                    var node = new ExternalLibrary.ExternalNode { Value = 89, Next = new() { Value = 97 } };
                    _ = LuminPackSerializer.Serialize(node);
                    var nodeResult = RoundTrip(node);
                    if (nodeResult.Value != 89 || nodeResult.Next?.Value != 97 || nodeResult.Next.Next != null)
                        throw new Exception("An external recursive type lost its shared operation boundary.");
                    var mixed = new ExternalLibrary.MixedStorage { First = 101, Middle = "declaration order", Last = 103 };
                    var twin = new MixedStorageTwin { First = 101, Middle = "declaration order", Last = 103 };
                    byte[] mixedBytes = LuminPackSerializer.Serialize(mixed);
                    byte[] twinBytes = LuminPackSerializer.Serialize(twin);
                    if (!mixedBytes.AsSpan().SequenceEqual(twinBytes))
                        throw new Exception("External mixed fields and auto-properties changed the source-declared wire order.");
                    var annotated = new ExternalLibrary.AnnotatedModel<int> { Value = 59 };
                    byte[] annotatedBytes = LuminPackSerializer.Serialize(annotated);
                    ExternalLibrary.AnnotatedModel<int> annotatedRestored = new();
                    LuminPackSerializer.Deserialize(annotatedBytes.AsSpan(), ref annotatedRestored);
                    if (annotatedRestored.Value != 59 || RoundTrip(annotated).Value != 59)
                        throw new Exception("An annotated external generic type was not generated.");
                    return restored.Number + restored.Value;
                }
                private static T RoundTrip<T>(T value)
                {
                    byte[] bytes = LuminPackSerializer.Serialize<T>(value);
                    T restored = default!;
                    LuminPackSerializer.Deserialize<T>(bytes.AsSpan(), ref restored);
                    string json = LuminPackSerializer.SerializeJson<T>(value);
                    LuminPackSerializer.DeserializeJson<T>(json, ref restored);
                    return restored;
                }
            }
            """;

        foreach (string mode in new[] { "Full", "Light", "Minimal" })
        {
            var tree = CSharpSyntaxTree.ParseText(consumerSource.Replace("MODE", mode, StringComparison.Ordinal), Parse, "Consumer.cs");
            var input = CSharpCompilation.Create("ExternalPackableConsumer" + mode, [tree], references.Append(libraryReference),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            var errors = output.GetDiagnostics().Concat(driver.GetRunResult().Diagnostics)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            Require(errors.Length == 0, string.Join(Environment.NewLine, errors));

            var serializeCall = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                .First(invocation => invocation.Expression.ToString() == "LuminPackSerializer.Serialize" &&
                    invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString() == "value");
            IMethodSymbol? selected = output.GetSemanticModel(tree).GetSymbolInfo(serializeCall).Symbol as IMethodSymbol;
            Require(selected is { IsGenericMethod: false }, "An external marked type selected the generic serializer fallback in " + mode + ".");
            var noteCall = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                .First(invocation => invocation.Expression.ToString() == "LuminPackSerializer.Serialize" &&
                    invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString() == "note");
            Require(output.GetSemanticModel(tree).GetSymbolInfo(noteCall).Symbol is IMethodSymbol { IsGenericMethod: false },
                "An ordinary external type selected a generic entry in " + mode + ".");
            string generated = string.Join(Environment.NewLine, driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .Select(file => file.SourceText.ToString()));
            Require(generated.Contains("_hidden", StringComparison.Ordinal) && generated.Contains("_baseHidden", StringComparison.Ordinal) &&
                generated.Contains("LuminPackMarshal.As<global::ExternalLibrary.ExternalNote", StringComparison.Ordinal),
                "External private storage must retain complete Local mirrors and unsafe reinterpretation.");
            Require(!generated.Contains("Computed", StringComparison.Ordinal), "An external computed property acquired a storage slot.");
            var jsonHelpers = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Where(type => type.Identifier.ValueText.StartsWith("Json_", StringComparison.Ordinal) &&
                    type.Identifier.ValueText.Contains("ExternalModel", StringComparison.Ordinal)).ToArray();
            Require(jsonHelpers.Length == 1, "Closed constructions duplicated the external generic JSON cache implementation.");

            var changedTree = CSharpSyntaxTree.ParseText(tree.ToString().Replace(
                "typeof(ExternalLibrary.ExternalModel<>)", "typeof(ExternalLibrary.ExternalModel<int>)", StringComparison.Ordinal), Parse, "Consumer.cs");
            var changedInput = input.ReplaceSyntaxTree(tree, changedTree);
            var retained = driver.RunGeneratorsAndUpdateCompilation(changedInput, out var retainedOutput, out _);
            GeneratorDriver fresh = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse);
            fresh = fresh.RunGeneratorsAndUpdateCompilation(changedInput, out var freshOutput, out _);
            string[] RetainedFiles(GeneratorDriver current) => current.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal).ToArray();
            Require(RetainedFiles(retained).SequenceEqual(RetainedFiles(fresh)), "Changing an assembly root reused stale generated output.");
            Require(!retainedOutput.GetDiagnostics().Concat(freshOutput.GetDiagnostics()).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "Changing an open assembly root to one closed construction produced invalid generated output.");
            Require(driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .Any(file => file.HintName.StartsWith("LuminPack.Formatter.", StringComparison.Ordinal) &&
                    file.SourceText.ToString().Contains("ExternalLibrary.ExternalModel<int>", StringComparison.Ordinal)),
                "The external generic formatter operation was not generated in " + mode + ".");

            using var consumerImage = new MemoryStream();
            var consumerEmit = output.Emit(consumerImage);
            Require(consumerEmit.Success, string.Join(Environment.NewLine, consumerEmit.Diagnostics));
            var context = new AssemblyLoadContext("ExternalPackableConsumer" + mode, isCollectible: true);
            try
            {
                context.LoadFromStream(new MemoryStream(libraryImage.ToArray()));
                consumerImage.Position = 0;
                Assembly assembly = context.LoadFromStream(consumerImage);
                Require((int)assembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! == 58,
                    "The external marked type failed its serialization round trip in " + mode + ".");
            }
            finally { context.Unload(); }
        }

        var lifetime = CreatePlanCache(references.Append(libraryReference).ToArray());
        for (int attempt = 0; attempt < 8 && lifetime.Input.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Require(!lifetime.Input.IsAlive, "Full referenced metadata retained a previous compilation through an operation cache.");
        GC.KeepAlive(lifetime.Cache);

        Console.WriteLine("Passed repeatable assembly-level roots, external generic constructions, direct APIs, and binary/JSON/size operations in Full/Light/Minimal.");
    }

    /// <summary>Builds a referenced type plan and discards all transient compilation and metadata symbols.</summary>
    /// <param name="references">Platform references and the external fixture image.</param>
    /// <returns>A surviving plan cache and a weak reference to its input compilation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Cache, WeakReference Input) CreatePlanCache(MetadataReference[] references)
    {
        var input = CSharpCompilation.Create("ExternalMetadataLifetime",
            [CSharpSyntaxTree.ParseText("[assembly: LuminPack.Attribute.LuminPackable(typeof(ExternalLibrary.ExternalNote))]", Parse)],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.BinaryOperationPlanCache", throwOnError: true)!;
        object cache = Activator.CreateInstance(type, nonPublic: true)!;
        type.GetMethod("Get", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cache,
            [input.GetTypeByMetadataName("ExternalLibrary.ExternalNote")!, input, new MetaInfo(Parse, Parse.LanguageVersion, true, true, true), false]);
        return (cache, new WeakReference(input));
    }

    /// <summary>Fails the regression with the supplied compiler or runtime detail.</summary>
    /// <param name="condition">Whether the asserted behavior succeeded.</param>
    /// <param name="message">The detail to report when the condition is false.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
