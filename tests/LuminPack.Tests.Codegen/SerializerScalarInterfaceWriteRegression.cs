using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks exact implementation write eligibility without broadening other interface operations.</summary>
internal static class SerializerScalarInterfaceWriteRegression
{
    /// <summary>Compiles and executes the accepted entries in all four tiers and checks retained-driver invalidation.</summary>
    /// <param name="references">The platform and current runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            using LuminPack.Utility;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable(GeneratorType.Custom)] public struct CustomElement
            {
                public int Value;
                public static void Serialize(ref LuminPackWriter writer, in CustomElement value) => writer.Write(value.Value);
                public static void Deserialize(ref LuminPackReader reader, ref CustomElement value) => value.Value=reader.Read<int>();
            }
            [LuminPackable, LuminPackOption(StructLayout=LuminPackStructLayout.Packed)]
            public struct PortableElement { public byte Tag; public long Value; }
            [LuminPackable] public struct UserElement { public long Value; }
            [LuminPackable(GeneratorType.CircleReference)] public sealed class Node
            {
                [LuminPackOrder(0)] public int Value;
                [LuminPackOrder(1)] public Node Next;
            }
            public static class ScalarWriteCases
            {
                public static byte[] List(IList<int> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Set(ISet<int> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] ReadOnly(IReadOnlySet<int> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Wide(ISet<long> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] GuidList(IList<Guid> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] DecimalSet(IReadOnlySet<decimal> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] User(IList<UserElement> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Text(IList<string> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Custom(ISet<CustomElement> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Portable(IList<PortableElement> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Managed(IList<Node> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Sequence(IEnumerable<int> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Open<T>(IList<T> value)=>LuminPackSerializer.Serialize(value);
                public static string Json(ISet<int> value)=>LuminPackSerializer.SerializeJson(value);
                public static int Read(ReadOnlySpan<byte> data,ref ISet<int> value)=>LuminPackSerializer.Deserialize(data,ref value);
                public static void Buffered(ISet<int> value,LuminBufferWriter buffer)=>LuminPackSerializer.Serialize(value,buffer);
                public static void Check()
                {
                    ISet<int> set=new HashSet<int>{3,7,11}; IList<int> list=new List<int>{3,7,11};
                    byte[] expected=List(list);
                    if(!expected.AsSpan().SequenceEqual(Set(set))||!expected.AsSpan().SequenceEqual(ReadOnly((IReadOnlySet<int>)set)))throw new Exception("Interface bytes differ");
                    using(var buffer=new LuminBufferWriter(true))
                    {
                        Buffered(set,buffer);
                        if(!expected.AsSpan().SequenceEqual(buffer.GetSpan()))throw new Exception("Buffered entry changed");
                    }
                    ISet<int> copy=null;Read(expected,ref copy);if(!copy.SetEquals(set))throw new Exception("Set read changed");
                    if(Json(set)!="[3,7,11]")throw new Exception("JSON path changed");
                    var item=new Node{Value=37};item.Next=item;IList<Node> graph=new List<Node>{item,item};
                    byte[] data=Managed(graph);IList<Node> restored=null;LuminPackSerializer.Deserialize(data,ref restored);
                    if(!ReferenceEquals(restored[0],restored[1])||!ReferenceEquals(restored[0],restored[0].Next))throw new Exception("Stateful interface lost identities");
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = Compile(source.Replace("MODE", mode, StringComparison.Ordinal), "ScalarWrites" + mode, parse, references);
            var (driver, output) = Generate(compilation, parse);
            var methods = Methods(driver);
            var exact = methods.Where(method => method.Identifier.ValueText == "RootWriteExactInterface").ToArray();
            Require(exact.Length == 6, "Expected the accepted framework scalar implementation roots in " + mode);
            foreach (var method in exact)
                Require(method.ParameterList.Parameters[1].Type!.ToString() is "global::System.Collections.Generic.IList<int>" or
                    "global::System.Collections.Generic.ISet<int>" or "global::System.Collections.Generic.IReadOnlySet<int>" or
                    "global::System.Collections.Generic.ISet<long>" or "global::System.Collections.Generic.IList<global::System.Guid>" or
                    "global::System.Collections.Generic.IReadOnlySet<decimal>", "An excluded contract acquired an exact write root.");
            var slow = methods.Where(method => method.Identifier.ValueText == "SerializeInterfaceSlow").ToArray();
            Require(slow.Length == 6 && slow.All(method => method.TypeParameterList is null && method.Body!.ToString().Contains("LuminBufferWriterPool.Rent", StringComparison.Ordinal)),
                "The original scalar pooled body must remain shared and profile-independent.");
            foreach (var method in methods.Where(method => method.Identifier.ValueText == "Serialize" && method.TypeParameterList?.Parameters.Count == 1 &&
                         method.ParameterList.Parameters.Count == 1 && method.Body!.ToString().Contains("RootWriteExactInterface", StringComparison.Ordinal)))
                Require(method.Body!.ToString().Contains("var selectedValue = value", StringComparison.Ordinal) &&
                    method.Body.ToString().Contains("RootWriteExactInterface(ref writer, selectedValue)", StringComparison.Ordinal),
                    "The exact implementation proof rereads the caller's mutable reference.");
            var trait = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.CodeEmitters.EmitterTypeTraits", true)!
                .GetMethod("GetExactScalarInterfaceWriteImplementation", BindingFlags.Static | BindingFlags.NonPublic)!;
            var open = compilation.GetTypeByMetadataName("ScalarWriteCases")!.GetMembers("Open").OfType<IMethodSymbol>().Single();
            Require(trait.Invoke(null, [open.Parameters[0].Type]) is null, "An unresolved element acquired exact write eligibility.");
            var handle = compilation.GetTypeByMetadataName("System.Runtime.InteropServices.GCHandle")!;
            var handleList = compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1")!.Construct(handle);
            Require(trait.Invoke(null, [handleList]) is null, "An unsupported low-level catalog type acquired an allocating interface entry.");
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("ScalarWriteCases")!.GetMethod("Check")!.Invoke(null, null);
            if (mode == "Minimal")
            {
                var compatibilityParse = new CSharpParseOptions(LanguageVersion.CSharp13);
                var compatibility = Compile(source.Replace("MODE", mode, StringComparison.Ordinal), "ScalarWritesCompatibility", compatibilityParse, references);
                var (compatibilityDriver, _) = Generate(compatibility, compatibilityParse);
                Require(!Methods(compatibilityDriver).Any(method => method.Identifier.ValueText is "RootWriteExactInterface" or "SerializeInterfaceSlow"),
                    "The pointer compatibility target acquired the managed exact write entry.");
            }
        }
        VerifyIncremental(references, parse);
        Console.WriteLine("Passed scalar interface writes, exclusions, pooled fallback, stateful/JSON isolation, four tiers and retained-driver invalidation.");
    }

    /// <summary>Checks that changing an interface element invalidates the frozen facade and matches a fresh driver.</summary>
    /// <param name="references">The current runtime references.</param>
    /// <param name="parse">The managed consumer's parse options.</param>
    private static void VerifyIncremental(MetadataReference[] references, CSharpParseOptions parse)
    {
        const string source = "using LuminPack; using LuminPack.Attribute; using System.Collections.Generic; [LuminPackable] public struct UserElement { public long Value; } public static class Changed { public static byte[] Write(IList<UserElement> value)=>LuminPackSerializer.Serialize(value); }";
        var first = Compile(source, "ChangedInterfaceWrite", parse, references);
        var (driver, _) = Generate(first, parse);
        Require(!Methods(driver).Any(method => method.Identifier.ValueText == "RootWriteExactInterface"), "A user element acquired framework scalar eligibility.");
        var edited = first.ReplaceSyntaxTree(first.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(source.Replace("IList<UserElement>", "IList<int>", StringComparison.Ordinal), parse));
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out _, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        var (fresh, _) = Generate(edited, parse);
        Require(Methods(driver).Count(method => method.Identifier.ValueText == "RootWriteExactInterface") == 1, "The newly accepted scalar did not invalidate its facade.");
        string[] Trees(GeneratorDriver value) => value.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Select(result => result.HintName + "\n" + result.SourceText).Order(StringComparer.Ordinal).ToArray();
        Require(Trees(driver).SequenceEqual(Trees(fresh), StringComparer.Ordinal), "Retained and fresh interface output differ.");
    }

    /// <summary>Creates one targeted consumer without building the large unit-test project.</summary>
    /// <param name="source">The complete consumer source.</param>
    /// <param name="name">The isolated assembly identity.</param>
    /// <param name="parse">The consumer language and target.</param>
    /// <param name="references">The platform and runtime metadata.</param>
    /// <returns>The targeted compilation.</returns>
    private static CSharpCompilation Compile(string source, string name, CSharpParseOptions parse, MetadataReference[] references)
        => CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));

    /// <summary>Runs the current production generators for one targeted consumer.</summary>
    /// <param name="compilation">The original consumer.</param>
    /// <param name="parse">The consumer parse options.</param>
    /// <returns>The retained driver and updated compilation.</returns>
    private static (GeneratorDriver Driver, Compilation Output) Generate(CSharpCompilation compilation, CSharpParseOptions parse)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        return (driver, output);
    }

    /// <summary>Finds declarations across every generated file.</summary>
    /// <param name="driver">The completed production generator run.</param>
    /// <returns>The emitted methods.</returns>
    private static MethodDeclarationSyntax[] Methods(GeneratorDriver driver)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();

    /// <summary>Reports a failed source, cache or execution invariant.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The exact failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
