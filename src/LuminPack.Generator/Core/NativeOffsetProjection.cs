using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Zero-extends nonnegative generated storage offsets before native pointer arithmetic.</summary>
internal sealed class NativeOffsetProjection : CSharpSyntaxRewriter
{
    private HashSet<string> _nativeOffsets = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Retains native-width parameter and local offsets without narrowing them to UInt32.</summary>
    /// <param name="node">The generated operation.</param>
    /// <returns>The operation with equivalent native indexing.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var previous = _nativeOffsets;
        _nativeOffsets = new HashSet<string>(previous, StringComparer.Ordinal);
        foreach (var parameter in node.DescendantNodes().OfType<ParameterSyntax>())
            if (IsNativeType(parameter.Type)) _nativeOffsets.Add(parameter.Identifier.ValueText);
        foreach (var declaration in node.DescendantNodes().OfType<VariableDeclarationSyntax>())
            if (IsNativeType(declaration.Type))
                foreach (var variable in declaration.Variables) _nativeOffsets.Add(variable.Identifier.ValueText);
        var result = base.VisitMethodDeclaration(node);
        _nativeOffsets = previous;
        return result;
    }

    /// <summary>Retains constant offsets and explicit backward addressing while narrowing indexed storage offsets.</summary>
    /// <param name="node">The generated pointer arithmetic operation.</param>
    /// <returns>The same address with an unsigned native offset for variable indexing.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var call = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
        if (call.Expression is not MemberAccessExpressionSyntax member ||
            !member.Expression.ToString().EndsWith("Unsafe", StringComparison.Ordinal) ||
            member.Name.Identifier.ValueText != "Add" || call.ArgumentList.Arguments.Count != 2) return call;
        var offset = call.ArgumentList.Arguments[1];
        var expression = offset.Expression;
        string value = expression.ToString();
        if (expression is LiteralExpressionSyntax or SizeOfExpressionSyntax or PrefixUnaryExpressionSyntax or CheckedExpressionSyntax ||
            !expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any()) return call;
        string replacement = IsNative(expression) ? "unchecked(" + value + ")" : "unchecked((nint)(uint)(" + value + "))";
        return call.WithArgumentList(call.ArgumentList.WithArguments(call.ArgumentList.Arguments.Replace(offset,
            offset.WithExpression(SyntaxFactory.ParseExpression(replacement).WithTriviaFrom(expression)))));
    }

    /// <summary>Recognizes compiler-owned native arithmetic without inferring it from unrelated argument text.</summary>
    /// <param name="expression">The complete offset expression.</param>
    /// <returns>Whether its result retains native width.</returns>
    private bool IsNative(ExpressionSyntax expression)
        => expression is ParenthesizedExpressionSyntax parenthesized ? IsNative(parenthesized.Expression) :
            expression is CastExpressionSyntax cast ? IsNativeType(cast.Type) :
            expression is IdentifierNameSyntax identifier ? _nativeOffsets.Contains(identifier.Identifier.ValueText) :
            expression is BinaryExpressionSyntax binary ? IsNative(binary.Left) || IsNative(binary.Right) :
            expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } &&
                member.Expression.ToString().EndsWith("Unsafe", StringComparison.Ordinal) && member.Name.Identifier.ValueText == "ByteOffset";

    /// <summary>Recognizes the generated native-integer type spellings.</summary>
    /// <param name="type">A declared local or parameter type.</param>
    /// <returns>Whether the declaration carries a native integer.</returns>
    private static bool IsNativeType(TypeSyntax type)
        => type?.ToString().Replace("global::", string.Empty) is "nint" or "nuint" or "IntPtr" or "UIntPtr" or "System.IntPtr" or "System.UIntPtr";
}
