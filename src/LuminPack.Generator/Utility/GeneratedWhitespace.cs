using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Lays out generated syntax with the line, indentation and spacing rules of the compiler's whitespace normalizer.</summary>
/// <remarks>
/// <c>NormalizeWhitespace</c> finds the token that follows every token by walking the tree again, which made it the largest
/// single cost of formatting a serializer unit. This rewriter lists the tokens once, decides each line break and separator
/// from a token and its successor, and tracks indentation while it descends. It replaces only whitespace and line breaks;
/// comments, directives, annotations and every other node and token are kept. Trivia is placed where a parser of the printed
/// text places it: a token's trailing trivia ends with its first line break, and the breaks of following blank lines lead the
/// next token, so later stages can use the laid-out tree in place of a parse of its text.
/// </remarks>
internal sealed class GeneratedWhitespace : CSharpSyntaxRewriter
{
    private readonly SyntaxToken[] _tokens;
    private readonly int[] _next;
    private readonly SyntaxTrivia _newLine;
    private readonly List<SyntaxTrivia> _indentation = new List<SyntaxTrivia>();
    private readonly Stack<int> _depths = new Stack<int>();
    private readonly List<SyntaxTrivia> _buffer = new List<SyntaxTrivia>();
    private readonly List<SyntaxTriviaList> _indentationLists = new List<SyntaxTriviaList>();
    private readonly SyntaxTriviaList _space = SyntaxFactory.TriviaList(SyntaxFactory.Space);
    private readonly SyntaxTriviaList _lineBreak;
    private int _index;
    private bool _afterLineBreak = true;
    // The line breaks of blank lines after a token, which a parser places in the next token's leading trivia.
    private int _pendingBreaks;

    /// <summary>Lists the tokens of the unit and their relevant successors.</summary>
    /// <param name="root">The unit to lay out.</param>
    /// <param name="newLine">The line terminator.</param>
    private GeneratedWhitespace(SyntaxNode root, string newLine)
    {
        var tokens = new List<SyntaxToken>();
        foreach (var token in root.DescendantTokens())
            tokens.Add(token);
        _tokens = tokens.ToArray();
        // A zero-width token, including the end of the file, is never a successor, as in the compiler's normalizer.
        _next = new int[_tokens.Length];
        int following = -1;
        for (int index = _tokens.Length - 1; index >= 0; index--)
        {
            _next[index] = following;
            if (_tokens[index].Span.Length > 0)
                following = index;
        }
        _newLine = SyntaxFactory.EndOfLine(newLine);
        _lineBreak = SyntaxFactory.TriviaList(_newLine);
        _depths.Push(0);
    }

    /// <summary>Formats a generated unit with four-space indentation.</summary>
    /// <typeparam name="T">The syntax type.</typeparam>
    /// <param name="root">The generated syntax.</param>
    /// <param name="newLine">The line terminator.</param>
    /// <returns>The same syntax with laid-out whitespace.</returns>
    internal static T Normalize<T>(T root, string newLine) where T : SyntaxNode
        => (T)new GeneratedWhitespace(root, newLine).Visit(root);

    /// <summary>Tracks the indentation depth of the node while its tokens are visited.</summary>
    /// <param name="node">A node of the original tree.</param>
    /// <returns>The node with laid-out tokens.</returns>
    public override SyntaxNode Visit(SyntaxNode node)
    {
        if (node == null)
            return null;
        _depths.Push(Depth(node, _depths.Peek()));
        var result = base.Visit(node);
        _depths.Pop();
        return result;
    }

    /// <summary>Computes the indentation depth of a node from its parent's depth.</summary>
    /// <param name="node">The node.</param>
    /// <param name="parentDepth">The depth of its parent.</param>
    /// <returns>The node's depth.</returns>
    private static int Depth(SyntaxNode node, int parentDepth)
    {
        var parent = node.Parent;
        if (parent == null || parent.IsKind(SyntaxKind.CompilationUnit))
            return 0;
        if (parent.IsKind(SyntaxKind.GlobalStatement))
            return parentDepth;
        if (node.IsKind(SyntaxKind.IfStatement) && parent.IsKind(SyntaxKind.ElseClause))
            return parentDepth;
        if (parent is BlockSyntax)
            return parentDepth + 1;
        if (parent is InitializerExpressionSyntax or AnonymousObjectMemberDeclaratorSyntax)
            return parentDepth + 1;
        if (node is StatementSyntax and not BlockSyntax)
            return parentDepth + 1;
        if (node is MemberDeclarationSyntax or AccessorDeclarationSyntax or TypeParameterConstraintClauseSyntax or SwitchSectionSyntax or
            UsingDirectiveSyntax or ExternAliasDirectiveSyntax or QueryExpressionSyntax or QueryContinuationSyntax)
            return parentDepth + 1;
        return parentDepth;
    }

    /// <summary>Lays out one token's leading and trailing whitespace.</summary>
    /// <param name="token">An original token.</param>
    /// <returns>The token with replaced whitespace.</returns>
    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        // Absent optional tokens are visited too, but they are not part of the listed unit.
        if (token.IsKind(SyntaxKind.None))
            return token;
        int index = Locate(token);
        if (token.IsMissing && token.FullSpan.Length == 0)
            return token;
        int depth = _depths.Peek();
        var next = index >= 0 && _next[index] >= 0 ? _tokens[_next[index]] : default;
        var leading = Leading(token.LeadingTrivia, depth, token.IsKind(SyntaxKind.EndOfFileToken));
        int lineBreaks = LineBreaksAfter(token, next);
        if (lineBreaks == 0 && next.HasLeadingTrivia && StartsLine(next.LeadingTrivia))
            lineBreaks = 1;
        var trailing = Trailing(token.TrailingTrivia, lineBreaks, NeedsSeparator(token, next));
        bool sameLeading = Same(token.LeadingTrivia, leading), sameTrailing = Same(token.TrailingTrivia, trailing);
        if (sameLeading)
            return sameTrailing ? token : token.WithTrailingTrivia(trailing);
        token = token.WithLeadingTrivia(leading);
        return sameTrailing ? token : token.WithTrailingTrivia(trailing);
    }

    /// <summary>Compares trivia text so that already laid-out tokens are not rebuilt.</summary>
    /// <param name="current">The token's trivia.</param>
    /// <param name="laidOut">The required trivia.</param>
    /// <returns>Whether both lists hold the same trivia kinds and text.</returns>
    private static bool Same(SyntaxTriviaList current, SyntaxTriviaList laidOut)
    {
        if (current.Count != laidOut.Count)
            return false;
        for (int index = 0; index < current.Count; index++)
        {
            var left = current[index];
            var right = laidOut[index];
            if (left.RawKind != right.RawKind || left.HasStructure || right.HasStructure || left.FullSpan.Length != right.FullSpan.Length ||
                left.ToString() != right.ToString())
                return false;
        }
        return true;
    }

    /// <summary>Recognizes trivia that holds more than whitespace and line breaks.</summary>
    /// <param name="trivia">A token's trivia.</param>
    /// <returns>Whether a comment, directive or other trivia must be kept.</returns>
    private static bool Significant(SyntaxTriviaList trivia)
    {
        foreach (var item in trivia)
            if (!item.IsKind(SyntaxKind.WhitespaceTrivia) && !item.IsKind(SyntaxKind.EndOfLineTrivia))
                return true;
        return false;
    }

    /// <summary>Gets the cached leading trivia holding only the indentation of a depth.</summary>
    /// <param name="depth">The indentation depth, at least one.</param>
    /// <returns>The single-trivia list.</returns>
    private SyntaxTriviaList IndentationList(int depth)
    {
        while (_indentationLists.Count <= depth)
            _indentationLists.Add(_indentationLists.Count == 0 ? default : SyntaxFactory.TriviaList(Indentation(_indentationLists.Count)));
        return _indentationLists[depth];
    }

    /// <summary>Finds the token in the precomputed order, which matches the rewriter's visit order.</summary>
    /// <param name="token">The visited token.</param>
    /// <returns>Its index, or -1 when it is not part of the listed unit.</returns>
    private int Locate(SyntaxToken token)
    {
        if (_index < _tokens.Length && _tokens[_index] == token)
            return _index++;
        for (int index = 0; index < _tokens.Length; index++)
            if (_tokens[index] == token)
            {
                _index = index + 1;
                return index;
            }
        return -1;
    }

    /// <summary>Gets the cached indentation for a depth.</summary>
    /// <param name="depth">The indentation depth.</param>
    /// <returns>Four spaces per level.</returns>
    private SyntaxTrivia Indentation(int depth)
    {
        while (_indentation.Count <= depth)
            _indentation.Add(SyntaxFactory.Whitespace(new string(' ', _indentation.Count * 4)));
        return _indentation[depth];
    }

    /// <summary>Keeps comments and directives before a token, each on its own indented line when the token starts a line.</summary>
    /// <param name="trivia">The original leading trivia.</param>
    /// <param name="depth">The token's indentation depth.</param>
    /// <param name="endOfFile">Whether the token ends the file and needs no indentation.</param>
    /// <returns>The laid-out leading trivia.</returns>
    private SyntaxTriviaList Leading(SyntaxTriviaList trivia, int depth, bool endOfFile)
    {
        bool lineStart = _afterLineBreak;
        int pending = _pendingBreaks;
        _pendingBreaks = 0;
        if (!Significant(trivia) && pending == 0)
        {
            _afterLineBreak = false;
            return !endOfFile && lineStart && depth != 0 ? IndentationList(depth) : default;
        }
        _buffer.Clear();
        for (int count = 0; count < pending; count++)
            _buffer.Add(_newLine);
        foreach (var item in trivia)
        {
            if (item.IsKind(SyntaxKind.WhitespaceTrivia) || item.IsKind(SyntaxKind.EndOfLineTrivia))
                continue;
            if (item.IsDirective)
            {
                if (!lineStart)
                    _buffer.Add(_newLine);
                _buffer.Add(item);
                lineStart = EndsLine(item);
                if (!lineStart)
                {
                    _buffer.Add(_newLine);
                    lineStart = true;
                }
                continue;
            }
            if (lineStart && depth != 0)
                _buffer.Add(Indentation(depth));
            else if (!lineStart)
                _buffer.Add(SyntaxFactory.Space);
            _buffer.Add(item);
            if (EndsLine(item))
                lineStart = true;
            else if (item.IsKind(SyntaxKind.SingleLineCommentTrivia) || item.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            {
                _buffer.Add(_newLine);
                lineStart = true;
            }
            else
                lineStart = false;
        }
        if (!endOfFile)
        {
            if (lineStart && depth != 0)
                _buffer.Add(Indentation(depth));
            else if (!lineStart && _buffer.Count != 0)
                _buffer.Add(SyntaxFactory.Space);
        }
        _afterLineBreak = false;
        return SyntaxFactory.TriviaList(_buffer);
    }

    /// <summary>Keeps trailing comments and ends the token with its separator or line breaks.</summary>
    /// <param name="trivia">The original trailing trivia.</param>
    /// <param name="lineBreaks">The line breaks required after the token.</param>
    /// <param name="separator">Whether a space must separate the token from its successor.</param>
    /// <returns>The laid-out trailing trivia.</returns>
    private SyntaxTriviaList Trailing(SyntaxTriviaList trivia, int lineBreaks, bool separator)
    {
        if (!Significant(trivia))
        {
            _afterLineBreak = lineBreaks > 0;
            _pendingBreaks = Math.Max(0, lineBreaks - 1);
            return lineBreaks > 0 ? _lineBreak : separator ? _space : default;
        }
        _buffer.Clear();
        bool lineEnded = false;
        foreach (var item in trivia)
        {
            if (item.IsKind(SyntaxKind.WhitespaceTrivia) || item.IsKind(SyntaxKind.EndOfLineTrivia))
                continue;
            _buffer.Add(SyntaxFactory.Space);
            _buffer.Add(item);
            lineEnded = EndsLine(item);
            if (!lineEnded && (item.IsKind(SyntaxKind.SingleLineCommentTrivia) || item.IsDirective))
                lineBreaks = Math.Max(lineBreaks, 1);
        }
        if (lineEnded)
        {
            lineBreaks--;
            // The trivia's own line break ends the trailing trivia.
            _pendingBreaks = Math.Max(0, lineBreaks);
            lineBreaks = 0;
        }
        if (lineBreaks > 0)
        {
            _buffer.Add(_newLine);
            _pendingBreaks = lineBreaks - 1;
            _afterLineBreak = true;
        }
        else
        {
            if (separator && !lineEnded)
                _buffer.Add(SyntaxFactory.Space);
            _afterLineBreak = lineEnded;
        }
        return SyntaxFactory.TriviaList(_buffer);
    }

    /// <summary>Recognizes trivia, such as a documentation comment, whose text already ends its line.</summary>
    /// <param name="trivia">Comment or directive trivia.</param>
    /// <returns>Whether the trivia text ends with a line break.</returns>
    private static bool EndsLine(SyntaxTrivia trivia)
    {
        if (!trivia.HasStructure)
            return false;
        string text = trivia.ToFullString();
        return text.Length != 0 && text[text.Length - 1] == '\n';
    }

    /// <summary>Recognizes leading trivia that cannot follow other code on the same line.</summary>
    /// <param name="trivia">The leading trivia of the successor.</param>
    /// <returns>Whether it holds a directive or a line comment.</returns>
    private static bool StartsLine(SyntaxTriviaList trivia)
    {
        foreach (var item in trivia)
            if (item.IsDirective || item.IsKind(SyntaxKind.SingleLineCommentTrivia) || item.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                item.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                return true;
        return false;
    }

    /// <summary>Counts the line breaks after a token.</summary>
    /// <param name="current">The token.</param>
    /// <param name="next">Its relevant successor, or none at the end of the unit.</param>
    /// <returns>Zero, one, or two for a following blank line.</returns>
    private static int LineBreaksAfter(SyntaxToken current, SyntaxToken next)
    {
        if (next.IsKind(SyntaxKind.None))
            return 0;
        switch (current.Kind())
        {
            case SyntaxKind.OpenBraceToken:
                return KeepsBracesInline(current) ? 0 : 1;
            case SyntaxKind.FinallyKeyword:
                return 1;
            case SyntaxKind.CloseBraceToken:
                return LineBreaksAfterCloseBrace(current, next);
            case SyntaxKind.CloseParenToken:
                return current.Parent is StatementSyntax && next.Parent != current.Parent ||
                    next.IsKind(SyntaxKind.OpenBraceToken) || next.IsKind(SyntaxKind.WhereKeyword) ? 1 : 0;
            case SyntaxKind.CloseBracketToken:
                if (current.Parent is AttributeListSyntax && current.Parent.Parent is not ParameterSyntax)
                    return 1;
                break;
            case SyntaxKind.SemicolonToken:
                return LineBreaksAfterSemicolon(current, next);
            case SyntaxKind.CommaToken:
                return current.Parent is EnumDeclarationSyntax || current.Parent is InitializerExpressionSyntax && !IsInlineInitializer(current.Parent) ? 1 : 0;
            case SyntaxKind.ElseKeyword:
                return next.IsKind(SyntaxKind.IfKeyword) ? 0 : 1;
            case SyntaxKind.ColonToken:
                if (current.Parent is LabeledStatementSyntax or SwitchLabelSyntax)
                    return 1;
                break;
        }
        switch (next.Kind())
        {
            case SyntaxKind.OpenBraceToken:
            case SyntaxKind.CloseBraceToken:
                return KeepsBracesInline(next) ? 0 : 1;
            case SyntaxKind.ElseKeyword:
            case SyntaxKind.FinallyKeyword:
                return 1;
            case SyntaxKind.OpenBracketToken:
                return next.Parent is AttributeListSyntax && next.Parent.Parent is not ParameterSyntax ? 1 : 0;
            case SyntaxKind.WhereKeyword:
                return current.Parent is TypeParameterListSyntax ? 1 : 0;
        }
        return 0;
    }

    /// <summary>Keeps the braces of interpolations, patterns and body-less accessor lists on the code's line.</summary>
    /// <param name="brace">An opening or closing brace.</param>
    /// <returns>Whether the brace does not start or end a line.</returns>
    private static bool KeepsBracesInline(SyntaxToken brace)
        => brace.Parent is InterpolationSyntax or PropertyPatternClauseSyntax || IsInlineInitializer(brace.Parent) ||
           brace.Parent is AccessorListSyntax accessors && WithoutBodies(accessors);

    /// <summary>Recognizes object and collection initializers, which stay on their line; array initializers list one element per line.</summary>
    /// <param name="node">A brace or comma parent.</param>
    /// <returns>Whether the node is an initializer laid out inline.</returns>
    private static bool IsInlineInitializer(SyntaxNode node)
    {
        if (node is not InitializerExpressionSyntax)
            return false;
        // An initializer passed as an argument or nested in another initializer stays on its line; elsewhere it lists one
        // element per line.
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (current is ArgumentSyntax or AttributeArgumentSyntax or InterpolationSyntax or InitializerExpressionSyntax)
                return true;
            if (current is not ExpressionSyntax)
                return false;
        }
        return false;
    }

    /// <summary>Recognizes an accessor list whose accessors have no block bodies.</summary>
    /// <param name="accessors">The accessor list.</param>
    /// <returns>Whether the list stays on one line.</returns>
    private static bool WithoutBodies(AccessorListSyntax accessors)
    {
        foreach (var accessor in accessors.Accessors)
            if (accessor.Body != null)
                return false;
        return true;
    }

    /// <summary>Separates a closed block from the next statement or member by a blank line.</summary>
    /// <param name="current">A closing brace.</param>
    /// <param name="next">Its successor.</param>
    /// <returns>The line breaks after the brace.</returns>
    private static int LineBreaksAfterCloseBrace(SyntaxToken current, SyntaxToken next)
    {
        if (current.Parent is InterpolationSyntax or PropertyPatternClauseSyntax or InitializerExpressionSyntax ||
            current.Parent?.Parent is AnonymousFunctionExpressionSyntax)
            return 0;
        if (current.Parent is AccessorListSyntax)
            return 1;
        switch (next.Kind())
        {
            case SyntaxKind.EndOfFileToken:
            case SyntaxKind.CloseBraceToken:
            case SyntaxKind.CatchKeyword:
            case SyntaxKind.FinallyKeyword:
            case SyntaxKind.ElseKeyword:
                return 1;
            case SyntaxKind.WhileKeyword when next.Parent.IsKind(SyntaxKind.DoStatement):
                return 1;
            case SyntaxKind.SemicolonToken:
                return 0;
            default:
                return 2;
        }
    }

    /// <summary>Ends statements and directives on their line, keeping a for header together.</summary>
    /// <param name="current">A semicolon.</param>
    /// <param name="next">Its successor.</param>
    /// <returns>The line breaks after the semicolon.</returns>
    private static int LineBreaksAfterSemicolon(SyntaxToken current, SyntaxToken next)
    {
        if (current.Parent is ForStatementSyntax)
            return 0;
        if (next.IsKind(SyntaxKind.CloseBraceToken))
            return current.Parent is AccessorDeclarationSyntax { Parent: AccessorListSyntax list } && WithoutBodies(list) ? 0 : 1;
        if (current.Parent is UsingDirectiveSyntax)
            return next.Parent is UsingDirectiveSyntax ? 1 : 2;
        if (current.Parent is ExternAliasDirectiveSyntax)
            return next.Parent is ExternAliasDirectiveSyntax ? 1 : 2;
        if (current.Parent is AccessorDeclarationSyntax { Parent: AccessorListSyntax accessors } && WithoutBodies(accessors))
            return 0;
        // An expression-bodied property is set apart from a following member of another kind.
        if (current.Parent is BasePropertyDeclarationSyntax && StartedMember(next) is { } following)
            return following.IsKind(current.Parent.Kind()) ? 1 : 2;
        return 1;
    }

    /// <summary>Finds the member declaration that a token starts.</summary>
    /// <param name="token">The first token of a line.</param>
    /// <returns>The member, or null when the token does not start one.</returns>
    private static MemberDeclarationSyntax StartedMember(SyntaxToken token)
    {
        for (var node = token.Parent; node != null && node.SpanStart == token.SpanStart; node = node.Parent)
            if (node is MemberDeclarationSyntax member)
                return member;
        return null;
    }

    /// <summary>Decides whether a space separates two tokens on one line.</summary>
    /// <param name="token">The token.</param>
    /// <param name="next">Its successor.</param>
    /// <returns>Whether a space follows the token.</returns>
    private static bool NeedsSeparator(SyntaxToken token, SyntaxToken next)
    {
        if (token.Parent == null || next.Parent == null)
            return false;
        // A property pattern and a body-less accessor list read as `{ Count: 0 }` and `{ get; }` on the code's line.
        if (token.IsKind(SyntaxKind.OpenBraceToken) && (token.Parent is PropertyPatternClauseSyntax || IsInlineInitializer(token.Parent)) ||
            next.IsKind(SyntaxKind.CloseBraceToken) && (next.Parent is PropertyPatternClauseSyntax || IsInlineInitializer(next.Parent)) ||
            next.IsKind(SyntaxKind.OpenBraceToken) && IsInlineInitializer(next.Parent))
            return true;
        if (token.IsKind(SyntaxKind.CloseBraceToken) && token.Parent is PropertyPatternClauseSyntax && IsWord(next.Kind()))
            return true;
        if (next.IsKind(SyntaxKind.OpenBraceToken) && next.Parent is AccessorListSyntax)
            return true;
        if (token.IsKind(SyntaxKind.OpenBraceToken) && token.Parent is AccessorListSyntax)
            return !next.IsKind(SyntaxKind.OpenBracketToken);
        if (token.Parent is AssignmentExpressionSyntax && IsAssignment(token.Kind()) ||
            next.Parent is AssignmentExpressionSyntax && IsAssignment(next.Kind()) ||
            token.Parent is BinaryExpressionSyntax && IsBinary(token.Kind()) ||
            next.Parent is BinaryExpressionSyntax && IsBinary(next.Kind()))
            return true;
        if (token.IsKind(SyntaxKind.GreaterThanToken) && token.Parent.IsKind(SyntaxKind.TypeArgumentList) && !SyntaxFacts.IsPunctuation(next.Kind()))
            return true;
        if (token.IsKind(SyntaxKind.CommaToken) && !next.IsKind(SyntaxKind.CommaToken) && !token.Parent.IsKind(SyntaxKind.EnumDeclaration))
            return true;
        if (token.IsKind(SyntaxKind.SemicolonToken) && !next.IsKind(SyntaxKind.SemicolonToken) && !next.IsKind(SyntaxKind.CloseParenToken))
            return true;
        if (token.IsKind(SyntaxKind.QuestionToken) && (token.Parent is ConditionalExpressionSyntax || token.Parent is TypeSyntax) &&
            !token.Parent.Parent.IsKind(SyntaxKind.TypeArgumentList))
            return true;
        if (token.IsKind(SyntaxKind.ColonToken))
            return !token.Parent.IsKind(SyntaxKind.InterpolationFormatClause);
        if (next.IsKind(SyntaxKind.ColonToken) && (next.Parent.IsKind(SyntaxKind.BaseList) || next.Parent.IsKind(SyntaxKind.TypeParameterConstraintClause) ||
            next.Parent is ConstructorInitializerSyntax))
            return true;
        if (token.IsKind(SyntaxKind.CloseBracketToken) && IsWord(next.Kind()))
            return true;
        if (token.IsKind(SyntaxKind.CloseParenToken) && IsWord(next.Kind()) && token.Parent.IsKind(SyntaxKind.TupleType))
            return true;
        if ((next.IsKind(SyntaxKind.QuestionToken) || next.IsKind(SyntaxKind.ColonToken)) && next.Parent.IsKind(SyntaxKind.ConditionalExpression))
            return true;
        if (token.IsKind(SyntaxKind.EqualsToken) || next.IsKind(SyntaxKind.EqualsToken))
            return true;
        if (token.IsKind(SyntaxKind.EqualsGreaterThanToken) || next.IsKind(SyntaxKind.EqualsGreaterThanToken))
            return true;
        if (SyntaxFacts.IsLiteralExpression(token.Kind()) && SyntaxFacts.IsLiteralExpression(next.Kind()))
            return true;
        if (next.IsKind(SyntaxKind.AsteriskToken) && next.Parent is PointerTypeSyntax)
            return false;
        if (token.IsKind(SyntaxKind.AsteriskToken) && token.Parent is PointerTypeSyntax &&
            (next.IsKind(SyntaxKind.IdentifierToken) || next.Parent.IsKind(SyntaxKind.IndexerDeclaration)))
            return true;
        if (SyntaxFacts.IsKeywordKind(token.Kind()))
        {
            if (!next.IsKind(SyntaxKind.ColonToken) && !next.IsKind(SyntaxKind.DotToken) && !next.IsKind(SyntaxKind.QuestionToken) &&
                !next.IsKind(SyntaxKind.SemicolonToken) && !next.IsKind(SyntaxKind.OpenBracketToken) &&
                (!next.IsKind(SyntaxKind.OpenParenToken) || KeywordNeedsSeparatorBeforeOpenParen(token.Kind()) || next.Parent.IsKind(SyntaxKind.TupleType)) &&
                !next.IsKind(SyntaxKind.CloseParenToken) && !next.IsKind(SyntaxKind.CloseBraceToken) && !next.IsKind(SyntaxKind.ColonColonToken) &&
                !next.IsKind(SyntaxKind.GreaterThanToken) && !next.IsKind(SyntaxKind.CommaToken))
                return true;
        }
        if (IsWord(token.Kind()) && IsWord(next.Kind()))
            return true;
        if (token.Span.Length > 1 && next.Span.Length > 1)
        {
            string text = token.Text;
            char last = text[text.Length - 1];
            if (last == next.Text[0] && last is '+' or '-' or '<' or '>' or '&' or '|' or '=' or ':')
                return true;
        }
        return false;
    }

    /// <summary>Recognizes a word token.</summary>
    /// <param name="kind">The token kind.</param>
    /// <returns>Whether the token is an identifier or keyword.</returns>
    private static bool IsWord(SyntaxKind kind) => kind == SyntaxKind.IdentifierToken || SyntaxFacts.IsKeywordKind(kind);

    /// <summary>Lists keywords that read as calls before an opening parenthesis.</summary>
    /// <param name="kind">The keyword.</param>
    /// <returns>Whether a space separates the keyword from a parenthesis.</returns>
    private static bool KeywordNeedsSeparatorBeforeOpenParen(SyntaxKind kind) => kind is not (SyntaxKind.TypeOfKeyword or SyntaxKind.DefaultKeyword or
        SyntaxKind.NewKeyword or SyntaxKind.BaseKeyword or SyntaxKind.ThisKeyword or SyntaxKind.CheckedKeyword or SyntaxKind.UncheckedKeyword or
        SyntaxKind.SizeOfKeyword or SyntaxKind.ArgListKeyword);

    /// <summary>Recognizes assignment operators.</summary>
    /// <param name="kind">The token kind.</param>
    /// <returns>Whether the token assigns.</returns>
    private static bool IsAssignment(SyntaxKind kind) => kind is SyntaxKind.EqualsToken or SyntaxKind.PlusEqualsToken or SyntaxKind.MinusEqualsToken or
        SyntaxKind.AsteriskEqualsToken or SyntaxKind.SlashEqualsToken or SyntaxKind.PercentEqualsToken or SyntaxKind.AmpersandEqualsToken or
        SyntaxKind.BarEqualsToken or SyntaxKind.CaretEqualsToken or SyntaxKind.LessThanLessThanEqualsToken or SyntaxKind.GreaterThanGreaterThanEqualsToken or
        SyntaxKind.QuestionQuestionEqualsToken;

    /// <summary>Recognizes binary operators.</summary>
    /// <param name="kind">The token kind.</param>
    /// <returns>Whether the token is a binary operator.</returns>
    private static bool IsBinary(SyntaxKind kind) => kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.AsteriskToken or SyntaxKind.SlashToken or
        SyntaxKind.PercentToken or SyntaxKind.LessThanLessThanToken or SyntaxKind.GreaterThanGreaterThanToken or SyntaxKind.LessThanToken or
        SyntaxKind.GreaterThanToken or SyntaxKind.LessThanEqualsToken or SyntaxKind.GreaterThanEqualsToken or SyntaxKind.EqualsEqualsToken or
        SyntaxKind.ExclamationEqualsToken or SyntaxKind.AmpersandToken or SyntaxKind.BarToken or SyntaxKind.CaretToken or SyntaxKind.AmpersandAmpersandToken or
        SyntaxKind.BarBarToken or SyntaxKind.QuestionQuestionToken or SyntaxKind.IsKeyword or SyntaxKind.AsKeyword;
}

/// <summary>Applies <see cref="GeneratedWhitespace"/> in fluent syntax pipelines.</summary>
internal static class GeneratedWhitespaceExtensions
{
    /// <summary>Lays out generated syntax with four-space indentation, as <c>NormalizeWhitespace</c> does.</summary>
    /// <typeparam name="T">The syntax type.</typeparam>
    /// <param name="node">The generated syntax.</param>
    /// <param name="newLine">The line terminator.</param>
    /// <returns>The same syntax with laid-out whitespace.</returns>
    internal static T NormalizeGenerated<T>(this T node, string newLine) where T : SyntaxNode => GeneratedWhitespace.Normalize(node, newLine);
}
