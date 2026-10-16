using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Aligns expanded ref-struct temporary lifetimes with the concrete entry's writer or reader.</summary>
internal static class BinaryEntryLocalScopes
{
    /// <summary>Normalizes operation lifetimes in a complete projected serializer source file.</summary>
    /// <param name="root">The projected source root.</param>
    /// <returns>The same root with compatible lexical storage scopes.</returns>
    internal static SyntaxNode NormalizeRoot(SyntaxNode root) => new StateTemporaries().Visit(root);

    /// <summary>Preserves a parameter reader's caller-wide value escape context for expanded span storage.</summary>
    private sealed class StateTemporaries : CSharpSyntaxRewriter
    {
        /// <summary>Seeds span locals from the original reader before their unchanged initialization.</summary>
        /// <param name="node">An expanded operation block.</param>
        /// <returns>The same execution order with reader-compatible span storage.</returns>
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            var statements = new List<StatementSyntax>();
            foreach (var statement in node.Statements)
            {
                var visited = (StatementSyntax)Visit(statement);
                if (visited is LocalDeclarationStatementSyntax local && local.Declaration.Variables.Count == 1 &&
                    (local.Modifiers.Any(token => token.IsKind(SyntaxKind.ScopedKeyword)) || local.Declaration.Type.GetFirstToken().IsKind(SyntaxKind.ScopedKeyword)) &&
                    !local.Declaration.Variables[0].Initializer.Value.DescendantNodesAndSelf().Any(expression =>
                        expression is StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax) &&
                    local.Declaration.Type.DescendantNodesAndSelf().OfType<GenericNameSyntax>().FirstOrDefault() is { } span && span.Identifier.ValueText is "Span" or "ReadOnlySpan" &&
                    node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.ParameterList.Parameters[0].Type?.ToString().EndsWith("LuminPackReader", StringComparison.Ordinal) == true)
                {
                    var variable = local.Declaration.Variables[0];
                    var seed = SyntaxFactory.ParseExpression("reader.CreateSpan<" + span.TypeArgumentList.Arguments[0] +
                        ">(ref global::System.Runtime.CompilerServices.Unsafe.As<byte, " + span.TypeArgumentList.Arguments[0] + ">(ref reader.GetCurrentSpanReference()), 0)");
                    statements.Add(local.WithModifiers(SyntaxFactory.TokenList(local.Modifiers.Where(token => !token.IsKind(SyntaxKind.ScopedKeyword))))
                        .WithDeclaration(local.Declaration.WithType(local.Declaration.Type.GetFirstToken().IsKind(SyntaxKind.ScopedKeyword)
                            ? SyntaxFactory.ParseTypeName(local.Declaration.Type.ToString().Substring("scoped".Length).TrimStart()) : local.Declaration.Type)
                            .WithVariables(SyntaxFactory.SingletonSeparatedList(variable.WithInitializer(SyntaxFactory.EqualsValueClause(seed))))));
                    statements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(variable.Identifier), variable.Initializer.Value)).WithTrailingTrivia(local.GetTrailingTrivia()));
                }
                else statements.Add(visited);
            }
            return node.WithStatements(SyntaxFactory.List(statements));
        }
    }

}
