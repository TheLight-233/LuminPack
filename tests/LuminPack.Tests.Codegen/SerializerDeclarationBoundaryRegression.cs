extern alias ModelBasedAnalyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks that unsupported declaration edges are reported before publishing invalid formatter code.</summary>
internal static class SerializerDeclarationBoundaryRegression
{
    /// <summary>Compiles invalid and supported signatures together with the generated formatters.</summary>
    /// <param name="references">The platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using LuminPack.Attribute;

            [LuminPackable] file class FilePayload { public int Count; }
            file class FileContainer
            {
                [LuminPackable] public class FileNestedPayload { public int Count; }
            }
            public class Container
            {
                private class Hidden
                {
                    [LuminPackable] public class HiddenPayload { public int Count; }
                }
            }
            [LuminPackable] public class GenericCallback
            {
                [LuminPackOnSerialized] public void Notify<T>() { }
            }
            [LuminPackable] public class GenericPool
            {
                [LuminPackPoolRent] public static GenericPool Rent<T>() => new();
            }
            [LuminPackable] public class PrivatePool
            {
                [LuminPackPoolRent] private static PrivatePool Rent() => new();
            }
            [LuminPackable] public class ReferenceConstructor
            {
                public int Count;
                public ReferenceConstructor(ref int count) => Count = count;
            }
            [LuminPackable] public class IncompatibleConstructor
            {
                public int Count;
                public IncompatibleConstructor(string count) => Count = count.Length;
            }
            internal class AccessibleContainer
            {
                [LuminPackable] public class SupportedConstructor
                {
                    public int Count;
                    public SupportedConstructor(in long count) => Count = (int)count;
                    [LuminPackOnDeserialized] internal void Notify() { }
                }
            }
            [LuminPackable] public class SupportedPool
            {
                public int Count;
                [LuminPackPoolRent] internal static SupportedPool Rent() => new();
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols:
            ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER", "NET11_0_OR_GREATER"]);
        var tree = CSharpSyntaxTree.ParseText(source, parse, "DeclarationBoundaries.cs");
        var input = TestCompilation.Create("DeclarationBoundaries", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Diagnostic[] errors = output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(item => item.ToString())));

        var expected = new (string Id, string Symbol)[]
        {
            ("LuminPack004", "Notify"),
            ("LuminPack031", "Rent"),
            ("LuminPack120", "FileNestedPayload"),
            ("LuminPack120", "FilePayload"),
            ("LuminPack120", "HiddenPayload"),
            ("LuminPack121", "Rent"),
            ("LuminPack122", "count"),
            ("LuminPack122", "count"),
        };
        var actual = diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)
            .Select(item => (item.Id, item.Location.SourceTree == tree
                ? source.Substring(item.Location.SourceSpan.Start, item.Location.SourceSpan.Length)
                : string.Empty)).OrderBy(item => item.Id, StringComparer.Ordinal)
            .ThenBy(item => item.Item2, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected))
            throw new InvalidOperationException("Unexpected declaration diagnostics: " + string.Join(", ", actual.AsEnumerable()));

        var hints = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(item => item.HintName).ToArray();
        foreach (string supported in new[] { "SupportedConstructor", "SupportedPool" })
            if (!hints.Any(hint => hint.Contains(supported, StringComparison.Ordinal)))
                throw new InvalidOperationException("Missing formatter for " + supported + ".");
        VerifyKeywordIdentifiers(references, parse);
        Console.WriteLine("Passed declaration diagnostics and supported constructor, callback and pool signatures.");
    }

    /// <summary>Compiles and exercises record and Union identifiers whose source spellings require escapes.</summary>
    /// <param name="references">The platform and runtime references.</param>
    /// <param name="parse">The consumer's language and framework options.</param>
    private static void VerifyKeywordIdentifiers(MetadataReference[] references, CSharpParseOptions parse)
    {
        parse = parse.WithFeatures([new("InterceptorsNamespaces", "LuminPack")]);
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;

            namespace @namespace.@event
            {
                [LuminPackable]
                public sealed class @class { public int @for; }

                public class @struct<@int> where @int : unmanaged
                {
                    [LuminPackable]
                    public sealed class @ref<@string> where @string : class
                    {
                        public @int @operator;
                        public @string Text;
                    }
                }

                [LuminPackable, LuminPackUnion(7, typeof(@case))]
                public abstract partial class @switch { }

                [LuminPackable]
                public sealed partial class @case : @switch { public int Number; }

                public static class Entry
                {
                    public static void Check()
                    {
                        var item = new @class { @for = 13 };
                        var bytes = LuminPackSerializer.Serialize(item);
                        var json = LuminPackSerializer.SerializeJson(item);
                        if (!bytes.AsSpan().SequenceEqual(new byte[] { 1, 13, 0, 0, 0 }) || json != "{\"for\":13}" ||
                            LuminPackSerializer.Deserialize<@class>(bytes).@for != 13 ||
                            LuminPackSerializer.DeserializeJson<@class>(json).@for != 13)
                            throw new InvalidOperationException("An escaped record identifier changed its serialization contract.");

                        var generic = new @struct<int>.@ref<string> { @operator = 17, Text = "generic" };
                        var genericBinary = LuminPackSerializer.Deserialize<@struct<int>.@ref<string>>(LuminPackSerializer.Serialize(generic));
                        var genericJson = LuminPackSerializer.DeserializeJson<@struct<int>.@ref<string>>(LuminPackSerializer.SerializeJson(generic));
                        if (genericBinary.@operator != 17 || genericBinary.Text != "generic" ||
                            genericJson.@operator != 17 || genericJson.Text != "generic")
                            throw new InvalidOperationException("An escaped nested generic identifier lost its formatter.");

                        @switch root = new @case { Number = 19 };
                        byte[] unionBytes = LuminPackSerializer.Serialize(root);
                        string unionJson = LuminPackSerializer.SerializeJson(root);
                        var binaryRoot = LuminPackSerializer.Deserialize<@switch>(unionBytes);
                        var jsonRoot = LuminPackSerializer.DeserializeJson<@switch>(unionJson);
                        using var document = System.Text.Json.JsonDocument.Parse(unionJson);
                        const uint caseHash = 0x5035AA42u;
                        if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(unionBytes) != caseHash ||
                            document.RootElement.GetProperty("$type").GetUInt32() != 7 ||
                            binaryRoot is not @case { Number: 19 } || jsonRoot is not @case { Number: 19 })
                            throw new InvalidOperationException("An escaped Union identifier lost its dispatch.");
                    }
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, parse, "KeywordIdentifiers.cs");
        var input = TestCompilation.Create("KeywordIdentifiers", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        var errors = emitted.Diagnostics.Concat(diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0 || driver.GetRunResult().Results.Any(result => result.Exception != null))
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(item => item.ToString())));
        System.Reflection.Assembly.Load(image.ToArray()).GetType("namespace.event.Entry")!.GetMethod("Check")!.Invoke(null, null);
    }
}
