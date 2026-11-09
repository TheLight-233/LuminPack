using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LuminPack.SourceGenerator;

internal static partial class GeneratedSourceLayout
{
    private const string UnionCases = "// Union cases";
    private const string SystemObject = "global::System.Object";
    private const int MaxCaseWidth = 260;

    // Each alias spells its target's own simple name, so generated code reads like ordinary source. An alias is declared only
    // when the consuming compilation declares nothing with that name, and attribute aliases are escaped to skip suffix lookup.
    private static readonly (string Alias, string Target)[] Aliases =
    {
        ("Unsafe", "global::System.Runtime.CompilerServices.Unsafe"),
        ("MemoryMarshal", "global::System.Runtime.InteropServices.MemoryMarshal"),
        ("MethodImpl", "global::System.Runtime.CompilerServices.MethodImplAttribute"),
        ("MethodImplOptions", "global::System.Runtime.CompilerServices.MethodImplOptions"),
        ("EditorBrowsable", "global::System.ComponentModel.EditorBrowsableAttribute"),
        ("EditorBrowsableState", "global::System.ComponentModel.EditorBrowsableState"),
        ("LuminPackSerializer", "global::LuminPack.LuminPackSerializer"),
        ("LuminPackReader", "global::LuminPack.Core.LuminPackReader"),
        ("LuminPackWriter", "global::LuminPack.Core.LuminPackWriter"),
        ("LuminPackJsonReader", "global::LuminPack.Core.LuminPackJsonReader"),
        ("LuminPackJsonWriter", "global::LuminPack.Core.LuminPackJsonWriter"),
        ("LuminPackInputPolicy", "global::LuminPack.Security.LuminPackInputPolicy"),
        ("LuminPackExceptionHelper", "global::LuminPack.Code.LuminPackExceptionHelper"),
        ("LuminPackMarshal", "global::LuminPack.Code.LuminPackMarshal"),
        ("ILuminPackOptions", "global::LuminPack.Option.ILuminPackOptions"),
        ("Preserve", "global::LuminPack.Attribute.PreserveAttribute"),
        ("LuminPackDefaultOptions", "global::LuminPack.Option.LuminPackDefaultOptions"),
        ("LuminPackDeclaredOptions", "global::LuminPack.Option.LuminPackDeclaredOptions"),
        ("LuminPackUtf16LengthOptions", "global::LuminPack.Option.LuminPackUtf16LengthOptions"),
        ("LuminPackUtf8LengthOptions", "global::LuminPack.Option.LuminPackUtf8LengthOptions"),
        ("LuminPackUtf8TokenOptions", "global::LuminPack.Option.LuminPackUtf8TokenOptions"),
        ("LuminPackUtf16TokenOptions", "global::LuminPack.Option.LuminPackUtf16TokenOptions"),
        ("LuminPackUtf8VarIntOptions", "global::LuminPack.Option.LuminPackUtf8VarIntOptions"),
        ("LuminPackUtf16VarIntOptions", "global::LuminPack.Option.LuminPackUtf16VarIntOptions"),
        ("LuminPackStringEncoding", "global::LuminPack.Option.LuminPackStringEncoding"),
        ("LuminPackStringRecording", "global::LuminPack.Option.LuminPackStringRecording"),
        ("LuminPackCode", "global::LuminPack.Code.LuminPackCode"),
        ("LuminPackFormatter", "global::LuminPack.Code.LuminPackFormatter"),
        ("LuminPackFormatterTable", "global::LuminPack.Code.LuminPackFormatterTable"),
        ("LuminPackConstUtf8", "global::LuminPack.LuminPackConstUtf8"),
        // Short or common names such as Code or Option are not aliased: in an expression, an inherited member of that name
        // from a referenced base type would take precedence, and the consuming compilation cannot report such members.
    };

    /// <summary>The number of helper aliases; bit <c>i</c> of an alias mask selects alias <c>i</c>.</summary>
    internal static int AliasCount => Aliases.Length;

    /// <summary>The mask of a consumer that declares none of the alias names.</summary>
    internal static int AllAliases => (1 << Aliases.Length) - 1;

    /// <summary>Gets an alias name.</summary>
    internal static string AliasName(int index) => Aliases[index].Alias;

    /// <summary>Gets an alias's fully qualified target.</summary>
    internal static string AliasTarget(int index) => Aliases[index].Target;

    /// <summary>Finds an alias by its name.</summary>
    internal static int AliasIndex(string alias)
    {
        for (int index = 0; index < Aliases.Length; index++)
            if (Aliases[index].Alias == alias)
                return index;
        throw new ArgumentException(alias);
    }

    /// <summary>The alias indexes keyed by the last identifier of their target, including attribute names without the suffix.</summary>
    private static readonly Dictionary<string, int> AliasByName = CreateAliasIndex();

    /// <summary>Indexes every alias by the identifier that ends its qualified target.</summary>
    /// <returns>The alias index of each target's last identifier.</returns>
    private static Dictionary<string, int> CreateAliasIndex()
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < Aliases.Length; i++)
        {
            string target = Aliases[i].Target;
            string name = target.Substring(target.LastIndexOf('.') + 1);
            index.Add(name, i);
            if (name.EndsWith("Attribute", StringComparison.Ordinal))
                index.Add(name.Substring(0, name.Length - "Attribute".Length), i);
        }
        return index;
    }

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
    /// <param name="published">The syntax the source was printed from, or null.</param>
    /// <param name="aliases">The aliases available in the consumer's declarations.</param>
    /// <returns>The same source with shorter bound names and compact case rows.</returns>
    private static string Present(string source, CompilationUnitSyntax published, int aliases)
    {
        bool union = source.IndexOf(UnionCases, StringComparison.Ordinal) >= 0;
        bool helpers = aliases != 0 && source.Contains("global::") && Aliases.Any(entry => source.Contains(entry.Target) ||
            entry.Target.EndsWith("Attribute", StringComparison.Ordinal) &&
            source.Contains(entry.Target.Substring(0, entry.Target.Length - "Attribute".Length))) ||
            source.Contains(SystemObject);
        bool json = source.IndexOf(JsonHelper, StringComparison.Ordinal) >= 0;
        bool numbered = AnyNumberedLocal.IsMatch(source);
        if (!union && !helpers && !json && !numbered)
            return source;

        var root = Equivalent(source, published) ? published : SyntaxFactory.ParseCompilationUnit(source, options: ParseOptions);
        if (json)
            root = JsonNames(root);
        // An alias spells its target's simple name, so only a declaration of that name in this file can give it another meaning.
        if (helpers)
            foreach (var token in root.DescendantTokens())
                if (token.IsKind(SyntaxKind.IdentifierToken) && AliasByName.TryGetValue(token.ValueText, out int index) &&
                    Aliases[index].Alias == token.ValueText && Declares(token) && !DeclaresTarget(token, Aliases[index].Target))
                    aliases &= ~(1 << index);
        var layout = new Presentation(helpers ? aliases : 0, union);
        var rewritten = (CompilationUnitSyntax)layout.Visit(root);
        var flat = Flatten(layout.AddUsings(rewritten));
        return Reindent(numbered ? PlainLocals(flat) : flat).ToFullString();
    }

    /// <summary>Checks that published syntax reads as the layout's own parse of its text would.</summary>
    /// <param name="source">The completed generated source.</param>
    /// <param name="published">The syntax the source was printed from, or null.</param>
    /// <returns>Whether the syntax can replace that parse.</returns>
    /// <remarks>The layout parses without preprocessor symbols and with documentation comments. Published syntax was parsed with
    /// the consumer's symbols, and a documentation comment parsed without documentation mode is a plain comment, so a file with a
    /// conditional directive or such a comment is parsed again.</remarks>
    private static bool Equivalent(string source, CompilationUnitSyntax published)
        => published != null && source.IndexOf("#if", StringComparison.Ordinal) < 0 &&
            (source.IndexOf("///", StringComparison.Ordinal) < 0 || !published.DescendantTrivia(descendIntoTrivia: false)
                .Any(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().StartsWith("///", StringComparison.Ordinal)));

    /// <summary>Removes bare nested blocks whose locals cannot meet another use of their names.</summary>
    /// <param name="root">The rewritten compilation unit.</param>
    /// <returns>The unit with those blocks' statements placed in their enclosing blocks.</returns>
    /// <remarks>A block is removed only when every name it declares is declared once in its method and used only inside the
    /// block, and when it holds no label, using declaration or local function, so no name binds differently and no
    /// lifetime ends at another point.</remarks>
    private static CompilationUnitSyntax Flatten(CompilationUnitSyntax root)
    {
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.Body != null && method.Body.DescendantNodes().Any(node => node is BlockSyntax { Parent: BlockSyntax })).ToList();
        return methods.Count == 0 ? root : root.ReplaceNodes(methods, (original, _) =>
        {
            var removable = Removable(original);
            return removable.Count == 0 ? original : (MethodDeclarationSyntax)new Flattening(removable).Visit(original);
        });
    }

    /// <summary>Selects the bare blocks of one method that can be removed.</summary>
    /// <param name="method">The original method.</param>
    /// <returns>The removable blocks.</returns>
    private static HashSet<BlockSyntax> Removable(MethodDeclarationSyntax method)
    {
        var candidates = new List<(BlockSyntax Block, List<string> Names)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in method.Body.DescendantNodes().OfType<BlockSyntax>())
        {
            if (block.Parent is not BlockSyntax || block.Statements.Count == 0 ||
                block.Statements.Any(statement => statement is LabeledStatementSyntax or LocalFunctionStatementSyntax ||
                    statement is LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not 0 }) ||
                block.OpenBraceToken.TrailingTrivia.Concat(block.CloseBraceToken.LeadingTrivia)
                    .Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)) ||
                block.OpenBraceToken.LeadingTrivia.Any(trivia => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia)))
                continue;
            var declared = block.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken) && Declares(token))
                .Select(token => token.ValueText).ToList();
            candidates.Add((block, declared));
            names.UnionWith(declared);
        }
        var removable = new HashSet<BlockSyntax>();
        if (candidates.Count == 0)
            return removable;
        // Only the names declared by candidate blocks need their declarations counted and their uses located.
        var declarations = new Dictionary<string, int>(StringComparer.Ordinal);
        var uses = new Dictionary<string, List<TextSpan>>(StringComparer.Ordinal);
        if (names.Count != 0)
            foreach (var token in method.DescendantTokens())
            {
                if (!token.IsKind(SyntaxKind.IdentifierToken) || !names.Contains(token.ValueText))
                    continue;
                if (Declares(token))
                    declarations[token.ValueText] = declarations.TryGetValue(token.ValueText, out int count) ? count + 1 : 1;
                else if (token.Parent is IdentifierNameSyntax)
                {
                    if (!uses.TryGetValue(token.ValueText, out var spans))
                        uses.Add(token.ValueText, spans = new List<TextSpan>());
                    spans.Add(token.Span);
                }
            }
        foreach (var (block, declared) in candidates)
            if (declared.All(name => declarations[name] == 1 &&
                (!uses.TryGetValue(name, out var spans) || spans.All(span => block.Span.Contains(span)))))
                removable.Add(block);
        return removable;
    }

    /// <summary>Moves every line of a fragment left by shortening its indentation trivia, without printing or parsing it.</summary>
    private sealed class Dedent : CSharpSyntaxRewriter
    {
        private readonly int _columns;

        /// <summary>Captures the indentation to remove.</summary>
        /// <param name="columns">The number of leading spaces each line loses.</param>
        internal Dedent(int columns) => _columns = columns;

        /// <summary>Shortens the whitespace that starts a line before a token or comment.</summary>
        /// <param name="token">A token of the fragment.</param>
        /// <returns>The token with shorter line indentation; multi-line token text is untouched.</returns>
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            var leading = token.LeadingTrivia;
            if (leading.Count == 0)
                return token;
            var trivia = new SyntaxTrivia[leading.Count];
            bool changed = false;
            for (int index = 0; index < leading.Count; index++)
            {
                var item = leading[index];
                bool lineStart = index == 0
                    ? token.GetPreviousToken() is var previous && (previous.RawKind == 0 || previous.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia))
                    : leading[index - 1].IsKind(SyntaxKind.EndOfLineTrivia) || leading[index - 1].IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia);
                if (lineStart && item.IsKind(SyntaxKind.WhitespaceTrivia) && item.Span.Length >= _columns)
                {
                    item = SyntaxFactory.Whitespace(item.ToString().Substring(_columns));
                    changed = true;
                }
                trivia[index] = item;
            }
            return changed ? token.WithLeadingTrivia(trivia) : token;
        }
    }

    /// <summary>Places the statements of removable blocks in their enclosing blocks.</summary>
    private sealed class Flattening : NodeRewriter
    {
        private readonly HashSet<BlockSyntax> _removable;

        /// <summary>Captures the blocks selected on the original method.</summary>
        /// <param name="removable">The original removable blocks.</param>
        internal Flattening(HashSet<BlockSyntax> removable) => _removable = removable;

        /// <summary>Splices removable child blocks after their own children were processed.</summary>
        /// <param name="node">An original block.</param>
        /// <returns>The block with its removable children spliced.</returns>
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            var result = (BlockSyntax)base.VisitBlock(node);
            if (!node.Statements.Any(statement => statement is BlockSyntax block && _removable.Contains(block)))
                return result;
            var statements = new List<StatementSyntax>();
            for (int index = 0; index < node.Statements.Count; index++)
            {
                if (node.Statements[index] is BlockSyntax original && _removable.Contains(original) && result.Statements[index] is BlockSyntax inner)
                {
                    var braceTrivia = inner.OpenBraceToken.LeadingTrivia;
                    int column = braceTrivia.Reverse().TakeWhile(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).Sum(trivia => trivia.Span.Length);
                    // Comments before the brace stay above the first statement, indented like it.
                    var comments = braceTrivia.Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)).ToList();
                    for (int position = 0; position < inner.Statements.Count; position++)
                    {
                        var statement = inner.Statements[position];
                        if (position == 0 && comments.Count != 0)
                        {
                            var indentation = statement.GetLeadingTrivia().Where(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).ToList();
                            var leading = new List<SyntaxTrivia>();
                            foreach (var comment in comments)
                            {
                                leading.AddRange(indentation.Skip(indentation.Count - 1));
                                leading.Add(comment);
                                leading.Add(SyntaxFactory.EndOfLine("\n"));
                            }
                            statement = statement.WithLeadingTrivia(leading.Concat(statement.GetLeadingTrivia()));
                        }
                        int depth = statement.GetLeadingTrivia().Reverse().TakeWhile(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).Sum(trivia => trivia.Span.Length);
                        statements.Add(depth > column ? (StatementSyntax)new Dedent(depth - column).Visit(statement) : statement);
                    }
                }
                else
                    statements.Add(result.Statements[index]);
            }
            return result.WithStatements(SyntaxFactory.List(statements));
        }
    }

    /// <summary>Recognizes a partial declaration of the alias's own target, which gives the name no other meaning.</summary>
    /// <param name="token">A declaring identifier.</param>
    /// <param name="target">The alias target.</param>
    /// <returns>Whether the token declares exactly the target type.</returns>
    private static bool DeclaresTarget(SyntaxToken token, string target)
        => token.Parent is BaseTypeDeclarationSyntax type && type.Parent is BaseNamespaceDeclarationSyntax container &&
            "global::" + container.Name + "." + token.ValueText == target;

    /// <summary>Recognizes an identifier that declares a name rather than referring to one.</summary>
    /// <param name="token">An identifier of the generated file.</param>
    /// <returns>Whether the token names a declared type, member, parameter, local, label or alias.</returns>
    private static bool Declares(SyntaxToken token) => token.Parent switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier == token,
        DelegateDeclarationSyntax type => type.Identifier == token,
        MethodDeclarationSyntax method => method.Identifier == token,
        PropertyDeclarationSyntax property => property.Identifier == token,
        EventDeclarationSyntax member => member.Identifier == token,
        VariableDeclaratorSyntax variable => variable.Identifier == token,
        ParameterSyntax parameter => parameter.Identifier == token,
        TypeParameterSyntax parameter => parameter.Identifier == token,
        LocalFunctionStatementSyntax function => function.Identifier == token,
        SingleVariableDesignationSyntax designation => designation.Identifier == token,
        ForEachStatementSyntax iteration => iteration.Identifier == token,
        CatchDeclarationSyntax exception => exception.Identifier == token,
        LabeledStatementSyntax label => label.Identifier == token,
        _ => token.Parent?.Parent is NameEqualsSyntax
    };

    /// <summary>Moves members that a whitespace normalization left at another depth to the depth of their declaration.</summary>
    /// <param name="root">The rewritten compilation unit.</param>
    /// <returns>The unit with every type member indented by four spaces per enclosing type or block namespace.</returns>
    private static CompilationUnitSyntax Reindent(CompilationUnitSyntax root)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            var misplaced = root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax)
                .OfType<MemberDeclarationSyntax>().Where(member => member.Parent is TypeDeclarationSyntax && Indentation(member) is int actual &&
                    actual != Depth(member) * 4).ToList();
            if (misplaced.Count == 0)
                return root;
            root = root.ReplaceNodes(misplaced, (original, _) => SyntaxFactory.ParseMemberDeclaration(
                GeneratedSourceIndentation.At(original, Depth(original) * 4), options: ParseOptions) ?? original);
        }
        return root;
    }

    /// <summary>Counts the declarations that indent a member.</summary>
    /// <param name="member">A type member.</param>
    /// <returns>The number of enclosing types and block-scoped namespaces.</returns>
    private static int Depth(SyntaxNode member)
        => member.Ancestors().Count(node => node is TypeDeclarationSyntax or NamespaceDeclarationSyntax);

    /// <summary>Measures the indentation of the line on which a member starts.</summary>
    /// <param name="member">A type member.</param>
    /// <returns>The member's column, or null when it does not start its line.</returns>
    private static int? Indentation(MemberDeclarationSyntax member)
    {
        var leading = member.GetFirstToken().LeadingTrivia;
        int column = 0;
        for (int index = leading.Count - 1; index >= 0; index--)
        {
            if (leading[index].IsKind(SyntaxKind.WhitespaceTrivia))
                column += leading[index].Span.Length;
            else
                return leading[index].IsKind(SyntaxKind.EndOfLineTrivia) || leading[index].IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    ? column : null;
        }
        var previous = member.GetFirstToken().GetPreviousToken();
        return previous.RawKind == 0 || previous.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia) ? column : null;
    }

    /// <summary>Shortens only exact global-qualified helper names and whitespace-only Union arms.</summary>
    private sealed class Presentation : NodeRewriter
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

        /// <summary>Writes a forwarding method as an expression-bodied member.</summary>
        /// <param name="node">The method.</param>
        /// <returns>The method with an expression body when its block holds one plain statement.</returns>
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var result = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
            return result.Body is { } body && Forwarded(body) is { } expression
                ? Arrow(result, result.ParameterList, result.ConstraintClauses).WithBody(null)
                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken)
                        .WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space), expression))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(body.CloseBraceToken.TrailingTrivia))
                : result;
        }

        /// <summary>Writes a forwarding local function as an expression-bodied function.</summary>
        /// <param name="node">The local function.</param>
        /// <returns>The function with an expression body when its block holds one plain statement.</returns>
        public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var result = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node);
            return result.Body is { } body && Forwarded(body) is { } expression
                ? Arrow(result, result.ParameterList, result.ConstraintClauses).WithBody(null)
                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken)
                        .WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space), expression))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(body.CloseBraceToken.TrailingTrivia))
                : result;
        }

        /// <summary>Finds the only expression of a block that just evaluates or returns it.</summary>
        /// <param name="body">A method or local function body.</param>
        /// <returns>The expression, or null for any other body, including one with comments or directives.</returns>
        private static ExpressionSyntax Forwarded(BlockSyntax body)
        {
            if (body.Statements.Count != 1 || body.DescendantTrivia().Any(trivia =>
                !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                return null;
            var expression = body.Statements[0] switch
            {
                ExpressionStatementSyntax statement => statement.Expression,
                ReturnStatementSyntax { Expression: { } value } when value is not RefExpressionSyntax => value,
                _ => null
            };
            // An expression that spans lines keeps its block, whose layout already reads well.
            return expression == null || expression.ToString().Contains('\n') ? null : expression.WithoutTrivia();
        }

        /// <summary>Joins the declaration head to the arrow on its last line.</summary>
        /// <typeparam name="T">The declaration kind.</typeparam>
        /// <param name="declaration">The declaration that still has its block.</param>
        /// <param name="parameters">The declaration's parameters.</param>
        /// <param name="constraints">The declaration's constraint clauses.</param>
        /// <returns>The declaration whose head no longer ends in a line break.</returns>
        private static T Arrow<T>(T declaration, ParameterListSyntax parameters, SyntaxList<TypeParameterConstraintClauseSyntax> constraints)
            where T : SyntaxNode
        {
            var last = constraints.Count != 0 ? constraints.Last().GetLastToken() : parameters.CloseParenToken;
            return declaration.ReplaceToken(last, last.WithTrailingTrivia());
        }

        /// <summary>Puts a constant array, such as a property-name table, on one line.</summary>
        /// <param name="node">The array creation.</param>
        /// <returns>The same array with one row of elements, or the ordinary traversal.</returns>
        /// <remarks>A byte literal needs no cast in a byte array, because an in-range integer constant converts implicitly.</remarks>
        public override SyntaxNode VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
        {
            var result = (ArrayCreationExpressionSyntax)base.VisitArrayCreationExpression(node);
            if (result.Initializer is not { Expressions.Count: > 0 } initializer || !initializer.ToFullString().Contains('\n') ||
                !initializer.Expressions.All(element => element is LiteralExpressionSyntax || element is CastExpressionSyntax { Expression: LiteralExpressionSyntax }))
                return result;
            bool bytes = result.Type.ElementType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ByteKeyword };
            var elements = initializer.Expressions.Select(element => (bytes && element is CastExpressionSyntax
                { Type: PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ByteKeyword }, Expression: LiteralExpressionSyntax literal } &&
                literal.Token.Value is int value && value is >= 0 and <= byte.MaxValue ? literal : element).WithoutTrivia().ToString());
            return SyntaxFactory.ParseExpression("new " + result.Type.WithoutTrivia() + " { " + string.Join(", ", elements) + " }").WithTriviaFrom(result);
        }

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
        /// <returns>The alias or predefined type, or null for a name to leave intact.</returns>
        private ExpressionSyntax Shorten(SyntaxNode node, string name)
        {
            if (name == "Object" && node.Parent is not QualifiedNameSyntax && node.ToString() == SystemObject)
                return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)).WithTriviaFrom(node);
            if (!AliasByName.TryGetValue(name, out int index) || (_available & (1 << index)) == 0)
                return null;
            string path = node.ToString(), target = Aliases[index].Target;
            bool attribute = node.Parent is AttributeSyntax && target.EndsWith("Attribute", StringComparison.Ordinal);
            if (path != target && !(attribute && path + "Attribute" == target))
                return null;
            _used |= 1 << index;
            // Escaping an attribute alias bypasses attribute-suffix lookup.
            return SyntaxFactory.IdentifierName(attribute ? "@" + Aliases[index].Alias : Aliases[index].Alias).WithTriviaFrom(node);
        }

        /// <summary>Places one ordinary case on its own line without moving comments or directives.</summary>
        /// <param name="section">A switch section inside a marked Union.</param>
        /// <returns>The original section or its single-line equivalent.</returns>
        private static SwitchSectionSyntax Compact(SwitchSectionSyntax section)
        {
            if (section.Labels.Count != 1 || section.Labels[0].IsKind(SyntaxKind.DefaultSwitchLabel) ||
                section.DescendantTrivia().Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                return section;
            var indent = section.GetLeadingTrivia().Reverse().TakeWhile(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).Reverse().ToList();
            // An expanded case body stays a block: only a case that reads as one table row is joined.
            if (section.Span.Length > MaxCaseWidth * 4)
                return section;
            var compact = section.NormalizeWhitespace(string.Empty, " ");
            if (indent.Sum(trivia => trivia.Span.Length) + compact.Span.Length > MaxCaseWidth)
                return section;
            return compact.WithLeadingTrivia(indent).WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine));
        }
    }
}
