using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

internal static partial class GeneratedSourceLayout
{
    private const string UnionCases = "// Union cases";

    private static readonly (string Alias, string Target)[] Aliases =
    {
        ("LPUnsafe", "global::System.Runtime.CompilerServices.Unsafe"),
        ("LPMemoryMarshal", "global::System.Runtime.InteropServices.MemoryMarshal"),
        ("LPMethodImpl", "global::System.Runtime.CompilerServices.MethodImplAttribute"),
        ("LPMethodImplOptions", "global::System.Runtime.CompilerServices.MethodImplOptions"),
    };

    /// <summary>Computes a value-compared alias mask without making every generated file depend on the compilation.</summary>
    /// <param name="context">The generator's incremental registration context.</param>
    /// <returns>The available aliases for the current compilation.</returns>
    internal static IncrementalValueProvider<int> AvailableAliases(IncrementalGeneratorInitializationContext context)
        => context.CompilationProvider.Select(static (compilation, cancellationToken) =>
        {
            var names = new HashSet<string>(Aliases.Select(entry => entry.Alias), StringComparer.Ordinal);
            var occupied = new HashSet<string>(compilation.GetSymbolsWithName(names.Contains, SymbolFilter.All, cancellationToken)
                .Select(symbol => symbol.Name), StringComparer.Ordinal);
            foreach (var tree in compilation.SyntaxTrees)
                if (tree.GetRoot(cancellationToken) is CompilationUnitSyntax unit)
                    foreach (var directive in unit.Usings)
                        if (directive.Alias?.Name.Identifier.ValueText is { } name)
                            occupied.Add(name);
            int mask = 0;
            for (int index = 0; index < Aliases.Length; index++)
                if (!occupied.Contains(Aliases[index].Alias))
                    mask |= 1 << index;
            return mask;
        });

    /// <summary>Applies aliases to qualified helper names and compacts explicitly marked Union switches.</summary>
    /// <param name="source">The completed generated source.</param>
    /// <param name="aliases">The aliases available in the consumer's declarations.</param>
    /// <returns>The same source with shorter bound names and compact case rows.</returns>
    private static string Present(string source, int aliases)
    {
        bool union = source.IndexOf(UnionCases, StringComparison.Ordinal) >= 0;
        bool helpers = aliases != 0 && Aliases.Any(entry => source.Contains(entry.Target) ||
            entry.Alias == "LPMethodImpl" && source.Contains("global::System.Runtime.CompilerServices.MethodImpl("));
        if (!union && !helpers)
            return source;

        var root = SyntaxFactory.ParseCompilationUnit(source, options: ParseOptions);
        if (helpers)
            foreach (var token in root.DescendantTokens())
                if (token.IsKind(SyntaxKind.IdentifierToken))
                    for (int index = 0; index < Aliases.Length; index++)
                        if (token.ValueText == Aliases[index].Alias)
                            aliases &= ~(1 << index);
        var layout = new Presentation(helpers ? aliases : 0, union);
        var rewritten = (CompilationUnitSyntax)layout.Visit(root);
        return layout.AddUsings(rewritten).ToFullString();
    }

    /// <summary>Shortens only exact global-qualified helper names and whitespace-only Union arms.</summary>
    private sealed class Presentation : CSharpSyntaxRewriter
    {
        private readonly int _available;
        private readonly bool _union;
        private int _used;

        /// <summary>Captures the enabled changes for one generated file.</summary>
        /// <param name="available">Alias names available in the file.</param>
        /// <param name="union">Whether the file contains a marked Union switch.</param>
        internal Presentation(int available, bool union)
        {
            _available = available;
            _union = union;
        }

        /// <summary>Declares only the aliases referenced by the rewritten file.</summary>
        /// <param name="root">The generated compilation unit.</param>
        /// <returns>The same unit with the required using directives.</returns>
        internal CompilationUnitSyntax AddUsings(CompilationUnitSyntax root)
        {
            if (_used == 0)
                return root;
            var directives = new List<UsingDirectiveSyntax>();
            for (int index = 0; index < Aliases.Length; index++)
                if ((_used & (1 << index)) != 0)
                    directives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.NameEquals(Aliases[index].Alias),
                            SyntaxFactory.ParseName(Aliases[index].Target))
                        .NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
            return root.WithUsings(root.Usings.AddRange(directives));
        }

        /// <summary>Leaves using targets qualified because file-level aliases are out of scope there.</summary>
        /// <param name="node">The original using directive.</param>
        /// <returns>The original directive.</returns>
        public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node) => node;

        /// <summary>Shortens a generated qualified type or attribute name.</summary>
        /// <param name="node">The qualified name.</param>
        /// <returns>The bound alias or the ordinary traversal.</returns>
        public override SyntaxNode VisitQualifiedName(QualifiedNameSyntax node)
            => Shorten(node, node.Right.Identifier.ValueText) ?? base.VisitQualifiedName(node);

        /// <summary>Shortens a generated qualified static helper expression.</summary>
        /// <param name="node">The member access.</param>
        /// <returns>The bound alias or the ordinary traversal.</returns>
        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            => Shorten(node, node.Name.Identifier.ValueText) ?? base.VisitMemberAccessExpression(node);

        /// <summary>Compacts marked Union case rows after any alias substitution.</summary>
        /// <param name="node">The generated switch.</param>
        /// <returns>The switch with its ordinary cases on single lines.</returns>
        public override SyntaxNode VisitSwitchStatement(SwitchStatementSyntax node)
        {
            var result = (SwitchStatementSyntax)base.VisitSwitchStatement(node);
            if (!_union || !node.GetLeadingTrivia().Any(trivia => trivia.ToString() == UnionCases))
                return result;
            return result.WithSections(SyntaxFactory.List(result.Sections.Select(Compact)));
        }

        /// <summary>Looks up an alias without changing any other qualified name.</summary>
        /// <param name="node">The candidate name or expression.</param>
        /// <param name="name">The last identifier of the name.</param>
        /// <returns>The replacement identifier, or null for a name to leave intact.</returns>
        private IdentifierNameSyntax Shorten(SyntaxNode node, string name)
        {
            int index = name switch
            {
                "Unsafe" => 0,
                "MemoryMarshal" => 1,
                "MethodImpl" or "MethodImplAttribute" => 2,
                "MethodImplOptions" => 3,
                _ => -1
            };
            if (index < 0 || (_available & (1 << index)) == 0)
                return null;
            string path = node.ToString();
            if (path != Aliases[index].Target &&
                !(index == 2 && node.Parent is AttributeSyntax && path + "Attribute" == Aliases[index].Target))
                return null;
            _used |= 1 << index;
            // Escaping an attribute alias bypasses attribute-suffix lookup.
            return SyntaxFactory.IdentifierName(index == 2 && node.Parent is AttributeSyntax ? "@" + Aliases[index].Alias : Aliases[index].Alias)
                .WithTriviaFrom(node);
        }

        /// <summary>Places one ordinary case on its own line without moving comments or directives.</summary>
        /// <param name="section">A switch section inside a marked Union.</param>
        /// <returns>The original section or its single-line equivalent.</returns>
        private static SwitchSectionSyntax Compact(SwitchSectionSyntax section)
        {
            if (section.Labels.Count != 1 || section.Labels[0].IsKind(SyntaxKind.DefaultSwitchLabel) ||
                section.DescendantTrivia().Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                return section;
            var indent = section.GetLeadingTrivia().Reverse().TakeWhile(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).Reverse();
            return section.NormalizeWhitespace(string.Empty, " ").WithLeadingTrivia(indent)
                .WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine));
        }
    }
}
