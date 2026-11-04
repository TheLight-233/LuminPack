using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Keeps small framing wrappers eligible for inlining while their traversal helpers retain separate budgets.</summary>
internal static class RootInliningPolicy
{
    /// <summary>Marks a bounded JSON write wrapper whose loops are already behind explicit call boundaries.</summary>
    /// <param name="method">The complete projected operation.</param>
    /// <returns>The operation with an inline hint only when its reachable wrapper has no loops or existing policy.</returns>
    internal static MethodDeclarationSyntax Apply(MethodDeclarationSyntax method)
    {
        if (method.Body == null || !method.ParameterList.Parameters[0].Type.ToString().Contains("LuminPackJsonWriter") ||
            method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
                attribute.Name.ToString().EndsWith("MethodImpl", System.StringComparison.Ordinal))) return method;

        if (!method.Body.Statements.OfType<LocalFunctionStatementSyntax>().Any(Outlined)) return method;
        int statements = 0, variables = 0, calls = 0;
        foreach (var node in method.Body.DescendantNodes(node => node is not LocalFunctionStatementSyntax helper || !Outlined(helper)))
        {
            if (node is StatementSyntax && ++statements > 24 || node is VariableDeclaratorSyntax && ++variables > 6 ||
                node is InvocationExpressionSyntax && ++calls > 16 ||
                node is ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or AnonymousFunctionExpressionSyntax) return method;
        }

        return method.AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::System.Runtime.CompilerServices.MethodImpl"),
                SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                    SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining"))))))));
    }

    /// <summary>Recognizes an explicit boundary that keeps a helper's traversal out of the wrapper's budget.</summary>
    /// <param name="helper">The generated local operation.</param>
    /// <returns>Whether the helper explicitly forbids inlining.</returns>
    private static bool Outlined(LocalFunctionStatementSyntax helper) => helper.AttributeLists.SelectMany(list => list.Attributes)
        .Any(attribute => attribute.Name.ToString().EndsWith("MethodImpl", System.StringComparison.Ordinal) &&
            attribute.ArgumentList?.Arguments.Any(argument => argument.Expression.ToString().EndsWith(".NoInlining", System.StringComparison.Ordinal)) == true);
}
