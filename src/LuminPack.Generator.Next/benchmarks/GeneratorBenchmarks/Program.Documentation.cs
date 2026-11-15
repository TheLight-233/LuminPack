using System.Xml.Linq;
using LuminPack.SourceGenerator.Next.Rendering;
using LuminPack.SourceGenerator.Next.Rendering.Facade;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    /// <summary>Checks public facade XML and verifies that implementation signatures carry no XML.</summary>
    /// <param name="references">The consumer's framework and runtime metadata.</param>
    /// <param name="parse">The target and language settings.</param>
    /// <returns>Zero after all XML and retained-driver contracts pass.</returns>
    private static int VerifyDocumentation(MetadataReference[] references, CSharpParseOptions parse)
    {
        int apis = 0;
        foreach (DocumentationMode mode in new[] { DocumentationMode.None, DocumentationMode.Parse, DocumentationMode.Diagnose })
        {
            CSharpParseOptions language = parse.WithDocumentationMode(mode);
            int callStart = DocumentationFixture.IndexOf("public static class DocEntry", StringComparison.Ordinal);
            string declarations = DocumentationFixture.Substring(0, callStart);
            var tree = CSharpSyntaxTree.ParseText(declarations, language, "Documentation.cs");
            var calls = CSharpSyntaxTree.ParseText("using LuminPack; " + DocumentationFixture.Substring(callStart), language, "Calls.cs");
            var input = CSharpCompilation.Create("NextDocumentation", new[] { tree, calls }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver CreateDriver() => CSharpGeneratorDriver.Create(new[] { new LuminPackSourceGenerator().AsSourceGenerator() }, parseOptions: language);
            GeneratorDriver driver = CreateDriver().RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            CheckDiagnostics(output, diagnostics);
            if (mode == DocumentationMode.Diagnose)
            {
                using var image = new MemoryStream();
                using var documentation = new MemoryStream();
                var emitted = output.Emit(image, xmlDocumentationStream: documentation);
                RequireIncremental(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
                CheckDiagnostics(output, emitted.Diagnostics);
                documentation.Position = 0;
                XDocument compiled = XDocument.Load(documentation);
                RequireIncremental(compiled.Descendants("member").Any(static member =>
                    ((string?)member.Attribute("name"))?.Contains("LuminPackSerializer.Serialize", StringComparison.Ordinal) == true),
                    "The compiler did not export public facade documentation.");
            }
            Dictionary<string, string> original = Sources(driver);
            foreach (string source in original.Values)
            {
                var root = CSharpSyntaxTree.ParseText(source, language.WithDocumentationMode(DocumentationMode.Parse)).GetRoot();
                foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    bool api = method.Parent is ClassDeclarationSyntax owner && owner.Identifier.ValueText == "LuminPackSerializer" &&
                        method.Modifiers.Any(SyntaxKind.PublicKeyword);
                    string comment = string.Concat(method.GetLeadingTrivia().Where(static trivia =>
                        trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)).Select(static trivia => trivia.ToFullString()));
                    if (!api)
                    {
                        RequireIncremental(comment.Length == 0, "XML leaked onto generated implementation: " + method.Identifier.ValueText);
                        continue;
                    }
                    string xml = string.Join((char)10, comment.Split((char)10).Select(static line => line.TrimStart().StartsWith("///", StringComparison.Ordinal)
                        ? line.TrimStart().Substring(3) : line));
                    XElement document = XElement.Parse("<documentation>" + xml + "</documentation>");
                    RequireIncremental(document.Elements("summary").Count() == 1, "Missing or repeated API summary: " + method.Identifier.ValueText);
                    foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
                        RequireIncremental(document.Elements("param").Count(element => (string?)element.Attribute("name") == parameter.Identifier.ValueText) == 1,
                            "Missing or repeated parameter XML: " + parameter.Identifier.ValueText);
                    if (method.TypeParameterList != null)
                        foreach (TypeParameterSyntax parameter in method.TypeParameterList.Parameters)
                            RequireIncremental(document.Elements("typeparam").Count(element => (string?)element.Attribute("name") == parameter.Identifier.ValueText) == 1,
                                "Missing or repeated type-parameter XML: " + parameter.Identifier.ValueText);
                    if (!method.ReturnType.IsKind(SyntaxKind.PredefinedType) || method.ReturnType.ToString() != "void")
                        RequireIncremental(document.Elements("returns").Count() == 1, "Missing return XML.");
                    apis++;
                }
                string completed = SerializerDocumentation.Facade(source, typeSummary: false);
                RequireIncremental(Tokens(source, language) == Tokens(completed, language), "Documentation changed executable tokens.");
            }
            RequireIncremental(original.Values.All(static source => !source.Contains("class LuminEntry_", StringComparison.Ordinal)),
                "An interceptor regenerated a concrete convenience overload container.");
        }
        foreach (string facade in new[] { SerializerFacadeSource.Binary, SerializerFacadeSource.Json })
            RequireIncremental(Tokens(facade, parse) == Tokens(SerializerDocumentation.Facade(facade, typeSummary: true), parse),
                "Generic facade XML changed executable tokens.");
        Console.WriteLine($"Next XML contracts: {apis} public APIs checked across three documentation modes with unchanged executable tokens.");
        return 0;

        static string Tokens(string source, CSharpParseOptions language) => string.Join("|", CSharpSyntaxTree.ParseText(source, language)
            .GetRoot().DescendantTokens().Select(static token => token.RawKind + ":" + token.Text));

        static void CheckDiagnostics(Compilation output, IEnumerable<Diagnostic> diagnostics)
        {
            foreach (Diagnostic diagnostic in diagnostics.Concat(output.GetDiagnostics()))
                RequireIncremental(diagnostic.Severity != DiagnosticSeverity.Error && diagnostic.Id is not
                    ("CS1570" or "CS1572" or "CS1573" or "CS1574" or "CS1580" or "CS1584" or "CS1658" or "CS0419"), diagnostic.ToString());
        }
    }

    private const string DocumentationFixture = """
        using System;
        using System.Collections.Generic;
        using System.Runtime.InteropServices;
        using LuminPack;
        using LuminPack.Attribute;
        [LuminPackable] public class DocBase { public int BaseValue; }
        [LuminPackable] public sealed class Documented : DocBase
        {
            public string Name { get; set; }
            [LuminPackInclude] private int Secret;
            [LuminPackIgnore] public int Ignored;
            public List<int> Values;
        }
        [LuminPackable, StructLayout(LayoutKind.Sequential, Pack = 4)]
        public sealed class LayoutOnly { public int Number; }
        [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native), StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct NativeExplicit
        {
            [FieldOffset(0)] public long Value;
            [FieldOffset(8), LuminPackIgnore] public int Padding;
        }
        [LuminPackable(GeneratorType.VersionTolerant)]
        public sealed class VersionedDoc { [LuminPackOldName("old_value")] public int Number; public string Name; }
        [LuminPackable(GeneratorType.CircleReference)]
        public sealed class CircleDoc { [LuminPackOrder(0)] public CircleDoc Next; [LuminPackOrder(1)] public int Number; }
        [LuminPackable] public sealed class GenericDoc<T> { public T Value; public string Name; }
        [LuminPackable, LuminPackUnion(1, typeof(KnownDoc))] public abstract partial class RootDoc { }
        [LuminPackable] public sealed partial class KnownDoc : RootDoc { public int Number; }
        public static class DocEntry
        {
            public static byte[] WriteArray(Documented[] value) => LuminPackSerializer.Serialize(value);
            public static byte[] WriteGeneric(GenericDoc<int> value) => LuminPackSerializer.Serialize(value);
            public static byte[] WriteVersion(VersionedDoc value) => LuminPackSerializer.Serialize(value);
            public static byte[] WriteCircle(CircleDoc value) => LuminPackSerializer.Serialize(value);
            public static byte[] WriteNative(NativeExplicit value) => LuminPackSerializer.Serialize(value);
            public static byte[] WriteLayout(LayoutOnly value) => LuminPackSerializer.Serialize(value);
            public static byte[] WriteObject(Documented value) => LuminPackSerializer.Serialize(value);
            public static byte[] WriteUnion(RootDoc value) => LuminPackSerializer.Serialize(value);
            public static Documented ReadObject(byte[] bytes) => LuminPackSerializer.Deserialize<Documented>(bytes);
            public static GenericDoc<System.Collections.Generic.Dictionary<int, string>> ReadGeneric(byte[] bytes)
                => LuminPackSerializer.Deserialize<GenericDoc<System.Collections.Generic.Dictionary<int, string>>>(bytes);
            public static System.Threading.Tasks.ValueTask<GenericDoc<System.Collections.Generic.Dictionary<int, string>>> ReadAsync(System.IO.Stream stream)
                => LuminPackSerializer.DeserializeAsync<GenericDoc<System.Collections.Generic.Dictionary<int, string>>>(stream);
        }
        """;
}
