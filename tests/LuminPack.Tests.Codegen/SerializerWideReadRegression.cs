using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks the publication and reuse of large nested reads inside a collection element loop.</summary>
internal static class SerializerWideReadRegression
{
    /// <summary>Executes the generated checked reader with sparse, reused, truncated and invalid payloads.</summary>
    /// <param name="references">The runtime and compiler references for the consumer.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            [LuminPackable] public class Event { public Guid Id; public string Label { get; set; } = "sensor"; public long Count; }
            [LuminPackable] public class Bucket { public string Source = "device"; public Event Records = new(); public bool Active = true; }
            [LuminPackable] public class Window { public int Revision; public Bucket A = new(), B = new(), C = new(), D = new(); }
            [LuminPackable] public class Zone { public int Revision; public Window A = new(), B = new(), C = new(), D = new(); }
            [LuminPackable] public class Snapshot { public int ReadMemberBlock0; public Zone A = new(), B = new(), C = new(), D = new(); }
            public static class Entry
            {
                public static void Check()
                {
                    var original = new Snapshot { ReadMemberBlock0 = 7, B = null };
                    original.A.A.A.Records.Label = "front";
                    original.D.D.D.Records.Count = 99;
                    var bytes = LuminPackSerializer.Serialize(new List<Snapshot> { original });
                    List<Snapshot> copy = null;
                    int count = LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (count != bytes.Length || copy.Count != 1 || copy[0].B != null || copy[0].D.D.D.Records.Count != 99)
                        throw new Exception("Fresh checked list read lost content.");
                    var element = copy[0];
                    var front = element.A;
                    var last = element.D.D.D.Records;
                    if (!LuminPackSerializer.Serialize(copy).SequenceEqual(bytes))
                        throw new Exception("Successful list read changed the wire.");
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (copy[0] != element || element.A != front || element.D.D.D.Records != last)
                        throw new Exception("Reused element identities changed.");
                    for (int damage = 0; damage < 2; damage++)
                    {
                        element.A.A.A.Records.Label = "before";
                        element.B = new Zone();
                        element.D = null;
                        byte[] input = damage == 0 ? bytes[..^1] : (byte[])bytes.Clone();
                        if (damage == 1) input[^1] = 7;
                        bool rejected = false;
                        try { LuminPackSerializer.Deserialize(input, ref copy); }
                        catch (Exception error) when (damage == 0
                            ? error.GetType().DeclaringType == typeof(global::LuminPack.Code.LuminPackExceptionHelper)
                            : error is System.IO.InvalidDataException) { rejected = true; }
                        if (!rejected || copy[0] != element || element.A != front || front.A.A.Records.Label != "front" ||
                            element.B != null || element.D != null)
                            throw new Exception("A failed member read changed the validated prefix or published an incomplete child.");
                    }
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        var input = TestCompilation.Create("WideReadRegression", [CSharpSyntaxTree.ParseText(source, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        if (!emitted.Success || diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Concat(emitted.Diagnostics).Where(d => d.Severity == DiagnosticSeverity.Error)));
        var helpers = output.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            .Where(helper => helper.Identifier.ValueText.StartsWith("ReadMemberBlock", StringComparison.Ordinal)).ToArray();
        if (!helpers.Any(helper => helper.Ancestors().OfType<MethodDeclarationSyntax>().First().ParameterList.Parameters.Last()
                .Type!.ToString().Contains("List<global::Snapshot>", StringComparison.Ordinal)) ||
            helpers.Any(helper => !helper.Modifiers.Any(SyntaxKind.StaticKeyword)))
            throw new InvalidOperationException("The wide list reader did not isolate its nested member reads.");
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
        Console.WriteLine("Passed wide list reader code shape, identity, null, invalid Boolean and truncated input publication.");
    }
}
