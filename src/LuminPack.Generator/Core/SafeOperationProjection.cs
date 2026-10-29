using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Frames external roots around their original formatters and selects streaming read guards.</summary>
internal sealed class SafeOperationProjection : CSharpSyntaxRewriter
{
    internal const string Policy = "global::LuminPack.Security.LuminPackInputPolicy";
    internal const string Schema = "__inputSchema";
    private readonly string _profile;
    private readonly bool _boundary;
    private readonly SafeReadProjection _reads;

    /// <summary>Captures the fixed profile and typed read projection.</summary>
    /// <param name="profile">The declared profile.</param><param name="boundary">Whether framing is enabled.</param>
    /// <param name="reads">The typed instrumentation.</param>
    private SafeOperationProjection(string profile, bool boundary, SafeReadProjection reads)
    { _profile = profile; _boundary = boundary; _reads = reads; }

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
        var rewritten = (ClassDeclarationSyntax)new SafeOperationProjection(options.TypeName, options.Input.Enabled, reads).Visit(container);
        var field = SyntaxFactory.ParseMemberDeclaration("private static readonly global::LuminPack.Security.LuminPackSafeSchema " + Schema + " = " + SafeSchemaBuilder.Build(type, compilation, metadata, options) + ";");
        return root.ReplaceNode(container, rewritten.AddMembers(field)).NormalizeWhitespace().ToFullString();
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
        string original = "static void __safeOriginalOperation(" + helperParameters + ") " + (read ? _reads.Rewrite(node, json) : node.Body);
        string check = Policy + ".CheckInputProfile<" + _profile + ", " + _profile + ">();";
        string text = "{ var __safeState = " + state + ".OptionState; var __safeOperation = " + Policy + ".BeginOperation(__safeState, " + Schema + ", " + (read ? "true" : "false") + ");" +
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
        return node.WithBody((BlockSyntax)SyntaxFactory.ParseStatement(text + original + "}"));
    }
}
