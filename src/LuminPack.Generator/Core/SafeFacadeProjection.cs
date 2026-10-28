using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Checks selected input sizes before facade staging, transcoding or initial token scanning.</summary>
internal sealed class SafeFacadeProjection : CSharpSyntaxRewriter
{
    private readonly CompiledOptions _options;
    private readonly bool _generic;
    private string _limit;

    /// <summary>Captures one concrete profile or the generated generic type selector.</summary>
    /// <param name="options">The concrete declaration policy.</param>
    /// <param name="generic">Whether the facade fragment owns generic roots.</param>
    internal SafeFacadeProjection(CompiledOptions options, bool generic) { _options = options; _generic = generic; }

    /// <summary>Finds explicit input policy declarations without changing disabled facade fragments.</summary>
    /// <param name="compilation">The complete consumer compilation.</param>
    /// <returns>Whether a policy can affect this compilation's generic input facades.</returns>
    internal static bool HasPolicies(Compilation compilation)
    {
        if (CompiledOptions.Resolve(null, compilation).Input.Enabled) return true;
        foreach (var tree in compilation.SyntaxTrees)
        foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().Where(d => d.AttributeLists.Count != 0))
            if (CompiledOptions.Resolve(compilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration), compilation).Input.Enabled) return true;
        return false;
    }

    /// <summary>Derives an input cap that also bounds facade-owned staging allocations.</summary>
    /// <param name="options">The root's resource constants.</param>
    /// <returns>A conservative encoded byte cap.</returns>
    internal static int Limit(CompiledOptions options) => (int)Math.Min(options.Input.Input, Math.Min(int.MaxValue, Math.Max(0L, options.Input.Allocation - 4096) / 16));

    /// <summary>Adds checks only to existing input operations selected by a concrete type or generic selector.</summary>
    /// <param name="node">The existing facade method.</param>
    /// <returns>The same method with bounded input ingestion.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Body == null || !node.Identifier.ValueText.StartsWith("Deserialize", StringComparison.Ordinal) || !_generic && !_options.Input.Enabled) return node;
        string previous = _limit;
        _limit = _generic && node.TypeParameterList?.Parameters.Any(p => p.Identifier.ValueText == "T") == true ?
            "global::LuminPack.LuminPackSerializer.FormatterDispatch.__LuminPackInputLimit<T>()" : Limit(_options).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string guard = "";
        foreach (var parameter in node.ParameterList.Parameters)
        {
            string name = parameter.Identifier.Text, type = parameter.Type?.ToString() ?? "";
            if (type == "string" || type.Contains("Span<char>"))
                guard += "global::LuminPack.Security.LuminPackInputPolicy.CheckCharacters(" + name + (type == "string" ? ".AsSpan()" : "") + ", " +
                    (_generic ? "(int)global::LuminPack.LuminPackSerializer.FormatterDispatch.__LuminPackJsonEncoding<T>()" : _options.Encoding.ToString()) + ", " + _limit + ");";
            else if (type.Contains("Span<byte>") || type.Contains("ReadOnlySequence<byte>"))
                guard += "global::LuminPack.Security.LuminPackInputPolicy.CheckInput(" + name + ".Length, " + _limit + ");";
            else if (type.EndsWith("LuminBufferWriter", StringComparison.Ordinal))
                guard += "global::LuminPack.Security.LuminPackInputPolicy.CheckInput(" + name + ".GetSpan().Length, " + _limit + ");";
        }
        var rewritten = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
        if (guard.Length != 0) rewritten = rewritten.WithBody(rewritten.Body.WithStatements(rewritten.Body.Statements.InsertRange(0, SyntaxFactory.ParseStatement("{" + guard + "}") is BlockSyntax block ? block.Statements : default)));
        _limit = previous;
        return rewritten;
    }

    /// <summary>Bounds stream reads before the temporary stream can grow beyond the declaration's cap.</summary>
    /// <param name="node">An existing asynchronous stream copy.</param>
    /// <returns>A bounded copy for selected contracts.</returns>
    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (_limit != null && node.Expression is AwaitExpressionSyntax && node.ToString().Contains("stream.CopyToAsync(data, cancellationToken)"))
            return SyntaxFactory.ParseStatement("if (" + _limit + " == int.MaxValue) { " + node + " } else { await global::LuminPack.Security.LuminPackInputPolicy.CopyInputAsync(stream, data, " + _limit + ", cancellationToken).ConfigureAwait(false); }");
        return base.VisitExpressionStatement(node);
    }
}
