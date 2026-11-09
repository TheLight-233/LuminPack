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
        var primary = SyntaxFactory.ParseCompilationUnit(first, options: options);
        var appended = SyntaxFactory.ParseCompilationUnit(second, options: options);
        var seen = new HashSet<string>(primary.Usings.Select(directive => directive.WithoutTrivia().ToString()), StringComparer.Ordinal);
        var usings = primary.Usings.AddRange(appended.Usings.Where(directive => seen.Add(directive.WithoutTrivia().ToString()))
            .Select(directive => directive.WithLeadingTrivia().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"))));
        // Generated units share the same header (nullable context and warning suppressions), so the appended unit's header
        // is dropped and its first member starts on a new line.
        var members = appended.Members;
        if (members.Count != 0)
            members = members.Replace(members[0], members[0].WithLeadingTrivia(new[] { SyntaxFactory.EndOfLine("\n") }
                .Concat(members[0].GetLeadingTrivia().Where(trivia => !trivia.IsDirective && !trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)))));
        return primary.WithUsings(usings).WithMembers(primary.Members.AddRange(members)).ToFullString();
    }
}
