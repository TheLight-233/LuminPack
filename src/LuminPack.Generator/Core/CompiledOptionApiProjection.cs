using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Removes runtime configuration arguments while preserving operation-state ownership and cleanup.</summary>
internal sealed class CompiledOptionApiProjection : CSharpSyntaxRewriter
{
    private readonly CompiledOptions _options;
    private HashSet<string> _parameters = new HashSet<string>(StringComparer.Ordinal);
    private bool _genericJson;

    /// <summary>Captures the declaration-time root policy for one facade fragment.</summary>
    /// <param name="options">The complete root profile.</param>
    internal CompiledOptionApiProjection(CompiledOptions options) => _options = options;

    /// <summary>Removes a runtime option parameter from generated declarations and specializes document support calls.</summary>
    /// <param name="node">An existing generated facade method.</param>
    /// <returns>The same overload shape without its former runtime configuration argument.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var previous = _parameters;
        bool previousJson = _genericJson;
        _genericJson = node.Identifier.ValueText.Contains("Json") && node.TypeParameterList?.Parameters.Any(parameter => parameter.Identifier.ValueText == "T") == true;
        _parameters = new HashSet<string>(node.ParameterList.Parameters.Where(parameter =>
            parameter.Type?.ToString().Contains("LuminPackSerializerOption") == true).Select(parameter => parameter.Identifier.ValueText), StringComparer.Ordinal);
        var rewritten = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node.WithParameterList(node.ParameterList.WithParameters(
            SyntaxFactory.SeparatedList(node.ParameterList.Parameters.Where(parameter => !_parameters.Contains(parameter.Identifier.ValueText))))));
        if (rewritten.Body != null)
        {
            if (node.Identifier.ValueText == "DeserializeJsonCore" && _genericJson)
                rewritten = rewritten.WithBody((BlockSyntax)SyntaxFactory.ParseStatement("{ var reader = new LuminPackJsonReader(ref buffer, state); if (!FormatterDispatch.__LuminPackJsonReadBoundary<T>(ref reader, false)) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"JSON input does not contain a value\"); global::LuminPack.LuminPackSerializer.DeserializeJson(ref reader, ref value); FormatterDispatch.__LuminPackJsonReadBoundary<T>(ref reader, true); return reader.CurrentIndex; }"));
            bool json = node.Identifier.ValueText.Contains("Json");
            rewritten = rewritten.WithBody((BlockSyntax)new CompiledOptionProjection(_options, "writer", json ? CompiledOptionProjection.JsonWriterMethods : CompiledOptionProjection.WriterMethods).Visit(rewritten.Body));
            rewritten = rewritten.WithBody((BlockSyntax)new CompiledOptionProjection(_options, "reader", json ? CompiledOptionProjection.JsonReaderMethods : CompiledOptionProjection.ReaderMethods).Visit(rewritten.Body));
            rewritten = rewritten.WithBody((BlockSyntax)new CompiledOptionProjection(_options, "evaluator", CompiledOptionProjection.EvaluatorMethods).Visit(rewritten.Body));
        }
        _parameters = previous;
        _genericJson = previousJson;
        return rewritten;
    }

    /// <summary>Removes the obsolete configuration input from compatibility helpers emitted for older language targets.</summary>
    /// <param name="node">A synchronous local helper inside an asynchronous facade.</param>
    /// <returns>The same helper with its enclosing compiled profile and original cleanup.</returns>
    public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var previous = _parameters;
        _parameters = new HashSet<string>(previous, StringComparer.Ordinal);
        foreach (var parameter in node.ParameterList.Parameters.Where(parameter => parameter.Type?.ToString().Contains("LuminPackSerializerOption") == true))
            _parameters.Add(parameter.Identifier.ValueText);
        var result = base.VisitLocalFunctionStatement(node.WithParameterList(node.ParameterList.WithParameters(
            SyntaxFactory.SeparatedList(node.ParameterList.Parameters.Where(parameter => parameter.Type?.ToString().Contains("LuminPackSerializerOption") != true)))));
        _parameters = previous;
        return result;
    }

    /// <summary>Removes option binding without altering state construction, reference tracking or finally blocks.</summary>
    /// <param name="node">A generated initialization statement.</param>
    /// <returns>The original statement, or no statement for the obsolete option binding.</returns>
    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (node.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } &&
            access.Name.Identifier.ValueText == "Init") return null;
        return base.VisitExpressionStatement(node);
    }

    /// <summary>Removes forwarded option arguments before rewriting other argument expressions.</summary>
    /// <param name="node">A generated invocation or construction argument list.</param>
    /// <returns>The original arguments except for the former option input.</returns>
    public override SyntaxNode VisitArgumentList(ArgumentListSyntax node)
        => base.VisitArgumentList(node.WithArguments(SyntaxFactory.SeparatedList(node.Arguments.Where(argument =>
            argument.Expression is not IdentifierNameSyntax name || !_parameters.Contains(name.Identifier.ValueText)))));

    /// <summary>Resolves former runtime option member reads directly to declaration constants.</summary>
    /// <param name="node">An emitted configuration member read.</param>
    /// <returns>The compile-time value or the unchanged expression.</returns>
    public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (node.Expression is IdentifierNameSyntax name && _parameters.Contains(name.Identifier.ValueText) ||
            node.Expression is MemberAccessExpressionSyntax owner && owner.Name.Identifier.ValueText == "Option")
            return Property(node.Name.Identifier.ValueText).WithTriviaFrom(node);
        return base.VisitMemberAccessExpression(node);
    }

    /// <summary>Resolves the former nullable option access to its mandatory compiled value.</summary>
    /// <param name="node">A generated nullable option property read.</param>
    /// <returns>The fixed declaration value or unchanged access.</returns>
    public override SyntaxNode VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        => node.Expression is IdentifierNameSyntax name && _parameters.Contains(name.Identifier.ValueText) && node.WhenNotNull is MemberBindingExpressionSyntax binding
            ? Property(binding.Name.Identifier.ValueText).WithTriviaFrom(node) : base.VisitConditionalAccessExpression(node);

    /// <summary>Removes obsolete null-default selection before evaluating the actual compile-time setting.</summary>
    /// <param name="node">A generated option null comparison.</param>
    /// <returns>A constant for the removed optional argument or the original expression.</returns>
    public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (node.Left is IdentifierNameSyntax name && _parameters.Contains(name.Identifier.ValueText) && node.Right.IsKind(SyntaxKind.NullLiteralExpression))
            return SyntaxFactory.LiteralExpression(node.IsKind(SyntaxKind.EqualsExpression) ? SyntaxKind.FalseLiteralExpression : SyntaxKind.TrueLiteralExpression).WithTriviaFrom(node);
        return base.VisitBinaryExpression(node);
    }

    /// <summary>Builds a literal for one recognized former option property.</summary>
    /// <param name="name">The original configuration property name.</param>
    /// <returns>The expression supplying the selected declaration constant.</returns>
    private ExpressionSyntax Property(string name) => SyntaxFactory.ParseExpression(name switch
    {
        "StringEncoding" => _genericJson ? "FormatterDispatch.__LuminPackJsonEncoding<T>()" : "default(" + _options.TypeName + ").StringEncoding",
        "StringRecording" => "(global::LuminPack.Option.LuminPackStringRecording)" + _options.Recording,
        "MaxJsonDepth" => _options.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "ValidateJsonSyntax" => _options.Strict ? "true" : "false",
        "StandardFormat" => "default(" + _options.TypeName + ").StandardFormat",
        _ => "default(" + _options.TypeName + ")." + name
    });

    /// <summary>Projects one complete generated syntax tree under its already resolved declaration policy.</summary>
    /// <param name="source">The original generator output.</param>
    /// <param name="options">The complete root profile.</param>
    /// <param name="parseOptions">The consuming language and preprocessor settings.</param>
    /// <returns>Generated source using compiled configuration.</returns>
    internal static string Rewrite(string source, CompiledOptions options, CSharpParseOptions parseOptions)
        => new CompiledOptionApiProjection(options).Visit(CSharpSyntaxTree.ParseText(source, parseOptions).GetRoot()).ToFullString();
}
