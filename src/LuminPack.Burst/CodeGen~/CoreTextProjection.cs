using System.Security.Cryptography;
using System.Text;
using System.Runtime.Loader;
using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Projects Core text traversal into Burst while mapping unsupported platform primitives to Core's portable fallback.</summary>
internal static class CoreTextProjection
{
    private static readonly CSharpParseOptions Modern = new(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET6_0_OR_GREATER"]);
    private static readonly CSharpParseOptions Portable = new(LanguageVersion.Preview);

    /// <summary>Writes or verifies deterministic Core text projections without building or editing Core.</summary>
    /// <param name="coreRoot">The authoritative Core source directory or a fixed copy.</param><param name="directory">The Burst generated-source directory.</param>
    /// <param name="verify">Whether files must already match.</param><returns>Whether every output matches.</returns>
    internal static bool Generate(string coreRoot, string directory, bool verify)
    {
        string file = "Utility/Text/StringSerializer.cs";
        var modern = Read(coreRoot, file, Modern);
        var portable = Read(coreRoot, file, Portable);
        string[] methods = ["PreferCustomSimd", "IsUniformUtf8Run", "SerializeAsUtf8", "TryEncodeTinyAscii", "SerializeAsciiRun", "TryWidenAscii", "SerializeAsUtf8Slow", "DeserializeFromUtf8", "TranscodeToUtf8", "EncodeAsciiRun", "TranscodeToUtf8Scalar", "TranscodeToUtf16", "IsBmp3", "IsUtf8ContinuationByte", "GetInvalidUtf8SequenceLength", "TryEncodeVectorRun", "MatchesUtf8", "MatchesShortUtf8", "MatchesUtf8Chunks"];
        var output = new StringBuilder(Header);
        output.AppendLine("    internal static unsafe class CoreTextAlgorithms\n    {");
        AppendOrigin(output, coreRoot, file);
        foreach (string name in methods)
        {
            var method = modern.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name);
            AppendMethod(output, method);
        }
        foreach (var field in modern.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == "Skip" || v.Identifier.ValueText.StartsWith("Bmp", StringComparison.Ordinal))))
            output.AppendLine(Rewrite(field));
        AppendOrigin(output, coreRoot, "Utility/Text/StringSerializer.Latin1.cs");
        foreach (var method in Read(coreRoot, "Utility/Text/StringSerializer.Latin1.cs", Modern).DescendantNodes().OfType<MethodDeclarationSyntax>()) AppendMethod(output, method);
        output.AppendLine("    }");
        output.AppendLine("    internal static unsafe class CoreTextFallback\n    {");
        // The portable loop is Core's own replacement/final-block implementation. Only its managed Encoding fast path is omitted.
        var fallback = portable.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "SerializeAsUtf8Slow");
        var originalFixed = fallback.Body!.Statements.OfType<FixedStatementSyntax>().Single();
        fallback = fallback.WithIdentifier(SyntaxFactory.Identifier("Encode")).WithBody(SyntaxFactory.Block(originalFixed))
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.UnsafeKeyword)));
        string fallbackText = Rewrite(fallback).Replace("TranscodeToUtf8(", "CoreTextAlgorithms.TranscodeToUtf8Scalar(", StringComparison.Ordinal);
        output.AppendLine(fallbackText);
        output.AppendLine("    }");
        foreach (var item in new[] { ("Internal/ShortAsciiTranscoder.cs", "CoreShortAscii", new[] { "TryEncode", "TryEncodeMedium", "NeedsJsonEscape" }), ("Internal/Utf8Latin1Transcoder.cs", "CoreLatinUtf8", new[] { "CreateShuffleMasks", "EncodePrefix", "StoreLane" }), ("Internal/Utf8TwoByteComparer.cs", "CoreTwoByteComparer", new[] { "Matches" }) })
        {
            var syntax = Read(coreRoot, item.Item1, Modern);
            AppendOrigin(output, coreRoot, item.Item1);
            output.AppendLine("    internal static unsafe class " + item.Item2 + "\n    {");
            if (item.Item2 == "CoreLatinUtf8") { if (!AppendLatinMasks(output, syntax)) return false; }
            else foreach (var field in syntax.DescendantNodes().OfType<FieldDeclarationSyntax>()) output.AppendLine(Rewrite(field));
            foreach (var method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => item.Item3.Contains(m.Identifier.ValueText) && m.Identifier.ValueText != "CreateShuffleMasks"))
            {
                if (item.Item2 == "CoreLatinUtf8")
                {
                    var mapped = method.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.UnsafeKeyword)));
                    string native = Rewrite(mapped);
                    if (method.Identifier.ValueText == "EncodePrefix") native = new NativeLatin1Primitives().Visit(SyntaxFactory.ParseMemberDeclaration(native))!.NormalizeWhitespace().ToFullString();
                    else native = native.Replace("Vector128<byte> packed", "global::Unity.Burst.Intrinsics.v128 packed", StringComparison.Ordinal).Replace("packed.AsUInt64().GetElement(0)", "packed.ULong0", StringComparison.Ordinal).Replace("packed.AsUInt64().GetElement(1)", "packed.ULong1", StringComparison.Ordinal);
                    output.AppendLine(native);
                }
                else AppendMethod(output, method);
            }
            output.AppendLine("    }");
        }
        AppendCrypto(output, coreRoot);
        AppendLayout(output, coreRoot);
        AppendLatin1Records(output, coreRoot);
        output.AppendLine("}\n#endif");
        string content = output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (content.Contains("System.Runtime.Intrinsics.", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("A Core intrinsic has no reviewed Burst primitive mapping.");
            return false;
        }
        string path = Path.Combine(directory, "CoreTextAlgorithms.g.cs");
        if (verify)
        {
            bool matches = File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal) == content;
            if (!matches) Console.Error.WriteLine("The Burst text projection is stale; regenerate it from the current Core source.");
            return matches;
        }
        Directory.CreateDirectory(directory);
        if (!File.Exists(path) || File.ReadAllText(path) != content) File.WriteAllText(path, content, new UTF8Encoding(false));
        return true;
    }

    private const string Header = "// <auto-generated/>\n// Projected from Core; change traversal in Core, not in this file.\n#if LUMINPACK_BURST\nusing System;\nusing System.Buffers;\nusing System.Runtime.CompilerServices;\nusing System.Runtime.InteropServices;\nusing Unsafe = LuminPack.Burst.KernelMemory;\nusing BitOperations = LuminPack.Burst.KernelBits;\nusing Utf8 = LuminPack.Burst.PortableUtf8;\nusing Ascii = LuminPack.Burst.PortableAscii;\nnamespace LuminPack.Burst\n{\n";

    /// <summary>Projects compact-record kernels directly from Core without framing or allocation ownership.</summary>
    /// <param name="output">Generated declarations.</param><param name="root">Authoritative Core root.</param>
    private static void AppendLatin1Records(StringBuilder output, string root)
    {
        const string file = "Utility/Text/StringSerializer.Latin1Record.cs";
        AppendOrigin(output, root, file);
        output.AppendLine("internal static unsafe class CoreLatin1Records\n{");
        foreach (var method in Read(root, file, Modern).DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText is "IsLatin1" or "IsLatin1Long" or "TryNarrowLatin1" or "TryNarrowLatin1Long" or "PackLatin1" or "WidenLatin1" or "WidenLatin1Long" or "MatchesLatin1" or "MatchesLatin1Long"))
        {
            var projected = method.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.UnsafeKeyword)));
            output.AppendLine(Rewrite(projected).Replace("string value", "ReadOnlySpan<char> value", StringComparison.Ordinal).Replace("value.AsSpan()", "value", StringComparison.Ordinal));
        }
        output.AppendLine("}");
    }

    /// <summary>Evaluates the exact Core table builder and emits a Burst-supported primitive constant array.</summary>
    /// <param name="output">The generated source.</param><param name="syntax">The authoritative table builder syntax.</param>
    /// <returns>Whether the original builder compiled and its constants were generated.</returns>
    private static bool AppendLatinMasks(StringBuilder output, SyntaxNode syntax)
    {
        var builder = syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "CreateShuffleMasks");
        string source = "using System; using System.Runtime.Intrinsics; public static class MaskFactory { " + builder.ToFullString() + " public static byte[] Flatten() { var vectors = CreateShuffleMasks(); var bytes = new byte[vectors.Length * 16]; for (int i=0; i<vectors.Length; i++) vectors[i].CopyTo(bytes.AsSpan(i*16,16)); return bytes; } }";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("CoreMaskProjection", [CSharpSyntaxTree.ParseText(source, Modern)], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) { Console.Error.WriteLine("Core mask projection failed: " + string.Join("\n", result.Diagnostics)); return false; }
        stream.Position = 0;
        var context = new AssemblyLoadContext("Core mask projection", isCollectible: true);
        byte[] bytes;
        try { bytes = (byte[])context.LoadFromStream(stream).GetType("MaskFactory")!.GetMethod("Flatten")!.Invoke(null, null)!; }
        finally { context.Unload(); }
        output.AppendLine("private static readonly ulong[] ShuffleMaskWords = new ulong[] {");
        for (int offset = 0; offset < bytes.Length; offset += 8)
            output.Append("0x").Append(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8)).ToString("X16")).Append("UL,");
        output.AppendLine("};");
        output.AppendLine("/// <summary>Loads a constant mask evaluated from Core's original table builder.</summary><param name=\"index\">Eight-character classification.</param><returns>The exact original sixteen mask bytes.</returns>\nprivate static global::Unity.Burst.Intrinsics.v128 ReadShuffleMask(uint index) => new global::Unity.Burst.Intrinsics.v128(ShuffleMaskWords[index * 2], ShuffleMaskWords[index * 2 + 1]);");
        return true;
    }

    /// <summary>Projects the existing one-, two- and four-block cipher dispatch and round bodies.</summary>
    /// <param name="output">The generated source.</param><param name="root">The fixed Core source root.</param>
    private static void AppendCrypto(StringBuilder output, string root)
    {
        const string file = "Cryptography/ChaCha20.cs";
        var syntax = Read(root, file, Modern);
        AppendOrigin(output, root, file);
        output.AppendLine("internal static unsafe class CoreChaCha20\n{");
        output.AppendLine("private static bool UseSse2 => Sse2.IsSupported;\nprivate static bool UseAvx2 => Avx2.IsSupported;");
        foreach (var method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            string name = method.Identifier.ValueText;
            if (name is "Initialize" or "LoadKeyStore" or "BlockAvx512x4" || method.ReturnType.ToString().Contains("Vector512", StringComparison.Ordinal) || method.ParameterList.ToString().Contains("Vector512", StringComparison.Ordinal)) continue;
            AppendMethod(output, method);
        }
        output.AppendLine("}");
    }

    /// <summary>Projects Core's layout plan and shuffle prefix with unmanaged descriptors replacing owned managed arrays.</summary>
    /// <param name="output">The generated source.</param><param name="root">The fixed Core source root.</param>
    private static void AppendLayout(StringBuilder output, string root)
    {
        const string file = "Utility/Layout/LuminLayout.cs";
        var syntax = Read(root, file, Modern);
        var type = syntax.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "LuminLayout");
        AppendOrigin(output, root, file);
        output.AppendLine("internal unsafe struct CoreLayout\n{");
        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>().Where(f => !f.Declaration.Variables.Any(v => v.Identifier.ValueText is "_segments" or "_grouped")))
            output.AppendLine(Rewrite(field).Replace("readonly ", "", StringComparison.Ordinal));
        var constructor = type.Members.OfType<ConstructorDeclarationSyntax>().Single();
        string body = Rewrite(constructor.Body!);
        body = body.Replace("LuminLayoutPlan.Create", "CoreLayoutPlan.Create", StringComparison.Ordinal);
        body = body.Replace("var runs = new List<Segment>();", "int segmentCount = 0;", StringComparison.Ordinal)
            .Replace("runs.Add(new Segment(source[start], destination[start], index - start));", "segmentCount++;", StringComparison.Ordinal)
            .Replace("_segments = runs.ToArray();", "", StringComparison.Ordinal)
            .Replace("_grouped = LuminGroupedLayout.Create(source, sourceStride, destination, destinationStride);", "", StringComparison.Ordinal)
            .Replace("_segments.Length", "segmentCount", StringComparison.Ordinal);
        output.AppendLine("/// <summary>Builds Core's shuffle plan over complete native offset descriptors.</summary><param name=\"source\">Source offsets.</param><param name=\"sourceStride\">Input stride.</param><param name=\"destination\">Destination offsets.</param><param name=\"destinationStride\">Output stride.</param><param name=\"allowVectors\">Original vector policy.</param>");
        output.AppendLine("internal void Initialize(ReadOnlySpan<int> source, int sourceStride, ReadOnlySpan<int> destination, int destinationStride, bool allowVectors) " + body);
        output.AppendLine("/// <summary>Maps Core's already validated immutable plan without rebuilding masks.</summary><param name=\"plan\">Pinned complete descriptor.</param>\ninternal void Initialize(in global::LuminPack.Utility.LuminLayoutPlan plan) { _sourceStride = plan.SourceStride; _destinationStride = plan.DestinationStride; _mode = plan.Mode; _perBlock = plan.PerBlock; _reserve = plan.Reserve; _inputVectors = plan.InputVectors; _outputVectors = plan.OutputVectors; _mask0 = Vector128.Create(plan.Mask0Low, plan.Mask0High).AsByte(); _mask1 = Vector128.Create(plan.Mask1Low, plan.Mask1High).AsByte(); _mask2 = Vector128.Create(plan.Mask2Low, plan.Mask2High).AsByte(); _mask3 = Vector128.Create(plan.Mask3Low, plan.Mask3High).AsByte(); }");
        output.AppendLine("/// <summary>Runs only Core's eligible complete-vector prefix.</summary><param name=\"source\">Input.</param><param name=\"destination\">Output.</param><param name=\"count\">Element count.</param><returns>The completed element count.</returns>\ninternal int Prefix(ref byte source, ref byte destination, int count) => _mode != 0 && count >= _reserve ? Blocks(ref source, ref destination, count) : 0;");
        foreach (var method in type.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText is "Shuffle" or "Blocks"))
            output.AppendLine(Rewrite(method).Replace("private ", "internal ", StringComparison.Ordinal).Replace("_mask0, _mask1, _mask2, _mask3)", "_mask0.Value, _mask1.Value, _mask2.Value, _mask3.Value)", StringComparison.Ordinal));
        output.AppendLine("}");
        const string planFile = "Utility/Layout/LuminLayoutPlan.cs";
        AppendOrigin(output, root, planFile);
        var planSyntax = Read(root, planFile, Modern).DescendantNodes().OfType<StructDeclarationSyntax>().Single();
        output.AppendLine(Rewrite(planSyntax).Replace("LuminLayoutPlan", "CoreLayoutPlan", StringComparison.Ordinal));
        const string wideFile = "Utility/Layout/Simd/LuminByteShuffle.cs";
        AppendOrigin(output, root, wideFile);
        output.AppendLine("internal static unsafe class CoreByteShuffle\n{");
        foreach (var method in Read(root, wideFile, Modern).DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText != "Copy512"))
        {
            var mapped = method.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.UnsafeKeyword)));
            string native = Rewrite(mapped).Replace("Vector128<byte>", "global::Unity.Burst.Intrinsics.v128", StringComparison.Ordinal);
            if (method.Identifier.ValueText == "Copy256")
            {
                native = native.Replace("Vector256.Create(", "NativeVectorOps.Join256(", StringComparison.Ordinal).Replace("Vector128.LoadUnsafe(", "NativeVectorOps.Load128(", StringComparison.Ordinal).Replace("Avx2.Shuffle(", "NativeVectorOps.Shuffle256(", StringComparison.Ordinal);
                native = Regex.Replace(native, @"(\w+) \|= ([^;]+);", "$1 = NativeVectorOps.Or256($1, $2);");
                native = Regex.Replace(native, @"(\w+)\.GetLower\(\)\.StoreUnsafe\(ref output, ([^;]+)\);", "NativeVectorOps.Store128($1.Lo128, ref output, $2);");
                native = Regex.Replace(native, @"(\w+)\.GetUpper\(\)\.StoreUnsafe\(ref output, ([^;]+)\);", "NativeVectorOps.Store128($1.Hi128, ref output, $2);");
            }
            output.AppendLine(native);
        }
        output.AppendLine("}");
    }

    /// <summary>Reads a selected conditional Core syntax tree.</summary>
    /// <param name="root">The Core source root.</param><param name="file">The relative source path.</param><param name="options">The desired original target branch.</param>
    /// <returns>The selected syntax.</returns>
    private static SyntaxNode Read(string root, string file, CSharpParseOptions options) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, file)), options).GetRoot();

    /// <summary>Records the exact originating source content.</summary>
    /// <param name="output">The generated source.</param><param name="root">The source root.</param><param name="file">The Core relative file.</param>
    private static void AppendOrigin(StringBuilder output, string root, string file) => output.Append("// Core source SHA256: ").Append(file).Append(' ').Append(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, file))))).Append('\n');

    /// <summary>Projects one unchanged Core method body with primitive substitutions.</summary>
    /// <param name="output">The generated source.</param><param name="method">The original method.</param>
    private static void AppendMethod(StringBuilder output, MethodDeclarationSyntax method)
    {
        method = method.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.UnsafeKeyword)));
        output.AppendLine(Rewrite(method));
    }

    /// <summary>Maps representation-only native primitives and removes unsupported 512-bit branches.</summary>
    /// <param name="node">The selected Core declaration.</param><returns>The mapped source.</returns>
    private static string Rewrite(SyntaxNode node)
    {
        string text = new ProjectionRewriter().Visit(node)!.NormalizeWhitespace().ToFullString();
        return text.Replace("global::LuminPack.Internal.ShortAsciiTranscoder", "CoreShortAscii", StringComparison.Ordinal)
            .Replace("global::LuminPack.Internal.Utf8Latin1Transcoder", "CoreLatinUtf8", StringComparison.Ordinal)
            .Replace("Utf8TwoByteComparer", "CoreTwoByteComparer", StringComparison.Ordinal)
            .Replace("StringSerializer.", "CoreTextAlgorithms.", StringComparison.Ordinal)
            .Replace("LuminByteShuffle.", "CoreByteShuffle.", StringComparison.Ordinal)
            .Replace("masks.Fill(0xFF)", "KernelMemory.InitBlockUnaligned(ref MemoryMarshal.GetReference(masks), 0xFF, (uint)masks.Length)", StringComparison.Ordinal)
            .Replace("string text", "ReadOnlySpan<char> text", StringComparison.Ordinal)
            .Replace("text.AsSpan()", "text", StringComparison.Ordinal)
            .Replace("LuminPackReader.Read<", "KernelMemory.ReadUnaligned<", StringComparison.Ordinal)
            .Replace("global::LuminPack.Core.KernelMemory.", "KernelMemory.", StringComparison.Ordinal)
            .Replace("LuminPackWriter.Write(", "KernelMemory.WriteUnaligned(", StringComparison.Ordinal)
            .Replace("decoded.Slice(0, written).SequenceEqual(text.Slice(0, written))", "KernelMemory.SequenceEqual<char>(decoded.Slice(0, written), text.Slice(0, written))", StringComparison.Ordinal)
            .Replace("decoded.Slice(0, written).SequenceEqual(text)", "KernelMemory.SequenceEqual<char>(decoded.Slice(0, written), text)", StringComparison.Ordinal)
            .Replace("Deserialize<LuminPackUtf8LengthOptions>", "Utf8.ToUtf16", StringComparison.Ordinal)
            .Replace("Unsafe.CopyBlockUnaligned", "KernelMemory.CopyBlockUnaligned", StringComparison.Ordinal);
    }

    /// <summary>Maps only vector representations and primitives inside Core's unchanged mixed Latin-1 traversal.</summary>
    private sealed class NativeLatin1Primitives : CSharpSyntaxRewriter
    {
        private int _width = 128;
        /// <inheritdoc/>
        public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
        {
            int before = _width;
            if (node.Condition.ToString() == "Avx2.IsSupported") _width = 256;
            if (node.Condition.ToString() == "Ssse3.IsSupported") _width = 128;
            var result = base.VisitIfStatement(node);
            _width = before;
            return result;
        }
        /// <inheritdoc/>
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Name.Identifier.ValueText == "Zero" && node.Expression is GenericNameSyntax name && name.Identifier.ValueText.StartsWith("Vector", StringComparison.Ordinal))
                return SyntaxFactory.ParseExpression("default(global::Unity.Burst.Intrinsics.v" + _width + ")");
            return base.VisitMemberAccessExpression(node);
        }
        /// <inheritdoc/>
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is not MemberAccessExpressionSyntax access) return base.VisitInvocationExpression(node);
            string owner = access.Expression.ToString(), name = access.Name.Identifier.ValueText;
            if (name == "AsByte") return Visit(access.Expression);
            if (name is "GetLower" or "GetUpper") return SyntaxFactory.ParseExpression(Visit(access.Expression) + (name == "GetLower" ? ".Lo128" : ".Hi128"));
            string? primitive = owner is "Vector128" or "Vector256" ? name switch
            {
                "Load" => "Load" + _width,
                "Create" when node.ArgumentList.Arguments.Count == 2 => "Join256",
                "Create" => "Broadcast16_" + _width,
                "EqualsAll" => "EqualsAll" + _width,
                "Equals" => "Equals16_" + _width,
                "ExtractMostSignificantBits" => "WordSigns" + _width,
                "ConditionalSelect" => "Select" + _width,
                _ => null
            } : owner is "Avx2" or "Ssse3" && name == "Shuffle" ? "Shuffle" + _width : null;
            if (primitive is null) return base.VisitInvocationExpression(node);
            return node.WithExpression(SyntaxFactory.ParseExpression("NativeVectorOps." + primitive)).WithArgumentList((ArgumentListSyntax)Visit(node.ArgumentList)!);
        }
        /// <inheritdoc/>
        public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (!node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(n => n.Identifier.ValueText == "chars")) return base.VisitBinaryExpression(node);
            string? primitive = node.Kind() switch { SyntaxKind.BitwiseAndExpression => "And", SyntaxKind.BitwiseOrExpression => "Or", SyntaxKind.LeftShiftExpression => "Left16_", SyntaxKind.RightShiftExpression => "Right16_", _ => null };
            if (primitive is null) return base.VisitBinaryExpression(node);
            return SyntaxFactory.ParseExpression("NativeVectorOps." + primitive + _width + "(" + Visit(node.Left) + ", " + Visit(node.Right) + ")");
        }
    }

    /// <summary>Keeps original active methods while selecting Burst's supported 128/256-bit fallback branches.</summary>
    private sealed class ProjectionRewriter : CSharpSyntaxRewriter
    {
        /// <inheritdoc/>
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.ToString() == "global::System.Runtime.Intrinsics.X86.Avx2")
                return SyntaxFactory.IdentifierName("Avx2").WithTriviaFrom(node);
            return base.VisitMemberAccessExpression(node);
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax name } && name.Identifier.ValueText == "ReadUnaligned" && name.TypeArgumentList.Arguments.SingleOrDefault() is GenericNameSyntax vector && vector.Identifier.ValueText is "Vector128" or "Vector256")
                return node.WithExpression(SyntaxFactory.ParseExpression("KernelVectorMemory.Read" + vector.Identifier.ValueText[6..] + vector.TypeArgumentList));
            return base.VisitInvocationExpression(node);
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (node.Expression.ToString() == "ShuffleMasks")
                return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("ReadShuffleMask"), SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(node.ArgumentList.Arguments[0])));
            return base.VisitElementAccessExpression(node);
        }
        /// <inheritdoc/>
        public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
        {
            var attributes = node.Attributes.Where(a => a.Name.ToString() != "SkipLocalsInit");
            return attributes.Any() ? base.VisitAttributeList(node.WithAttributes(SyntaxFactory.SeparatedList(attributes))) : null;
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
        {
            if (node.Condition.ToString().Contains("Avx512", StringComparison.Ordinal)) return node.Else is null ? SyntaxFactory.EmptyStatement() : Visit(node.Else.Statement);
            return base.VisitIfStatement(node);
        }

        /// <inheritdoc/>
        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia) => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia) ? default : base.VisitTrivia(trivia);
    }
}
