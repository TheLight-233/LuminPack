using System.Reflection;
using LuminPack.Build;
using LuminPack.SourceGenerator.Next.Pipeline;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    /// <summary>Exercises compiler interception, IL rewriting and registered fallback in applications with and without a friend.</summary>
    /// <param name="references">The framework and runtime metadata.</param>
    /// <param name="parse">The modern target and language settings.</param>
    /// <returns>The number of compile and execution matrix entries.</returns>
    private static int VerifyStaticBackends(MetadataReference[] references, CSharpParseOptions parse)
    {
        string[] paths = references.OfType<PortableExecutableReference>().Select(reference => reference.FilePath!).ToArray();
        int checks = 0;
        foreach (string backend in new[] { "compiler", "il", "disabled" })
        foreach (bool friend in new[] { false, true })
        {
            bool compiler = backend == "compiler", il = backend == "il";
            CSharpParseOptions syntax = compiler ? parse : parse.WithFeatures(new[]
            {
                new KeyValuePair<string, string>("InterceptorsNamespaces", "Unrelated"),
                new KeyValuePair<string, string>("InterceptorsPreviewNamespaces", string.Empty),
            });
            if (il)
                syntax = syntax.WithPreprocessorSymbols(syntax.PreprocessorSymbolNames.Append("LUMINPACK_IL_REWRITE"));
            string source = StaticBackendFixture.Replace("FRIEND", friend
                ? "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"StaticBackendFriend\")]" : string.Empty)
                .Replace("REGISTRATION", compiler || il ? "Disabled" : "Auto");
            string name = "StaticBackend_" + backend + (friend ? "_friend" : string.Empty);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source, syntax, name + ".cs", System.Text.Encoding.UTF8);
            CSharpCompilation input = CSharpCompilation.Create(name, new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: syntax)
                .RunGeneratorsAndUpdateCompilation(input, out Compilation output, out var diagnostics);
            var generation = driver.GetRunResult().Results.Single();
            RequireIncremental(generation.Exception == null, generation.Exception?.ToString() ?? string.Empty);
            foreach (Diagnostic diagnostic in diagnostics)
                RequireIncremental(diagnostic.Severity != DiagnosticSeverity.Error, name + ": " + diagnostic);
            var binding = generation.GeneratedSources.SingleOrDefault(file => file.HintName.EndsWith("Interceptors.g.cs", StringComparison.Ordinal));
            RequireIncremental((binding.SourceText != null) == (compiler || il), name + " static binding publication.");
            if (binding.SourceText != null)
            {
                string text = binding.SourceText.ToString();
                RequireIncremental(text.Contains(il ? "StaticCallAttribute" : "InterceptsLocationAttribute", StringComparison.Ordinal),
                    name + " mapping attribute.");
            }
            bool retained = output.GetTypeByMetadataName("LuminPack.LuminPackSerializer+FriendPayloadFormatter") is { } root &&
                root.GetMembers("ReadJson").Length != 0;
            RequireIncremental(retained == friend, name + " uncalled friend payload retention.");

            using var pe = new MemoryStream();
            using var pdb = new MemoryStream();
            var emitted = output.Emit(pe, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
            RequireIncremental(emitted.Success, name + ": " + string.Join(Environment.NewLine, emitted.Diagnostics));
            byte[] image = pe.ToArray(), symbols = pdb.ToArray();
            var rewritten = StaticCallAssembly.Rewrite(image, symbols, paths);
            RequireIncremental(il ? rewritten is { Calls: 6, Pdb.Length: > 0 } : rewritten == null, name + " IL rewrite and symbols.");
            if (rewritten != null)
            {
                image = rewritten.Pe;
                symbols = rewritten.Pdb;
            }
            Assembly assembly = Assembly.Load(image, symbols);
            RequireIncremental((bool)assembly.GetType("StaticBackendFixture")!.GetMethod("Exercise")!.Invoke(null, null)!,
                name + " binary and JSON profile round trip.");
            checks++;
            if (friend)
            {
                VerifyFriend(output, image, assembly, references, syntax);
                checks++;
            }
        }
        return checks;
    }

    /// <summary>Compiles and executes calls to the retained state entries from a separate friend assembly.</summary>
    /// <param name="producer">The compilation that declared the friend.</param>
    /// <param name="image">The emitted producer with any required IL rewrite applied.</param>
    /// <param name="assembly">The loaded producer.</param>
    /// <param name="references">The framework and runtime references.</param>
    /// <param name="parse">The consumer's compiler settings.</param>
    private static void VerifyFriend(Compilation producer, byte[] image, Assembly assembly, MetadataReference[] references, CSharpParseOptions parse)
    {
        var source = CSharpSyntaxTree.ParseText(FriendBackendFixture, parse, "StaticBackendFriend.cs");
        var input = CSharpCompilation.Create("StaticBackendFriend", new[] { source },
            references.Append(MetadataReference.CreateFromImage(image)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
        using var pe = new MemoryStream();
        var emitted = input.Emit(pe);
        RequireIncremental(emitted.Success, producer.AssemblyName + ": " + string.Join(Environment.NewLine, emitted.Diagnostics));

        var context = new System.Runtime.Loader.AssemblyLoadContext(producer.AssemblyName + "_Friend", isCollectible: true);
        context.Resolving += (_, name) => name.Name == producer.AssemblyName ? assembly : null;
        try
        {
            pe.Position = 0;
            var friend = context.LoadFromStream(pe);
            RequireIncremental((bool)friend.GetType("FriendEntry")!.GetMethod("Exercise")!.Invoke(null, null)!,
                producer.AssemblyName + " external friend state entries.");
        }
        finally
        {
            context.Unload();
        }
    }

    private const string FriendBackendFixture = """
        #nullable enable
        using LuminPack;
        using LuminPack.Core;
        using LuminPack.Option;
        using LuminPack.Utility;

        public static class FriendEntry
        {
            public static bool Exercise()
            {
                var value = new FriendPayload { Text = "friend \u4E2D" };
                using var buffer = LuminBufferWriterPool.Rent();
                var writer = new LuminPackWriter(buffer);
                FriendPayloadState.Write<LuminPackUtf8VarIntOptions>(ref writer, in value);
                var data = writer.GetSpan();
                var reader = new LuminPackReader(ref data);
                FriendPayload? copy = null;
                FriendPayloadState.Read<LuminPackUtf8VarIntOptions>(ref reader, ref copy);
                return copy?.Text == value.Text && reader.Position == data.Length;
            }
        }
        """;

    private const string StaticBackendFixture = """
        using System;
        using System.Buffers;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;

        FRIEND
        [assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.REGISTRATION)]

        [LuminPackable] public sealed class BackendPayload { public int Number; public string? Text; }
        [LuminPackable] internal sealed class FriendPayload { public string? Text; }

        public static class StaticBackendFixture
        {
            public static void Main() => Exercise();

            public static bool Exercise()
            {
                var value = new BackendPayload { Number = 29, Text = "\u4E2D" };
                byte[] bytes = LuminPackSerializer.Serialize<BackendPayload, LuminPackUtf8VarIntOptions>(value);
                BackendPayload binary = LuminPackSerializer.Deserialize<BackendPayload, LuminPackUtf8VarIntOptions>(bytes);
                var single = new ReadOnlySequence<byte>(bytes);
                var first = new Segment(bytes.AsMemory(0, 1));
                var last = first.Append(bytes.AsMemory(1));
                var multiple = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
                BackendPayload singleCopy = LuminPackSerializer.Deserialize<BackendPayload, LuminPackUtf8VarIntOptions>(in single);
                BackendPayload multipleCopy = LuminPackSerializer.Deserialize<BackendPayload, LuminPackUtf8VarIntOptions>(in multiple);
                string text = LuminPackSerializer.SerializeJson<BackendPayload, LuminPackUtf8LengthOptions>(value);
                BackendPayload json = LuminPackSerializer.DeserializeJson<BackendPayload, LuminPackUtf8LengthOptions>(text);
                return binary.Number == 29 && binary.Text == value.Text && json.Number == 29 && json.Text == value.Text &&
                    singleCopy.Number == 29 && singleCopy.Text == value.Text && multipleCopy.Number == 29 && multipleCopy.Text == value.Text;
            }

            private sealed class Segment : ReadOnlySequenceSegment<byte>
            {
                internal Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

                internal Segment Append(ReadOnlyMemory<byte> memory)
                {
                    var segment = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
                    Next = segment;
                    return segment;
                }
            }
        }
        """;
}
