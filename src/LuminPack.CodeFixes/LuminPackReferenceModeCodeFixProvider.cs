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

/// <summary>Migrates the obsolete reference-tracking generation mode to its declaration option.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LuminPackReferenceModeCodeFixProvider)), Shared]
public sealed class LuminPackReferenceModeCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "CS0618";
    private const string Mode = "CircleReference";
    private const string Option = "TrackReferences";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null || model == null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Location.IsInSource || diagnostic.Location.SourceTree != root.SyntaxTree)
                continue;
            var argument = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<AttributeArgumentSyntax>();
            var packable = argument?.FirstAncestorOrSelf<AttributeSyntax>();
            var type = packable?.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (argument == null || packable == null || type == null ||
                model.GetSymbolInfo(argument.Expression, context.CancellationToken).Symbol is not IFieldSymbol { Name: Mode } field ||
                field.ContainingType.ToDisplayString() != CodeFixSyntax.AttributeNamespace + "GeneratorType" ||
                !CodeFixSyntax.IsAttribute(packable, model, "LuminPackable") ||
                model.GetDeclaredSymbol(type, context.CancellationToken) is not INamedTypeSymbol { TypeKind: TypeKind.Class } symbol)
                continue;

            var option = symbol.GetAttributes()
                .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == CodeFixSyntax.AttributeNamespace + "LuminPackOptionAttribute")?
                .ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) as AttributeSyntax;
            context.RegisterCodeFix(CodeAction.Create("Use version tolerance with reference tracking",
                cancellation => MigrateAsync(context.Document.Project.Solution, packable, argument, option, cancellation), DiagnosticId), diagnostic);
        }
    }

    /// <summary>Replaces the obsolete argument and enables tracking on the same type.</summary>
    /// <param name="solution">The workspace before the migration.</param>
    /// <param name="packable">The attribute that selects CircleReference mode.</param>
    /// <param name="argument">The bound enum argument.</param>
    /// <param name="option">Any existing option attribute, including one on another partial declaration.</param>
    /// <param name="cancellationToken">The workspace cancellation token.</param>
    /// <returns>The edited solution.</returns>
    private static async Task<Solution> MigrateAsync(Solution solution, AttributeSyntax packable, AttributeArgumentSyntax argument,
        AttributeSyntax? option, CancellationToken cancellationToken)
    {
        ExpressionSyntax mode = argument.Expression is MemberAccessExpressionSyntax member
            ? member.WithName(member.Name.WithIdentifier(SyntaxFactory.Identifier("VersionTolerant").WithTriviaFrom(member.Name.Identifier)))
            : SyntaxFactory.ParseExpression("global::" + CodeFixSyntax.AttributeNamespace + "GeneratorType.VersionTolerant")
                .WithTriviaFrom(argument.Expression);
        var replacement = packable.ReplaceNode(argument.Expression, mode).WithAdditionalAnnotations(Formatter.Annotation);
        if (option == null)
        {
            var attributes = (AttributeListSyntax)packable.Parent!;
            var added = Track(SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::" +
                CodeFixSyntax.AttributeNamespace + "LuminPackOptionAttribute")));
            return await CodeFixSyntax.RewriteAsync(solution, new[] { attributes }, (_, _) =>
                attributes.WithAttributes(attributes.Attributes.Replace(packable, replacement).Add(added))
                    .WithAdditionalAnnotations(Formatter.Annotation), cancellationToken).ConfigureAwait(false);
        }
        return await CodeFixSyntax.RewriteAsync(solution, new[] { packable, option },
            (original, rewritten) => original == packable ? replacement : Track((AttributeSyntax)rewritten),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Enables tracking and keeps the other arguments in the same option attribute.</summary>
    /// <param name="attribute">The existing or newly generated option attribute.</param>
    /// <returns>The attribute with tracking set to true.</returns>
    private static AttributeSyntax Track(AttributeSyntax attribute)
    {
        var expression = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
        var arguments = attribute.ArgumentList ?? SyntaxFactory.AttributeArgumentList();
        var previous = arguments.Arguments.FirstOrDefault(item => item.NameEquals?.Name.Identifier.ValueText == Option);
        arguments = previous == null
            ? arguments.AddArguments(SyntaxFactory.AttributeArgument(expression).WithNameEquals(SyntaxFactory.NameEquals(Option)))
            : arguments.ReplaceNode(previous.Expression, expression.WithTriviaFrom(previous.Expression));
        return attribute.WithArgumentList(arguments).WithAdditionalAnnotations(Formatter.Annotation);
    }
}
