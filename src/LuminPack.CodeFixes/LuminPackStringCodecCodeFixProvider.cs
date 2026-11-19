using System.Collections.Generic;
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

/// <summary>Completes a stateless profile by forwarding missing binary string operations to a built-in codec.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LuminPackStringCodecCodeFixProvider)), Shared]
public sealed class LuminPackStringCodecCodeFixProvider : CodeFixProvider
{
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("CS0535");

    /// <inheritdoc/>
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var declaration = root?.FindNode(context.Span).FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (declaration == null || model?.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol { TypeKind: TypeKind.Struct } profile)
            return;
        var options = model.Compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        var contract = model.Compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackStringCodec");
        if (contract == null || !profile.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, options)))
            return;
        var missing = contract.GetMembers().Where(m => (m is IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary }) &&
            profile.FindImplementationForInterfaceMember(m) == null).ToArray();
        if (missing.Length == 0)
            return;
        var encoding = ConstantSetting(profile, "StringEncoding", model.Compilation, context.CancellationToken);
        var recording = ConstantSetting(profile, "StringRecording", model.Compilation, context.CancellationToken);
        foreach (var unit in new[] { "Utf8", "Utf16" })
        foreach (var framing in new[] { "Length", "Token", "VarInt" })
        {
            if ((encoding != null && encoding != unit.ToUpperInvariant()) || (recording != null && recording != framing))
                continue;
            string name = "LuminPack" + unit + framing + "Strings";
            var codec = model.Compilation.GetTypeByMetadataName("LuminPack.Option." + name);
            if (codec == null)
                continue;
            context.RegisterCodeFix(CodeAction.Create("Forward string operations to " + name,
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root!.ReplaceNode(declaration,
                    declaration.AddMembers(missing.Select(m => Forward(m, codec)).ToArray())))), name), context.Diagnostics);
        }
    }

    /// <summary>Recognizes a directly returned enum constant across partial declarations.</summary>
    /// <param name="profile">The profile being completed.</param>
    /// <param name="name">The legacy metadata property.</param>
    /// <param name="compilation">The active compilation.</param>
    /// <param name="token">Cancellation for semantic lookup.</param>
    /// <returns>The enum field name when the property unambiguously returns one constant.</returns>
    private static string? ConstantSetting(INamedTypeSymbol profile, string name, Compilation compilation, CancellationToken token)
    {
        var property = profile.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault();
        if (property?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(token) is not PropertyDeclarationSyntax syntax)
            return null;
        IEnumerable<ExpressionSyntax> values = syntax.ExpressionBody != null ? new[] { syntax.ExpressionBody.Expression } :
            syntax.DescendantNodes().OfType<ReturnStatementSyntax>().Select(r => r.Expression).OfType<ExpressionSyntax>()
                .Concat(syntax.AccessorList?.Accessors.Select(a => a.ExpressionBody?.Expression).OfType<ExpressionSyntax>() ?? Enumerable.Empty<ExpressionSyntax>());
        var model = compilation.GetSemanticModel(syntax.SyntaxTree);
        var fields = values.Select(expr => model.GetSymbolInfo(expr, token).Symbol is IFieldSymbol { HasConstantValue: true } f &&
            SymbolEqualityComparer.Default.Equals(f.ContainingType, property.Type) ? f.Name : null).Distinct().ToArray();
        return fields.Length == 1 ? fields[0] : null;
    }

    /// <summary>Builds one forwarding member from the compiled interface signature.</summary>
    /// <param name="member">The unimplemented interface operation or metadata property.</param>
    /// <param name="codec">The concrete stateless codec.</param>
    /// <returns>The documented forwarding declaration.</returns>
    private static MemberDeclarationSyntax Forward(ISymbol member, INamedTypeSymbol codec)
    {
        string receiver = "default(" + codec.ToDisplayString(TypeFormat) + ")." + member.Name;
        const string inline = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
        string source;
        if (member is IPropertySymbol p)
            source = "public " + p.Type.ToDisplayString(TypeFormat) + " " + p.Name + " { " + inline + " get => " + receiver + "; }";
        else
        {
            var m = (IMethodSymbol)member;
            string refReturn = m.ReturnsByRef ? "ref " : "";
            string signature = string.Join(", ", m.Parameters.Select(p => Scope(p) + Passing(p) + p.Type.ToDisplayString(TypeFormat) + " " + p.Name));
            string args = string.Join(", ", m.Parameters.Select(p => Passing(p) + p.Name));
            source = inline + "\npublic " + refReturn + m.ReturnType.ToDisplayString(TypeFormat) + " " + m.Name +
                "(" + signature + ") => " + refReturn + receiver + "(" + args + ");";
        }
        return SyntaxFactory.ParseMemberDeclaration("/// <inheritdoc/>\n" + source)!.WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Returns the passing convention required at both declaration and call.</summary>
    /// <param name="parameter">The interface parameter.</param>
    /// <returns>The reference modifier and its following space.</returns>
    private static string Passing(IParameterSymbol parameter) => parameter.RefKind switch
    {
        RefKind.Ref => "ref ",
        RefKind.Out => "out ",
        RefKind.In => "in ",
        _ => ""
    };

    /// <summary>Reads scoped parameter metadata using the Roslyn API supported by the analyzer package.</summary>
    /// <param name="parameter">The imported interface parameter.</param>
    /// <returns>The explicit scope modifier when the interface requires it.</returns>
    private static string Scope(IParameterSymbol parameter) =>
        parameter.ToDisplayParts(TypeFormat.WithParameterOptions(SymbolDisplayParameterOptions.IncludeParamsRefOut |
            SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName))
            .Any(part => part.Kind == SymbolDisplayPartKind.Keyword && part.ToString() == "scoped") ? "scoped " : "";
}
