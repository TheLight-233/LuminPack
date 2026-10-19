using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks portable layout proofs, generated collection loops, and retained raw operations.</summary>
internal static class SerializerPortableStructRegression
{
    /// <summary>Checks complete entries on both generation modes and legacy/Unity API surfaces.</summary>
    /// <param name="references">Current runtime and platform metadata.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Minimal" })
            foreach (bool modern in new[] { true, false }) Verify(references, mode, modern);
        Console.WriteLine("Passed portable layout proofs, packed array/List loops, architecture gates, raw fast paths, and incremental opt-in edits.");
    }

    /// <summary>Compiles one fixture, inspects its actual operations, and invalidates the marker incrementally.</summary>
    /// <param name="references">Runtime and framework metadata.</param>
    /// <param name="mode">The generation reachability mode.</param>
    /// <param name="modern">Whether modern APIs are available rather than the Unity fallback.</param>
    private static void Verify(MetadataReference[] references, string mode, bool modern)
    {
        string source = $$"""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.{{mode}})]
            namespace PortableModels;
            [LuminPackable, LuminPackSupport32bit]
            public struct Padded { public byte Tag; public long Value; }
            [LuminPackable, LuminPackSupport32bit, StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Compact { public byte Tag; public long Value; }
            [LuminPackable] public struct Raw { public byte Tag; public long Value; }
            [LuminPackable, LuminPackSupport32bit]
            public struct Interleaved { public long First { get; set; } public byte Tag; public int Last { get; set; } public short Tail; }
            public static class Entry
            {
                public static int Run()
                {
                    var values = new[] { new Padded { Tag = 7, Value = 123456789012345 } };
                    var bytes = LuminPackSerializer.Serialize(values);
                    var copy = LuminPackSerializer.Deserialize<Padded[]>(bytes);
                    var listBytes = LuminPackSerializer.Serialize(new List<Padded>(values));
                    var list = LuminPackSerializer.Deserialize<List<Padded>>(listBytes);
                    var rawBytes = LuminPackSerializer.Serialize(new Raw[2]);
                    var compactBytes = LuminPackSerializer.Serialize(new Compact[2]);
                    var interleaved = new Interleaved { First = 123456789012345, Tag = 7, Last = 123456789, Tail = 13 };
                    var interleavedBytes = LuminPackSerializer.Serialize(interleaved);
                    var interleavedCopy = LuminPackSerializer.Deserialize<Interleaved>(interleavedBytes);
                    if (interleavedBytes.Length != 15 || BitConverter.ToInt64(interleavedBytes, 0) != interleaved.First ||
                        interleavedBytes[8] != interleaved.Tag || BitConverter.ToInt32(interleavedBytes, 9) != interleaved.Last ||
                        BitConverter.ToInt16(interleavedBytes, 13) != interleaved.Tail || interleavedCopy.First != interleaved.First ||
                        interleavedCopy.Last != interleaved.Last)
                        throw new InvalidOperationException("Declaration-ordered backing storage was not preserved.");
                    if (copy[0].Value != values[0].Value || list[0].Value != values[0].Value)
                        throw new InvalidOperationException("Portable field read failed.");
                    return bytes.Length + listBytes.Length + rawBytes.Length + compactBytes.Length;
                }
            }
            """;
        string[] symbols = modern ? ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"] : ["UNITY_2022_3_OR_NEWER"];
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: symbols);
        var tree = CSharpSyntaxTree.ParseText(source, parse, "Portable.cs");
        var input = CSharpCompilation.Create("Portable_" + Guid.NewGuid().ToString("N"), [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        var sources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
        string generated = string.Join("\n", sources.Select(result => result.SourceText.ToString()));
        Require(generated.Contains("LocalPortableLocalPortableModels_Padded", StringComparison.Ordinal), "Padded storage was not expanded.");
        Require(!generated.Contains("LocalPortableLocalPortableModels_Compact", StringComparison.Ordinal), "Padding-free storage lost its raw fast path.");
        var methods = sources.SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        var packedWriters = methods.Where(method => method.ParameterList.Parameters.Count == 2 &&
            method.ParameterList.Parameters[0].Type?.ToString().Contains("LuminPackWriter", StringComparison.Ordinal) == true &&
            method.ParameterList.Parameters[1].Type?.ToString().Contains("Padded", StringComparison.Ordinal) == true).ToArray();
        Require(packedWriters.Any(method => method.ToString().Contains("WriteUnaligned", StringComparison.Ordinal)), "Missing direct scalar writes.");
        Require(packedWriters.Any(method => method.ToString().Contains("for (", StringComparison.Ordinal)), "Missing indexed collection traversal.");
        Require(packedWriters.All(method => !method.ToString().Contains("DangerousWriteUnmanagedArray", StringComparison.Ordinal) &&
            !method.ToString().Contains("DangerousWriteUnmanagedSpan", StringComparison.Ordinal) &&
            !method.ToString().Contains("foreach", StringComparison.Ordinal)), "Packed elements reached a raw/foreach collection path.");
        Require(generated.Contains("SizeOf<global::System.IntPtr>()", StringComparison.Ordinal) &&
            generated.Contains("WriteUnmanagedWithoutSizeReturn", StringComparison.Ordinal), "Missing the 32-bit safe scalar branch.");
        Require(generated.Contains("DangerousWriteUnmanagedArray", StringComparison.Ordinal), "Unmarked arrays lost their raw path.");
        Require(generated.Contains("CreateFreshListArray", StringComparison.Ordinal), "Fresh portable lists lost the existing allocation/count/version path.");
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var assembly = Assembly.Load(image.ToArray());
        int expected = 13 + 13 + 4 + 2 * System.Runtime.InteropServices.Marshal.SizeOf(assembly.GetType("PortableModels.Raw")!) + 22;
        Require((int)assembly.GetType("PortableModels.Entry")!.GetMethod("Run")!.Invoke(null, null)! == expected, "Incorrect packed or raw payload length.");

        var edited = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(source.Replace("[LuminPackable, LuminPackSupport32bit]\n", "[LuminPackable]\n", StringComparison.Ordinal), parse, "Portable.cs"));
        var retained = driver.RunGenerators(edited);
        GeneratorDriver fresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: parse);
        fresh = fresh.RunGenerators(edited);
        Require(Text(retained) == Text(fresh), "Portable marker edits did not invalidate the cached operation.");

        var contiguous = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(source.Replace("public byte Tag; public long Value;", "public long Tag; public long Value;", StringComparison.Ordinal), parse, "Portable.cs"));
        var changedLayout = driver.RunGenerators(contiguous);
        Require(!Text(changedLayout).Contains("LocalPortableLocalPortableModels_Padded", StringComparison.Ordinal), "A padding-free field edit retained the packed operation.");
        fresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: parse).RunGenerators(contiguous);
        Require(Text(changedLayout) == Text(fresh), "A storage layout edit did not invalidate the cached operation.");
        if (mode == "Minimal" && modern)
        {
            var invalidTree = CSharpSyntaxTree.ParseText("using LuminPack.Attribute; [LuminPackable, LuminPackSupport32bit] public struct InvalidPortable { public System.IntPtr Native; }", parse, "Invalid.cs");
            var invalid = input.RemoveAllSyntaxTrees().AddSyntaxTrees(invalidTree);
            var rejected = driver.RunGenerators(invalid).GetRunResult();
            Require(rejected.Diagnostics.Any(item => item.Id == "LuminPack111"), "Native-width storage silently retained a raw copy.");
            invalidTree = CSharpSyntaxTree.ParseText("using LuminPack; using LuminPack.Attribute; [LuminPackable, LuminPackSupport32bit] public struct InvalidPortable<T> where T : unmanaged { public T Native; } public static class InvalidEntry { public static byte[] Run() => LuminPackSerializer.Serialize(new InvalidPortable<System.IntPtr>()); }", parse, "InvalidGeneric.cs");
            rejected = driver.RunGenerators(input.RemoveAllSyntaxTrees().AddSyntaxTrees(invalidTree)).GetRunResult();
            Require(rejected.Diagnostics.Any(item => item.Id == "LuminPack111"), "Unsupported closed generic storage silently retained a raw copy.");
        }
    }

    /// <summary>Canonicalizes all generated files for retained-versus-fresh comparison.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <returns>Ordered generated source.</returns>
    private static string Text(GeneratorDriver driver) => string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .OrderBy(source => source.HintName, StringComparer.Ordinal).Select(source => source.HintName + source.SourceText));

    /// <summary>Reports a structural or runtime regression.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
