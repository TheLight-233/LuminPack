extern alias ModelBasedAnalyzer;

using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NextGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks source and imported packing, declared size and explicit offsets retain the same storage contract.</summary>
internal static class SerializerImportedLayoutRegression
{
    /// <summary>Compiles layout metadata separately before exercising both generation tiers.</summary>
    /// <param name="references">The current platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        var foreign = TestCompilation.Create("ImportedAuditLayouts", [CSharpSyntaxTree.ParseText("""
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
                        var copy = global::LuminPackUnitTest.ConcreteReads.Read<Packet>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Packet concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                        if (copy.Packed.GetValue() != value.Packed.GetValue() || copy.Explicit.GetValue() != value.Explicit.GetValue())
                            throw new InvalidOperationException("Imported storage was read at the wrong field offsets.");
                        var list = new[] { value, value, value };
                        var copies = global::LuminPackUnitTest.ConcreteReads.Read<Packet[]>(LuminPackSerializer.Serialize(list), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Packet[] concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                        if (copies.Length != 3 || copies[2].Packed.GetValue() != value.Packed.GetValue())
                            throw new InvalidOperationException("Imported vector offsets changed nested storage.");
                        var packed = global::LuminPackUnitTest.ConcreteReads.ReadJson<ImportedAudit.Packed>(LuminPackSerializer.SerializeJson(value.Packed), static (string concreteReadInput, ref ImportedAudit.Packed concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget));
                        var explicitValue = global::LuminPackUnitTest.ConcreteReads.ReadJson<ImportedAudit.Explicit>(LuminPackSerializer.SerializeJson(value.Explicit), static (string concreteReadInput, ref ImportedAudit.Explicit concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget));
                        if (packed.GetValue() != value.Packed.GetValue() || explicitValue.GetValue() != value.Explicit.GetValue())
                            throw new InvalidOperationException("Imported Local views lost Pack, Size or FieldOffset.");
                    }
                }
                """;
            var input = TestCompilation.Create("ImportedAuditConsumer_" + mode, [CSharpSyntaxTree.ParseText(source, parse)],
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
        VerifyConsumingLayoutDefaults(references, parse);
        Console.WriteLine("Passed imported Pack, Size and FieldOffset storage in Full and Minimal modes.");
    }

    /// <summary>Exercises consumer defaults for unannotated storage and producer defaults for authored contracts.</summary>
    /// <param name="references">The current platform and runtime references.</param>
    /// <param name="parse">The generator's current language and conditional symbols.</param>
    private static void VerifyConsumingLayoutDefaults(MetadataReference[] references, CSharpParseOptions parse)
    {
        SyntaxTree foreign = CSharpSyntaxTree.ParseText("""
            using LuminPack.Attribute;
            [assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
            namespace ForeignLayoutDefaults;
            public struct Plain
            {
                public byte Tag;
                private long number;
                public long Number => number;
                public static Plain Create() => new Plain { Tag = 13, number = 17 };
            }
            [LuminPackable]
            public struct Authored { public byte Tag; public long Number; }
            [LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
            public struct ExplicitNative { public byte Tag; public long Number; }
            """, parse, "ForeignLayoutDefaults.cs");
        CSharpCompilation producer = TestCompilation.Create("ForeignLayoutDefaults", [foreign], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var reference = new MemoryStream();
        Require(producer.Emit(reference).Success, "Could not emit the layout-default metadata assembly.");
        byte[] foreignBytes = reference.ToArray();
        using (var dependency = new MemoryStream(foreignBytes)) AssemblyLoadContext.Default.LoadFromStream(dependency);
        foreach (string mode in new[] { "Packed", "Tagged", "Native" })
        {
            string source = $$"""
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                using ForeignLayoutDefaults;
                using LuminPack;
                using LuminPack.Attribute;
                [assembly: LuminPackOption(StructLayout = LuminPackStructLayout.{{mode}})]
                public static class Entry
                {
                    public static void Run()
                    {
                        bool packed = "{{mode}}" == "Packed";
                        bool tagged = "{{mode}}" == "Tagged";
                        var plain = Plain.Create();
                        byte[] bytes = LuminPackSerializer.Serialize(new[] { plain });
                        Require(bytes.Length == 4 + (packed ? 9 : Unsafe.SizeOf<Plain>() + (tagged ? 1 : 0)),
                            "Unannotated foreign struct did not follow the consumer layout.");
                        Plain[] plainCopy = null!;
                        Require(LuminPackSerializer.Deserialize(bytes, ref plainCopy) == bytes.Length && plainCopy[0].Tag == plain.Tag &&
                            plainCopy[0].Number == plain.Number, "Foreign private field did not survive consumer layout transfer.");
                        var pairs = new[] { KeyValuePair.Create((byte)23, 29L) };
                        bytes = LuminPackSerializer.Serialize(pairs);
                        Require(bytes.Length == 4 + (packed ? 9 : Unsafe.SizeOf<KeyValuePair<byte, long>>() + (tagged ? 1 : 0)),
                            "Framework pair did not follow the consumer layout.");
                        Require(LuminPackSerializer.Serialize(new Authored[] { default }).Length == 4 + Unsafe.SizeOf<Authored>(),
                            "A declaration-owned layout was replaced by the consumer default.");
                        Require(LuminPackSerializer.Serialize(new ExplicitNative[] { default }).Length == 4 + Unsafe.SizeOf<ExplicitNative>(),
                            "An explicit layout was replaced by the consumer default.");
                    }
                    private static void Require(bool condition, string message)
                    {
                        if (!condition) throw new InvalidOperationException(message);
                    }
                }
                """;
            CSharpCompilation consumer = TestCompilation.Create("ConsumerLayoutDefaults_" + mode,
                [CSharpSyntaxTree.ParseText(source, parse, "ConsumerLayoutDefaults.cs")], references.Append(MetadataReference.CreateFromImage(foreignBytes)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new NextGenerator().AsSourceGenerator()], parseOptions: parse);
            driver.RunGeneratorsAndUpdateCompilation(consumer, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Run")!.Invoke(null, null);
        }
    }

    /// <summary>Reports a compiler or storage invariant with the original diagnostic context.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The error details.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
