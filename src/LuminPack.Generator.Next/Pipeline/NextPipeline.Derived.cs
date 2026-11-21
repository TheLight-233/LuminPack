using System.Threading;
using LuminPack.SourceGenerator.Next.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Pipeline;

internal static partial class NextPipeline
{
    /// <summary>Models a source class reached through an opted-in ancestor.</summary>
    /// <param name="context">The candidate declaration with a base clause.</param>
    /// <param name="cancellationToken">Cancels symbol discovery and analysis.</param>
    /// <returns>The derived payload's facts, or null when there is no payload to publish.</returns>
    private static ModeledPayload? AnalyzeDerivedDeclaration(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol type ||
            TypeModelFactory.HasDirectPackableMarker(type) || !TypeModelFactory.InheritsPackableMarker(type))
            return null;

        foreach (SyntaxReference part in type.DeclaringSyntaxReferences)
        {
            if (part.GetSyntax(cancellationToken) is not TypeDeclarationSyntax { BaseList: not null })
                continue;
            if (part.SyntaxTree != declaration.SyntaxTree || part.Span != declaration.Span)
                return null;
            break;
        }

        ModeledPayload payload = AnalyzeDeclaration(type, context.SemanticModel.Compilation, cancellationToken);
        // An abstract intermediary with no declared cases can still supply the storage inherited by concrete leaves.
        // Publish it as a Union when the ordinary discovery path finds a complete contract.
        if (type.IsAbstract && !TypeModelFactory.IsUnion(type) && payload.CustomRegistration == null && !payload.Result.IsComplete)
            return null;
        return payload;
    }
}
