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
            using System;
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
                Require(method.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)), "Missing generated method documentation: " + method);
        }

        string serializer = generated.Single(file => file.HintName == "LuminPackSerializer.g.cs").SourceText.ToString();
        var methods = CSharpSyntaxTree.ParseText(serializer, options.WithDocumentationMode(DocumentationMode.Parse)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var modelOverloads = methods.Where(method => method.TypeParameterList == null &&
            (method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "global::DocumentationModel") ||
             method.ReturnType.ToString().Contains("global::DocumentationModel", StringComparison.Ordinal))).ToArray();
        Require(modelOverloads.Length >= 10, "Expected all concrete buffer, span, JSON, size, and asynchronous overloads.");
        foreach (var overload in modelOverloads)
        {
            var documentation = ReadDocumentation(overload);
            Require(documentation.Descendants("see").Any(element => (string?)element.Attribute("cref") == "T:DocumentationModel"), "Missing concrete type link: " + overload.Identifier);
            var memberNames = documentation.Descendants("list").First().Elements("item").Select(item => item.Element("term")!.Value).ToArray();
            Require(memberNames.Length == 3 && memberNames[0].EndsWith(".Id", StringComparison.Ordinal) && memberNames[1].EndsWith("._secret", StringComparison.Ordinal) && memberNames[2].EndsWith(".Name", StringComparison.Ordinal),
                "Documented member order or Include/Ignore filtering differs from the formatter contract.");
            Require(!documentation.Value.Contains("Ignored", StringComparison.Ordinal), "Ignored object member leaked into the documented contract.");
            if (overload.ReturnType.ToString() == "int" && overload.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() is "ReadOnlySpan<char>" or "string"))
                Require(documentation.Element("returns")?.Value.Contains("consumed bytes", StringComparison.Ordinal) == true,
                    "Character-input overload must document encoded byte units in its return-value description.");
        }

        var raw = ReadDocumentation(methods.First(method => method.Identifier.ValueText == "Serialize" && method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "global::DocumentationRaw")));
        Require(raw.Value.Contains("Explicit", StringComparison.Ordinal) && raw.Value.Contains("runtime-defined", StringComparison.Ordinal) == false, "Explicit storage offsets should come from FieldOffset attributes.");
        Require(raw.Value.Contains("padding", StringComparison.Ordinal) && raw.Value.Contains("Ignored", StringComparison.Ordinal), "Unmanaged storage documentation must include ignored storage and padding semantics.");
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
    private static XElement ReadDocumentation(MethodDeclarationSyntax method)
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
