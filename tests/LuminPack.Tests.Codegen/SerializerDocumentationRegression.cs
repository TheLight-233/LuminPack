using System.Text.RegularExpressions;
using System.Reflection;
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
        VerifyExecutableTokens();
        VerifyDocumentationText();
        foreach (var mode in new[] { DocumentationMode.None, DocumentationMode.Parse, DocumentationMode.Diagnose })
        {
            VerifyDocumentationMode(references, mode);
        }
        VerifyNavigationEdits(references);
    }

    /// <summary>Refreshes model and collection Root links after raw storage becomes managed storage.</summary>
    /// <param name="references">The platform and runtime references for the consumer.</param>
    private static void VerifyNavigationEdits(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            [LuminPackable] public struct NavigationItem { public int Value; }
            public static class NavigationEntry
            {
                public static byte[] Write(List<NavigationItem> value) => LuminPackSerializer.Serialize(value);
                public static void Read(byte[] bytes, ref List<NavigationItem> value) => LuminPackSerializer.Deserialize(bytes.AsSpan(), ref value);
                public static byte[] WriteItem(NavigationItem value) => LuminPackSerializer.Serialize(value);
                public static void ReadItem(byte[] bytes, ref NavigationItem value) => LuminPackSerializer.Deserialize(bytes.AsSpan(), ref value);
            }
            """;
        foreach (string mode in new[] { "Full", "Light" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose,
                preprocessorSymbols: ["NET5_0_OR_GREATER", "NET6_0_OR_GREATER", "NET7_0_OR_GREATER", "NET8_0_OR_GREATER"]);
            var tree = CSharpSyntaxTree.ParseText(source, parse, "Navigation.cs");
            var settings = CSharpSyntaxTree.ParseText("[assembly: LuminPack.Attribute.LuminPackGeneratorOptions(LuminPack.Attribute.LuminPackGenerationMode." + mode + ")]", parse);
            var input = CSharpCompilation.Create("NavigationEdits" + mode, [tree, settings], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver NewDriver() => CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            Require(!output.GetDiagnostics().Concat(diagnostics).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.Id == "CS1574"),
                "Initial collection navigation does not bind in " + mode + ".");
            string originalLinks = CollectionNavigation(driver);
            var edited = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(source.Replace("public int Value;", "public string Value;", StringComparison.Ordinal), parse, "Navigation.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out output, out diagnostics);
            Require(!output.GetDiagnostics().Concat(diagnostics).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.Id == "CS1574"),
                "Edited collection navigation does not bind in " + mode + ".");
            string changedLinks = CollectionNavigation(driver);
            Require(changedLinks != originalLinks, "The edited storage fixture did not change model Root availability in " + mode + ".");
            string Text(GeneratorDriver current) => string.Join("\n", current.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .OrderBy(file => file.HintName, StringComparer.Ordinal).Select(file => file.HintName + "\n" + file.SourceText));
            Require(Text(driver) == Text(NewDriver().RunGenerators(edited)), "A retained collection Root link differs from fresh output in " + mode + ".");
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out output, out diagnostics);
            Require(CollectionNavigation(driver) == originalLinks && Text(driver) == Text(NewDriver().RunGenerators(input)),
                "Restoring raw collection storage retained stale Root navigation in " + mode + ".");
        }
    }

    /// <summary>Reads navigation links of the edited storage type and its collection.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>A deterministic sequence of the collection's actual Root links.</returns>
    private static string CollectionNavigation(GeneratorDriver driver)
        => string.Join("|", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Where(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal))
            .SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).Where(IsUserApi)
            .Where(method => method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == "value" &&
                parameter.Type?.ToString().Replace(" ", string.Empty, StringComparison.Ordinal) is
                    "global::System.Collections.Generic.List<global::NavigationItem>" or "global::NavigationItem"))
            .SelectMany(method => ReadDocumentation(method).Descendants("see").Select(element => (string?)element.Attribute("cref")))
            .Where(target => target?.Contains(".__LuminRoots_", StringComparison.Ordinal) == true).OrderBy(target => target, StringComparer.Ordinal));

    /// <summary>Requires final text rendering to match structured trivia across XML modes and declaration boundaries.</summary>
    private static void VerifyDocumentationText()
    {
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.GeneratedSourceDocumentation", throwOnError: true)!;
        var structured = type.GetMethod("WithDocumentation", BindingFlags.Static | BindingFlags.NonPublic)!;
        var render = type.GetMethod("DocumentationText", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var mode in new[] { DocumentationMode.None, DocumentationMode.Parse, DocumentationMode.Diagnose })
        foreach (string leading in new[]
        {
            "",
            "\t/// <summary>Old summary.</summary>\n\t",
            "    /** <summary>Block documentation.</summary> */\n    ",
            "    // Ordinary comment.\n    /// <summary>First.</summary>\n    // Keep this comment.\n    /// <remarks>Second.</remarks>\n    ",
            "#if false\n    disabled text\n#endif\n    /// <summary>Active summary.</summary>\n    ",
            "#pragma warning disable CS1591\n    /// <summary>Summary.</summary>\n    #region API\n    "
        })
        {
            string source = "class C {\n" + leading +
                "[System.Obsolete]\n\tpublic static string Serialize(string value) => value;\n}\n";
            var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview, mode)).GetRoot();
            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            const string documentation = "/// <summary>Updated &amp; escaped text.</summary>\r\n" +
                "\t/// <param name=\"value\">The input.</param>\n" +
                "/// <returns><see cref=\"string\" /></returns>\n";
            foreach (string indentation in new[] { "", "    ", "\t" })
            {
                var replaced = (BaseMethodDeclarationSyntax)structured.Invoke(null, [method, documentation, indentation])!;
                string text = (string)render.Invoke(null, [method, documentation, indentation])!;
                string expected = root.ReplaceNode(method, replaced).ToFullString();
                string actual = source[..method.FullSpan.Start] + text + source[method.SpanStart..];
                Require(actual == expected, "Final documentation rendering changed XML, trivia or attributes in " + mode + ".");
            }
        }
    }

    /// <summary>Requires documentation filtering to preserve directives, literals, method bodies and attributes.</summary>
    private static void VerifyExecutableTokens()
    {
        const string source = """"
            #nullable enable
            namespace LuminPack
            {
                public static class LuminPackSerializer
                {
                    /// <summary>Retained API documentation.</summary>
                    public static string Serialize(string value)
                    {
                        /// <summary>Internal local documentation.</summary>
                        static string Local() => @"first
            /// literal payload
            last";
                        return value + Local();
                    }
                    /// <summary>Internal implementation documentation.</summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                    private static int Root() => 17;
                }
            }
            """";
        var options = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse);
        var type = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.GeneratedSourceDocumentation", throwOnError: true)!;
        string projected = (string)type.GetMethod("Complete", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [source, options])!;
        var before = CSharpSyntaxTree.ParseText(source, options).GetRoot();
        var after = CSharpSyntaxTree.ParseText(projected, options).GetRoot();
        Require(before.DescendantTokens().Select(token => (token.RawKind, token.Text)).SequenceEqual(
            after.DescendantTokens().Select(token => (token.RawKind, token.Text))), "Documentation filtering changed executable tokens.");
        Require(projected.Contains("/// literal payload", StringComparison.Ordinal) &&
            !projected.Contains("Internal local documentation", StringComparison.Ordinal) &&
            !projected.Contains("Internal implementation documentation", StringComparison.Ordinal),
            "Documentation filtering must distinguish XML trivia from literal payload text.");
    }

    /// <summary>Checks that overload comments are complete independently of the consuming compilation's documentation mode.</summary>
    /// <param name="references">The platform and runtime references used by the compilation.</param>
    /// <param name="documentationMode">The consuming compilation's documentation processing mode.</param>
    private static void VerifyDocumentationMode(MetadataReference[] references, DocumentationMode documentationMode)
    {
        const string source = """
            #nullable enable
            #pragma warning disable CS1591
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

            [LuminPackable, LuminPackUnion(0, typeof(DocumentationCase))]
            public abstract partial class DocumentationUnion { }

            [LuminPackable]
            public partial class DocumentationCase : DocumentationUnion { public int Value; }

            [LuminPackable]
            internal class DocumentationInternal { public int Value; }

            [LuminPackable]
            public class DocumentationBox<T> { public T Value = default!; }

            [LuminPackable]
            public class DocumentationBoxHolder { public DocumentationBox<int> Box = new(); }

            [LuminPackable]
            public struct DocumentationEmpty { }

            [LuminPackable, LuminPackOption(UntrustedData = true)]
            public class DocumentationGuarded { public int Value; public string Name = ""; }

            [LuminPackable]
            public class DocumentationCollections
            {
                public KeyValuePair<int, string> Pair;
                public List<string> Names = new();
                public Dictionary<string, int> Lookup = new();
                public int[] Raw = Array.Empty<int>();
            }
            """;

        var options = new CSharpParseOptions(LanguageVersion.Preview, documentationMode,
            preprocessorSymbols: ["NET5_0_OR_GREATER", "NET6_0_OR_GREATER", "NET7_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var compilation = CSharpCompilation.Create("SerializerDocumentationRegression", [CSharpSyntaxTree.ParseText(source, options)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic> { ["CS1591"] = ReportDiagnostic.Error }));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: options);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.Id is "CS0419" or "CS1570" or "CS1572" or "CS1573" or "CS1574" or "CS1584" or "CS1587" or "CS1658").ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
        using var assembly = new MemoryStream();
        using var xml = new MemoryStream();
        var emitted = output.Emit(assembly, xmlDocumentationStream: xml);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        if (documentationMode != DocumentationMode.None)
        {
            xml.Position = 0;
            var entries = XDocument.Load(xml).Descendants("member").Select(member => (string)member.Attribute("name")!).ToArray();
            Require(entries.Length != 0 && entries.All(name => name == "T:LuminPack.LuminPackSerializer" ||
                name.StartsWith("M:LuminPack.LuminPackSerializer.", StringComparison.Ordinal) &&
                !name.Contains("__Lumin", StringComparison.Ordinal)),
                "Emitted XML must contain only the serializer facade and its caller-facing methods.");
        }
        var generated = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
        foreach (var file in generated)
        {
            if (documentationMode != DocumentationMode.None)
            {
                var model = output.GetSemanticModel(file.SyntaxTree);
                foreach (var link in file.SyntaxTree.GetRoot().DescendantNodes(descendIntoTrivia: true).OfType<XmlCrefAttributeSyntax>()
                    .Where(attribute => attribute.Cref.ToString().Contains(".__LuminRoots_", StringComparison.Ordinal)))
                {
                    Require(model.GetSymbolInfo(link.Cref).Symbol is IMethodSymbol target &&
                        target.Name.StartsWith("Root", StringComparison.Ordinal) && target.DeclaringSyntaxReferences.Length == 1,
                        "A Root documentation link did not bind to one generated method: " + link);
                }
            }
            var documentedSyntax = CSharpSyntaxTree.ParseText(file.SourceText, options.WithDocumentationMode(DocumentationMode.Parse)).GetRoot();
            foreach (var method in documentedSyntax.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                if (!IsUserApi(method))
                {
                    Require(!method.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)),
                        "Internal roots, forwarders, and support methods must not retain XML documentation: " + method.GetFirstToken());
                    continue;
                }
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
                     stateMethod.Identifier.ValueText.StartsWith("Deserialize", StringComparison.Ordinal)) &&
                    stateMethod.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText is "writer" or "reader" or "state")
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
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).Where(IsUserApi).ToArray();
        VerifyBuiltInRootNavigation(generated, methods);
        var genericModelOverloads = methods.Where(method => method.Identifier.ValueText != "Sizeof" &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().Contains("global::DocumentationBox<int>", StringComparison.Ordinal) == true)).ToArray();
        Require(genericModelOverloads.Length > 2 && genericModelOverloads.All(method => ReadDocumentation(method).Descendants("see")
            .Count(element => ((string?)element.Attribute("cref"))?.Contains(".__LuminRoots_", StringComparison.Ordinal) == true) == 1),
            "Closed generic model overloads lost their direct Root navigation links.");
        var modelOverloads = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" } && method.TypeParameterList == null &&
            (method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "global::DocumentationModel") ||
             method.ReturnType.ToString().Contains("global::DocumentationModel", StringComparison.Ordinal))).ToArray();
        Require(modelOverloads.Length >= 10, "Expected all concrete buffer, span, JSON, size, and asynchronous overloads.");
        foreach (var overload in modelOverloads)
        {
            var documentation = ReadDocumentation(overload);
            var implementation = overload.GetLeadingTrivia().Where(trivia => trivia.HasStructure)
                .SelectMany(trivia => trivia.GetStructure()!.DescendantNodes()).OfType<XmlCrefAttributeSyntax>()
                .Where(attribute => attribute.Cref.ToString().Contains(".__LuminRoots_", StringComparison.Ordinal)).ToArray();
            if (overload.Identifier.ValueText != "Sizeof")
            {
                Require(implementation.Length == 1, "A concrete model overload has no direct Root navigation link.");
            }
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

    /// <summary>Requires concrete built-in overloads to link to actual roots and omit links when an operation has none.</summary>
    /// <param name="sources">The completed formatter and facade contributions.</param>
    /// <param name="methods">Caller-facing methods parsed with documentation enabled.</param>
    private static void VerifyBuiltInRootNavigation(IEnumerable<GeneratedSourceResult> sources, MethodDeclarationSyntax[] methods)
    {
        static string Payload(TypeSyntax type) => type.ToString().Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        var payloads = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Collections.Generic.KeyValuePair<int,string>",
            "System.Collections.Generic.List<string>",
            "System.Collections.Generic.Dictionary<string,int>",
            "int[]"
        };
        var roots = sources.SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.ParameterList.Parameters.Count == 2 && method.Identifier.ValueText is "RootWrite" or "RootRead" or "RootWriteJson" or "RootReadJson" &&
                method.Parent is ClassDeclarationSyntax owner && owner.Identifier.ValueText.StartsWith("__LuminRoots_", StringComparison.Ordinal))
            .ToDictionary(method => Payload(method.ParameterList.Parameters[1].Type!) + "|" + method.Identifier.ValueText,
                method => "global::LuminPack.LuminPackSerializer." + ((ClassDeclarationSyntax)method.Parent!).Identifier.ValueText + "." + method.Identifier.ValueText,
                StringComparer.Ordinal);
        var observed = new HashSet<string>(StringComparer.Ordinal);
        int links = 0;
        foreach (var method in methods)
        {
            var value = method.ParameterList.Parameters.FirstOrDefault(parameter => parameter.Identifier.ValueText is "value" or "input");
            if (value?.Type == null || !payloads.Contains(Payload(value.Type))) continue;
            string payload = Payload(value.Type);
            observed.Add(payload);
            string name = method.Identifier.ValueText;
            string operation = "Root" + (name.StartsWith("Deserialize", StringComparison.Ordinal) ? "Read" : "Write") +
                (name.Contains("Json", StringComparison.Ordinal) ? "Json" : "");
            string? expected = name == "Sizeof" ? null : roots.GetValueOrDefault(payload + "|" + operation);
            var actual = ReadDocumentation(method).Descendants("see").Select(element => (string?)element.Attribute("cref"))
                .Where(target => target?.Contains(".__LuminRoots_", StringComparison.Ordinal) == true).ToArray();
            Require(expected == null ? actual.Length == 0 : actual.Length == 1 && actual[0] == expected,
                "A concrete built-in API has missing or fictitious Root navigation: " + payload + " " + name);
            links += actual.Length;
        }
        Require(observed.SetEquals(payloads) && links >= 30, "Built-in Root navigation was not exercised across the selected collection shapes.");
    }

    /// <summary>Identifies directly callable serializer APIs independently of the generator's documentation filter.</summary>
    /// <param name="method">A generated method whose documentation is being checked.</param>
    /// <returns>Whether the method is a caller-facing API.</returns>
    private static bool IsUserApi(BaseMethodDeclarationSyntax method) => method is MethodDeclarationSyntax declaration &&
        method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer", Parent: BaseNamespaceDeclarationSyntax } &&
        (method.Modifiers.Any(SyntaxKind.PublicKeyword) || method.Modifiers.Any(SyntaxKind.InternalKeyword) &&
            (declaration.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "Sizeof" or "SerializeAsync" or "DeserializeAsync") &&
            declaration.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText is not ("writer" or "reader" or "evaluator" or "state"));

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
