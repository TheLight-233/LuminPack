using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.CodeFixes;

/// <summary>Offers explicit constructor choices without guessing during Fix All.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LuminPackConstructorCodeFixProvider)), Shared]
public sealed class LuminPackConstructorCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("LuminPack025", "LuminPack027");

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null || model == null) return;
        foreach (var diagnostic in context.Diagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!diagnostic.Location.IsInSource || diagnostic.Location.SourceTree != root.SyntaxTree) continue;
            if (diagnostic.AdditionalLocations.Count == 0 && root.FindNode(diagnostic.Location.SourceSpan) is not TypeDeclarationSyntax) continue;
            var location = diagnostic.AdditionalLocations.FirstOrDefault() ?? diagnostic.Location;
            var targetDocument = location.SourceTree == null ? null : context.Document.Project.Solution.GetDocument(location.SourceTree);
            if (targetDocument == null) continue;
            var targetRoot = await targetDocument.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var targetModel = await targetDocument.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var declaration = targetRoot?.FindNode(location.SourceSpan).FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (declaration == null || targetModel?.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type) continue;
            bool select = diagnostic.Id == "LuminPack025";
            var constructors = type.InstanceConstructors.Where(constructor => !constructor.IsImplicitlyDeclared)
                .SelectMany(constructor => constructor.DeclaringSyntaxReferences)
                .Select(reference => reference.GetSyntax(context.CancellationToken)).OfType<ConstructorDeclarationSyntax>().ToArray();
            foreach (var candidate in constructors.Where(constructor => select == constructor.Modifiers.Any(SyntaxKind.PublicKeyword)))
            {
                var solution = context.Document.Project.Solution;
                var targets = select ? constructors : new[] { candidate };
                // Bind attributes in their own documents; partial types may span files.
                var annotations = new System.Collections.Generic.Dictionary<SyntaxNode, AttributeSyntax[]>();
                foreach (var constructor in targets)
                {
                    var document = solution.GetDocument(constructor.SyntaxTree);
                    if (document == null) continue;
                    var constructorModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                    if (constructorModel == null) continue;
                    annotations[constructor] = constructor.AttributeLists.SelectMany(list => list.Attributes)
                        .Where(attribute => CodeFixSyntax.IsAttribute(attribute, constructorModel, "LuminPackConstructor")).ToArray();
                }
                solution = await CodeFixSyntax.RewriteAsync(solution, annotations.Keys, (original, rewritten) =>
                {
                    var edited = select ? CodeFixSyntax.RemoveAttributes((MemberDeclarationSyntax)rewritten, annotations[original])
                        : (MemberDeclarationSyntax)rewritten;
                    bool chosen = original.SyntaxTree == candidate.SyntaxTree && original.Span == candidate.Span;
                    if (select) return chosen ? CodeFixSyntax.AddAttribute(edited, "LuminPackConstructor") : edited;
                    return CodeFixSyntax.WithAccessibility(edited, SyntaxKind.PublicKeyword);
                }, context.CancellationToken).ConfigureAwait(false);
                string signature = candidate.Identifier.ValueText + candidate.ParameterList;
                string title = select ? "Use constructor " + signature : "Make constructor public: " + signature;
                context.RegisterCodeFix(CodeAction.Create(title, cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(solution);
                }, diagnostic.Id + ":" + signature), diagnostic);
            }
        }
    }
}
