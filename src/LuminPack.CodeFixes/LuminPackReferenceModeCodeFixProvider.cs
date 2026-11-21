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
    private const string DiagnosticId = "CS0117";
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
                argument.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: Mode } member ||
                model.GetTypeInfo(member.Expression, context.CancellationToken).Type?.ToDisplayString() != CodeFixSyntax.AttributeNamespace + "GeneratorType" ||
                model.GetTypeInfo(packable, context.CancellationToken).Type?.ToDisplayString() != CodeFixSyntax.AttributeNamespace + "LuminPackableAttribute" ||
                model.GetDeclaredSymbol(type, context.CancellationToken) is not INamedTypeSymbol { TypeKind: TypeKind.Class } symbol)
                continue;

            var option = symbol.GetAttributes()
                .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == CodeFixSyntax.AttributeNamespace + "LuminPackOptionAttribute")?
                .ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) as AttributeSyntax;
            context.RegisterCodeFix(CodeAction.Create("Use version tolerance with reference tracking",
                cancellation => MigrateAsync(context.Document.Project.Solution, packable, argument, option, cancellation), DiagnosticId), diagnostic);
        }
    }

    /// <summary>Moves the version selection to the declaration options and enables tracking.</summary>
    /// <param name="solution">The workspace before the migration.</param>
    /// <param name="packable">The attribute that selects CircleReference mode.</param>
    /// <param name="argument">The bound enum argument.</param>
    /// <param name="option">Any existing option attribute, including one on another partial declaration.</param>
    /// <param name="cancellationToken">The workspace cancellation token.</param>
    /// <returns>The edited solution.</returns>
    private static async Task<Solution> MigrateAsync(Solution solution, AttributeSyntax packable, AttributeArgumentSyntax argument,
        AttributeSyntax? option, CancellationToken cancellationToken)
    {
        var member = (MemberAccessExpressionSyntax)argument.Expression;
        var mode = member.WithName(member.Name.WithIdentifier(SyntaxFactory.Identifier("Version").WithTriviaFrom(member.Name.Identifier)));
        var replacement = packable.WithArgumentList(null).WithTriviaFrom(packable).WithAdditionalAnnotations(Formatter.Annotation);
        if (option == null)
        {
            var attributes = (AttributeListSyntax)packable.Parent!;
            var added = SetOptions(SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::" +
                CodeFixSyntax.AttributeNamespace + "LuminPackOptionAttribute")), mode);
            return await CodeFixSyntax.RewriteAsync(solution, new[] { attributes }, (_, _) =>
                attributes.WithAttributes(attributes.Attributes.Replace(packable, replacement).Add(added))
                    .WithAdditionalAnnotations(Formatter.Annotation), cancellationToken).ConfigureAwait(false);
        }
        ExpressionSyntax optionMode = packable.SyntaxTree == option.SyntaxTree ? mode : SyntaxFactory.ParseExpression(
            "global::" + CodeFixSyntax.AttributeNamespace + "GeneratorType.Version").WithTriviaFrom(mode);
        return await CodeFixSyntax.RewriteAsync(solution, new[] { packable, option },
            (original, rewritten) => original == packable ? replacement : SetOptions((AttributeSyntax)rewritten, optionMode),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sets the version and tracking options while keeping the other arguments.</summary>
    /// <param name="attribute">The existing or new declaration option.</param>
    /// <param name="mode">The expression selecting version-tolerant records.</param>
    /// <returns>The option with both properties set.</returns>
    private static AttributeSyntax SetOptions(AttributeSyntax attribute, ExpressionSyntax mode)
    {
        var arguments = attribute.ArgumentList ?? SyntaxFactory.AttributeArgumentList();
        arguments = SetArgument(arguments, "GeneratorType", mode);
        arguments = SetArgument(arguments, Option, SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));
        return attribute.WithArgumentList(arguments).WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Inserts or replaces a named option.</summary>
    /// <param name="arguments">The existing option arguments.</param>
    /// <param name="name">The option to set.</param>
    /// <param name="expression">The new value.</param>
    /// <returns>The updated argument list.</returns>
    private static AttributeArgumentListSyntax SetArgument(AttributeArgumentListSyntax arguments, string name, ExpressionSyntax expression)
    {
        var previous = arguments.Arguments.FirstOrDefault(item => item.NameEquals?.Name.Identifier.ValueText == name);
        return previous == null
            ? arguments.AddArguments(SyntaxFactory.AttributeArgument(expression).WithNameEquals(SyntaxFactory.NameEquals(name)))
            : arguments.ReplaceNode(previous.Expression, expression.WithTriviaFrom(previous.Expression));
    }
}
