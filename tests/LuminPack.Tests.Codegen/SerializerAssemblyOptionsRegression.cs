extern alias ModelBasedAnalyzer;

using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks profiles and contract settings on exact and open external assembly markers.</summary>
internal static class SerializerAssemblyOptionsRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Executes the generated contracts and checks failures at invalid option targets.</summary>
    /// <param name="references">The runtime and platform metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string library = """
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            namespace TargetedOptions;
            public sealed class Box<T> { public string Text = ""; public T Value = default!; }
            [LuminPackable, LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16)]
            public sealed class Annotated { public string Text = ""; }
            public struct NativeValue { public byte Tag; public long Number; }
            public struct VersionedValue { public int Number; }
            public sealed class CustomValue
            {
                public byte Value;
                public static void Serialize(ref LuminPackWriter writer, in CustomValue value) => writer.Write(value.Value);
                public static void Deserialize(ref LuminPackReader reader, ref CustomValue value)
                { value = new CustomValue(); reader.Read(ref value.Value); }
            }
            """;
        byte[] libraryImage = Emit(Create("TargetedOptionLibrary", library, references));
        MetadataReference[] consumerReferences = [.. references, MetadataReference.CreateFromImage(libraryImage)];

        const string declarations = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using TargetedOptions;
            [assembly: LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16)]
            [assembly: LuminPackable(typeof(Box<>))]
            [assembly: LuminPackOption(typeof(Box<>), StringEncoding = LuminPackStringEncoding.UTF8)]
            [assembly: LuminPackOption(typeof(Box<int>), GeneratorType = GeneratorType.Version, StringRecording = LuminPackStringRecording.VarInt)]
            [assembly: LuminPackable(typeof(Annotated))]
            [assembly: LuminPackOption(typeof(Annotated), StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.VarInt)]
            [assembly: LuminPackable(typeof(NativeValue))]
            [assembly: LuminPackOption(typeof(NativeValue), StructLayout = LuminPackStructLayout.Packed)]
            [assembly: LuminPackable(typeof(VersionedValue))]
            [assembly: LuminPackOption(typeof(VersionedValue), GeneratorType = GeneratorType.Version)]
            [assembly: LuminPackable(typeof(CustomValue))]
            [assembly: LuminPackOption(typeof(CustomValue), GeneratorType = GeneratorType.Custom)]
            [LuminPackable, LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]
            public sealed class OpenTwin { public string Text = ""; public long Value; }
            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.VarInt)]
            public sealed class ExactTwin { public string Text = ""; public int Value; }
            [LuminPackable, LuminPackOption(StringRecording = LuminPackStringRecording.VarInt)]
            public sealed class AnnotatedTwin { public string Text = ""; }
            [LuminPackable] public sealed class UntargetedRecord { public string Text = ""; }
            [LuminPackable, LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16, StringRecording = LuminPackStringRecording.Length)]
            public sealed class UntargetedTwin { public string Text = ""; }
            [LuminPackable] public sealed class Nested { public Box<int> Child = new(); }
            """;
        const string entry = """
            public static class Entry
            {
                private static void Same(byte[] actual, byte[] expected, string reason)
                { if (!actual.AsSpan().SequenceEqual(expected)) throw new Exception(reason); }
                public static void Check()
                {
                    const string text = "ascii and é漢";
                    var exact = new Box<int> { Text = text, Value = 18 };
                    var exactBytes = LuminPackSerializer.Serialize(exact);
                    Same(exactBytes, LuminPackSerializer.Serialize(new ExactTwin { Text = text, Value = 18 }), "Exact type lost Version or its open profile.");
                    Box<int> restored = new();
                    LuminPackSerializer.Deserialize(exactBytes, ref restored);
                    if (restored.Text != text || restored.Value != 18) throw new Exception("Exact type did not round-trip.");
                    var open = new Box<long> { Text = text, Value = 29 };
                    Same(LuminPackSerializer.Serialize(open), LuminPackSerializer.Serialize(new OpenTwin { Text = text, Value = 29 }), "Open option was not selected.");
                    var annotated = new Annotated { Text = text };
                    Same(LuminPackSerializer.Serialize(annotated), LuminPackSerializer.Serialize(new AnnotatedTwin { Text = text }), "Type-owned options did not override the targeted value or take its remaining settings.");
                    Same(LuminPackSerializer.Serialize(new UntargetedRecord { Text = text }), LuminPackSerializer.Serialize(new UntargetedTwin { Text = text }), "Targeted options leaked into the default assembly profile.");
                    var value = new NativeValue { Tag = 4, Number = 71 };
                    var packed = LuminPackSerializer.Serialize(value);
                    if (packed.Length != 9) throw new Exception("The targeted Packed contract was not selected.");
                    NativeValue packedResult = default;
                    LuminPackSerializer.Deserialize(packed, ref packedResult);
                    if (packedResult.Tag != 4 || packedResult.Number != 71) throw new Exception("The targeted Packed value did not round-trip.");
                    var versioned = new VersionedValue[] { new() { Number = 72 } };
                    var versionedBytes = LuminPackSerializer.Serialize(versioned);
                    if (versionedBytes.Length <= 8) throw new Exception("An unmanaged array bypassed the targeted Version contract.");
                    VersionedValue[] versionedResult = [];
                    LuminPackSerializer.Deserialize(versionedBytes, ref versionedResult);
                    if (versionedResult.Length != 1 || versionedResult[0].Number != 72) throw new Exception("The versioned array did not round-trip.");
                    var nested = new Nested { Child = exact };
                    var nestedBytes = LuminPackSerializer.Serialize(nested);
                    Nested nestedResult = new();
                    LuminPackSerializer.Deserialize(nestedBytes, ref nestedResult);
                    var json = LuminPackSerializer.SerializeJson(nested);
                    LuminPackSerializer.DeserializeJson(json, ref nestedResult);
                    if (nestedResult.Child.Text != text || nestedResult.Child.Value != 18) throw new Exception("The targeted nested type did not round-trip.");
                    var customBytes = LuminPackSerializer.Serialize<CustomValue>(new CustomValue { Value = 93 });
                    Same(customBytes, new byte[] { 93 }, "Custom write did not use its public delegate.");
                    CustomValue custom = null!;
                    LuminPackSerializer.Deserialize<CustomValue>(customBytes, ref custom);
                    if (custom.Value != 93) throw new Exception("Custom read did not use its public delegate.");
                }
            }
            """;
        var input = Create("TargetedOptionConsumer", declarations + entry, consumerReferences);
        GeneratorDriver driver = Driver().RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        byte[] image = Emit(output);
        var context = new AssemblyLoadContext("TargetedOptionConsumer", isCollectible: true);
        try
        {
            context.LoadFromStream(new MemoryStream(libraryImage));
            context.LoadFromStream(new MemoryStream(image)).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
        }
        finally { context.Unload(); }

        var edited = Create("TargetedOptionConsumer", declarations.Replace("StringEncoding = LuminPackStringEncoding.UTF8)]",
            "StringEncoding = LuminPackStringEncoding.UTF16)]", StringComparison.Ordinal) + entry, consumerReferences);
        var retained = driver.RunGenerators(edited);
        var fresh = Driver().RunGenerators(edited);
        Require(Files(retained).SequenceEqual(Files(fresh)), "An edited targeted option retained stale generated files.");
        Require(!Files(retained).SequenceEqual(Files(driver)), "The edited targeted option did not affect emitted code.");

        const string invalid = """
            using LuminPack.Attribute;
            using TargetedOptions;
            [assembly: LuminPackable(typeof(Box<>))]
            [assembly: LuminPackOption(typeof(Box<int>))]
            [assembly: LuminPackOption(typeof(Box<int>))]
            [assembly: LuminPackOption(typeof(Annotated))]
            [assembly: LuminPackOption(typeof(Local))]
            [assembly: LuminPackOption]
            [assembly: LuminPackOption]
            [LuminPackOption(typeof(Annotated))] public sealed class Local
            { [LuminPackOption, LuminPackOption] public int Value; }
            """;
        var invalidInput = Create("InvalidTargetedOptions", invalid, consumerReferences);
        var invalidDiagnostics = Driver().RunGenerators(invalidInput).GetRunResult().Diagnostics;
        Require(invalidDiagnostics.Count(item => item.Id == "LuminPack123" && item.Location.IsInSource) == 6,
            "Invalid targeted scopes, stray options or duplicate defaults were not diagnosed.\n" + string.Join(Environment.NewLine, invalidDiagnostics));
        Console.WriteLine("Passed targeted assembly profiles, mode and layout selection, registration, incremental edits and option target diagnostics.");
    }

    private static string[] Files(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(item => item.GeneratedSources)
        .Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal).ToArray();

    private static GeneratorDriver Driver() => CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: Parse);

    private static CSharpCompilation Create(string name, string source, MetadataReference[] references) => CSharpCompilation.Create(name,
        [CSharpSyntaxTree.ParseText(source, Parse, name + ".cs")], references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

    private static byte[] Emit(Compilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return image.ToArray();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
