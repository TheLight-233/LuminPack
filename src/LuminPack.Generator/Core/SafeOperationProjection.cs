using System;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Frames external roots around their original formatters and selects streaming read guards.</summary>
internal sealed class SafeOperationProjection : CSharpSyntaxRewriter
{
    internal const string Policy = "global::LuminPack.Security.LuminPackInputPolicy";
    internal const string Schema = "__inputSchema";
    internal const string FixedProofParameter = "__inputProven";
    private readonly string _profile;
    private readonly bool _boundary;
    private readonly SafeReadProjection _reads;
    private readonly FixedInput _fixed;

    /// <summary>Holds symbol-free scalar and string facts for one supported positional operation.</summary>
    private sealed class FixedInput
    {
        internal int Width = 1, Members;
        internal bool HasStrings;
        internal readonly System.Collections.Generic.List<int> Booleans = new System.Collections.Generic.List<int>();
        internal readonly System.Collections.Generic.List<string> Guards = new System.Collections.Generic.List<string>();
    }

    /// <summary>Captures the fixed profile and typed read projection.</summary>
    /// <param name="profile">The declared profile.</param><param name="boundary">Whether framing is enabled.</param>
    /// <param name="reads">The typed instrumentation.</param>
    /// <param name="fixedInput">The callback-free scalar plan, or null when the original accounting path is required.</param>
    private SafeOperationProjection(string profile, bool boundary, SafeReadProjection reads, FixedInput fixedInput)
    { _profile = profile; _boundary = boundary; _reads = reads; _fixed = fixedInput; }

    /// <summary>Preserves disabled templates and adds guards to selected streaming plans.</summary>
    /// <param name="source">The formatter template.</param><param name="type">The closed type.</param>
    /// <param name="compilation">The consumer compilation.</param><param name="metadata">Generation settings.</param>
    /// <param name="streaming">Whether an enclosing external root requires nested guards.</param>
    /// <returns>The original or guarded operation source.</returns>
    internal static string Rewrite(string source, ITypeSymbol type, Compilation compilation, MetaInfo metadata, bool streaming = false)
    {
        var options = CompiledOptions.Resolve(type, compilation);
        if (!options.Input.Enabled && !streaming) return source;
        var active = new ActiveOperationSyntax().Visit(CSharpSyntaxTree.ParseText(source, metadata.ParseOptions).GetRoot());
        var root = CSharpSyntaxTree.Create((CompilationUnitSyntax)active, metadata.ParseOptions).GetRoot();
        var container = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.ValueText.StartsWith("LuminPackExtensions_", StringComparison.Ordinal));
        if (container == null) return source;
        var reads = new SafeReadProjection(type, compilation, metadata, options, compilation.AddSyntaxTrees(root.SyntaxTree).GetSemanticModel(root.SyntaxTree));
        var rewritten = (ClassDeclarationSyntax)new SafeOperationProjection(options.TypeName, options.Input.Enabled, reads,
            CreateFixedInput(type, compilation, metadata, options)).Visit(container);
        var field = SyntaxFactory.ParseMemberDeclaration("private static readonly global::LuminPack.Security.LuminPackSafeSchema " + Schema + " = " + SafeSchemaBuilder.Build(type, compilation, metadata, options) + ";");
        return root.ReplaceNode(container, rewritten.AddMembers(field)).NormalizeWhitespace().ToFullString();
    }

    /// <summary>Selects callback-free scalar/string classes without changing nested, pooled or specialized contracts.</summary>
    /// <param name="type">The current operation type.</param>
    /// <param name="compilation">The actual consumer references.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <param name="options">The root's compiled profile.</param>
    /// <returns>The scalar plan, or null for an unsupported runtime or object contract.</returns>
    private static FixedInput CreateFixedInput(ITypeSymbol type, Compilation compilation, MetaInfo metadata, CompiledOptions options)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false } named ||
            !CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(named) ||
            compilation.GetTypeByMetadataName("LuminPack.Security.LuminPackInputPolicy")?.GetMembers("TryOpenFixedFrame").OfType<IMethodSymbol>()
                .Any(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 6 &&
                    method.ReturnType.SpecialType == SpecialType.System_Boolean) != true) return null;
        var data = LuminPackSourceGenerator.GetSerializationMetadata(named, compilation, metadata);
        bool supported = !data.isUnion && data.generatorType == LuminPack.Code.GeneratorType.Object && data.RentPoolMethod == null &&
            data.callBackMethods.Count == 0 && data.fields.All(field => !field.IsCompress && !field.IsVolatile &&
                (field.TypeSymbol?.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
                    SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
                    SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Char or SpecialType.System_String ||
                    field.TypeSymbol?.ToDisplayString() is "System.Guid" or "System.TimeSpan"));
        if (!supported) return null;
        var result = new FixedInput { Members = data.fields.Count };
        foreach (var field in data.fields)
        {
            if (field.TypeSymbol.SpecialType == SpecialType.System_String)
            {
                result.HasStrings = true;
                result.Guards.Add("if (!" + Policy + ".TrySkipProvenString(__proofData, ref __proofPosition, " +
                    (field.Options ?? options).Mode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")) return false;");
                continue;
            }
            if (field.TypeSymbol.SpecialType == SpecialType.System_Boolean) result.Booleans.Add(result.Width);
            int width = field.TypeSymbol.SpecialType switch
            {
                SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => 1,
                SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Char => 2,
                SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
                SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => 8,
                _ => field.TypeSymbol.ToDisplayString() == "System.Guid" ? 16 : 8
            };
            result.Guards.Add("if (__proofData.Length - __proofPosition < " + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + ") return false;");
            if (field.TypeSymbol.SpecialType == SpecialType.System_Boolean)
                result.Guards.Add("if (__proofData[__proofPosition] > 1) return false;");
            result.Guards.Add("__proofPosition += " + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";");
            result.Width += width;
        }
        if (result.HasStrings && (data.SelectedConstructor?.Parameters.Count > 0 ||
            compilation.GetTypeByMetadataName("LuminPack.Security.LuminPackInputPolicy")?.GetMembers("TryOpenProvenFrame").Any() != true ||
            compilation.GetTypeByMetadataName("LuminPack.Security.LuminPackInputPolicy")?.GetMembers("TrySkipProvenString").Any() != true)) return null;
        return result;
    }

    /// <summary>Retains one formatter body with its existing state, callbacks, construction and reuse.</summary>
    /// <param name="node">The original operation.</param>
    /// <returns>The same operation with selected framing and checks.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Body == null || node.ParameterList.Parameters.Count != 2 || !BinaryOperationPlan.IsOperation(node.Identifier.ValueText)) return node;
        var parameters = node.ParameterList.Parameters;
        string state = parameters[0].Identifier.Text, stateType = parameters[0].Type.ToString();
        bool json = stateType.Contains("Json"), read = stateType.Contains("Reader"), write = stateType.Contains("Writer");
        // The evaluator state no longer owns option or policy state, so internal width operations keep their original body.
        if (!read && !write) return node;
        if (!_boundary) return read ? node.WithBody(_reads.Rewrite(node, json)) : node;
        if (node.Identifier.ValueText is "ReadValueWithCompress" or "WriteValueWithCompress")
            return node.WithBody((BlockSyntax)SyntaxFactory.ParseStatement("{ " + Policy + ".RejectLegacyInputCompression(); }"));
        string helperParameters = string.Join(", ", parameters.Select(p => p.WithModifiers(SyntaxFactory.TokenList(p.Modifiers.Where(m => !m.IsKind(SyntaxKind.ThisKeyword)))).ToString()));
        string payload = (parameters[1].Modifiers.Any(SyntaxKind.InKeyword) ? "in " : parameters[1].Modifiers.Any(SyntaxKind.RefKeyword) ? "ref " : "") + parameters[1].Identifier.Text;
        bool fixedInput = _fixed != null && read && !json && node.Identifier.ValueText == "ReadValue";
        string original = "static void __safeOriginalOperation(" + helperParameters +
            (fixedInput ? ", bool " + FixedProofParameter + " = false" : "") + ") " +
            (read ? _reads.Rewrite(node, json, fixedInput ? FixedProofParameter : null, fixedInput ? _fixed.Members : -1) : node.Body);
        string check = Policy + ".CheckInputProfile<" + _profile + ", " + _profile + ">();";
        string text = "{ ";
        string admission = _fixed?.HasStrings == true
            ? "__safeTryOpenPayload(" + state + ".Buffer.Slice(" + state + ".Position), out var __fixedPayload, out int __fixedConsumed)"
            : Policy + ".TryOpenFixedFrame(" + state + ".Buffer.Slice(" + state + ".Position), " + Schema + ", " +
                (_fixed?.Width ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " +
                (_fixed?.Members ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture) + ", out var __fixedPayload, out int __fixedConsumed)";
        if (fixedInput)
            text += check + "if (" + admission + (_fixed.HasStrings || _fixed.Booleans.Count == 0 ? "" : " && (__fixedPayload[0] == 255 || (" +
                    string.Join(" && ", _fixed.Booleans.Select(offset => "__fixedPayload[" + offset.ToString(System.Globalization.CultureInfo.InvariantCulture) + "] <= 1")) + "))") +
                ") { if (__fixedPayload[0] != 255) { __fixedPayload = __fixedPayload.Slice(1" +
                (_fixed.HasStrings ? "" : ", " + (_fixed.Width - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)) + "); var __fixedReader = new " + stateType +
                "(ref __fixedPayload); __safeOriginalOperation(ref __fixedReader, " + payload + ", true);" +
                "if (__fixedReader.Position != __fixedPayload.Length) " + Policy + ".RejectIncompleteValue(); } " + state + ".Advance(__fixedConsumed); return; }" +
                "__safeFixedFallback(ref " + state + ", " + payload + ");" +
                "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] " +
                "static void __safeFixedFallback(" + helperParameters + ") { ";
        text += "var __safeState = " + state + ".OptionState; var __safeOperation = " + Policy + ".BeginOperation(__safeState, " + Schema + ", " + (read ? "true" : "false") + ");" +
            "if (!__safeOperation.OwnsBoundary) { __safeOriginalOperation(ref " + state + ", " + payload + "); return; }";
        if (read)
        {
            text += "bool __safeDone = false; global::System.Runtime.InteropServices.GCHandle __safePin = default; int __safeConsumed = 0;";
            if (json) text += "int __safeJsonStart = " + state + ".Position - (" + Schema + ".DocumentEncoding == 0 ? 1 : 2); int __safeJsonDepth = " + state + ".Depth - 1;";
            text += "try { " + check;
            if (json) text += "global::System.ReadOnlySpan<byte> __safePayload = " + Policy + ".OpenStreamingJson(" + state + ".Buffer.Slice(__safeJsonStart), " + Schema + ");";
            else text += "global::System.ReadOnlySpan<byte> __safePayload = " + Policy + ".OpenStreaming(" + state + ".Buffer.Slice(" + state + ".Position), " + Schema + ", out __safeConsumed, out byte[] __safeDecoded);" +
                "if (__safeDecoded != null) __safePin = global::System.Runtime.InteropServices.GCHandle.Alloc(__safeDecoded, global::System.Runtime.InteropServices.GCHandleType.Pinned);";
            text += "var __safeValueState = new " + stateType + "(ref __safePayload, __safeState);";
            if (json) text += Policy + ".PrimeJson(ref __safeValueState, " + Schema + ");";
            text += "__safeOriginalOperation(ref __safeValueState, " + payload + ");";
            if (json) text += "__safeConsumed = " + Policy + ".CompleteStreamingJson(__safeValueState.Buffer, __safeValueState.Position, " + Schema + ");" +
                "if (__safeJsonDepth == 0) " + Policy + ".EnsureJsonFrameEnd(" + state + ".Buffer.Slice(__safeJsonStart + __safeConsumed), " + Schema + ");";
            else text += "if (__safeValueState.Position != __safePayload.Length) " + Policy + ".RejectIncompleteValue();";
            text += "__safeDone = true; } finally { try { if (__safeDone) { ";
            text += json ? state + ".Advance(__safeJsonStart + __safeConsumed - " + state + ".Position); " + state + ".Depth = __safeJsonDepth; " + state +
                ".GrammarState = global::LuminPack.Core.LuminPackJsonReader.JsonStateAfterValue; if (__safeJsonDepth < 64) " + state +
                ".ObjectContainerBits &= __safeJsonDepth == 0 ? 0UL : ulong.MaxValue >> (64 - __safeJsonDepth); " + state + ".CompleteValue();" : state + ".Advance(__safeConsumed);";
            text += "} } finally { if (__safePin.IsAllocated) __safePin.Free(); " + Policy + ".EndOperation(__safeOperation); } }";
        }
        else text += "try { " + check + "var __safeStart = " + Policy + (json ? ".BeginWriteJson(" : ".BeginWrite(") + "ref " + state + ", " + Schema + ");" +
            "__safeOriginalOperation(ref " + state + ", " + payload + "); " + Policy + (json ? ".EndWriteJson(" : ".EndWrite(") + "ref " + state + ", " + Schema + ", __safeStart);" +
            "} finally { " + Policy + ".EndOperation(__safeOperation); }";
        string validation = fixedInput && _fixed.HasStrings
            ? "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] " +
              "static bool __safeTryOpenPayload(global::System.ReadOnlySpan<byte> __proofInput, out global::System.ReadOnlySpan<byte> __proofPayload, out int __proofConsumed) { " +
              "return " + Policy + ".TryOpenProvenFrame(__proofInput, " + Schema + ", " + _fixed.Members.ToString(System.Globalization.CultureInfo.InvariantCulture) +
              ", out __proofPayload, out __proofConsumed) && __safeValidatePayload(__proofPayload); } " +
              "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)] " +
              "static bool __safeValidatePayload(global::System.ReadOnlySpan<byte> __proofData) { if (__proofData[0] == 255) return true; int __proofPosition = 1; " +
              string.Join(" ", _fixed.Guards) + " return __proofPosition == __proofData.Length; }" : "";
        return node.WithBody((BlockSyntax)SyntaxFactory.ParseStatement(text + (fixedInput ? "}" : "") + original + validation + "}"));
    }
}
