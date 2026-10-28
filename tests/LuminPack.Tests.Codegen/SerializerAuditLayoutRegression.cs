using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using LuminPack.SourceGenerator;

/// <summary>Checks packed framework leaves and the source/reference-assembly layout contract.</summary>
internal static class SerializerAuditLayoutRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, preprocessorSymbols:
        ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER", "NET11_0_OR_GREATER"]);

    /// <summary>Compiles with SDK reference metadata and compares a producer with its reference-assembly consumer.</summary>
    /// <param name="references">The runtime and generator harness references.</param>
    internal static void Run(MetadataReference[] references)
    {
        string packs = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "../../../packs/Microsoft.NETCore.App.Ref"));
        string referenceDirectory = Directory.GetDirectories(packs).SelectMany(path => Directory.GetDirectories(Path.Combine(path, "ref")))
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal).First(path => Path.GetFileName(path) == "net11.0");
        var sdk = Directory.GetFiles(referenceDirectory, "*.dll").Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(LuminPack.Attribute.LuminPackableAttribute).Assembly.Location)).ToArray();
        var producer = Compile("AuditLayoutProducer", """
            using System;
            using System.Runtime.InteropServices;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            [assembly: LuminPackOption(StructLayout=LuminPackStructLayout.Native)]
            [LuminPackable, LuminPackOption(StructLayout=LuminPackStructLayout.Tagged)] public struct Shared { public byte Tag; private long hidden; public long Number { get => hidden; set => hidden = value; } }
            [LuminPackable, LuminPackOption(StructLayout=LuminPackStructLayout.Tagged), StructLayout(LayoutKind.Sequential, Pack=4)] public struct SharedPacked { public byte Tag; public long Value; }
            [LuminPackable, LuminPackOption(StructLayout=LuminPackStructLayout.Tagged), StructLayout(LayoutKind.Explicit)] public struct SharedExplicit { [FieldOffset(0)] public long A; [FieldOffset(0)] public long B; [FieldOffset(8)] public byte C; }
            [LuminPackable, LuminPackOption(StructLayout=LuminPackStructLayout.Packed)] public struct Packed { public byte Tag; public Guid Id; }
            public static class Producer {
              public static byte[] Write() => LuminPack.LuminPackSerializer.Serialize(new[] { new Shared { Tag=17, Number=123456789012345 } });
              public static byte[] WritePacked() => LuminPack.LuminPackSerializer.Serialize(new[] { new SharedPacked { Tag=17, Value=123456789012345 } });
              public static byte[] WriteExplicit() => LuminPack.LuminPackSerializer.Serialize(new[] { new SharedExplicit { A=123456789012345, C=17 } });
              public static int PackedWidth() => LuminPack.LuminPackSerializer.Serialize(new Packed { Tag=7, Id=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff") }).Length;
            }
            """, sdk);
        using var implementation = new MemoryStream();
        Require(producer.Emit(implementation));
        using var reference = new MemoryStream();
        Require(producer.Emit(reference, options: new EmitOptions(metadataOnly: true, includePrivateMembers: false)));
        implementation.Position = 0;
        var runtimeProducer = AssemblyLoadContext.Default.LoadFromStream(implementation);
        if ((int)runtimeProducer.GetType("Producer")!.GetMethod("PackedWidth")!.Invoke(null, null)! != 17)
            throw new InvalidOperationException("Packed Guid used SDK placeholder storage.");
        byte[] bytes = (byte[])runtimeProducer.GetType("Producer")!.GetMethod("Write")!.Invoke(null, null)!;
        var consumer = Compile("AuditLayoutConsumer", """
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            public static class Consumer {
              public static bool Read(byte[] bytes) {
                Shared[] copy = null;
                LuminPack.LuminPackSerializer.Deserialize(bytes, ref copy);
                return copy.Length==1 && copy[0].Tag==17 && copy[0].Number==123456789012345;
              }
              public static bool ReadPacked(byte[] bytes) { SharedPacked[] copy=null; LuminPack.LuminPackSerializer.Deserialize(bytes, ref copy); return copy.Length==1 && copy[0].Tag==17 && copy[0].Value==123456789012345; }
              public static bool ReadExplicit(byte[] bytes) { SharedExplicit[] copy=null; LuminPack.LuminPackSerializer.Deserialize(bytes, ref copy); return copy.Length==1 && copy[0].C==17 && copy[0].A==123456789012345; }
            }
            """, sdk.Append(MetadataReference.CreateFromImage(reference.ToArray())).ToArray());
        using var consumerImage = new MemoryStream();
        Require(consumer.Emit(consumerImage));
        consumerImage.Position = 0;
        var runtimeConsumer = AssemblyLoadContext.Default.LoadFromStream(consumerImage);
        if (!(bool)runtimeConsumer.GetType("Consumer")!.GetMethod("Read")!.Invoke(null, [bytes])!)
            throw new InvalidOperationException("Source and reference-assembly consumers selected different framing.");
        foreach (string suffix in new[] { "Packed", "Explicit" })
        {
            var record = (byte[])runtimeProducer.GetType("Producer")!.GetMethod("Write" + suffix)!.Invoke(null, null)!;
            if (!(bool)runtimeConsumer.GetType("Consumer")!.GetMethod("Read" + suffix)!.Invoke(null, [record])!)
                throw new InvalidOperationException("Reference assembly lost " + suffix + " layout metadata.");
        }
        Console.WriteLine("Passed SDK reference-assembly packed leaves and private-field cross-assembly tagged storage.");
    }

    /// <summary>Generates concrete and lazy operations for one source fixture.</summary>
    /// <param name="name">The assembly identity.</param>
    /// <param name="source">The fixture source.</param>
    /// <param name="references">The exact reference metadata.</param>
    /// <returns>The compilation including generated operations.</returns>
    private static Compilation Compile(string name, string source, MetadataReference[] references)
    {
        var input = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, Parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        if (diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics));
        return output;
    }

    /// <summary>Reports a fixture compilation failure with its complete diagnostics.</summary>
    /// <param name="result">The emit result.</param>
    private static void Require(EmitResult result)
    {
        if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
    }
}
