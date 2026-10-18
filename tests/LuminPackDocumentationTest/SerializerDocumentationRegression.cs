using System.Text.RegularExpressions;
using System.Xml.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks that overload documentation describes the same members and layouts as generated formatters.</summary>
internal static class SerializerDocumentationRegression
{
    /// <summary>Compiles representative contracts and verifies their generated XML documentation.</summary>
    /// <param name="references">The platform and LuminPack references used by the regression compilation.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (var mode in new[] { DocumentationMode.None, DocumentationMode.Parse, DocumentationMode.Diagnose })
        {
            VerifyDocumentationMode(references, mode);
        }
    }

    /// <summary>Checks that overload comments are complete independently of the consuming compilation's documentation mode.</summary>
    /// <param name="references">The platform and runtime references used by the compilation.</param>
    /// <param name="documentationMode">The consuming compilation's documentation processing mode.</param>
    private static void VerifyDocumentationMode(MetadataReference[] references, DocumentationMode documentationMode)
    {
        const string source = """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Attribute;

            [LuminPackable]
            public class DocumentationModel
            {
                [LuminPackIgnore] public int Ignored;
                [LuminPackOrder(2)] public string Name = "";
                [LuminPackOrder(0)] public int Id;
                [LuminPackInclude, LuminPackOrder(1)] private long _secret;
                [LuminPackOrder(3)] public List<(int Code, int Count)> Entries = new();
                [LuminPackOrder(4)] public string? Note;
            }

            [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 24)]
            public struct DocumentationRaw
            {
                [FieldOffset(0)] public long Key;
                [LuminPackIgnore, FieldOffset(8)] public int Ignored;
                [FieldOffset(16)] public double Value;
            }

            [LuminPackable(GeneratorType.VersionTolerant)]
            public class DocumentationVersioned
            {
                [LuminPackOrder(3)] public int VersionedValue;
            }

            [LuminPackable(GeneratorType.CircleReference)]
            public class DocumentationReference
            {
                [LuminPackOrder(0)] public int ReferenceValue;
            }
            """;

        var options = new CSharpParseOptions(LanguageVersion.Preview, documentationMode,
            preprocessorSymbols: ["NET5_0_OR_GREATER", "NET6_0_OR_GREATER", "NET7_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var compilation = CSharpCompilation.Create("SerializerDocumentationRegression", [CSharpSyntaxTree.ParseText(source, options)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: options);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.Id is "CS0419" or "CS1570" or "CS1572" or "CS1573" or "CS1574" or "CS1584" or "CS1587" or "CS1658").ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
        var generated = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
        foreach (var file in generated)
        {
            var documentedSyntax = CSharpSyntaxTree.ParseText(file.SourceText, options.WithDocumentationMode(DocumentationMode.Parse)).GetRoot();
            foreach (var method in documentedSyntax.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                Require(method.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)), "Missing generated method documentation: " + method);
                var documentation = ReadDocumentation(method);
                Require(documentation.Elements("summary").Count() == 1 && !string.IsNullOrWhiteSpace(documentation.Element("summary")!.Value),
                    "Generated method documentation must contain one meaningful summary: " + method.GetFirstToken());
                foreach (var parameter in method.ParameterList.Parameters)
                    Require(documentation.Elements("param").Count(element => (string?)element.Attribute("name") == parameter.Identifier.ValueText) == 1,
                        "Generated method documentation must describe each parameter exactly once: " + parameter);
                if (method is MethodDeclarationSyntax { TypeParameterList: { } typeParameters })
                    foreach (var parameter in typeParameters.Parameters)
                        Require(documentation.Elements("typeparam").Count(element => (string?)element.Attribute("name") == parameter.Identifier.ValueText) == 1,
                            "Generated method documentation must describe each type parameter exactly once: " + parameter);
                if (method is MethodDeclarationSyntax stateMethod &&
                    (stateMethod.Identifier.ValueText.StartsWith("Serialize", StringComparison.Ordinal) ||
                     stateMethod.Identifier.ValueText.StartsWith("Deserialize", StringComparison.Ordinal) || stateMethod.Identifier.ValueText == "Sizeof") &&
                    stateMethod.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText is "writer" or "reader" or "evaluator" or "state")
                {
                    string usage = documentation.Element("remarks")?.Value ?? string.Empty;
                    Require(usage.Contains("state directly", StringComparison.Ordinal) && usage.Contains("buffer position", StringComparison.Ordinal) &&
                        usage.Contains("reference tracking", StringComparison.Ordinal) && usage.Contains("Options are compile-time", StringComparison.Ordinal) &&
                        usage.Contains("caller owns initialization and completion", StringComparison.Ordinal),
                        "State overload documentation lost buffer, reference, option, or lifetime ownership semantics.");
                }
                if (method.Modifiers.Count != 0)
                {
                    int column = method.Modifiers[0].GetLocation().GetLineSpan().StartLinePosition.Character;
                    foreach (var attribute in method.AttributeLists)
                        Require(attribute.GetLocation().GetLineSpan().StartLinePosition.Character == column,
                            "Generated method attributes must retain their declaration's indentation: " + attribute);
                }
            }
        }

        var serializerFiles = generated.Where(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal)).ToArray();
        string serializer = string.Join(Environment.NewLine, serializerFiles.Select(file => file.SourceText.ToString()));
        var methods = serializerFiles.SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, options.WithDocumentationMode(DocumentationMode.Parse))
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        var modelOverloads = methods.Where(method => method.TypeParameterList == null &&
            (method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "global::DocumentationModel") ||
             method.ReturnType.ToString().Contains("global::DocumentationModel", StringComparison.Ordinal))).ToArray();
        Require(modelOverloads.Length >= 10, "Expected all concrete buffer, span, JSON, size, and asynchronous overloads.");
        foreach (var overload in modelOverloads)
        {
            var documentation = ReadDocumentation(overload);
            Require(documentation.Element("summary")!.Elements("see").Any(element =>
                (string?)element.Attribute("cref") == "T:DocumentationModel" && element.IsEmpty),
                "Missing compact concrete type link in the overload summary: " + overload.Identifier);
            Require(documentation.Element("remarks")!.Value.Contains("Reference type. Uses managed reference handling.", StringComparison.Ordinal),
                "Compact overload remarks lost the value/reference or managed-storage classification.");
            Require(documentation.Descendants("see").Any(element =>
                ((string?)element.Attribute("cref"))?.StartsWith("T:System.Collections.Generic.List{", StringComparison.Ordinal) == true &&
                element.Value == "System.Collections.Generic.List<(int Code, int Count)>"),
                "Generic member documentation lost its closed arguments or named tuple elements.");
            Require(documentation.Descendants("see").Any(element =>
                (string?)element.Attribute("cref") == "T:System.String" && element.Value == "string?"),
                "Nullable member documentation lost its annotated reference type marker.");
            var memberNames = documentation.Descendants("list").First().Elements("item").Select(item => item.Element("term")!.Value).ToArray();
            Require(memberNames.Length == 5 && memberNames[0].EndsWith(".Id", StringComparison.Ordinal) && memberNames[1].EndsWith("._secret", StringComparison.Ordinal) &&
                memberNames[2].EndsWith(".Name", StringComparison.Ordinal) && memberNames[3].EndsWith(".Entries", StringComparison.Ordinal) && memberNames[4].EndsWith(".Note", StringComparison.Ordinal),
                "Documented member order or Include/Ignore filtering differs from the formatter contract.");
            Require(!documentation.Value.Contains("Ignored", StringComparison.Ordinal), "Ignored object member leaked into the documented contract.");
            if (overload.ReturnType.ToString() == "int" && overload.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() is "ReadOnlySpan<char>" or "string"))
                Require(documentation.Element("returns")?.Value.Contains("consumed bytes", StringComparison.Ordinal) == true,
                    "Character-input overload must document encoded byte units in its return-value description.");
        }

        var raw = ReadDocumentation(methods.First(method => method.Identifier.ValueText == "Serialize" && method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "global::DocumentationRaw")));
        Require(raw.Value.Contains("Explicit", StringComparison.Ordinal) && raw.Value.Contains("runtime-defined", StringComparison.Ordinal) == false, "Explicit storage offsets should come from FieldOffset attributes.");
        Require(raw.Value.Contains("padding", StringComparison.Ordinal) && raw.Value.Contains("Ignored", StringComparison.Ordinal), "Unmanaged storage documentation must include ignored storage and padding semantics.");
        Require(raw.Element("remarks")!.Value.Contains("Value type. Contains no managed references.", StringComparison.Ordinal),
            "Compact unmanaged remarks lost the value/reference or managed-storage classification.");
        var jsonMembers = raw.Descendants("list").Last().Elements("item").Select(item => item.Element("term")!.Value).ToArray();
        Require(jsonMembers.SequenceEqual(new[] { "Key", "Value" }), "JSON member selection must remain distinct from unmanaged storage copying.");
        Require(serializer.Contains("Generation mode: <c>VersionTolerant</c>", StringComparison.Ordinal), "Missing version-tolerant mode documentation.");
        Require(serializer.Contains("Generation mode: <c>CircleReference</c>", StringComparison.Ordinal), "Missing circular-reference mode documentation.");
        Require(methods.Where(method => method.TypeParameterList?.Parameters.Any(parameter => parameter.Identifier.ValueText == "T") == true && method.Identifier.ValueText.StartsWith("Serialize", StringComparison.Ordinal))
            .All(method => ReadDocumentation(method).Descendants("typeparamref").Any()), "Generic overloads must describe their formatter-selected type contract.");
        foreach (var overload in methods.Where(method => method.Identifier.ValueText == "DeserializeAsync" && method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == "value")))
            Require(ReadDocumentation(overload).Elements("param").Single(element => (string?)element.Attribute("name") == "value").Value.Contains("ignored", StringComparison.Ordinal),
                "Asynchronous compatibility overloads must accurately document their unused initial value.");
        foreach (var overload in methods.Where(method => method.ReturnType.ToString() == "byte[]" && method.Identifier.ValueText is "Compress" or "Decompress"))
            Require(ReadDocumentation(overload).Element("returns")?.Value.Contains("byte array", StringComparison.Ordinal) == true,
                "Allocating compression overloads must document their returned payload rather than a byte count.");

        File.WriteAllText(Path.Combine(Path.GetTempPath(), "LuminPack.SerializerDocumentationRegression.g.cs"), serializer);
    }

    /// <summary>Parses the XML comment associated with a generated method.</summary>
    /// <param name="method">The generated declaration.</param>
    /// <returns>The documentation element containing summary, remarks, and parameter details.</returns>
    private static XElement ReadDocumentation(BaseMethodDeclarationSyntax method)
    {
        string xml = string.Join(Environment.NewLine, method.GetLeadingTrivia().Where(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)).Select(trivia => trivia.ToFullString()));
        return XElement.Parse("<documentation>" + Regex.Replace(xml, @"^[ \t]*///[ ]?", "", RegexOptions.Multiline) + "</documentation>");
    }

    /// <summary>Fails the regression run when a generated contract expectation is not met.</summary>
    /// <param name="condition">The expectation being checked.</param>
    /// <param name="message">The failure description.</param>
    /// <exception cref="InvalidOperationException">The expectation fails.</exception>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
