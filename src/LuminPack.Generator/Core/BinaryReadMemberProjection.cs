using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Separates large sibling member reads in binary methods that would otherwise exhaust the native optimizer.</summary>
internal sealed class BinaryReadMemberProjection : CSharpSyntaxRewriter
{
    private const int WideReaderLocals = 384;
    private const int LargeMemberLocals = 64;
    private readonly IReadOnlyDictionary<string, ITypeSymbol> _types;

    /// <summary>Uses the analyzed formatter types to restrict extraction to class receivers.</summary>
    /// <param name="types">The consumer's canonical closed payload types.</param>
    internal BinaryReadMemberProjection(IReadOnlyDictionary<string, ITypeSymbol> types) => _types = types;

    /// <summary>Moves complete, independently bound member branches into static functions with the same reader and instance.</summary>
    /// <param name="node">The final generated method after storage and profile binding.</param>
    /// <returns>The original reader or a reader with independently compiled member branches.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Identifier.ValueText is not ("RootRead" or "RootReadCompressed" or "RootReadFresh") ||
            node.Body == null || node.ParameterList.Parameters.Count != 2)
            return node;
        var reader = node.ParameterList.Parameters[0];
        var body = node.Body;
        // This threshold counts source locals. It selects the exceptionally wide graph, independently of the runtime's IL limit.
        var locals = body.DescendantNodes(part => part is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
            .OfType<VariableDeclaratorSyntax>();
        if (!locals.Skip(WideReaderLocals - 1).Any())
            return node;
        var declarations = node.DescendantNodes().Select(DeclaredName).Where(token => token.RawKind != 0).ToArray();
        var names = new HashSet<string>(node.DescendantTokens().Select(token => token.ValueText));
        var calls = new Dictionary<IfStatementSyntax, StatementSyntax>();
        var helpers = new List<StatementSyntax>();
        foreach (var variable in locals)
        {
            if (variable.Initializer?.Value is not AssignmentExpressionSyntax allocation ||
                !allocation.IsKind(SyntaxKind.CoalesceAssignmentExpression) || allocation.Left is not IdentifierNameSyntax storage ||
                allocation.Right is not ObjectCreationExpressionSyntax creation || variable.Parent is not VariableDeclarationSyntax { Type.IsVar: true } ||
                variable.Parent.Parent?.Parent is not BlockSyntax block || !_types.TryGetValue(creation.Type.ToString(), out var type) ||
                type.TypeKind != TypeKind.Class)
                continue;
            // The receiver inherits the static type of the left operand of ??=. Require the root parameter or an
            // element's explicitly typed storage alias to match the constructed type before passing it by value.
            var storageType = node.ParameterList.Parameters.FirstOrDefault(parameter => parameter.Identifier.ValueText == storage.Identifier.ValueText)?.Type ??
                block.Statements.OfType<LocalDeclarationStatementSyntax>().Select(local => local.Declaration)
                    .FirstOrDefault(local => local.Variables.Any(v => v.Identifier.ValueText == storage.Identifier.ValueText))?.Type;
            if (storageType is RefTypeSyntax alias)
                storageType = alias.Type;
            if (!SyntaxFactory.AreEquivalent(storageType is NullableTypeSyntax nullable ? nullable.ElementType : storageType, creation.Type))
                continue;
            string instance = variable.Identifier.Text;
            foreach (var branch in block.Statements.OfType<IfStatementSyntax>())
            {
                if (branch.Else == null || branch.Ancestors().OfType<IfStatementSyntax>().Any(calls.ContainsKey) ||
                    branch.Statement is not ExpressionStatementSyntax
                    { Expression: AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax target } } } ||
                    target.Identifier.Text != instance || !branch.DescendantNodes().OfType<VariableDeclaratorSyntax>().Skip(LargeMemberLocals - 1).Any())
                    continue;
                // Reject any control transfer or lexical capture at the new boundary. Sibling scopes may reuse names;
                // conservatively keeping such a branch in place preserves the generated bindings on every target.
                var outside = new HashSet<string>(declarations.Where(token => !branch.FullSpan.Contains(token.Span))
                    .Select(token => token.ValueText));
                outside.Remove(reader.Identifier.ValueText);
                outside.Remove(variable.Identifier.ValueText);
                if (branch.DescendantNodes().Any(part => part is ReturnStatementSyntax or GotoStatementSyntax or LabeledStatementSyntax or
                    BreakStatementSyntax or ContinueStatementSyntax or YieldStatementSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax ||
                    part is IdentifierNameSyntax identifier && (outside.Contains(identifier.Identifier.ValueText) ||
                        identifier.Identifier.Text == instance && !(identifier.Parent is MemberAccessExpressionSyntax access && access.Expression == identifier))))
                    continue;
                string helper = "ReadMemberBlock" + helpers.Count;
                while (!names.Add(helper))
                    helper += "_";
                var function = (LocalFunctionStatementSyntax)SyntaxFactory.ParseStatement(
                    "static void " + helper + "(" + reader.WithoutTrivia().NormalizeWhitespace() + ", " + creation.Type + " " + instance + ") {}");
                helpers.Add(function.WithBody(SyntaxFactory.Block(branch)));
                calls.Add(branch, SyntaxFactory.ParseStatement(helper + "(ref " + reader.Identifier.Text + ", " + instance + ");"));
            }
        }
        if (helpers.Count < 2)
            return node;
        var rewritten = body.ReplaceNodes(calls.Keys, (original, _) => calls[original].WithTriviaFrom(original));
        return node.WithBody(rewritten.AddStatements(helpers.ToArray()));
    }

    /// <summary>Identifies local bindings that a moved branch could otherwise capture.</summary>
    /// <param name="node">A declaration in the original method.</param>
    /// <returns>The binding token, or a missing token for other syntax.</returns>
    private static SyntaxToken DeclaredName(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax local => local.Identifier,
        SingleVariableDesignationSyntax pattern => pattern.Identifier,
        ParameterSyntax parameter => parameter.Identifier,
        ForEachStatementSyntax loop => loop.Identifier,
        CatchDeclarationSyntax handler => handler.Identifier,
        LocalFunctionStatementSyntax function => function.Identifier,
        _ => default
    };
}
