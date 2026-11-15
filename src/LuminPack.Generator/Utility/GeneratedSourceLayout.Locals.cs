using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

internal static partial class GeneratedSourceLayout
{
    // A local that expansion numbered to keep it apart from another, such as reader2 or seenFields0.
    private static readonly Regex NumberedLocal = new(@"^([A-Za-z][A-Za-z]*?)([0-9]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex AnyNumberedLocal = new("[A-Za-z][A-Za-z]*[0-9]+ = ", RegexOptions.CultureInvariant);
    private static readonly Regex WordDigits = new("(?:Utf|utf|Int|int|UInt|Base|base|Hash|hash|Byte|byte|Bit|bit|Float|Vector|X|x)$", RegexOptions.CultureInvariant);

    /// <summary>Drops the number from expanded locals whose plain name the method does not use.</summary>
    /// <param name="root">A generated unit.</param>
    /// <returns>The unit with those locals renamed.</returns>
    /// <remarks>A local keeps its number when the plain name appears anywhere in its method, in any role, so no name can bind
    /// differently. Of several numbered locals with one plain name, the lowest number takes it.</remarks>
    private static CompilationUnitSyntax PlainLocals(CompilationUnitSyntax root)
    {
        var renames = new Dictionary<SyntaxToken, string>();
        foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
        {
            var declared = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string name in DeclaredLocals(method))
            {
                var match = NumberedLocal.Match(name);
                // Digits that belong to a word, as in isUtf8 or valueInt32, are not a number added by expansion.
                if (!match.Success || match.Groups[1].Value.Length < 2 || WordDigits.IsMatch(match.Groups[1].Value) ||
                    SyntaxFacts.GetKeywordKind(match.Groups[1].Value) != SyntaxKind.None ||
                    SyntaxFacts.GetContextualKeywordKind(match.Groups[1].Value) != SyntaxKind.None)
                    continue;
                if (!declared.TryGetValue(match.Groups[1].Value, out var names))
                    declared.Add(match.Groups[1].Value, names = new List<string>());
                if (!names.Contains(name))
                    names.Add(name);
            }
            if (declared.Count == 0)
                continue;
            var used = new HashSet<string>(method.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)).Select(token => token.ValueText),
                StringComparer.Ordinal);
            var chosen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in declared)
                if (!used.Contains(pair.Key))
                    chosen.Add(pair.Value.OrderBy(name => int.Parse(NumberedLocal.Match(name).Groups[2].Value)).ThenBy(name => name, StringComparer.Ordinal).First(),
                        pair.Key);
            if (chosen.Count == 0)
                continue;
            foreach (var token in method.DescendantTokens())
                if (token.IsKind(SyntaxKind.IdentifierToken) && chosen.TryGetValue(token.ValueText, out string plain) &&
                    !(token.Parent is SimpleNameSyntax { Parent: MemberAccessExpressionSyntax access } name && access.Name == name))
                    renames[token] = plain;
        }
        return renames.Count == 0 ? root : root.ReplaceTokens(renames.Keys, (original, _) =>
            SyntaxFactory.Identifier(original.LeadingTrivia, renames[original], original.TrailingTrivia));
    }

    /// <summary>Lists the names a method declares for locals, parameters of nested functions and pattern variables.</summary>
    /// <param name="method">The method being inspected.</param>
    /// <returns>The resulting <c>IEnumerable&lt;string&gt;</c>.</returns>
    private static IEnumerable<string> DeclaredLocals(BaseMethodDeclarationSyntax method)
    {
        foreach (var node in method.DescendantNodes())
        {
            switch (node)
            {
                case VariableDeclaratorSyntax variable:
                    yield return variable.Identifier.ValueText;
                    break;
                case SingleVariableDesignationSyntax designation:
                    yield return designation.Identifier.ValueText;
                    break;
                case ForEachStatementSyntax loop:
                    yield return loop.Identifier.ValueText;
                    break;
                case CatchDeclarationSyntax { Identifier.ValueText.Length: > 0 } exception:
                    yield return exception.Identifier.ValueText;
                    break;
            }
        }
    }
}
