using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks assembly-level roots for referenced generic packable types.</summary>
internal static class SerializerAssemblyPackableRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Generates and executes direct formatters for an external generic type in each generation tier.</summary>
    /// <param name="references">Runtime and platform references used by generated compilations.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string librarySource = """
            namespace ExternalLibrary;
            public sealed class ExternalModel<T>
            {
                public int Number { get; set; }
                public T Value { get; set; } = default!;
            }
            public sealed class ExternalNote
            {
                public string Label { get; set; } = string.Empty;
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
            [assembly: LuminPackable(typeof(ExternalLibrary.ExternalModel<>))]
            [assembly: LuminPackable(typeof(ExternalLibrary.ExternalNote))]
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
                    if (LuminPackSerializer.Sizeof(value) != bytes.Length)
                        throw new Exception("External formatter size did not match its binary payload.");

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
                    byte[] noteBytes = LuminPackSerializer.Serialize(note);
                    ExternalLibrary.ExternalNote noteRestored = new();
                    LuminPackSerializer.Deserialize(noteBytes.AsSpan(), ref noteRestored);
                    if (noteRestored.Label != note.Label)
                        throw new Exception("A second assembly-level marker was not generated.");
                    return restored.Number + restored.Value;
                }
            }
            """;

        foreach (string mode in new[] { "Full", "Light", "Minimal" })
        {
            var tree = CSharpSyntaxTree.ParseText(consumerSource.Replace("MODE", mode, StringComparison.Ordinal), Parse, "Consumer.cs");
            var input = CSharpCompilation.Create("ExternalPackableConsumer" + mode, [tree], references.Append(libraryReference),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: Parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            var errors = output.GetDiagnostics().Concat(driver.GetRunResult().Diagnostics)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            Require(errors.Length == 0, string.Join(Environment.NewLine, errors));

            var serializeCall = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                .First(invocation => invocation.Expression.ToString() == "LuminPackSerializer.Serialize" &&
                    invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString() == "value");
            IMethodSymbol? selected = output.GetSemanticModel(tree).GetSymbolInfo(serializeCall).Symbol as IMethodSymbol;
            Require(selected is { IsGenericMethod: false }, "An external marked type selected the generic serializer fallback in " + mode + ".");
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

        Console.WriteLine("Passed repeatable assembly-level roots, external generic constructions, direct APIs, and binary/JSON/size operations in Full/Light/Minimal.");
    }

    /// <summary>Fails the regression with the supplied compiler or runtime detail.</summary>
    /// <param name="condition">Whether the asserted behavior succeeded.</param>
    /// <param name="message">The detail to report when the condition is false.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
