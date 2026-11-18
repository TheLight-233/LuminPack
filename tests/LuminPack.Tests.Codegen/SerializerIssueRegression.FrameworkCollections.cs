extern alias ModelBasedAnalyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

internal static partial class SerializerIssueRegression
{
    /// <summary>Checks framework scalar collection dependencies in both binary and JSON output.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunFrameworkCollections(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Numerics;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public partial class Holder { public Vector4[] Values; }
            public static class Entry
            {
                public static void Check()
                {
                    Vector4[] vectors = { new Vector4(1, 2, 3, 4), new Vector4(-5, 6, 7, 8) };
                    byte[] bytes = LuminPackSerializer.Serialize(vectors);
                    byte[] expected = { 2, 0, 0, 0, 0, 0, 128, 63, 0, 0, 0, 64, 0, 0, 64, 64, 0, 0, 128, 64,
                        0, 0, 160, 192, 0, 0, 192, 64, 0, 0, 224, 64, 0, 0, 0, 65 };
                    if (!bytes.AsSpan().SequenceEqual(expected)) throw new Exception("Vector4 binary wire");
                    Vector4[] copy = null;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (!copy.AsSpan().SequenceEqual(vectors)) throw new Exception("Vector4 binary");
                    string json = LuminPackSerializer.SerializeJson(vectors);
                    if (json != "[[1,2,3,4],[-5,6,7,8]]") throw new Exception("Vector4 JSON wire: " + json);
                    copy = null;
                    LuminPackSerializer.DeserializeJson(json, ref copy);
                    if (!copy.AsSpan().SequenceEqual(vectors)) throw new Exception("Vector4 JSON");
                    Vector2[] pairs = { new Vector2(9, 10) };
                    string pairJson = LuminPackSerializer.SerializeJson(pairs);
                    Vector2[] pairCopy = null;
                    LuminPackSerializer.DeserializeJson(pairJson, ref pairCopy);
                    if (!pairCopy.AsSpan().SequenceEqual(pairs)) throw new Exception("Vector2 JSON");
                    var list = new List<Vector4>(vectors);
                    var listBytes = LuminPackSerializer.Serialize(list);
                    List<Vector4> listCopy = null;
                    LuminPackSerializer.Deserialize(listBytes, ref listCopy);
                    if (listCopy.Count != 2 || listCopy[1] != vectors[1]) throw new Exception("Vector list binary");
                    string listJson = LuminPackSerializer.SerializeJson(list);
                    listCopy = null;
                    LuminPackSerializer.DeserializeJson(listJson, ref listCopy);
                    if (listCopy.Count != 2 || listCopy[1] != vectors[1]) throw new Exception("Vector list JSON");
                    var holder = new Holder { Values = vectors };
                    string holderJson = LuminPackSerializer.SerializeJson(holder);
                    Holder holderCopy = null;
                    LuminPackSerializer.DeserializeJson(holderJson, ref holderCopy);
                    if (!holderCopy.Values.AsSpan().SequenceEqual(vectors)) throw new Exception("Nested vector JSON");

                    var integers = ImmutableArray.Create(31, -19, 7);
                    var integersCopy = ImmutableArray<int>.Empty;
                    var integerBytes = LuminPackSerializer.Serialize(integers);
                    if (LuminPackSerializer.Deserialize(integerBytes, ref integersCopy) != integerBytes.Length ||
                        !integersCopy.AsSpan().SequenceEqual(integers.AsSpan())) throw new Exception("Immutable raw array wire");
                    var wide = ImmutableArray.Create(long.MinValue, long.MaxValue);
                    var wideCopy = default(ImmutableArray<long>);
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(wide), ref wideCopy);
                    if (!wideCopy.AsSpan().SequenceEqual(wide.AsSpan())) throw new Exception("Immutable wide raw array wire");

                    var previous = integersCopy;
                    bool rejected = false;
                    try { LuminPackSerializer.Deserialize(integerBytes.AsSpan(0, integerBytes.Length - 1), ref integersCopy); }
                    catch (Exception error) when (error.GetType().Name == "LuminPackException") { rejected = true; }
                    if (!rejected || integersCopy != previous) throw new Exception("Truncated immutable raw array was published");
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(default(ImmutableArray<int>)), ref integersCopy);
                    if (!integersCopy.IsDefault) throw new Exception("Immutable raw array null marker");
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(ImmutableArray<int>.Empty), ref integersCopy);
                    if (integersCopy.IsDefault || integersCopy.Length != 0) throw new Exception("Immutable raw array empty marker");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
            Execute(references, source.Replace("MODE", mode, StringComparison.Ordinal), "Vectors" + mode);
        const string singleOperation = """
            using System;
            using System.Numerics;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            public static class Entry
            {
                public static void Check()
                {
                    Vector4[] value = { new Vector4(1, 2, 3, 4) };
                    OPERATION
                }
            }
            """;
        Execute(references, singleOperation.Replace("OPERATION", "if (LuminPackSerializer.Serialize(value).Length != 20) throw new Exception();", StringComparison.Ordinal), "VectorBinaryOnly");
        Execute(references, singleOperation.Replace("OPERATION", "if (LuminPackSerializer.SerializeJson(value) != \"[[1,2,3,4]]\") throw new Exception();", StringComparison.Ordinal), "VectorJsonOnly");
        VerifyMemoryStorageInitializer(references);
        Console.WriteLine("Passed framework scalar arrays, lists and nested collections across four generation modes.");
    }

    /// <summary>Checks that retained mutable memory readers request the initializer when Core supplies it.</summary>
    /// <param name="references">Platform and runtime references.</param>
    private static void VerifyMemoryStorageInitializer(MetadataReference[] references)
    {
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            DECLARATIONS
            public static class Entry
            {
                public static void Main()
                {
                    int[] storage = [11, 0, 13];
                    Memory<int> slice = storage.AsMemory(1, 1);
                    Memory<long> wide = new long[1];
                    OPERATION
                }
            }
            """;
        const string read = """
            if (LuminPackSerializer.Deserialize(new byte[] { 1, 0, 0, 0, 23, 0, 0, 0 }, ref slice) != 8 ||
                LuminPackSerializer.Deserialize(new byte[] { 1, 0, 0, 0, 29, 0, 0, 0, 0, 0, 0, 0 }, ref wide) != 12 ||
                storage[0] != 11 || storage[1] != 23 || storage[2] != 13 || wide.Span[0] != 29)
                throw new Exception("Mutable memory storage reuse changed.");
            """;
        const string readOnly = """
            [assembly: LuminPackGeneratorOptions(ForceGenerateDeserialize = true)]
            [LuminPackable] public sealed class Holder { public ReadOnlyMemory<int> Data; }
            """;
        var core = references.Single(reference => Path.GetFileName(reference.Display) == "LuminPack.Core.dll");
        var older = MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(core.Display!, "InitializeMemoryStorage"));
        MetadataReference[] olderReferences = references.Select(reference => ReferenceEquals(reference, core) ? older : reference).ToArray();
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"])
            .WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        Verify("MemoryStorageRead", "", read, references, expected: 1);
        Verify("MemoryStorageReadOnly", readOnly, "", references, expected: 0);
        Verify("MemoryStorageOlder", "", read, olderReferences, expected: 0);

        void Verify(string name, string declarations, string operation, MetadataReference[] runtime, int expected)
        {
            string text = source.Replace("DECLARATIONS", declarations, StringComparison.Ordinal)
                .Replace("OPERATION", operation, StringComparison.Ordinal);
            var compilation = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(text, parse, "Entry.cs")],
                runtime, new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse)
                .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            int initializers = driver.GetRunResult().Results.SelectMany(item => item.GeneratedSources)
                .Count(item => item.SourceText.ToString().Contains("LuminPackMarshal.InitializeMemoryStorage()", StringComparison.Ordinal));
            if (initializers != expected)
                throw new InvalidOperationException($"{name} published {initializers} Memory storage initializers, expected {expected}.");
            using var image = new MemoryStream();
            var result = output.Emit(image);
            if (!result.Success || diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error))
                throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Concat(result.Diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error)));
            if (expected != 0)
                System.Reflection.Assembly.Load(image.ToArray()).EntryPoint!.Invoke(null, null);
        }
    }
}
