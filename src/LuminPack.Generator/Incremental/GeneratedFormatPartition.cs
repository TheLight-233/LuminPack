using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Separates finished JSON members from binary and shared declarations without rewriting their bodies.</summary>
internal sealed class GeneratedFormatPartition
{
    private static readonly ConditionalWeakTable<string, ConditionalWeakTable<CSharpParseOptions, GeneratedFormatPartition>> Cache = new();

    internal readonly string Shared;
    internal readonly string Json;

    /// <summary>Stores the two independent source contributions.</summary>
    /// <param name="shared">The binary and shared declarations.</param>
    /// <param name="json">The JSON-only declarations.</param>
    private GeneratedFormatPartition(string shared, string json)
    {
        Shared = shared;
        Json = json;
    }

    /// <summary>Returns the nonempty format files with their existing hint names.</summary>
    /// <param name="hintName">The hint name of the original contribution.</param>
    /// <param name="binary">Whether to include binary and shared declarations.</param>
    /// <param name="json">Whether to include JSON declarations.</param>
    /// <returns>The requested files in format order.</returns>
    internal IEnumerable<FrozenGeneratedSource> Files(string hintName, bool binary = true, bool json = true)
    {
        if (binary && Shared.Length != 0)
            yield return new FrozenGeneratedSource(hintName, Shared);
        if (json && Json.Length != 0)
            // The JSON part is published by the JSON generator, so it keeps the same name.
            yield return new FrozenGeneratedSource(hintName, Json);
    }

    /// <summary>Partitions one generated file once for both publishers.</summary>
    /// <param name="source">The completed formatter or serializer source.</param>
    /// <param name="options">The exact parse and preprocessing options.</param>
    /// <returns>Source text for the binary and JSON publishers.</returns>
    internal static GeneratedFormatPartition Get(string source, CSharpParseOptions options)
        => Cache.GetValue(source, static _ => new()).GetValue(options, parse =>
            Split(source, CSharpSyntaxTree.ParseText(source, parse.WithDocumentationMode(DocumentationMode.Parse)).GetCompilationUnitRoot()));

    /// <summary>Captures the partition while a completed formatter syntax tree is still available.</summary>
    /// <param name="source">The complete source text used to identify the frozen contribution.</param>
    /// <param name="options">The exact parse and preprocessing options used by the publishers.</param>
    /// <param name="root">The completed formatter syntax.</param>
    internal static void Capture(string source, CSharpParseOptions options, CompilationUnitSyntax root)
        => Cache.GetValue(source, static _ => new()).GetValue(options, _ => Split(source, root));

    /// <summary>Copies only the namespace and partial type shells needed by each format.</summary>
    /// <param name="source">The complete generated contribution.</param>
    /// <param name="root">The syntax of the completed generated contribution.</param>
    /// <returns>The source contribution split at declaration boundaries, each part carrying its syntax to its publisher.</returns>
    private static GeneratedFormatPartition Split(string source, CompilationUnitSyntax root)
    {
        if (source.IndexOf("Json", StringComparison.Ordinal) < 0)
            return new GeneratedFormatPartition(PublishedSyntax.Attach(source, root), string.Empty);
        if (source.IndexOf("#if", StringComparison.Ordinal) >= 0 &&
            root.DescendantTrivia().Any(trivia => trivia.GetStructure() is ConditionalDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax &&
                !trivia.Token.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().Any(method => method.Span.Contains(trivia.Span))))
            return new GeneratedFormatPartition(PublishedSyntax.Attach(source, root), string.Empty);
        var (shared, json) = Partition(root.Members);
        if (json.Count == 0)
            return new GeneratedFormatPartition(PublishedSyntax.Attach(source, root), string.Empty);
        var jsonRoot = root.WithMembers(json);
        if (shared.Count == 0)
            return new GeneratedFormatPartition(string.Empty, Print(jsonRoot));
        return new GeneratedFormatPartition(Print(root.WithMembers(shared)), Print(jsonRoot));
    }

    /// <summary>Prints one format's part and attaches its syntax for the publisher.</summary>
    /// <param name="root">The part's syntax.</param>
    /// <returns>The part's text.</returns>
    private static string Print(CompilationUnitSyntax root) => PublishedSyntax.Attach(root.ToFullString(), root);

    /// <summary>Separates members and recursively visits the generated partial containers.</summary>
    /// <param name="members">Members of the current namespace or generated type.</param>
    /// <returns>Members for each publisher in their original relative order.</returns>
    private static (SyntaxList<MemberDeclarationSyntax> Shared, SyntaxList<MemberDeclarationSyntax> Json) Partition(
        SyntaxList<MemberDeclarationSyntax> members)
    {
        var shared = new List<MemberDeclarationSyntax>(members.Count);
        var json = new List<MemberDeclarationSyntax>();
        foreach (var member in members)
        {
            if (member is NamespaceDeclarationSyntax ns)
            {
                var nested = Partition(ns.Members);
                if (nested.Shared.Count != 0)
                    shared.Add(ns.WithMembers(nested.Shared));
                if (nested.Json.Count != 0)
                    json.Add(ns.WithMembers(nested.Json));
            }
            else if (member is ClassDeclarationSyntax type && type.Modifiers.Any(SyntaxKind.PartialKeyword) &&
                type.AttributeLists.Count == 0)
            {
                var nested = Partition(type.Members);
                if (nested.Shared.Count != 0)
                    shared.Add(type.WithMembers(nested.Shared));
                if (nested.Json.Count != 0)
                {
                    var part = type.WithMembers(nested.Json);
                    if (nested.Shared.Count != 0)
                        part = part.WithLeadingTrivia(part.GetLeadingTrivia().Where(trivia =>
                            !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) &&
                            !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)));
                    json.Add(part);
                }
            }
            else if (IsJson(member))
                json.Add(member);
            else
                shared.Add(member);
        }
        return (SyntaxFactory.List(shared), SyntaxFactory.List(json));
    }

    /// <summary>Recognizes the JSON methods and support types owned by a generated formatter or facade.</summary>
    /// <param name="member">A generated member.</param>
    /// <returns>Whether it belongs exclusively to JSON.</returns>
    private static bool IsJson(MemberDeclarationSyntax member)
    {
        if (member is ClassDeclarationSyntax helper)
            return helper.Identifier.ValueText.StartsWith("Json_", StringComparison.Ordinal);
        if (member is not MethodDeclarationSyntax method)
            return false;
        string name = method.Identifier.ValueText;
        if (name is "SerializeJson" or "DeserializeJson" or "SerializeJsonCore" or "DeserializeJsonCore" or "RootWriteJson" or "RootReadJson" ||
            name.StartsWith("SerializeJson_", StringComparison.Ordinal) || name.StartsWith("DeserializeJson_", StringComparison.Ordinal))
            return true;
        // A JSON state in any parameter marks a JSON operation, including helpers that take their value first.
        return method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() is "LuminPackJsonWriter" or "LuminPackJsonReader" or
            "global::LuminPack.Core.LuminPackJsonWriter" or "global::LuminPack.Core.LuminPackJsonReader" or
            "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader");
    }
}
