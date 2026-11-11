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
    private const string JsonHelper = "Json_global";

    // _utf8_Name, _utf16_Name, _jsonPrefixUtf8_Name, _jsonSepUtf16_Name and the like.
    private static readonly Regex JsonField = new("^_(?:(utf8|utf16)|json(?:Prefix|Sep)(Utf8|Utf16))_(.+)$", RegexOptions.CultureInvariant);

    /// <summary>Spells a type's JSON name tables as nested classes, such as <c>JsonArmorItem.Utf8.ItemId</c>.</summary>
    /// <param name="root">A generated unit with a <c>Json_</c> helper class.</param>
    /// <returns>The unit with each table grouped under its encoding.</returns>
    /// <remarks>The emitters spell each name as one field of the helper, such as <c>Json_global_N_ArmorItem._utf8_ItemId</c>.
    /// The new names are a function of the old ones alone, and every file passes through this step, so declarations and uses
    /// in different files, such as a closed generic formatter using its definition's helper, keep matching. A member's property-name field, prefixed or separated, is <c>Utf8.Name</c> or <c>Utf16.Name</c>
    /// and its complete JSON head is <c>Utf8Head.Name</c> or <c>Utf16Head.Name</c>.</remarks>
    private static CompilationUnitSyntax JsonNames(CompilationUnitSyntax root)
    {
        return (CompilationUnitSyntax)new JsonNameRewriter().Visit(root);
    }

    /// <summary>Names a helper class after its type without the <c>global</c> spelling and separators.</summary>
    private static string HelperName(string name)
    {
        string type = name.Substring("Json_".Length);
        if (type.StartsWith("global_", StringComparison.Ordinal))
            type = type.Substring("global_".Length);
        return "Json" + new string(type.Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>Splits one table field name into its nested class and member.</summary>
    private static bool TryJsonField(string identifier, out string table, out string member)
    {
        var match = JsonField.Match(identifier);
        if (!match.Success)
        {
            table = member = null;
            return false;
        }
        table = match.Groups[1].Success
            ? (match.Groups[1].Value == "utf8" ? "Utf8" : "Utf16")
            : match.Groups[2].Value + "Head";
        member = match.Groups[3].Value;
        if (member == table)
            member += "Name";
        if (SyntaxFacts.GetKeywordKind(member) != SyntaxKind.None)
            member = "@" + member;
        return true;
    }

    /// <summary>Renames the helpers and moves their table fields into nested classes.</summary>
    private sealed class JsonNameRewriter : NodeRewriter
    {
        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            bool helper = IsHelper(node.Identifier.ValueText);
            if (!helper && !(node.Identifier.ValueText == "LuminPackOldNames" && node.Parent is ClassDeclarationSyntax parent &&
                IsHelper(parent.Identifier.ValueText)))
                return visited;
            if (helper)
                visited = visited.WithIdentifier(SyntaxFactory.Identifier(HelperName(node.Identifier.ValueText)).WithTriviaFrom(node.Identifier));
            return Group(visited);
        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            if (visited.Name is IdentifierNameSyntax field && Qualifier(node.Expression) is { } owner && (IsHelper(owner) || owner == "LuminPackOldNames") &&
                TryJsonField(field.Identifier.ValueText, out string table, out string member))
                return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    visited.WithName(SyntaxFactory.IdentifierName(table)), SyntaxFactory.IdentifierName(member)).WithTriviaFrom(visited);
            return visited;
        }

        public override SyntaxNode VisitGenericName(GenericNameSyntax node)
        {
            var visited = (GenericNameSyntax)base.VisitGenericName(node)!;
            return IsHelper(node.Identifier.ValueText)
                ? visited.WithIdentifier(SyntaxFactory.Identifier(HelperName(node.Identifier.ValueText)).WithTriviaFrom(node.Identifier))
                : visited;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (IsHelper(node.Identifier.ValueText))
                return node.WithIdentifier(SyntaxFactory.Identifier(HelperName(node.Identifier.ValueText)).WithTriviaFrom(node.Identifier));
            // An unqualified use inside the helper names the nested table.
            if ((node.Parent is not MemberAccessExpressionSyntax { Name: var accessed } || accessed != node) &&
                node.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => IsHelper(type.Identifier.ValueText)))
                if (TryJsonField(node.Identifier.ValueText, out string table, out string member))
                    return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(table), SyntaxFactory.IdentifierName(member)).WithTriviaFrom(node);
            return node;
        }

        /// <summary>Recognizes a helper class name.</summary>
        private static bool IsHelper(string name) => name.StartsWith(JsonHelper, StringComparison.Ordinal);

        /// <summary>Gets the rightmost name of a qualifier.</summary>
        private static string Qualifier(ExpressionSyntax expression) => expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            SimpleNameSyntax name => name.Identifier.ValueText,
            _ => null,
        };

        /// <summary>Moves table fields and properties into one nested class per table, placed where the first table field was.</summary>
        private static ClassDeclarationSyntax Group(ClassDeclarationSyntax type)
        {
            var tables = new Dictionary<string, List<MemberDeclarationSyntax>>(StringComparer.Ordinal);
            var order = new List<string>();
            var kept = new List<MemberDeclarationSyntax>();
            int position = -1;
            foreach (var member in type.Members)
            {
                string identifier = member switch
                {
                    FieldDeclarationSyntax { Declaration.Variables.Count: 1 } field => field.Declaration.Variables[0].Identifier.ValueText,
                    PropertyDeclarationSyntax property => property.Identifier.ValueText,
                    _ => null,
                };
                if (identifier == null || !TryJsonField(identifier, out string table, out string name))
                {
                    kept.Add(member);
                    continue;
                }
                if (position < 0)
                    position = kept.Count;
                if (!tables.TryGetValue(table, out var members))
                {
                    tables.Add(table, members = new List<MemberDeclarationSyntax>());
                    order.Add(table);
                }
                var renamed = member switch
                {
                    FieldDeclarationSyntax field => (MemberDeclarationSyntax)field.WithDeclaration(field.Declaration.WithVariables(
                        SyntaxFactory.SingletonSeparatedList(field.Declaration.Variables[0].WithIdentifier(SyntaxFactory.Identifier(name))))),
                    PropertyDeclarationSyntax property => property.WithIdentifier(SyntaxFactory.Identifier(name)),
                    _ => member,
                };
                members.Add(renamed.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));
            }
            if (position < 0)
                return type;
            var nested = order.Select(table => (MemberDeclarationSyntax)SyntaxFactory.ClassDeclaration(table)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithMembers(SyntaxFactory.List(tables[table])).NormalizeWhitespace(eol: "\n")
                .WithLeadingTrivia(SyntaxFactory.EndOfLine("\n")).WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")));
            kept.InsertRange(position, nested);
            return type.WithMembers(SyntaxFactory.List(kept));
        }
    }
}
