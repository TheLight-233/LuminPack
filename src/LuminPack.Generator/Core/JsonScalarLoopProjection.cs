using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

/// <summary>Applies execution hints to scalar JSON read and write loops after state localization.</summary>
internal sealed class JsonScalarLoopProjection : NodeRewriter
{
    /// <summary>Retains small roots and isolates repeated floating-point parsers in call-heavy roots.</summary>
    /// <param name="node">A generated operation after scalar calls have their canonical typed form.</param>
    /// <returns>The same traversal and publication order with shared local scalar entry points when selected.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Identifier.ValueText != "RootReadJson" || node.Body == null || node.ParameterList.Parameters.Count != 2 ||
            node.ParameterList.Parameters[0].Type.ToString().Replace("global::", string.Empty) != "LuminPack.Core.LuminPackJsonReader")
            return base.VisitMethodDeclaration(node);
        string reader = node.ParameterList.Parameters[0].Identifier.ValueText;
        var calls = node.Body.DescendantNodes(item => item is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
            .OfType<InvocationExpressionSyntax>().Where(call => call.ArgumentList.Arguments.Count == 0 &&
                call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax state, Name: GenericNameSyntax name } &&
                state.Identifier.ValueText == reader && name.Identifier.ValueText == "Read" && name.TypeArgumentList.Arguments.Count == 2 &&
                IsFloatingPoint(name.TypeArgumentList.Arguments[0])).ToArray();
        if (!GeneratedExecutionPolicy.IsolateJsonScalarLoops(node.Body, calls.Length))
            return node;
        var names = new HashSet<string>(node.DescendantTokens().Select(token => token.ValueText));
        var replacements = new Dictionary<InvocationExpressionSyntax, InvocationExpressionSyntax>();
        var helpers = new List<StatementSyntax>();
        string Unique(string proposed)
        {
            while (!names.Add(proposed))
                proposed += "_";
            return proposed;
        }
        foreach (var group in calls.GroupBy(call => ((MemberAccessExpressionSyntax)call.Expression).Name.ToString()))
        {
            var original = (MemberAccessExpressionSyntax)group.First().Expression;
            var scalar = ((GenericNameSyntax)original.Name).TypeArgumentList.Arguments[0];
            string helper = Unique("__luminReadJsonScalar"), state = Unique("__luminJsonScalarState");
            var operation = group.First().WithExpression(original.WithExpression(SyntaxFactory.IdentifierName(state)));
            helpers.Add(SyntaxFactory.ParseStatement(
                "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] " +
                "static " + scalar + " " + helper + "(ref global::LuminPack.Core.LuminPackJsonReader " + state + ") { return " + operation + "; }"));
            foreach (var call in group)
                replacements.Add(call, SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(helper),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(reader)).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword))))));
        }
        var body = node.Body.ReplaceNodes(replacements.Keys, (original, rewritten) => replacements[original].WithTriviaFrom(rewritten));
        return node.WithBody(body.WithStatements(body.Statements.AddRange(helpers)));
    }

    /// <summary>Recognizes canonical built-in single and double payloads without matching user type names.</summary>
    /// <param name="type">The compiler-selected scalar payload argument.</param>
    /// <returns>Whether the runtime operation includes floating-point scanning.</returns>
    private static bool IsFloatingPoint(TypeSyntax type)
        => type.ToString().Replace("global::", string.Empty) is "float" or "double" or "System.Single" or "System.Double";
}
