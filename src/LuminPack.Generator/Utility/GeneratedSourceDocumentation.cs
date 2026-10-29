using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Documents caller-facing serializer APIs without retaining XML on generated implementation details.</summary>
internal static class GeneratedSourceDocumentation
{
    /// <summary>Completes serializer API comments and removes internal implementation documentation.</summary>
    /// <param name="source">The complete source file to document.</param>
    /// <param name="options">The consuming compilation's language and conditional compilation options.</param>
    /// <returns>The unchanged executable source with documentation limited to serializer APIs.</returns>
    internal static string Complete(string source, CSharpParseOptions options)
    {
        var root = CSharpSyntaxTree.ParseText(source, options.WithDocumentationMode(DocumentationMode.None)).GetRoot();
        if (root.DescendantNodes(node => node is not BaseMethodDeclarationSyntax).OfType<ClassDeclarationSyntax>()
            .Any(type => type.Identifier.ValueText == "LuminPackSerializer" && type.Parent is BaseNamespaceDeclarationSyntax))
            root = CSharpSyntaxTree.ParseText(source, options.WithDocumentationMode(DocumentationMode.Parse)).GetRoot();
        return "#pragma warning disable CS1591" + Environment.NewLine + CompleteRoot(root).ToFullString();
    }

    /// <summary>Completes method documentation directly on an already parsed generated tree.</summary>
    /// <param name="root">A source root with documentation parsing enabled.</param>
    /// <returns>The same executable syntax with missing XML trivia inserted.</returns>
    internal static SyntaxNode CompleteRoot(SyntaxNode root)
    {
        var removed = root.DescendantTrivia().Where(trivia => IsDocumentation(trivia) && !KeepDocumentation(trivia)).ToArray();
        if (removed.Length != 0) root = root.ReplaceTrivia(removed, static (_, _) => default);
        var apis = root.DescendantNodes(node => node is not BaseMethodDeclarationSyntax)
            .OfType<BaseMethodDeclarationSyntax>().Where(IsUserApi).ToArray();
        if (apis.Length == 0) return root;
        string source = root.ToFullString();
        var methods = new Dictionary<BaseMethodDeclarationSyntax, BaseMethodDeclarationSyntax>();
        foreach (var method in apis)
        {
            var existing = method.GetLeadingTrivia().Where(IsDocumentation).ToArray();
            var elements = existing.Select(trivia => trivia.GetStructure()).OfType<DocumentationCommentTriviaSyntax>()
                .SelectMany(comment => comment.Content).OfType<XmlElementSyntax>().ToArray();
            var names = new HashSet<string>(elements.Select(element => element.StartTag.Name.LocalName.ValueText), StringComparer.Ordinal);
            var parameters = new HashSet<string>(elements.Where(element => element.StartTag.Name.LocalName.ValueText == "param")
                .SelectMany(element => element.StartTag.Attributes.OfType<XmlNameAttributeSyntax>())
                .Select(attribute => attribute.Identifier.Identifier.ValueText), StringComparer.Ordinal);
            var typeParameters = new HashSet<string>(elements.Where(element => element.StartTag.Name.LocalName.ValueText == "typeparam")
                .SelectMany(element => element.StartTag.Attributes.OfType<XmlNameAttributeSyntax>())
                .Select(attribute => attribute.Identifier.Identifier.ValueText), StringComparer.Ordinal);

            string indentation = GetIndentation(method, source, out bool startsOnOwnLine);

            var documentation = new StringBuilder();
            if (!startsOnOwnLine)
            {
                documentation.AppendLine().Append(indentation);
            }

            if (!names.Contains("summary"))
                documentation.Append("/// <summary>").Append(GetSummary(method)).AppendLine("</summary>");
            bool missing = !names.Contains("summary");
            foreach (var parameter in method.ParameterList.Parameters)
            {
                string name = parameter.Identifier.ValueText;
                if (parameters.Contains(name)) continue;
                missing = true;
                documentation.Append(indentation).Append("/// <param name=\"").Append(Escape(name)).Append("\">")
                    .Append(GetParameterDescription(parameter)).AppendLine("</param>");
            }

            if (method is MethodDeclarationSyntax declaration)
            {
                if (declaration.TypeParameterList != null)
                {
                    foreach (var parameter in declaration.TypeParameterList.Parameters)
                    {
                        string name = parameter.Identifier.ValueText;
                        if (typeParameters.Contains(name)) continue;
                        missing = true;
                        documentation.Append(indentation).Append("/// <typeparam name=\"").Append(Escape(name))
                            .Append("\">The type represented by <c>").Append(Escape(name)).AppendLine("</c>.</typeparam>");
                    }
                }

                if (!names.Contains("returns") && (!declaration.ReturnType.IsKind(SyntaxKind.PredefinedType) ||
                    ((PredefinedTypeSyntax)declaration.ReturnType).Keyword.Kind() != SyntaxKind.VoidKeyword))
                {
                    missing = true;
                    documentation.Append(indentation).AppendLine("/// <returns>The value produced by the generated operation.</returns>");
                }
            }
            if (!missing && existing.Length <= 1) continue;
            var combined = new StringBuilder();
            foreach (var trivia in existing)
                combined.AppendLine(trivia.ToFullString().TrimEnd());
            combined.Append(documentation);
            methods.Add(method, WithDocumentation(method, combined.ToString(), indentation));
        }

        if (methods.Count == 0)
        {
            return root;
        }

        return new DocumentationInserter(methods).Visit(root);
    }

    /// <summary>Recognizes APIs called directly on the generated serializer facade.</summary>
    /// <param name="method">A generated declaration before documentation is emitted.</param>
    /// <returns>Whether caller-facing documentation belongs on the declaration.</returns>
    internal static bool IsUserApi(BaseMethodDeclarationSyntax method)
    {
        if (method is not MethodDeclarationSyntax declaration ||
            method.Parent is not ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer", Parent: BaseNamespaceDeclarationSyntax }) return false;
        if (method.Modifiers.Any(SyntaxKind.PublicKeyword)) return true;
        if (!method.Modifiers.Any(SyntaxKind.InternalKeyword)) return false;
        // Non-public payloads still expose convenience overloads to their consuming assembly.
        // Internal state forwarders and Union case entries are implementation details.
        string name = declaration.Identifier.ValueText;
        return (name is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "Sizeof" or "SerializeAsync" or "DeserializeAsync") &&
            declaration.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText is not ("writer" or "reader" or "evaluator" or "state");
    }

    /// <summary>Retains only leading API comments and the serializer's caller-facing type description.</summary>
    /// <param name="trivia">An XML comment in the completed generated contribution.</param>
    /// <returns>Whether the comment describes an API or its facade type.</returns>
    private static bool KeepDocumentation(SyntaxTrivia trivia)
    {
        for (var node = trivia.Token.Parent; node != null; node = node.Parent)
        {
            if (node is LocalFunctionStatementSyntax) return false;
            if (node is BaseMethodDeclarationSyntax method) return trivia.Span.End <= method.SpanStart && IsUserApi(method);
            if (node is MemberDeclarationSyntax member)
                return member is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer", Parent: BaseNamespaceDeclarationSyntax };
        }
        return false;
    }

    /// <summary>Recognizes structured XML comments without treating ordinary comments as documentation.</summary>
    /// <param name="trivia">The leading trivia attached to a generated declaration.</param>
    /// <returns>Whether the trivia contains an XML documentation comment.</returns>
    private static bool IsDocumentation(SyntaxTrivia trivia) => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
        trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) ||
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().StartsWith("///", StringComparison.Ordinal);

    /// <summary>Uses the declaration line rather than a detached attribute's trivia to recover indentation.</summary>
    /// <param name="method">The generated declaration.</param>
    /// <param name="source">Its enclosing root's exact text.</param>
    /// <param name="startsOnOwnLine">Whether the declaration follows only whitespace on its line.</param>
    /// <returns>The existing indentation or the enclosing declarations' normal indentation.</returns>
    internal static string GetIndentation(BaseMethodDeclarationSyntax method, string source, out bool startsOnOwnLine)
    {
        var token = method.Modifiers.Count != 0 ? method.Modifiers[0] : method is MethodDeclarationSyntax declaration
            ? declaration.ReturnType.GetFirstToken() : method.GetFirstToken();
        int position = token.SpanStart;
        int lineStart = source.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
        string indentation = source.Substring(lineStart, position - lineStart);
        startsOnOwnLine = indentation.All(character => character == ' ' || character == '\t');
        return startsOnOwnLine ? indentation : new string(' ', method.Ancestors().Count(node =>
            node is TypeDeclarationSyntax or NamespaceDeclarationSyntax) * 4);
    }

    /// <summary>Replaces only XML trivia and restores indentation before the first declaration token or attribute.</summary>
    /// <param name="method">The generated method whose executable syntax must remain unchanged.</param>
    /// <param name="documentation">Complete XML comment text, ending with a line break.</param>
    /// <param name="indentation">The declaration's original indentation.</param>
    /// <returns>The same declaration with one complete documentation block.</returns>
    internal static BaseMethodDeclarationSyntax WithDocumentation(BaseMethodDeclarationSyntax method, string documentation, string indentation)
    {
        string normalized = string.Join(Environment.NewLine + indentation,
            documentation.Replace("\r\n", "\n").TrimEnd().Split('\n').Select(line => line.TrimStart(' ', '\t'))) + Environment.NewLine;
        var replacement = SyntaxFactory.ParseLeadingTrivia(normalized);
        var leading = new List<SyntaxTrivia>();
        bool inserted = false;
        foreach (var trivia in method.GetLeadingTrivia())
        {
            if (IsDocumentation(trivia))
            {
                if (!inserted) leading.AddRange(replacement);
                inserted = true;
            }
            else leading.Add(trivia);
        }
        if (!inserted) leading.AddRange(replacement);
        while (leading.Count != 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia))
            leading.RemoveAt(leading.Count - 1);
        if (indentation.Length != 0) leading.Add(SyntaxFactory.Whitespace(indentation));
        return method.WithLeadingTrivia(leading);
    }

    /// <summary>Inserts XML trivia while avoiding traversal of unchanged executable bodies.</summary>
    private sealed class DocumentationInserter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<BaseMethodDeclarationSyntax, BaseMethodDeclarationSyntax> _methods;

        /// <summary>Captures missing documentation for declarations in the current root.</summary>
        /// <param name="methods">Original methods and their replacements with complete XML trivia.</param>
        internal DocumentationInserter(Dictionary<BaseMethodDeclarationSyntax, BaseMethodDeclarationSyntax> methods) { _methods = methods; }

        /// <summary>Changes declaration trivia without visiting or rewriting its body.</summary>
        /// <param name="node">The current declaration or structural container.</param>
        /// <returns>The documented declaration or an unchanged structural traversal.</returns>
        public override SyntaxNode Visit(SyntaxNode node)
            => node is BaseMethodDeclarationSyntax method ? _methods.TryGetValue(method, out var documented)
                ? documented : method
                : base.Visit(node);
    }

    /// <summary>Describes the role of an emitted method in serialization, mapping, or dispatch.</summary>
    /// <param name="method">The generated method declaration.</param>
    /// <returns>The summary text for its XML documentation.</returns>
    private static string GetSummary(BaseMethodDeclarationSyntax method)
    {
        if (method is ConstructorDeclarationSyntax)
        {
            return "Initializes the generated serialization or mapping state.";
        }

        string name = (method as MethodDeclarationSyntax)?.Identifier.ValueText ?? string.Empty;
        if (name.Contains("CalculateOffset") || name.Contains("Sizeof")) return "Calculates the binary payload size for the supplied value.";
        if (name.Contains("DeserializeJson") || name.Contains("ReadJson")) return "Reads the supplied value from its JSON representation.";
        if (name.Contains("SerializeJson") || name.Contains("WriteJson")) return "Writes the supplied value in its JSON representation.";
        if (name.Contains("Deserialize") || name.StartsWith("Read", StringComparison.Ordinal)) return "Reads the supplied value from its binary representation.";
        if (name.Contains("Serialize") || name.StartsWith("Write", StringComparison.Ordinal)) return "Writes the supplied value in its binary representation.";
        if (name.Contains("Register")) return "Registers or locates the formatter used by generated dispatch.";
        if (name.Contains("Dispatch") || name.Contains("Union")) return "Resolves the generated dispatch for the requested union member.";
        if (name.StartsWith("Get", StringComparison.Ordinal)) return "Obtains the value or reference required by the generated formatter.";
        if (name.StartsWith("Set", StringComparison.Ordinal)) return "Updates the target member used by the generated formatter.";
        if (name.Contains("Map")) return "Maps the configured source members to the destination members.";
        if (name.Contains("Compress")) return "Processes the binary payload using the configured compression format.";
        return "Implements the generated serialization or mapping operation.";
    }

    /// <summary>Describes an emitted parameter according to its role in generated code.</summary>
    /// <param name="parameter">The parameter to document.</param>
    /// <returns>The escaped description for its XML parameter element.</returns>
    private static string GetParameterDescription(ParameterSyntax parameter)
    {
        string name = parameter.Identifier.ValueText;
        string description = name switch
        {
            "value" => "The value consumed or produced by the formatter.",
            "reader" => "The reader supplying serialized input.",
            "writer" or "writerBuffer" or "bufferWriter" => "The writer receiving serialized output.",
            "evaluator" => "The evaluator accumulating the binary payload size.",
            "source" or "src" => "The source value to process.",
            "destination" or "dest" => "The destination receiving the result.",
            "option" or "options" => "The serialization configuration for this operation.",
            "buffer" => "The buffer containing serialized data.",
            "cancellationToken" => "The token used to cancel the asynchronous operation.",
            _ => "The " + name + " used by this generated operation."
        };
        if (parameter.Modifiers.Any(SyntaxKind.OutKeyword)) description = "Receives the " + name + " produced by this operation.";
        return Escape(description);
    }

    /// <summary>Escapes text before placing it in an XML documentation element.</summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The XML-safe text.</returns>
    private static string Escape(string text) => SecurityElement.Escape(text);
}
