using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Checks source and imported packing, declared size and explicit offsets retain the same storage contract.</summary>
internal static class SerializerImportedLayoutRegression
{
    /// <summary>Compiles layout metadata separately before exercising both generation tiers.</summary>
    /// <param name="references">The current platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        var foreign = CSharpCompilation.Create("ImportedAuditLayouts", [CSharpSyntaxTree.ParseText("""
            using System.Runtime.InteropServices;
            using LuminPack.Attribute;
            namespace ImportedAudit;
            [LuminPackable, StructLayout(LayoutKind.Sequential, Pack = 1, Size = 12)]
            public struct Packed
            {
                public byte Tag;
                [LuminPackInclude] private long _value;
                public long GetValue() => _value;
                public static Packed Create() => new Packed { Tag = 17, _value = long.MinValue + 123 };
            }
            [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 16)]
            public struct Explicit
            {
                [FieldOffset(0)] public byte Tag;
                [LuminPackInclude, FieldOffset(3)] private long _value;
                public long GetValue() => _value;
                public static Explicit Create() => new Explicit { Tag = 23, _value = long.MaxValue - 321 };
            }
            """, parse)], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var foreignImage = new MemoryStream();
        Require(foreign.Emit(foreignImage).Success, "Could not emit the independent layout assembly.");
        byte[] foreignBytes = foreignImage.ToArray();
        using (var dependency = new MemoryStream(foreignBytes)) AssemblyLoadContext.Default.LoadFromStream(dependency);
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            string source = $$"""
                using System;
                using LuminPack;
                using LuminPack.Attribute;
                [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.{{mode}})]
                [assembly: LuminPackable(typeof(ImportedAudit.Packed))]
                [assembly: LuminPackable(typeof(ImportedAudit.Explicit))]
                namespace ImportedAuditConsumer;
                [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
                public struct Packet { public byte Head; public ImportedAudit.Packed Packed; public ImportedAudit.Explicit Explicit; public byte Tail; }
                public static class Entry
                {
                    public static void Run()
                    {
                        var value = new Packet { Head = 5, Packed = ImportedAudit.Packed.Create(), Explicit = ImportedAudit.Explicit.Create(), Tail = 7 };
                        var bytes = LuminPackSerializer.Serialize(value);
                        if (bytes.Length != 20 || bytes[0] != 5 || bytes[1] != 17 || BitConverter.ToInt64(bytes, 2) != value.Packed.GetValue() ||
                            bytes[10] != 23 || BitConverter.ToInt64(bytes, 11) != value.Explicit.GetValue() || bytes[19] != 7)
                            throw new InvalidOperationException("Imported metadata changed the portable wire.");
                        var copy = LuminPackSerializer.Deserialize<Packet>(bytes);
                        if (copy.Packed.GetValue() != value.Packed.GetValue() || copy.Explicit.GetValue() != value.Explicit.GetValue())
                            throw new InvalidOperationException("Imported storage was read at the wrong field offsets.");
                        var list = new[] { value, value, value };
                        var copies = LuminPackSerializer.Deserialize<Packet[]>(LuminPackSerializer.Serialize(list));
                        if (copies.Length != 3 || copies[2].Packed.GetValue() != value.Packed.GetValue())
                            throw new InvalidOperationException("Imported vector offsets changed nested storage.");
                        var packed = LuminPackSerializer.DeserializeJson<ImportedAudit.Packed>(LuminPackSerializer.SerializeJson(value.Packed));
                        var explicitValue = LuminPackSerializer.DeserializeJson<ImportedAudit.Explicit>(LuminPackSerializer.SerializeJson(value.Explicit));
                        if (packed.GetValue() != value.Packed.GetValue() || explicitValue.GetValue() != value.Explicit.GetValue())
                            throw new InvalidOperationException("Imported Local views lost Pack, Size or FieldOffset.");
                    }
                }
                """;
            var input = CSharpCompilation.Create("ImportedAuditConsumer_" + mode, [CSharpSyntaxTree.ParseText(source, parse)],
                references.Append(MetadataReference.CreateFromImage(foreignBytes)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            using var image = new MemoryStream();
            var result = output.Emit(image);
            Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("ImportedAuditConsumer.Entry")!.GetMethod("Run")!.Invoke(null, null);
        }
        Console.WriteLine("Passed imported Pack, Size and FieldOffset storage in Full and Minimal modes.");
    }

    /// <summary>Reports a compiler or storage invariant with the original diagnostic context.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The error details.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
