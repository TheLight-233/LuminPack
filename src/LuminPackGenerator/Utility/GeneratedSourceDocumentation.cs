using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Supplies documentation for generated methods without rewriting their executable tokens.</summary>
internal static class GeneratedSourceDocumentation
{
    /// <summary>Adds XML comments to generated methods that do not already have documentation.</summary>
    /// <param name="source">The complete source file to document.</param>
    /// <param name="options">The consuming compilation's language and conditional compilation options.</param>
    /// <returns>The source with documentation inserted before undocumented method declarations.</returns>
    internal static string Complete(string source, CSharpParseOptions options)
    {
        var root = CSharpSyntaxTree.ParseText(source, options.WithDocumentationMode(DocumentationMode.Parse)).GetRoot();
        var additions = new List<(int Position, string Documentation)>();
        foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
        {
            if (method.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                                      trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)))
            {
                continue;
            }

            int position = method.SpanStart;
            int lineStart = source.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
            string indentation = source.Substring(lineStart, position - lineStart);
            bool startsOnOwnLine = indentation.All(character => character == ' ' || character == '\t');
            if (!startsOnOwnLine)
            {
                indentation = new string(' ', method.Ancestors().OfType<TypeDeclarationSyntax>().Count() * 4);
            }

            var documentation = new StringBuilder();
            if (!startsOnOwnLine)
            {
                documentation.AppendLine().Append(indentation);
            }

            documentation.Append("/// <summary>").Append(GetSummary(method)).AppendLine("</summary>");
            foreach (var parameter in method.ParameterList.Parameters)
            {
                string name = parameter.Identifier.ValueText;
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
                        documentation.Append(indentation).Append("/// <typeparam name=\"").Append(Escape(name))
                            .Append("\">The type represented by <c>").Append(Escape(name)).AppendLine("</c>.</typeparam>");
                    }
                }

                if (!declaration.ReturnType.IsKind(SyntaxKind.PredefinedType) ||
                    ((PredefinedTypeSyntax)declaration.ReturnType).Keyword.Kind() != SyntaxKind.VoidKeyword)
                {
                    documentation.Append(indentation).AppendLine("/// <returns>The value produced by the generated operation.</returns>");
                }
            }

            documentation.Append(indentation);
            additions.Add((position, documentation.ToString()));
        }

        if (additions.Count == 0)
        {
            return source;
        }

        var result = new StringBuilder(source.Length + additions.Sum(addition => addition.Documentation.Length));
        int copied = 0;
        foreach (var addition in additions.OrderBy(addition => addition.Position))
        {
            result.Append(source, copied, addition.Position - copied);
            result.Append(addition.Documentation);
            copied = addition.Position;
        }

        return result.Append(source, copied, source.Length - copied).ToString();
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
