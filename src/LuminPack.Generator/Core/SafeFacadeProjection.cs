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

    /// <summary>Gets the encoded input cap, which also bounds facade-owned staging allocations.</summary>
    /// <param name="options">The root's frozen limits.</param>
    /// <returns>The encoded byte cap.</returns>
    internal static int Limit(CompiledOptions options) => options.Input.Input;

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
        // A forwarding overload leaves the check to the overload that reads.
        bool forwarding = node.ExpressionBody?.Expression is InvocationExpressionSyntax ||
            node.Body?.Statements.Count == 1 && node.Body.Statements[0] is ReturnStatementSyntax { Expression: InvocationExpressionSyntax forward } &&
            forward.Expression.ToString().Contains("Deserialize");
        foreach (var parameter in forwarding ? Enumerable.Empty<ParameterSyntax>() : node.ParameterList.Parameters)
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
        if (!_generic && node.Identifier.ValueText.StartsWith("DeserializeJson", StringComparison.Ordinal) && !ReferenceEquals(_options.JsonInput, _options))
            rewritten = (MethodDeclarationSyntax)new JsonProfile(_options.TypeName, _options.JsonInput.TypeName).Visit(rewritten);
        if (guard.Length != 0) rewritten = rewritten.WithBody(rewritten.Body.WithStatements(rewritten.Body.Statements.InsertRange(0, SyntaxFactory.ParseStatement("{" + guard + "}") is BlockSyntax block ? block.Statements : default)));
        _limit = previous;
        return rewritten;
    }

    /// <summary>Selects the strict untrusted JSON profile for a concrete facade's document reads.</summary>
    private sealed class JsonProfile : CSharpSyntaxRewriter
    {
        private readonly string _declared, _strict;
        /// <summary>Captures both profile spellings.</summary><param name="declared">The binary profile.</param><param name="strict">The JSON profile.</param>
        internal JsonProfile(string declared, string strict) { _declared = declared; _strict = strict; }
        /// <summary>Replaces the declared profile argument.</summary><param name="node">A type argument list.</param><returns>The selected list.</returns>
        public override SyntaxNode VisitTypeArgumentList(TypeArgumentListSyntax node)
            => node.WithArguments(SyntaxFactory.SeparatedList(node.Arguments.Select(argument =>
                argument.ToString() == _declared ? SyntaxFactory.ParseTypeName(_strict).WithTriviaFrom(argument) : (TypeSyntax)Visit(argument)),
                node.Arguments.GetSeparators()));
    }

    /// <summary>Bounds pooled stream buffering with the declaration's cap, reading at most one byte beyond it.</summary>
    /// <param name="node">An existing call.</param>
    /// <returns>The bounded stream read for selected contracts.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
        if (_limit != null && node.Expression.ToString().EndsWith("LuminPackStreamInput.ReadAsync", StringComparison.Ordinal) && result.ArgumentList.Arguments.Count == 3)
            return result.WithArgumentList(result.ArgumentList.WithArguments(result.ArgumentList.Arguments.Replace(result.ArgumentList.Arguments[1],
                SyntaxFactory.Argument(SyntaxFactory.ParseExpression(_limit)))));
        return result;
    }
}
