using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace LuminPack.SourceGenerator;

/// <summary>Places an existing syntax fragment at its emitted nesting depth without reparsing its body.</summary>
internal static class GeneratedSourceIndentation
{
    /// <summary>Adjusts line prefixes while preserving multiline tokens and inactive source exactly.</summary>
    /// <param name="node">The already parsed fragment with internally consistent indentation.</param>
    /// <param name="spaces">The indentation of the fragment's outermost code.</param>
    /// <returns>The fragment at its destination depth, with unchanged executable token text.</returns>
    internal static string At(SyntaxNode node, int spaces)
    {
        string source = node.ToFullString();
        int origin = node.FullSpan.Start;
        var protectedSpans = node.DescendantTokens().Where(token => token.Text.IndexOf('\n') >= 0 || token.Text.IndexOf('\r') >= 0)
            .Select(token => token.Span).Concat(node.DescendantTrivia().Where(trivia => trivia.IsKind(SyntaxKind.DisabledTextTrivia))
                .Select(trivia => trivia.Span)).OrderBy(span => span.Start)
            .Select(span => new TextSpan(span.Start - origin, span.Length)).ToArray();
        int common = int.MaxValue;
        int protectedIndex = 0;
        for (int start = 0; start < source.Length;)
        {
            int end = source.IndexOf('\n', start);
            if (end < 0)
                end = source.Length;
            int content = ContentStart(start, end);
            if (content < end && source[content] != '#' && !Protected(start))
                common = Math.Min(common, content - start);
            start = end + 1;
        }
        if (common == int.MaxValue || common == spaces)
            return source;
        var result = new StringBuilder(source.Length);
        protectedIndex = 0;
        for (int start = 0; start < source.Length;)
        {
            int end = source.IndexOf('\n', start);
            if (end < 0)
                end = source.Length;
            int next = end < source.Length ? end + 1 : end;
            int content = ContentStart(start, end);
            if (content < end && source[content] != '#' && !Protected(start))
            {
                result.Append(' ', spaces);
                result.Append(source, start + common, next - start - common);
            }
            else
                result.Append(source, start, next - start);
            start = next;
        }
        return result.ToString();

        // A line can begin inside a verbatim/raw string or inactive text; its prefix is data, not indentation.
        bool Protected(int position)
        {
            while (protectedIndex < protectedSpans.Length && protectedSpans[protectedIndex].End <= position)
                protectedIndex++;
            return protectedIndex < protectedSpans.Length && protectedSpans[protectedIndex].Contains(position);
        }

        int ContentStart(int start, int end)
        {
            while (start < end && (source[start] == ' ' || source[start] == '\t' || source[start] == '\r'))
                start++;
            return start;
        }
    }
}
