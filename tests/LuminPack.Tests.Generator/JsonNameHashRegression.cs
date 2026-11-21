extern alias ModelBasedAnalyzer;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using LuminPack.Build;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks compile-time name constants against the runtime's shared XXH3 implementation.</summary>
internal static class JsonNameHashRegression
{
    /// <summary>Checks Next at every length boundary and with non-ASCII code units.</summary>
    internal static void Run()
    {
        Type next = typeof(ModelBasedGenerator).Assembly.GetType("LuminPack.SourceGenerator.Next.Rendering.Emitters.JsonNames", true)!;
        var byteHash = Bind<byte[]>(next, "ComputeUtf8BytesHash");
        var charHash = Bind<string>(next, "ComputeUtf16Hash");
        byte[] storage = new byte[8192];
        new Random(73681).NextBytes(storage);
        for (int length = 0; length <= 4096; length++)
        for (int offset = 0; offset < 16; offset++)
        {
            byte[] input = storage.AsSpan(offset, length).ToArray();
            ulong expected = LuminPack.Internal.XxHash3.Hash64Utf8(input);
            Require(byteHash(input) == expected, "UTF-8 bytes", length, offset);
            string text = new string(MemoryMarshal.Cast<byte, char>(input.AsSpan(0, length & ~1)));
            expected = LuminPack.Internal.XxHash3.Hash64Utf16(text);
            Require(charHash(text) == expected, "UTF-16 code units", length, offset);
        }
        foreach (string text in new[] { "", "Count", "\u540d\u79f0\U0001f680", "\u00e9", "e\u0301", "\uD800", "\u0000Name" })
        {
            byte[] input = Encoding.UTF8.GetBytes(text);
            Require(byteHash(input) == LuminPack.Internal.XxHash3.Hash64Utf8(input), "encoded name", input.Length, 0);
            Require(charHash(text) == LuminPack.Internal.XxHash3.Hash64Utf16(text), "raw code units", text.Length, 0);
        }
        Console.WriteLine("JSON name hashes: Next matches Core for 65,552 byte ranges and UTF-16 ranges, plus Unicode names.");
        VerifyGeneratedLookups();
    }

    /// <summary>Invokes an emitted UTF-8 lookup without allocating a string.</summary>
    /// <param name="name">The decoded property-name bytes.</param>
    /// <returns>The selected member index, or minus one.</returns>
    private delegate int ByteLookup(ReadOnlySpan<byte> name);

    /// <summary>Invokes an emitted UTF-16 lookup without transcoding.</summary>
    /// <param name="name">The decoded property-name code units.</param>
    /// <returns>The selected member index, or minus one.</returns>
    private delegate int CharLookup(ReadOnlySpan<char> name);

    /// <summary>Compiles Next name tables against the portable runtime and executes their lookups.</summary>
    private static void VerifyGeneratedLookups()
    {
        MetadataReference[] references = CompatibilityCompilationReferences.Create();
        string runtime = references.OfType<PortableExecutableReference>().Single(r => Path.GetFileName(r.FilePath) == "LuminPack.Core.dll").FilePath!;
        var parse = new CSharpParseOptions(LanguageVersion.CSharp10).WithPreprocessorSymbols("LUMINPACK_IL_REWRITE");
        foreach (string shape in new[] { "Sparse", "FourPerLength", "FivePerLength", "Encoding", "Dense", "Circle", "Aliases" })
        {
            string[] names = Enumerable.Range(0, 16).Select(i => shape switch
            {
                "Sparse" => "Name" + new string('x', i),
                "FourPerLength" => "Property" + i.ToString("D3") + new string('x', i / 4),
                "FivePerLength" => "Property" + i.ToString("D3") + new string('x', i / 5),
                "Encoding" => "P" + new string('\u4e2d', i) + new string('a', 15 - i),
                _ => "Property" + i.ToString("D3"),
            }).ToArray();
            var source = new StringBuilder("using LuminPack; using LuminPack.Attribute; using LuminPack.Option;\n");
            source.AppendLine("[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Auto, ForceGenerateDeserializeJson = true)]");
            source.AppendLine(shape == "Circle" ? "[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)]" :
                shape == "Aliases" ? "[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]" : "[LuminPackable]");
            source.AppendLine("public partial class Payload {");
            string alias = new string('a', 257) + "\u540d\u79f0";
            for (int i = 0; i < names.Length; i++)
            {
                if (shape == "Aliases" && i == 0)
                    source.AppendLine("[LuminPackOldName(\"" + alias + "\")]");
                if (shape == "Circle")
                    source.AppendLine("[LuminPackOrder(" + i + ")]");
                source.AppendLine("public string " + names[i] + ";");
            }
            source.AppendLine("}");
            if (shape != "Circle")
            {
                string json = "{\"unknown\":{\"nested\":[1]}," + string.Join(",", names.Select((name, index) =>
                    "\"" + (shape == "Aliases" && index == 0 ? alias : index == 1 ? "\\u" + ((int)name[0]).ToString("X4") + name.Substring(1) : name) + "\":\"value" + index + "\"").Reverse()) + "}";
                source.AppendLine("public static class Entry { public static void Main() => Exercise(); public static bool Exercise() {");
                source.AppendLine("string json = " + SymbolDisplay.FormatLiteral(json, true) + ";");
                foreach (string encoding in new[] { "Utf8", "Utf16" })
                {
                    source.AppendLine("var result" + encoding + " = LuminPackSerializer.DeserializeJson<Payload, LuminPack" + encoding + "LengthOptions>(json);");
                    for (int i = 0; i < names.Length; i++)
                        source.AppendLine("if (result" + encoding + "." + names[i] + " != \"value" + i + "\") return false;");
                }
                source.AppendLine("return true; } }");
            }
            else
                source.AppendLine("public static class Entry { public static void Main() {} }");
            string assemblyName = "HashLookup" + shape;
            var input = CSharpCompilation.Create(assemblyName, new[] { CSharpSyntaxTree.ParseText(source.ToString(), parse, assemblyName + ".cs", Encoding.UTF8) }, references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            ISourceGenerator[] generators = { new ModelBasedGenerator().AsSourceGenerator() };
            var driver = CSharpGeneratorDriver.Create(generators, parseOptions: parse).RunGeneratorsAndUpdateCompilation(input, out Compilation output, out _);
            var lookups = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources)
                .SelectMany(s => s.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(m => m.Identifier.ValueText.StartsWith("GetJsonPropertyIndex", StringComparison.Ordinal)).ToArray();
            bool hash8 = UsesReferenceHash(lookups.Single(m => m.Identifier.ValueText == "GetJsonPropertyIndexUtf8"));
            bool hash16 = UsesReferenceHash(lookups.Single(m => m.Identifier.ValueText == "GetJsonPropertyIndexUtf16"));
            Require(hash8 == (shape is not ("Sparse" or "FourPerLength" or "Encoding")), "UTF-8 dispatch " + assemblyName, names.Length, 0);
            Require(hash16 == (shape is not ("Sparse" or "FourPerLength")), "UTF-16 dispatch " + assemblyName, names.Length, 0);
            using var pe = new MemoryStream();
            using var pdb = new MemoryStream();
            var result = output.Emit(pe, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
            if (!result.Success)
                throw new InvalidOperationException(assemblyName + ": " + string.Join("\n", result.Diagnostics));
            byte[] image = pe.ToArray();
            var rewritten = StaticCallAssembly.Rewrite(image, pdb.ToArray(), references.OfType<PortableExecutableReference>().Select(r => r.FilePath!).ToArray());
            var context = new AssemblyLoadContext(assemblyName, isCollectible: true);
            try
            {
                context.LoadFromAssemblyPath(runtime);
                using var execution = new MemoryStream(rewritten?.Pe ?? image);
                var assembly = context.LoadFromStream(execution);
                if (shape != "Circle")
                    Require(assembly.GetType("Entry")!.GetMethod("Exercise")!.Invoke(null, null) is true, "JSON read " + assemblyName, 0, 0);
                var methods = assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)).ToArray();
                var bytes = methods.Single(m => m.Name == "GetJsonPropertyIndexUtf8").CreateDelegate<ByteLookup>();
                var chars = methods.Single(m => m.Name == "GetJsonPropertyIndexUtf16").CreateDelegate<CharLookup>();
                for (int i = names.Length - 1; i >= 0; i--)
                {
                    Require(bytes(Encoding.UTF8.GetBytes(names[i])) == i, assemblyName, i, 0);
                    Require(chars(names[i]) == i, assemblyName, i, 1);
                    Require(bytes(Encoding.UTF8.GetBytes(names[i] + "!")) == -1, "unknown " + assemblyName, i, 0);
                    Require(chars(names[i].ToLowerInvariant()) == -1, "case " + assemblyName, i, 1);
                }
                if (shape == "Aliases")
                    Require(bytes(Encoding.UTF8.GetBytes(alias)) == 0 && chars(alias) == 0, "long alias " + assemblyName, alias.Length, 0);
                if (shape == "Circle")
                    Require(bytes("$id"u8) == -2 && chars("$ref") == -3, "reference metadata " + assemblyName, 0, 0);
                Require(bytes(ReadOnlySpan<byte>.Empty) == -1 && chars(ReadOnlySpan<char>.Empty) == -1, "empty " + assemblyName, 0, 0);
            }
            finally
            {
                context.Unload();
            }
        }
        Console.WriteLine("JSON name lookup: Next passed sparse/dense tables, aliases, reference metadata and unknown names on the portable runtime.");
    }

    /// <summary>Recognizes the emitted by-reference hash call independently of line wrapping and aliases.</summary>
    /// <param name="method">The generated lookup declaration.</param>
    /// <returns>Whether the lookup hashes the validated name through a ref argument.</returns>
    private static bool UsesReferenceHash(MethodDeclarationSyntax method) => method.DescendantNodes().OfType<InvocationExpressionSyntax>()
        .Any(call => call.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == "Hash64" &&
            call.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword));

    /// <summary>Binds the actual private or internal generator entry without copying its algorithm into the test.</summary>
    /// <typeparam name="T">The generator input type.</typeparam>
    /// <param name="owner">The owning generator type.</param>
    /// <param name="name">The method name.</param>
    /// <returns>The generator hash entry.</returns>
    private static Func<T, ulong> Bind<T>(Type owner, string name) =>
        owner.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<T, ulong>>();

    /// <summary>Reports the exact input range on a compile-time/runtime mismatch.</summary>
    /// <param name="matches">Whether the hash values agree.</param>
    /// <param name="encoding">The input encoding.</param>
    /// <param name="length">The input length.</param>
    /// <param name="offset">The original alignment.</param>
    private static void Require(bool matches, string encoding, int length, int offset)
    {
        if (!matches)
            throw new InvalidOperationException($"JSON name hash mismatch: {encoding}, length {length}, offset {offset}.");
    }
}
