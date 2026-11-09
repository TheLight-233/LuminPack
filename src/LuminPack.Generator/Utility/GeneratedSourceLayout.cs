using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LuminPack.SourceGenerator;

/// <summary>Formats generated files at the publication boundary.</summary>
/// <remarks>The line wrapper parses only long lines. Alias and case layout run only on files that use them.</remarks>
internal static partial class GeneratedSourceLayout
{
    private const int MaxWidth = 140;
    // An operand of a logical chain, such as one field-pair proof, reads best whole on its line.
    private const int MaxChainOperandWidth = 200;
    private const string CommentMarker = "/// ";
    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Preview);

    /// <summary>Cleared only by the generated-code contract tests, whose assertions read each statement and comment as one line.</summary>
    internal static bool Enabled = true;

    /// <summary>Lays out one completed source file, returning the same string when no line needs a change.</summary>
    /// <param name="source">The complete generated source.</param>
    /// <param name="aliases">The aliases available in the consuming compilation.</param>
    /// <returns>The formatted source with compact Union case rows.</returns>
    internal static string Apply(string source, int aliases)
    {
        var root = PublishedSyntax.Take(source);
        if (!Enabled || string.IsNullOrEmpty(source))
            return source;
        source = Present(source, root, aliases);
        return LayOutLines(source);
    }

    /// <summary>Spaces and wraps the lines of a file whose names and aliases are already final.</summary>
    /// <param name="source">The complete presented source.</param>
    /// <returns>The published source.</returns>
    internal static string LayOutLines(string source)
    {
        // A line of a multi-line literal is not a syntax fragment, so such a file keeps its code lines.
        bool wrapCode = !source.Contains("@\"") && !source.Contains("\"\"\"");
        if (wrapCode)
            source = SpaceLines(source);
        if (!NeedsLayout(source))
            return source;
        string newLine = source.Contains("\r\n") ? "\r\n" : "\n";
        string[] lines = source.Split('\n');
        var result = new StringBuilder(source.Length + source.Length / 8);
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].TrimEnd('\r');
            if (index != 0)
                result.Append(newLine);
            string trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;
            if (index + 1 < lines.Length && IsAttributedAccessorHead(trimmed, lines[index + 1]))
            {
                int brace = line.IndexOf("{[", StringComparison.Ordinal);
                string padding = line.Substring(0, indent);
                result.Append(line.Substring(0, brace).TrimEnd()).Append(newLine);
                result.Append(padding).Append('{').Append(newLine);
                result.Append(padding).Append("    ").Append(line, brace + 1, line.Length - brace - 1).Append(newLine);
                string accessor = lines[++index].TrimEnd('\r').TrimStart();
                result.Append(padding).Append("    ").Append(accessor, 0, accessor.Length - 2).Append(newLine);
                result.Append(padding).Append('}');
            }
            else if (line.Length <= MaxWidth || trimmed.StartsWith("case ", StringComparison.Ordinal) ||
                trimmed.StartsWith("default(", StringComparison.Ordinal))
                result.Append(line);
            else if (trimmed.StartsWith("///", StringComparison.Ordinal))
                AppendDocumentation(result, line, indent, newLine);
            else if (wrapCode && !trimmed.StartsWith("//", StringComparison.Ordinal) && trimmed[0] != '#')
                AppendCode(result, trimmed, indent, newLine);
            else
                result.Append(line);
        }
        return result.ToString();
    }

    /// <summary>Keeps one blank line between members and none at the edges of a block.</summary>
    /// <param name="source">The complete generated source without multi-line literals.</param>
    /// <returns>The source with collapsed blank lines, or the same string when nothing changes.</returns>
    private static string SpaceLines(string source)
    {
        string newLine = source.Contains("\r\n") ? "\r\n" : "\n";
        string[] lines = source.Split('\n');
        var result = new StringBuilder(source.Length);
        string previous = null;
        bool changed = false;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].TrimEnd('\r');
            string trimmed = line.TrimStart();
            if (trimmed.Length == 0)
            {
                string next = NextCode(lines, index + 1);
                // A blank line is dropped after another blank line or an opening brace, and before a closing brace.
                if (previous == null || previous.Length == 0 || previous.EndsWith("{", StringComparison.Ordinal) ||
                    next == null || next.StartsWith("}", StringComparison.Ordinal))
                {
                    changed = true;
                    continue;
                }
            }
            else if (previous is { } last && last.TrimStart() == "}" && last.Length - 1 == line.Length - trimmed.Length && StartsMember(trimmed))
            {
                // A member that directly follows the closing brace of the previous member gets its separating line.
                result.Append(newLine);
                changed = true;
            }
            if (previous != null)
                result.Append(newLine);
            result.Append(line);
            previous = trimmed.Length == 0 ? string.Empty : line;
        }
        return changed ? result.ToString() : source;
    }

    /// <summary>Finds the next nonblank line.</summary>
    /// <param name="lines">The source lines.</param>
    /// <param name="start">The first line to inspect.</param>
    /// <returns>The trimmed line, or null at the end of the file.</returns>
    private static string NextCode(string[] lines, int start)
    {
        for (int index = start; index < lines.Length; index++)
        {
            string trimmed = lines[index].Trim();
            if (trimmed.Length != 0)
                return trimmed;
        }
        return null;
    }

    /// <summary>Recognizes the first line of a member declaration.</summary>
    /// <param name="trimmed">The line without indentation.</param>
    /// <returns>Whether the line starts with an attribute, a documentation comment or a member modifier.</returns>
    private static bool StartsMember(string trimmed)
        => trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("///", StringComparison.Ordinal) || IsMemberStart(trimmed) ||
            trimmed.StartsWith("static ", StringComparison.Ordinal);

    /// <summary>Checks without parsing whether any line exceeds the width or carries an attributed accessor.</summary>
    /// <param name="source">The complete generated source.</param>
    /// <returns>Whether the source needs a layout pass.</returns>
    private static bool NeedsLayout(string source)
    {
        if (source.Contains("{["))
            return true;
        int start = 0;
        for (int end = source.IndexOf('\n'); end >= 0; end = source.IndexOf('\n', start))
        {
            if (end - start > MaxWidth)
                return true;
            start = end + 1;
        }
        return source.Length - start > MaxWidth;
    }

    /// <summary>Recognizes a property whose whitespace normalization left an attributed accessor list on two lines.</summary>
    /// <param name="trimmed">The current line without indentation.</param>
    /// <param name="next">The following line.</param>
    /// <returns>Whether the lines hold a property head with an attribute and one closing accessor.</returns>
    private static bool IsAttributedAccessorHead(string trimmed, string next)
        => trimmed.EndsWith("]", StringComparison.Ordinal) && trimmed.IndexOf(" {[", StringComparison.Ordinal) > 0 &&
            next.TrimEnd('\r').EndsWith("; }", StringComparison.Ordinal);

    /// <summary>Appends one code line, broken at the outermost syntax lists that keep it within the width.</summary>
    /// <param name="result">The output.</param>
    /// <param name="code">The line without indentation.</param>
    /// <param name="indent">The indentation of the line.</param>
    /// <param name="newLine">The line terminator of the file.</param>
    private static void AppendCode(StringBuilder result, string code, int indent, string newLine)
    {
        SyntaxNode root = IsMemberStart(code)
            ? SyntaxFactory.ParseMemberDeclaration(code, 0, ParseOptions, consumeFullText: false)
            : SyntaxFactory.ParseStatement(code, 0, ParseOptions, consumeFullText: false);
        var breaks = new SortedDictionary<int, int>();
        if (root != null)
            Wrap(root, TextSpan.FromBounds(0, root.Span.End), code.Length, indent, indent, breaks);
        result.Append(' ', indent);
        int start = 0;
        foreach (var item in breaks)
        {
            result.Append(code.Substring(start, item.Key - start).TrimEnd()).Append(newLine).Append(' ', item.Value);
            start = item.Key;
        }
        result.Append(code, start, code.Length - start);
    }

    /// <summary>Selects the parser for a line, since a statement cannot start with an accessibility modifier.</summary>
    /// <param name="code">The line without indentation.</param>
    /// <returns>Whether the line starts a member declaration.</returns>
    private static bool IsMemberStart(string code)
        => code.StartsWith("public ", StringComparison.Ordinal) || code.StartsWith("internal ", StringComparison.Ordinal) ||
            code.StartsWith("private ", StringComparison.Ordinal) || code.StartsWith("protected ", StringComparison.Ordinal);

    /// <summary>Breaks the outermost breakable syntax in a range, then repeats inside any part that is still too wide.</summary>
    /// <param name="root">The parsed line.</param>
    /// <param name="range">The text range to lay out.</param>
    /// <param name="end">The offset at which the range's physical line ends, including text outside the parsed syntax.</param>
    /// <param name="column">The column at which the range starts after earlier breaks.</param>
    /// <param name="indent">The indentation of the physical line holding the range.</param>
    /// <param name="breaks">Line break offsets and the indentation that follows each.</param>
    private static void Wrap(SyntaxNode root, TextSpan range, int end, int column, int indent, SortedDictionary<int, int> breaks)
    {
        // A new line inside an interpolation hole is not valid for every language version the consumer can select.
        var nodes = root.DescendantNodesAndSelf(node => node.Span.IntersectsWith(range) && node is not InterpolatedStringExpressionSyntax);
        foreach (var node in nodes)
        {
            if (!range.Contains(node.Span))
                continue;
            var positions = BreakPositions(node);
            // Every part must be shorter than the range, which bounds the recursion. A missing node of a partial line can sit at its end.
            positions.RemoveAll(position => position <= range.Start || position >= range.End);
            if (positions.Count == 0)
                continue;
            int inner = indent + 4;
            int limit = node is BinaryExpressionSyntax ? MaxChainOperandWidth : MaxWidth;
            if (column + (positions[0] - range.Start) > limit)
                Wrap(root, TextSpan.FromBounds(range.Start, positions[0]), positions[0], column, indent, breaks);
            for (int i = 0; i < positions.Count; i++)
            {
                breaks[positions[i]] = inner;
                bool last = i + 1 == positions.Count;
                int partEnd = last ? end : positions[i + 1];
                if (inner + (partEnd - positions[i]) > limit)
                    Wrap(root, TextSpan.FromBounds(positions[i], last ? range.End : positions[i + 1]), partEnd, inner, inner, breaks);
            }
            return;
        }
    }

    /// <summary>Finds the positions at which one syntax node can move its parts to separate lines.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns>Ascending token positions, or an empty list when the node is not a list of parts.</returns>
    private static List<int> BreakPositions(SyntaxNode node)
    {
        var positions = new List<int>();
        switch (node)
        {
            case BinaryExpressionSyntax binary when IsChain(binary) && !(binary.Parent is BinaryExpressionSyntax parent && parent.Kind() == binary.Kind()):
                for (ExpressionSyntax current = binary; current is BinaryExpressionSyntax link && link.Kind() == binary.Kind(); current = link.Left)
                    positions.Add(link.Right.SpanStart);
                positions.Reverse();
                break;
            case ConditionalExpressionSyntax conditional:
                positions.Add(conditional.QuestionToken.SpanStart);
                positions.Add(conditional.ColonToken.SpanStart);
                break;
            case ArgumentListSyntax arguments when arguments.Arguments.Count > 1:
                positions.AddRange(arguments.Arguments.Select(argument => argument.SpanStart));
                break;
            case ParameterListSyntax parameters when parameters.Parameters.Count > 1:
                positions.AddRange(parameters.Parameters.Select(parameter => parameter.SpanStart));
                break;
            case ArrowExpressionClauseSyntax arrow:
                positions.Add(arrow.ArrowToken.SpanStart);
                break;
        }
        return positions;
    }

    /// <summary>Recognizes operators whose operands read naturally on successive lines.</summary>
    /// <param name="binary">The binary expression.</param>
    /// <returns>Whether the expression is a logical or coalescing operation.</returns>
    private static bool IsChain(BinaryExpressionSyntax binary)
        => binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression) || binary.IsKind(SyntaxKind.CoalesceExpression);

    /// <summary>Puts each XML paragraph on its own comment line and fills what is still too wide at spaces outside tags.</summary>
    /// <param name="result">The output.</param>
    /// <param name="line">The complete comment line.</param>
    /// <param name="indent">The indentation of the line.</param>
    /// <param name="newLine">The line terminator of the file.</param>
    private static void AppendDocumentation(StringBuilder result, string line, int indent, string newLine)
    {
        if (line.IndexOf("<code", StringComparison.Ordinal) >= 0)
        {
            result.Append(line);
            return;
        }
        string prefix = line.Substring(0, indent) + CommentMarker;
        string body = line.Substring(indent + 3).TrimStart();
        var lines = new List<string>();
        foreach (string part in body.Replace("<para>", "\n<para>").Replace("</para>", "</para>\n").Split('\n'))
        {
            if (part.Length != 0)
                Fill(part, prefix, lines);
        }
        result.Append(string.Join(newLine, lines));
    }

    /// <summary>Fills comment lines up to the width, breaking only at spaces outside XML tags.</summary>
    /// <param name="part">The text of one paragraph.</param>
    /// <param name="prefix">The indentation and comment marker.</param>
    /// <param name="lines">The completed comment lines.</param>
    private static void Fill(string part, string prefix, List<string> lines)
    {
        var current = new StringBuilder(prefix);
        int wordStart = 0;
        bool insideTag = false;
        for (int i = 0; i <= part.Length; i++)
        {
            if (i < part.Length && part[i] == '<')
                insideTag = true;
            else if (i < part.Length && part[i] == '>')
                insideTag = false;
            if (i < part.Length && (part[i] != ' ' || insideTag))
                continue;
            string word = part.Substring(wordStart, i - wordStart);
            if (current.Length > prefix.Length && current.Length + 1 + word.Length > MaxWidth)
            {
                lines.Add(current.ToString());
                current.Clear().Append(prefix);
            }
            if (current.Length > prefix.Length)
                current.Append(' ');
            current.Append(word);
            wordStart = i + 1;
        }
        lines.Add(current.ToString());
    }
}
