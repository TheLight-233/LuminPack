using System;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Chooses implementation reuse at existing typed operation boundaries without changing their contracts.</summary>
internal static class GeneratedExecutionPolicy
{
    private const int InlineJsonCallLimit = 16;

    /// <summary>Identifies complete operations whose repeated expansion enlarges their callers' live execution state.</summary>
    /// <param name="operationName">The exact normal, fresh, specialized or JSON operation selected by analysis.</param>
    /// <param name="operation">Its compiler-bound body before nested expansion.</param>
    /// <returns>Whether to call the existing concrete state overload instead of copying its body.</returns>
    internal static bool Reuse(string operationName, BinaryOperation operation)
    {
        if (operationName.Contains(":local:")) return false;
        return operationName.EndsWith(":json", StringComparison.Ordinal) && HasLargeJsonBody(operation.Body);
    }

    /// <summary>Separates source composition from the runtime's method-inlining budget.</summary>
    /// <param name="body">The final state body's generated statements.</param>
    /// <param name="json">Whether the state reads or writes JSON.</param>
    /// <returns>Whether the method should leave native inlining to the runtime.</returns>
    internal static bool LetRuntimeChooseInlining(BlockSyntax body, bool json)
        => body != null && (json && HasLargeJsonBody(body) || body.DescendantNodes().Any(node =>
            node is ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or
                WhileStatementSyntax or DoStatementSyntax));

    /// <summary>Bounds repeated JSON operation work while retaining small scalar and literal sequences for expansion.</summary>
    /// <param name="body">The selected complete operation body.</param>
    /// <returns>Whether its call-site count exceeds the small-body expansion budget.</returns>
    private static bool HasLargeJsonBody(BlockSyntax body)
        => body.DescendantNodes().OfType<InvocationExpressionSyntax>().Take(InlineJsonCallLimit + 1).Count() > InlineJsonCallLimit;
}
