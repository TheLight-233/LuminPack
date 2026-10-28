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
            foreach (bool modern in new[] { true, false })
            {
                Verify(references, mode, modern);
                VerifyAssembly(references, mode, modern);
            }
        SerializerPortableTupleRegression.Run(references);
        Console.WriteLine("Passed portable layout proofs, packed array/List loops, architecture gates, raw fast paths, and incremental opt-in edits.");
    }

    /// <summary>Checks assembly scope, managed exclusions, imported raw storage and settings-only incremental edits.</summary>
    /// <param name="references">Current runtime and framework metadata.</param>
    /// <param name="mode">The generation mode to exercise.</param>
    /// <param name="modern">Whether modern or Unity conditional generation is selected.</param>
    private static void VerifyAssembly(MetadataReference[] references, string mode, bool modern)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: modern
            ? ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"] : ["UNITY_2022_3_OR_NEWER"]);
        var foreign = CSharpCompilation.Create("ForeignPortableScope", [CSharpSyntaxTree.ParseText(
            "using LuminPack.Attribute; namespace Foreign; [LuminPackable] public struct Raw { public byte Tag; public long Value; }", parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var foreignImage = new MemoryStream();
        Require(foreign.Emit(foreignImage).Success, "Could not create the unmarked referenced assembly.");
        var settings = CSharpSyntaxTree.ParseText($$"""
            using LuminPack.Attribute;
            [assembly: LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.{{mode}})]
            [assembly: LuminPackable(typeof(Foreign.Raw))]
            """, parse, "Settings.cs");
        var models = CSharpSyntaxTree.ParseText("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Attribute;
            namespace AssemblyPortableModels;
            [LuminPackable] public struct Padded { public byte Tag; public long Value; }
            [LuminPackable, StructLayout(LayoutKind.Sequential, Pack = 1)] public struct Compact { public byte Tag; public long Value; }
            [LuminPackable] public struct Managed { public string Text; }
            [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Native)] public struct OptedOut { public byte Tag; public long Value; }
            [LuminPackable] public struct Generic<T> where T : unmanaged { public byte Tag; public T Value; }
            public static class Entry
            {
                public static int Run()
                {
                    var value = new Padded { Tag = 7, Value = 123456789012345 };
                    var bytes = LuminPackSerializer.Serialize(value);
                    if (bytes.Length != 9 || bytes[0] != 7 || BitConverter.ToInt64(bytes, 1) != value.Value)
                        throw new InvalidOperationException("Assembly opt-in did not pack storage.");
                    var list = LuminPackSerializer.Deserialize<List<Padded>>(LuminPackSerializer.Serialize(new List<Padded> { value }));
                    var generic = LuminPackSerializer.Deserialize<Generic<long>>(LuminPackSerializer.Serialize(new Generic<long> { Tag = 7, Value = value.Value }));
                    var managed = LuminPackSerializer.Deserialize<Managed>(LuminPackSerializer.Serialize(new Managed { Text = "abc" }));
                    var foreign = LuminPackSerializer.Serialize(new Foreign.Raw { Tag = 7, Value = value.Value });
                    var compact = LuminPackSerializer.Serialize(new Compact());
                    if (list[0].Value != value.Value || generic.Value != value.Value || managed.Text != "abc" || compact.Length != 9)
                        throw new InvalidOperationException("Assembly collection, generic or managed path failed.");
                    return foreign.Length;
                }
            }
            """, parse, "Models.cs");
        var input = CSharpCompilation.Create("AssemblyPortable_" + mode + "_" + modern, [settings, models],
            references.Append(MetadataReference.CreateFromImage(foreignImage.ToArray())),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        string generated = Text(driver);
        Require(generated.Contains("LocalPortableLocalAssemblyPortableModels_Padded", StringComparison.Ordinal), "Missing assembly-level opt-in.");
        Require(generated.Contains("LocalPortableLocalForeign_Raw", StringComparison.Ordinal), "Imported unmarked storage with a variable layout lost its layout models.");
        Require(!generated.Contains("LocalPortableLocalAssemblyPortableModels_Compact", StringComparison.Ordinal), "Packed storage lost its raw fast path.");
        Require(!generated.Contains("LocalPortableLocalAssemblyPortableModels_OptedOut", StringComparison.Ordinal), "A struct-level StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Native did not override the assembly option.");
        Require(generated.Contains("LuminPortable<global::AssemblyPortableModels.Padded, ", StringComparison.Ordinal), "A packed collection lost its vectorized prefix.");
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var context = new System.Runtime.Loader.AssemblyLoadContext("PortableAssemblyScope", isCollectible: true);
        foreignImage.Position = 0;
        image.Position = 0;
        var foreignAssembly = context.LoadFromStream(foreignImage);
        var assembly = context.LoadFromStream(image);
        Require((int)assembly.GetType("AssemblyPortableModels.Entry")!.GetMethod("Run")!.Invoke(null, null)! == 9,
            "Imported unmarked storage with a variable layout was not written without padding.");
        context.Unload();
        var changed = input.ReplaceSyntaxTree(settings, CSharpSyntaxTree.ParseText(settings.ToString().Replace("[assembly: LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]", "", StringComparison.Ordinal), parse, "Settings.cs"));
        var retained = driver.RunGenerators(changed);
        var fresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse).RunGenerators(changed);
        // Without the marker the padded struct is no longer packed, but its variable layout still gets a tagged native payload.
        Require(Text(retained) == Text(fresh) &&
            !Text(retained).Contains("LuminPortable<global::AssemblyPortableModels.Padded, ", StringComparison.Ordinal) &&
            Text(retained).Contains("LuminStorageTag<global::AssemblyPortableModels.Padded, ", StringComparison.Ordinal),
            "An assembly-only marker edit retained stale packed operations.");

        var foreignTree = foreign.SyntaxTrees.Single();
        var markedForeign = foreign.ReplaceSyntaxTree(foreignTree, CSharpSyntaxTree.ParseText(
            foreignTree.ToString().Replace("namespace Foreign;", "[assembly: LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)] namespace Foreign;", StringComparison.Ordinal), parse));
        using var markedForeignImage = new MemoryStream();
        Require(markedForeign.Emit(markedForeignImage).Success, "Could not create the opted-in referenced assembly.");
        var originalReference = input.References.Last();
        var markedReference = MetadataReference.CreateFromImage(markedForeignImage.ToArray());
        var referenceEdit = input.ReplaceReference(originalReference, markedReference);
        var referenceRetained = driver.RunGeneratorsAndUpdateCompilation(referenceEdit, out var referenceOutput, out var referenceDiagnostics);
        var referenceFresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse).RunGenerators(referenceEdit);
        Require(!referenceDiagnostics.Any(item => item.Severity == DiagnosticSeverity.Error) && Text(referenceRetained) == Text(referenceFresh) &&
            Text(referenceRetained).Contains("LocalPortableLocalForeign_Raw", StringComparison.Ordinal), "A referenced assembly marker edit retained stale raw operations.");
        using var referenceImage = new MemoryStream();
        var referenceEmit = referenceOutput.Emit(referenceImage);
        Require(referenceEmit.Success, string.Join(Environment.NewLine, referenceEmit.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var referenceContext = new System.Runtime.Loader.AssemblyLoadContext("PortableReferenceScope", isCollectible: true);
        markedForeignImage.Position = 0;
        referenceImage.Position = 0;
        referenceContext.LoadFromStream(markedForeignImage);
        var referenceAssembly = referenceContext.LoadFromStream(referenceImage);
        Require((int)referenceAssembly.GetType("AssemblyPortableModels.Entry")!.GetMethod("Run")!.Invoke(null, null)! == 9,
            "The declaring assembly's marker did not select the imported packed format.");
        referenceContext.Unload();
        Require(Text(referenceRetained.RunGenerators(input)) == Text(driver), "Removing an imported assembly marker retained stale packed operations.");

        var invalid = input.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            "using LuminPack.Attribute; [LuminPackable] public struct NativePortable { public System.IntPtr Value; }", parse, "InvalidAssembly.cs"));
        Require(driver.RunGenerators(invalid).GetRunResult().Diagnostics.Any(item => item.Id == "LuminPack111"), "An unsupported assembly-scoped struct silently used raw storage.");
        var misplaced = input.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            "using LuminPack.Attribute; [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)] public class MisplacedPortable { public int Value; }", parse, "Misplaced.cs"));
        Require(driver.RunGenerators(misplaced).GetRunResult().Diagnostics.Any(item => item.Id == "LuminPack110" && item.GetMessage().Contains("StructLayout", StringComparison.Ordinal)),
            "StructLayout on a class was not rejected.");
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
            [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
            public struct Padded { public byte Tag; public long Value; }
            [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed), StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Compact { public byte Tag; public long Value; }
            [LuminPackable] public struct Raw { public byte Tag; public long Value; }
            [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
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
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
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
            !method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call => call.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } &&
                generic.TypeArgumentList.ToString().Contains("Padded", StringComparison.Ordinal) && call.ArgumentList.ToString().Contains("AsSpan", StringComparison.Ordinal)) &&
            !method.ToString().Contains("foreach", StringComparison.Ordinal)), "Packed elements reached a raw/foreach collection path.");
        Require(generated.Contains("SizeOf<global::System.IntPtr>()", StringComparison.Ordinal) &&
            generated.Contains("LuminPackWriter.Write", StringComparison.Ordinal), "Missing the 32-bit safe scalar branch.");
        Require(methods.Any(method => method.Identifier.ValueText == "RootWrite" && method.ParameterList.Parameters.Count == 2 &&
            method.ParameterList.Parameters[1].Type!.ToString().Contains("PortableModels.Raw[]", StringComparison.Ordinal) &&
            method.Body!.ToString().Contains("AsSpan", StringComparison.Ordinal)), "Unmarked variable-layout arrays lost their native block path.");
        Require(generated.Contains("CreateFreshListArray", StringComparison.Ordinal), "Fresh portable lists lost the existing allocation/count/version path.");
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var assembly = Assembly.Load(image.ToArray());
        int expected = 13 + 13 + 4 + 1 + 2 * System.Runtime.InteropServices.Marshal.SizeOf(assembly.GetType("PortableModels.Raw")!) + 22;
        Require((int)assembly.GetType("PortableModels.Entry")!.GetMethod("Run")!.Invoke(null, null)! == expected, "Incorrect packed, tagged or raw payload length.");

        var edited = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(source.Replace("[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]\n", "[LuminPackable]\n", StringComparison.Ordinal), parse, "Portable.cs"));
        var retained = driver.RunGenerators(edited);
        GeneratorDriver fresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        fresh = fresh.RunGenerators(edited);
        Require(Text(retained) == Text(fresh), "Portable marker edits did not invalidate the cached operation.");

        var contiguous = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(source.Replace("public byte Tag; public long Value;", "public long Tag; public long Value;", StringComparison.Ordinal), parse, "Portable.cs"));
        var changedLayout = driver.RunGenerators(contiguous);
        Require(!Text(changedLayout).Contains("LocalPortableLocalPortableModels_Padded", StringComparison.Ordinal), "A padding-free field edit retained the packed operation.");
        fresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse).RunGenerators(contiguous);
        if (Text(changedLayout) != Text(fresh) && Environment.GetEnvironmentVariable("LUMINPACK_MECHANISM_CAPTURE") is { Length: > 0 } capture)
        {
            Directory.CreateDirectory(capture);
            File.WriteAllText(Path.Combine(capture, "portable-retained.txt"), Text(changedLayout));
            File.WriteAllText(Path.Combine(capture, "portable-fresh.txt"), Text(fresh));
        }
        Require(Text(changedLayout) == Text(fresh), "A storage layout edit did not invalidate the cached operation.");
        if (mode == "Minimal" && modern)
        {
            var invalidTree = CSharpSyntaxTree.ParseText("using LuminPack.Attribute; [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)] public struct InvalidPortable { public System.IntPtr Native; }", parse, "Invalid.cs");
            var invalid = input.RemoveAllSyntaxTrees().AddSyntaxTrees(invalidTree);
            var rejected = driver.RunGenerators(invalid).GetRunResult();
            Require(rejected.Diagnostics.Any(item => item.Id == "LuminPack111"), "Native-width storage silently retained a raw copy.");
            invalidTree = CSharpSyntaxTree.ParseText("using LuminPack; using LuminPack.Attribute; [LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)] public struct InvalidPortable<T> where T : unmanaged { public T Native; } public static class InvalidEntry { public static byte[] Run() => LuminPackSerializer.Serialize(new InvalidPortable<System.IntPtr>()); }", parse, "InvalidGeneric.cs");
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
