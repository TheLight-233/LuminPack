using System;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Chooses source composition, fresh storage and native inline hints from typed operation contracts.</summary>
internal static class GeneratedExecutionPolicy
{
    private const int InlineCallLimit = 16;

    /// <summary>Separates fresh Union element construction from heap-backed reusable element storage.</summary>
    /// <param name="element">The canonical element contract in the analyzed object graph.</param>
    /// <returns>Whether a fresh collection may read each Union into a local before publishing its result.</returns>
    /// <remarks>Ordinary objects and Custom formatters can publish before reading their fields, so they retain direct storage.</remarks>
    internal static bool UseFreshUnionLoop(ITypeSymbol element)
        => element is INamedTypeSymbol { IsReferenceType: true } named &&
            (named.TypeKind == TypeKind.Interface || named.IsAbstract) &&
            (TypeMetaChecker.TryCheckPackableAttribute(named) || TypeMetaChecker.TryCheckUnionAttribute(named)) &&
            !LuminPackExtensionGenerator.IsCustomGeneratorType(named);

    /// <summary>Identifies complete operations whose repeated expansion enlarges their callers' live execution state.</summary>
    /// <param name="operationName">The exact normal, fresh, specialized or JSON operation selected by analysis.</param>
    /// <param name="operation">Its compiler-bound body before nested expansion.</param>
    /// <returns>Whether to call the existing concrete state overload instead of copying its body.</returns>
    internal static bool Reuse(string operationName, BinaryOperation operation)
    {
        if (operationName == "WriteMemberValue")
            return true;
        if (operationName.Contains(":local:"))
            return false;
        return operationName.EndsWith(":json", StringComparison.Ordinal) && LetRuntimeChooseInlining(operation.Body);
    }

    /// <summary>Separates source composition from the runtime's method-inlining budget.</summary>
    /// <param name="body">The final state body's generated statements.</param>
    /// <returns>Whether the method should leave native inlining to the runtime.</returns>
    internal static bool LetRuntimeChooseInlining(BlockSyntax body)
        => body != null && (HasLargeBody(body) || body.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
            call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "WriteMemberValue" or "SerializeMember" or "RootWriteMember" }) || body.DescendantNodes().Any(node =>
            node is ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or
                WhileStatementSyntax or DoStatementSyntax));

    /// <summary>Protects compound scalar parsers from repeated partial inlining in a large JSON operation.</summary>
    /// <param name="body">The final composed state body.</param>
    /// <param name="parserSites">The number of floating-point parser sites in this method scope.</param>
    /// <returns>Whether those parsers need an independent native inlining budget.</returns>
    internal static bool IsolateJsonScalarLoops(BlockSyntax body, int parserSites)
        => parserSites > 1 && body.DescendantNodes(node => node is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
            .OfType<InvocationExpressionSyntax>().Take(InlineCallLimit + 1).Count() > InlineCallLimit;

    /// <summary>Identifies call-heavy bodies before their callees consume the caller's native inlining budget.</summary>
    /// <param name="body">The selected complete operation body.</param>
    /// <returns>Whether its call-site count exceeds the small-body expansion budget.</returns>
    private static bool HasLargeBody(BlockSyntax body)
        => body.DescendantNodes().OfType<InvocationExpressionSyntax>().Take(InlineCallLimit + 1).Count() > InlineCallLimit;
}
