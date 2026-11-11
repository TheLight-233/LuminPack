using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Joins generated compilation units that one generator publishes as a single file.</summary>
internal static class GeneratedSourceMerge
{
    /// <summary>Joins two generated units, keeping the first unit's header and each using directive once.</summary>
    /// <param name="first">The primary unit, or an empty string.</param>
    /// <param name="second">The unit appended after it, or null or empty.</param>
    /// <param name="options">The parse options of the consuming compilation.</param>
    /// <returns>The joined text, or whichever unit is present.</returns>
    internal static string Join(string first, string second, CSharpParseOptions options)
    {
        if (string.IsNullOrEmpty(second))
            return first ?? string.Empty;
        if (string.IsNullOrEmpty(first))
            return second;
        return Join(new[] { first, second }, options);
    }

    /// <summary>Merges a group of nonempty generated units, parsing each fragment once.</summary>
    /// <param name="sources">The fragments in publication order.</param>
    /// <param name="options">The parse options of the consuming compilation.</param>
    /// <returns>The complete generated file.</returns>
    internal static string Join(IEnumerable<string> sources, CSharpParseOptions options)
    {
        using var input = sources.GetEnumerator();
        if (!input.MoveNext())
            return string.Empty;
        string first = input.Current;
        if (!input.MoveNext())
            return first;

        var primary = SyntaxFactory.ParseCompilationUnit(first, options: options);
        var seen = new HashSet<string>(primary.Usings.Select(directive => directive.WithoutTrivia().ToString()), StringComparer.Ordinal);
        var usings = new List<UsingDirectiveSyntax>(primary.Usings);
        var members = new List<MemberDeclarationSyntax>(primary.Members);
        do
        {
            var appended = SyntaxFactory.ParseCompilationUnit(input.Current, options: options);
            usings.AddRange(appended.Usings.Where(directive => seen.Add(directive.WithoutTrivia().ToString()))
                .Select(directive => directive.WithLeadingTrivia().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"))));
            var additions = appended.Members;
            // Keep the first file's common header. The appended members start on a new line.
            if (additions.Count != 0)
                additions = additions.Replace(additions[0], additions[0].WithLeadingTrivia(new[] { SyntaxFactory.EndOfLine("\n") }
                    .Concat(additions[0].GetLeadingTrivia().Where(trivia => !trivia.IsDirective && !trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)))));
            members.AddRange(additions);
        }
        while (input.MoveNext());
        return primary.WithUsings(SyntaxFactory.List(usings)).WithMembers(SyntaxFactory.List(members)).ToFullString();
    }
}
