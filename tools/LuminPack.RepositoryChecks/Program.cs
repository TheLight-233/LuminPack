using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

return RepositoryStandards.Verify(args.Length == 0 ? Directory.GetCurrentDirectory() : args[0]);

/// <summary>Checks source documentation, implementation language, and centralized exception handling.</summary>
internal static class RepositoryStandards
{
    private static readonly string[][] SymbolConfigurations =
    [
        ["NETSTANDARD2_1"],
        ["NET5_0_OR_GREATER", "NET6_0_OR_GREATER", "NET7_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER", "NET11_0_OR_GREATER", "DEBUG", "LUMINPACK_BUFFERWRITER_SECOND_TLS"],
        ["UNITY_2021_2_OR_NEWER", "UNITY_2022_3_OR_NEWER", "ENABLE_IL2CPP", "DEBUG"]
    ];

    /// <summary>Validates repository sources without requiring project-specific analyzer packages.</summary>
    /// <param name="directory">The repository root or a directory below it.</param>
    /// <returns>Zero when every check passes; one when a standards violation is found.</returns>
    internal static int Verify(string directory)
    {
        string root = FindRoot(directory);
        var failures = new SortedSet<string>(StringComparer.Ordinal);
        int methods = 0;
        int files = 0;
        foreach (string folder in new[] { "src", "tests", "benchmarks", "tools", "docs", "skills", ".github" })
        {
            string path = Path.Combine(root, folder);
            if (!Directory.Exists(path)) continue;
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Where(IsRepositoryText))
            {
                string relative = Path.GetRelativePath(root, file);
                string source = File.ReadAllText(file);
                if (!Path.GetFileName(file).Equals("README.md", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (Match match in Regex.Matches(source, @"[\u3400-\u4dbf\u4e00-\u9fff]"))
                    {
                        int line = source.AsSpan(0, match.Index).Count('\n') + 1;
                        failures.Add($"{relative}:{line}: Use English prose and Unicode escapes for multilingual fixtures.");
                    }
                }

                if (Path.GetExtension(file) != ".cs") continue;
                files++;
                var seen = new HashSet<int>();
                foreach (string[] symbols in SymbolConfigurations)
                {
                    var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose, preprocessorSymbols: symbols), file);
                    foreach (var diagnostic in tree.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.Id == "CS1570"))
                        failures.Add($"{relative}:{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}: {diagnostic.Id}: {diagnostic.GetMessage()}");
                    var syntax = tree.GetRoot();
                    foreach (var method in syntax.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
                    {
                        if (seen.Add(method.SpanStart)) methods++;
                        CheckDocumentation(method, relative, failures);
                    }

                    if (relative.StartsWith("src" + Path.DirectorySeparatorChar, StringComparison.Ordinal) && Path.GetFileName(file) != "LuminPackExceptionHelper.cs")
                    {
                        foreach (var token in syntax.DescendantTokens().Where(token => token.IsKind(SyntaxKind.ThrowKeyword)))
                            failures.Add($"{relative}:{token.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: Route exceptions through LuminPackExceptionHelper.");
                        foreach (var literal in syntax.DescendantTokens().Where(token => token.IsKind(SyntaxKind.StringLiteralToken) && Regex.IsMatch(token.ValueText, @"^\s*throw(?:\s+new\b|\s*;)")))
                            failures.Add($"{relative}:{literal.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: Generated code must use LuminPackExceptionHelper.");
                    }
                }
            }
        }

        foreach (string failure in failures) Console.Error.WriteLine(failure);
        Console.WriteLine($"Checked {methods} method declarations in {files} C# files across .NET Standard, modern .NET, and Unity branches; {failures.Count} violation(s).");
        return failures.Count == 0 ? 0 : 1;
    }

    /// <summary>Checks XML syntax and documentation coverage for one active method declaration.</summary>
    /// <param name="method">The declaration to inspect.</param>
    /// <param name="file">The repository-relative diagnostic path.</param>
    /// <param name="failures">The collection receiving standards violations.</param>
    private static void CheckDocumentation(BaseMethodDeclarationSyntax method, string file, ISet<string> failures)
    {
        string location = $"{file}:{method.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
        string comments = string.Join("\n", method.GetLeadingTrivia().Where(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)).Select(trivia => trivia.ToFullString()));
        if (comments.Length == 0)
        {
            failures.Add($"{location}: Missing XML method documentation.");
            return;
        }

        try
        {
            var document = XElement.Parse("<documentation>" + Regex.Replace(comments, @"^[ \t]*///[ ]?", "", RegexOptions.Multiline) + "</documentation>");
            if (document.Element("inheritdoc") != null || document.Element("include") != null) return;
            if (!document.Elements("summary").Any(summary => !string.IsNullOrWhiteSpace(summary.Value)))
                failures.Add($"{location}: Add a non-empty summary.");
            var parameters = method.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
            var documented = document.Elements("param").Select(element => (string?)element.Attribute("name") ?? "").ToHashSet(StringComparer.Ordinal);
            foreach (string missing in parameters.Except(documented)) failures.Add($"{location}: Missing param documentation for {missing}.");
            foreach (string extra in documented.Except(parameters)) failures.Add($"{location}: Unknown documented parameter {extra}.");
            if (method is MethodDeclarationSyntax declaration)
            {
                foreach (var parameter in declaration.TypeParameterList?.Parameters ?? default)
                {
                    if (!document.Elements("typeparam").Any(element => (string?)element.Attribute("name") == parameter.Identifier.ValueText))
                        failures.Add($"{location}: Missing typeparam documentation for {parameter.Identifier.ValueText}.");
                }
                if (declaration.ReturnType.ToString() != "void" && document.Element("returns") == null)
                    failures.Add($"{location}: Missing return-value documentation.");
            }
        }
        catch (XmlException exception)
        {
            failures.Add($"{location}: Invalid XML documentation: {exception.Message}");
        }
    }

    /// <summary>Filters build products, local scratch files, and non-text artifacts from the scan.</summary>
    /// <param name="file">The candidate file path.</param>
    /// <returns>Whether the file belongs to the maintained repository text.</returns>
    private static bool IsRepositoryText(string file)
    {
        if (file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".tmp" or "performance")) return false;
        return Path.GetExtension(file) is ".cs" or ".tt" or ".md" or ".py" or ".yml" or ".yaml";
    }

    /// <summary>Locates the repository root containing the solution.</summary>
    /// <param name="directory">The directory at which to begin searching.</param>
    /// <returns>The absolute repository root.</returns>
    private static string FindRoot(string directory)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(directory)); current != null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "LuminPack.sln"))) return current.FullName;
        }
        Console.Error.WriteLine("Could not locate LuminPack.sln from the supplied directory.");
        Environment.Exit(1);
        return string.Empty;
    }
}
