using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace LuminPack.CodeFixes;

/// <summary>Provides mechanical IDE fixes for LuminPack generator diagnostics.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LuminPackCodeFixProvider)), Shared]
public sealed class LuminPackCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
        "LuminPack001", "LuminPack002", "LuminPack005", "LuminPack006", "LuminPack007",
        "LuminPack008", "LuminPack009", "LuminPack014", "LuminPack021", "LuminPack028",
        "LuminPack029", "LuminPack033", "LuminPack034", "LuminPack035", "LuminPack101", "LuminPack103");

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<MemberDeclarationSyntax>();
            if (node == null) continue;
            var solution = context.Document.Project.Solution;
            string? title = null;

            switch (diagnostic.Id)
            {
                case "LuminPack002":
                case "LuminPack014":
                case "LuminPack035":
                    var location = diagnostic.Id == "LuminPack002" ? diagnostic.AdditionalLocations.FirstOrDefault() : diagnostic.Location;
                    var targetDocument = location?.SourceTree == null ? null : solution.GetDocument(location.SourceTree);
                    if (targetDocument == null) break;
                    var targetRoot = await targetDocument.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                    var targetModel = await targetDocument.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                    var target = targetRoot?.FindNode(location!.SourceSpan).FirstAncestorOrSelf<TypeDeclarationSyntax>();
                    if (target == null || targetModel?.GetDeclaredSymbol(target, context.CancellationToken) is not INamedTypeSymbol type ||
                        type.IsStatic || type.IsRefLikeType) break;
                    bool annotate = diagnostic.Id != "LuminPack035";
                    if (annotate && type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                        CodeFixSyntax.AttributeNamespace + "LuminPackableAttribute")) break;
                    title = annotate ? "Add [LuminPackable] and partial declarations" : "Make type and containing types partial";
                    solution = await CodeFixSyntax.RewriteAsync(solution, CodeFixSyntax.TypeDeclarations(type, context.CancellationToken),
                        (original, rewritten) =>
                        {
                            var declaration = (TypeDeclarationSyntax)rewritten;
                            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                                declaration = declaration.WithoutLeadingTrivia().AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                                    .WithLeadingTrivia(declaration.GetLeadingTrivia());
                            return (annotate && original.SyntaxTree == target.SyntaxTree && original.Span == target.Span
                                ? CodeFixSyntax.AddAttribute(declaration, "LuminPackable") : declaration)
                                .WithAdditionalAnnotations(Formatter.Annotation);
                        }, context.CancellationToken).ConfigureAwait(false);
                    break;

                case "LuminPack005":
                case "LuminPack103":
                    if (node is not MethodDeclarationSyntax inaccessibleMethod) break;
                    title = "Make method internal";
                    solution = await CodeFixSyntax.RewriteAsync(solution,
                        CodeFixSyntax.MethodDeclarations(inaccessibleMethod, model, context.CancellationToken),
                        (_, rewritten) => CodeFixSyntax.WithAccessibility((MemberDeclarationSyntax)rewritten, SyntaxKind.InternalKeyword),
                        context.CancellationToken).ConfigureAwait(false);
                    break;

                case "LuminPack028":
                case "LuminPack029":
                    if (node is not TypeDeclarationSyntax nested || model.GetDeclaredSymbol(nested, context.CancellationToken) is not INamedTypeSymbol nestedType) break;
                    title = "Make nested type internal";
                    solution = await CodeFixSyntax.RewriteAsync(solution,
                        nestedType.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(context.CancellationToken)),
                        (_, rewritten) => CodeFixSyntax.WithAccessibility((MemberDeclarationSyntax)rewritten, SyntaxKind.InternalKeyword),
                        context.CancellationToken).ConfigureAwait(false);
                    break;

                case "LuminPack033":
                case "LuminPack101":
                    if (node is not MethodDeclarationSyntax method || method.Modifiers.Any(SyntaxKind.StaticKeyword) ||
                        method.Modifiers.Any(token => token.IsKind(SyntaxKind.AbstractKeyword) || token.IsKind(SyntaxKind.VirtualKeyword) ||
                            token.IsKind(SyntaxKind.OverrideKeyword)) || method.ExplicitInterfaceSpecifier != null) break;
                    title = "Make method static";
                    solution = await CodeFixSyntax.RewriteAsync(solution,
                        CodeFixSyntax.MethodDeclarations(method, model, context.CancellationToken),
                        (_, rewritten) =>
                        {
                            var declaration = (MethodDeclarationSyntax)rewritten;
                            if (declaration.Modifiers.Any(SyntaxKind.StaticKeyword)) return declaration;
                            return declaration.WithoutLeadingTrivia().AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                                .WithLeadingTrivia(declaration.GetLeadingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
                        }, context.CancellationToken).ConfigureAwait(false);
                    break;

                case "LuminPack008":
                case "LuminPack009":
                    if (node is not ClassDeclarationSyntax union || model.GetDeclaredSymbol(union, context.CancellationToken) is not INamedTypeSymbol unionType) break;
                    title = "Make union root abstract";
                    solution = await CodeFixSyntax.RewriteAsync(solution,
                        unionType.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(context.CancellationToken)),
                        (_, rewritten) =>
                        {
                            var declaration = (ClassDeclarationSyntax)rewritten;
                            var modifiers = SyntaxFactory.TokenList(declaration.WithoutLeadingTrivia().Modifiers.Where(token => !token.IsKind(SyntaxKind.SealedKeyword)));
                            if (!modifiers.Any(SyntaxKind.AbstractKeyword)) modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.AbstractKeyword));
                            return declaration.WithModifiers(modifiers).WithLeadingTrivia(declaration.GetLeadingTrivia())
                                .WithAdditionalAnnotations(Formatter.Annotation);
                        }, context.CancellationToken).ConfigureAwait(false);
                    break;

                default:
                    string[] names = diagnostic.Id switch
                    {
                        "LuminPack001" => new[] { "LuminPackable" },
                        "LuminPack006" => new[] { "LuminPackOnSerializing", "LuminPackOnSerialized", "LuminPackOnDeserializing", "LuminPackOnDeserialized" },
                        "LuminPack007" => new[] { "LuminPackInclude", "LuminPackIgnore" },
                        "LuminPack021" => new[] { "LuminPackInclude" },
                        "LuminPack034" => new[] { "LuminPackCompress" },
                        _ => System.Array.Empty<string>()
                    };
                    var attributes = node.AttributeLists.SelectMany(list => list.Attributes)
                        .Where(attribute => CodeFixSyntax.IsAttribute(attribute, model, names)).ToArray();
                    if (attributes.Length == 0) break;
                    title = "Remove invalid LuminPack attributes";
                    solution = solution.WithDocumentSyntaxRoot(context.Document.Id,
                        root.ReplaceNode(node, CodeFixSyntax.RemoveAttributes(node, attributes)));
                    break;
            }

            if (title != null)
                context.RegisterCodeFix(CodeAction.Create(title, cancellationToken => ReturnSolutionAsync(solution, cancellationToken), diagnostic.Id), diagnostic);
        }
    }

    /// <summary>Returns a prepared immutable edit while honoring action cancellation.</summary>
    /// <param name="solution">The solution containing the proposed fix.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The edited solution.</returns>
    private static Task<Solution> ReturnSolutionAsync(Solution solution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(solution);
    }
}
