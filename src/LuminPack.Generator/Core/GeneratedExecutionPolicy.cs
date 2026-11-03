using System;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Chooses implementation reuse at existing typed operation boundaries without changing their contracts.</summary>
internal static class GeneratedExecutionPolicy
{
    private const int InlineCallLimit = 16;

    /// <summary>Identifies complete operations whose repeated expansion enlarges their callers' live execution state.</summary>
    /// <param name="operationName">The exact normal, fresh, specialized or JSON operation selected by analysis.</param>
    /// <param name="operation">Its compiler-bound body before nested expansion.</param>
    /// <returns>Whether to call the existing concrete state overload instead of copying its body.</returns>
    internal static bool Reuse(string operationName, BinaryOperation operation)
    {
        if (operationName.Contains(":local:")) return false;
        return operationName.EndsWith(":json", StringComparison.Ordinal) && LetRuntimeChooseInlining(operation.Body);
    }

    /// <summary>Separates source composition from the runtime's method-inlining budget.</summary>
    /// <param name="body">The final state body's generated statements.</param>
    /// <returns>Whether the method should leave native inlining to the runtime.</returns>
    internal static bool LetRuntimeChooseInlining(BlockSyntax body)
        => body != null && (HasLargeBody(body) || body.DescendantNodes().Any(node =>
            node is ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or
                WhileStatementSyntax or DoStatementSyntax));

    /// <summary>Identifies call-heavy bodies before their callees consume the caller's native inlining budget.</summary>
    /// <param name="body">The selected complete operation body.</param>
    /// <returns>Whether its call-site count exceeds the small-body expansion budget.</returns>
    private static bool HasLargeBody(BlockSyntax body)
        => body.DescendantNodes().OfType<InvocationExpressionSyntax>().Take(InlineCallLimit + 1).Count() > InlineCallLimit;
}
