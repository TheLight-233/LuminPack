using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Removes the generated operations a project never calls from a finished file.</summary>
/// <remarks>A method belongs to the operation of the serializer state it takes, or, without a state, to the facade operation its
/// name starts with. Operations only call operations of the same kind and shared helpers, so a project-wide selection is
/// consistent across files. A selected removal that other code in the file still names is kept, together with what it names.</remarks>
internal static class GeneratedOperationFilter
{
    /// <summary>Removes the unselected operations.</summary>
    /// <param name="source">A finished generated file.</param>
    /// <param name="operations">The operations the project calls.</param>
    /// <param name="options">The parse options of the consuming compilation.</param>
    /// <returns>The file without unselected operations, or the original text when nothing is removed.</returns>
    internal static string Apply(string source, GeneratedOperations operations, CSharpParseOptions options)
    {
        if (operations == GeneratedOperations.All || string.IsNullOrEmpty(source))
            return source;
        var root = CSharpSyntaxTree.ParseText(source, options).GetCompilationUnitRoot();
        var candidates = root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax)
            .OfType<MemberDeclarationSyntax>().Where(member => (Operation(member) & ~operations) != 0 && (Operation(member) & operations) == 0)
            .ToList();
        if (candidates.Count == 0)
            return source;

        // Keep a removal that remaining code still names, until nothing more is kept.
        var removed = new HashSet<MemberDeclarationSyntax>(candidates);
        bool changed = true;
        while (changed)
        {
            changed = false;
            var named = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in root.DescendantTokens(node => !removed.Contains(node)))
                if (token.IsKind(SyntaxKind.IdentifierToken) && token.Parent is SimpleNameSyntax)
                    named.Add(token.ValueText);
            // A name that a kept overload also declares refers to that overload, since kept code calls its own operation.
            var declared = new HashSet<string>(root.DescendantNodes(node => !removed.Contains(node)).OfType<MethodDeclarationSyntax>()
                .Where(method => !removed.Contains(method)).Select(method => method.Identifier.ValueText), StringComparer.Ordinal);
            foreach (var member in removed.ToArray())
                if (Name(member) is { } name && named.Contains(name) && !declared.Contains(name))
                {
                    removed.Remove(member);
                    changed = true;
                }
        }
        return removed.Count == 0 ? source : root.RemoveNodes(removed, SyntaxRemoveOptions.KeepNoTrivia)!.ToFullString();
    }

    /// <summary>Classifies a generated member by the operation it implements.</summary>
    /// <param name="member">A namespace, type or method member.</param>
    /// <returns>The member's operation, or none for shared declarations.</returns>
    private static GeneratedOperations Operation(MemberDeclarationSyntax member)
    {
        if (member is ClassDeclarationSyntax helper && helper.Identifier.ValueText.StartsWith("Json_", StringComparison.Ordinal))
            return GeneratedOperations.JsonWrite | GeneratedOperations.JsonRead;
        if (member is not MethodDeclarationSyntax method)
            return GeneratedOperations.None;
        var states = GeneratedOperations.None;
        foreach (var parameter in method.ParameterList.Parameters)
            states |= State(parameter.Type);
        if (states != GeneratedOperations.None)
            // A method that takes two kinds of state is shared.
            return (states & (states - 1)) == 0 ? states : GeneratedOperations.None;
        string name = method.Identifier.ValueText;
        if (name is "GetJsonPropertyIndexUtf8" or "GetJsonPropertyIndexUtf16")
            return GeneratedOperations.JsonRead;
        if (name.StartsWith("SerializeJson", StringComparison.Ordinal))
            return GeneratedOperations.JsonWrite;
        if (name.StartsWith("DeserializeJson", StringComparison.Ordinal))
            return GeneratedOperations.JsonRead;
        if (name.StartsWith("Serialize", StringComparison.Ordinal))
            return GeneratedOperations.BinaryWrite;
        if (name.StartsWith("Deserialize", StringComparison.Ordinal))
            return GeneratedOperations.BinaryRead;
        return GeneratedOperations.None;
    }

    /// <summary>Maps a parameter type to the serializer state it names.</summary>
    /// <param name="type">The parameter type.</param>
    /// <returns>The state's operation, or none.</returns>
    private static GeneratedOperations State(TypeSyntax type)
    {
        string text = type?.ToString();
        if (text == null)
            return GeneratedOperations.None;
        int dot = text.LastIndexOf('.');
        return (dot < 0 ? text : text.Substring(dot + 1)) switch
        {
            "LuminPackWriter" or "LuminPackEvaluator" => GeneratedOperations.BinaryWrite,
            "LuminPackReader" => GeneratedOperations.BinaryRead,
            "LuminPackJsonWriter" => GeneratedOperations.JsonWrite,
            "LuminPackJsonReader" => GeneratedOperations.JsonRead,
            _ => GeneratedOperations.None,
        };
    }

    /// <summary>Gets the name other code uses for a member.</summary>
    /// <param name="member">A removable member.</param>
    /// <returns>The member's identifier.</returns>
    private static string Name(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        _ => null,
    };
}
