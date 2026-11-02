using System;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Adds untrusted-input checks to the original binary read templates of an untrusted graph.</summary>
/// <remarks>
/// Untrusted records use the ordinary wire format and the ordinary writers. Reads keep their original bodies; checks are
/// inserted at the actual read sites with the root's limits frozen as constants. Unsupported shapes fail closed.
/// </remarks>
internal sealed class SafeOperationProjection : CSharpSyntaxRewriter
{
    internal const string Policy = "global::LuminPack.Security.LuminPackInputPolicy";
    private readonly SafeReadProjection _reads;
    private readonly string _rejection;

    /// <summary>Captures the typed read projection and the graph's support decision.</summary>
    /// <param name="reads">The typed read instrumentation.</param>
    /// <param name="rejection">The first unsupported shape, or null when the graph is accepted.</param>
    private SafeOperationProjection(SafeReadProjection reads, string rejection) { _reads = reads; _rejection = rejection; }

    /// <summary>Adds checks to the binary reads of one type's templates.</summary>
    /// <param name="source">The formatter template.</param><param name="type">The closed type.</param>
    /// <param name="compilation">The consumer compilation.</param><param name="metadata">Generation settings.</param>
    /// <param name="input">The enclosing untrusted root's frozen limits.</param>
    /// <returns>The checked operation source.</returns>
    internal static string Rewrite(string source, ITypeSymbol type, Compilation compilation, MetaInfo metadata, CompiledInputPolicy input)
    {
        var active = new ActiveOperationSyntax().Visit(CSharpSyntaxTree.ParseText(source, metadata.ParseOptions).GetRoot());
        var root = CSharpSyntaxTree.Create((CompilationUnitSyntax)active, metadata.ParseOptions).GetRoot();
        var container = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.ValueText.StartsWith("LuminPackExtensions_", StringComparison.Ordinal));
        if (container == null) return source;
        var reads = new SafeReadProjection(type, compilation, metadata, input, compilation.AddSyntaxTrees(root.SyntaxTree).GetSemanticModel(root.SyntaxTree));
        // Input-selected shapes fail closed in both directions only under an explicit hostile-input declaration; the implicit
        // default keeps them working with checked reads.
        string rejection = input.Explicit ? SafeContract.Rejection(type, compilation, metadata) : null;
        var rewritten = (ClassDeclarationSyntax)new SafeOperationProjection(reads, rejection).Visit(container);
        return root.ReplaceNode(container, rewritten).NormalizeWhitespace().ToFullString();
    }

    /// <summary>Retains one formatter body with its existing state, callbacks, construction and reuse.</summary>
    /// <param name="node">The original operation.</param>
    /// <returns>The same operation with checked reads, or a fail-closed body.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Body == null || node.ParameterList.Parameters.Count != 2 || !BinaryOperationPlan.IsOperation(node.Identifier.ValueText)) return node;
        string stateType = node.ParameterList.Parameters[0].Type.ToString();
        bool json = stateType.Contains("Json"), read = stateType.Contains("Reader"), write = stateType.Contains("Writer");
        // Size evaluation reads no input.
        if (!read && !write) return node;
        if (_rejection != null)
            return node.WithBody((BlockSyntax)SyntaxFactory.ParseStatement("{ " + Policy + ".RejectUnsupported(" +
                SymbolDisplay.FormatLiteral(_rejection, true) + "); }"));
        // Writers are trusted code, and the strict JSON profile bounds documents; only binary reads need checks.
        return read && !json ? node.WithBody(_reads.Rewrite(node)) : node;
    }
}
