using System.Reflection;
using System.Text;
using LuminPack.Core;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks width-based grouped set stores, exclusions and optional runtime capability invalidation.</summary>
internal static class SerializerSmallHashSetWriteRegression
{
    /// <summary>Compiles and executes all supported small scalar families in each generation tier.</summary>
    /// <param name="references">The platform and current runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = Compile(Source(mode), parse, references);
            var (driver, output) = Generate(compilation, parse);
            VerifySelection(driver, expected: true);
            Execute(output);
            if (mode != "Minimal") continue;

            // Rename only the optional metadata entry. All other runtime APIs remain the actual production APIs.
            byte[] runtime = File.ReadAllBytes(typeof(LuminPackWriter).Assembly.Location);
            int entry = runtime.AsSpan().IndexOf("WriteSmallHashSetRaw\0"u8);
            Require(entry >= 0, "The runtime must expose the newly approved helper.");
            runtime[entry] = (byte)'X';
            var withoutHelper = MetadataReference.CreateFromImage(runtime);
            var original = references.OfType<PortableExecutableReference>().Single(reference => reference.FilePath == typeof(LuminPackWriter).Assembly.Location);
            var compatibility = compilation.ReplaceReference(original, withoutHelper);
            driver = driver.RunGeneratorsAndUpdateCompilation(compatibility, out var compatibilityOutput, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            VerifySelection(driver, expected: false);
            Execute(compatibilityOutput);
            var (fresh, _) = Generate(compatibility, parse);
            Require(Trees(driver).SequenceEqual(Trees(fresh), StringComparer.Ordinal), "Removing the optional runtime entry left stale grouped calls.");

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            VerifySelection(driver, expected: true);
            var (restored, _) = Generate(compilation, parse);
            Require(Trees(driver).SequenceEqual(Trees(restored), StringComparer.Ordinal), "Restoring the runtime entry left stale fallback calls.");
        }
        Console.WriteLine("Passed small native HashSet families, exact interfaces, exclusions, four tiers and retained/fresh runtime capability invalidation.");
    }

    /// <summary>Creates concrete serializer calls without a generic test-side serializer fallback.</summary>
    /// <param name="mode">The generation tier under test.</param>
    /// <returns>A complete executable consumer model.</returns>
    private static string Source(string mode)
    {
        var source = new StringBuilder("""
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using System.Buffers.Binary;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Utility;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public struct UserElement { public byte Value; }
            [LuminPackable(GeneratorType.Custom)] public struct CustomElement
            {
                public byte Value;
                public static void Serialize(ref LuminPackWriter writer,in CustomElement value)=>writer.Write(value.Value);
                public static void Deserialize(ref LuminPackReader reader,ref CustomElement value)=>value.Value=reader.Read<byte>();
            }
            [LuminPackable] public sealed class SetHolder { public HashSet<byte> Value; }
            public static class SmallCases
            {
                public static byte[] Wide(HashSet<long> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] User(HashSet<UserElement> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Custom(HashSet<CustomElement> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Nested(SetHolder value)=>LuminPackSerializer.Serialize(value);
                public static void Buffered(HashSet<byte> value,LuminBufferWriter buffer)=>LuminPackSerializer.Serialize(value,buffer);
                public static void BufferedInterface(ISet<byte> value,LuminBufferWriter buffer)=>LuminPackSerializer.Serialize(value,buffer);
                public static string Json(HashSet<byte> value)=>LuminPackSerializer.SerializeJson(value);
                private static void Check<T>(HashSet<T> set,Func<HashSet<T>,byte[]> root,Func<ISet<T>,byte[]> viaInterface,Func<IReadOnlySet<T>,byte[]> viaReadOnly) where T:unmanaged
                {
                    for(int pass=0;pass<2;pass++)
                    {
                        T[] values=set.ToArray();byte[] expected=new byte[4+values.Length*Unsafe.SizeOf<T>()];
                        BinaryPrimitives.WriteInt32LittleEndian(expected,set.Count);MemoryMarshal.AsBytes(values.AsSpan()).CopyTo(expected.AsSpan(4));
                        if(!expected.AsSpan().SequenceEqual(root(set))||!expected.AsSpan().SequenceEqual(viaInterface(set))||!expected.AsSpan().SequenceEqual(viaReadOnly(set)))throw new Exception("Native set bytes changed");
                        if(set.Count!=0)set.Remove(set.First());
                    }
                    if(!root(null).AsSpan().SequenceEqual(viaInterface(null))||!root(null).AsSpan().SequenceEqual(viaReadOnly(null)))throw new Exception("Null set framing changed");
                }
            """.Replace("MODE", mode, StringComparison.Ordinal));
        string[] types = ["bool", "byte", "sbyte", "short", "ushort", "char", "Half"];
        foreach (string type in types)
        {
            source.Append($$"""
                public static byte[] Write{{type}}(HashSet<{{type}}> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Interface{{type}}(ISet<{{type}}> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] ReadOnly{{type}}(IReadOnlySet<{{type}}> value)=>LuminPackSerializer.Serialize(value);
                """);
        }
        source.Append("public static void Check(){ foreach(int count in new[]{0,1,2,3,4,5,7,8,9,97}){");
        foreach (string type in types)
        {
            string value = type == "bool" ? "index%2==0" : "(" + type + ")(index*3)";
            source.Append($"Check<{type}>(Enumerable.Range(0,count).Select(index=>{value}).ToHashSet(),Write{type},Interface{type},ReadOnly{type});");
        }
        source.Append("""
                }
                var set=new HashSet<byte>{3,7,11};using(var buffer=new LuminBufferWriter(true)){Buffered(set,buffer);if(!Writebyte(set).AsSpan().SequenceEqual(buffer.GetSpan()))throw new Exception("Buffered bytes changed");buffer.ResetForReuse();BufferedInterface(set,buffer);if(!Writebyte(set).AsSpan().SequenceEqual(buffer.GetSpan()))throw new Exception("Buffered interface bytes changed");}
                if(Json(set)!="[3,7,11]")throw new Exception("JSON changed");
                SetHolder value=new SetHolder{Value=set};byte[] bytes=Nested(value);SetHolder copy=null;LuminPackSerializer.Deserialize(bytes,ref copy);
                if(!copy.Value.SetEquals(set))throw new Exception("Nested set changed");
                }}
                """);
        return source.ToString();
    }

    /// <summary>Checks exact-interface kernels and excluded larger/user/custom formatter roots.</summary>
    /// <param name="driver">The completed production generator driver.</param>
    /// <param name="expected">Whether the runtime currently exposes the selected kernel.</param>
    private static void VerifySelection(GeneratorDriver driver, bool expected)
    {
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        var selected = methods.Where(method => method.ToString().Contains("LuminPackMarshal.WriteSmallHashSetRaw(", StringComparison.Ordinal)).ToArray();
        Require(expected ? selected.Length >= 21 : selected.Length == 0, "Grouped-store capability selection failed.");
        foreach (var method in selected)
            Require(!method.ParameterList.ToString().Contains("UserElement", StringComparison.Ordinal) &&
                !method.ParameterList.ToString().Contains("CustomElement", StringComparison.Ordinal) &&
                !method.ParameterList.ToString().Contains("HashSet<long>", StringComparison.Ordinal), "An excluded raw contract selected small stores.");
        var exact = methods.Where(method => method.Identifier.ValueText == "RootWriteExactInterface").ToArray();
        Require(exact.Length == 14, "Expected both exact interface contracts for all seven small scalar families.");
        Require(exact.All(method => method.Body!.ToString().Contains(expected ? "WriteSmallHashSetRaw" : "WriteHashSetRaw", StringComparison.Ordinal)), "Exact interfaces selected a stale kernel.");
        if (expected)
            Require(!methods.Any(method => method.Body?.ToString().Contains("LuminPackMarshal.WriteHashSetRaw(", StringComparison.Ordinal) == true &&
                method.ParameterList.ToString().Contains("ISet<byte>", StringComparison.Ordinal)), "A pooled small-set interface body lost the accepted kernel selection.");
    }

    /// <summary>Creates a fresh consumer compilation for one generation tier.</summary>
    /// <param name="source">The complete consumer source.</param>
    /// <param name="parse">Current language and target symbols.</param>
    /// <param name="references">The actual runtime references.</param>
    /// <returns>The isolated input compilation.</returns>
    private static CSharpCompilation Compile(string source, CSharpParseOptions parse, MetadataReference[] references)
        => CSharpCompilation.Create("SmallSets" + Guid.NewGuid().ToString("N"), [CSharpSyntaxTree.ParseText(source, parse, "SmallSets.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

    /// <summary>Runs both production generators and rejects generator errors.</summary>
    /// <param name="compilation">The current consumer compilation.</param>
    /// <param name="parse">The current parsing options.</param>
    /// <returns>The retained driver and updated compilation.</returns>
    private static (GeneratorDriver Driver, Compilation Output) Generate(CSharpCompilation compilation, CSharpParseOptions parse)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        return (driver, output);
    }

    /// <summary>Compiles and executes complete record and nested/buffered/JSON checks.</summary>
    /// <param name="output">The generated consumer compilation.</param>
    private static void Execute(Compilation output)
    {
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("SmallCases")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Orders complete generated trees by their stable hint names.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>The complete generated source snapshots.</returns>
    private static string[] Trees(GeneratorDriver driver)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(result => result.HintName + "\n" + result.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a failed exact selection, compilation or runtime invariant.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
