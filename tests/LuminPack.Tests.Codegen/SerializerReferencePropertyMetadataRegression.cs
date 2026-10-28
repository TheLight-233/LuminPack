using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

/// <summary>Rejects missing imported automatic-property storage instead of silently dropping wire members.</summary>
internal static class SerializerReferencePropertyMetadataRegression
{
    /// <summary>Checks complete metadata, missing reference storage, exclusions and reference replacement in every tier.</summary>
    /// <param name="references">The runtime and platform metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var producer = CSharpCompilation.Create("ReferencePropertyStorage_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText("""
                using LuminPack.Attribute;
                namespace ImportedProperties;
                [LuminPackable] public sealed class Record { public int First;public string Name{get;set;}="";public long Last; }
                [LuminPackable] public sealed class Computed { public int Number;public int Double=>Number*2; }
                [LuminPackable] public record FieldRecord { public int Number; }
                [LuminPackable(GeneratorType.Custom)] public sealed class Custom
                {
                    public string Name{get;set;}="";
                    public static void Serialize(ref LuminPack.Core.LuminPackWriter writer,in Custom value)=>writer.Write(value.Name);
                    public static void Deserialize(ref LuminPack.Core.LuminPackReader reader,ref Custom value)=>value=new(){Name=reader.Read()};
                }
                """, parse)], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        byte[] implementation = Emit(producer);
        byte[] reference = Emit(producer, new EmitOptions(metadataOnly: true, includePrivateMembers: false));
        AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(implementation));
        var complete = MetadataReference.CreateFromImage(implementation);
        var incomplete = MetadataReference.CreateFromImage(reference);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            string source = $$"""
                using System;
                using LuminPack;
                using LuminPack.Attribute;
                [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.{{mode}})]
                [assembly:LuminPackable(typeof(ImportedProperties.Record))]
                [assembly:LuminPackable(typeof(ImportedProperties.Computed))]
                [assembly:LuminPackable(typeof(ImportedProperties.FieldRecord))]
                public static class Entry
                {
                    public static void Check()
                    {
                        var source=new ImportedProperties.Record{First=137,Name="important",Last=719};
                        byte[] bytes=LuminPackSerializer.Serialize(source);ImportedProperties.Record copy=null;
                        if(LuminPackSerializer.Deserialize(bytes,ref copy)!=bytes.Length||copy.Name!=source.Name||copy.First!=137||copy.Last!=719)
                            throw new Exception("Complete imported property contract changed");
                        string json=LuminPackSerializer.SerializeJson(source);copy=null;LuminPackSerializer.DeserializeJson(json,ref copy);
                        if(copy.Name!=source.Name||!json.Contains("\"Name\":\"important\""))throw new Exception("Complete imported JSON property changed");
                        if(LuminPackSerializer.SerializeJson(new ImportedProperties.Computed{Number=7})!="{\"Number\":7}")throw new Exception("Computed property acquired storage");
                        if(LuminPackSerializer.SerializeJson(new ImportedProperties.FieldRecord{Number=7})!="{\"Number\":7}")throw new Exception("Synthesized record comparison property acquired storage");
                    }
                }
                """;
            var input = CSharpCompilation.Create("ReferencePropertyConsumer_" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source, parse, "PropertyConsumer.cs")], references.Append(complete),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var (driver, output, diagnostics) = Generate(input, parse);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Execute(output);
            var replaced = input.ReplaceReference(complete, incomplete);
            var validator = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.ReferencedTypeMetadata")!
                .GetMethod("ValidatePropertyStorage", BindingFlags.Static | BindingFlags.NonPublic)!;
            var customDiagnostics = (Diagnostic[])validator.Invoke(null, [replaced.GetTypeByMetadataName("ImportedProperties.Custom")!, replaced])!;
            Require(customDiagnostics.Length == 0, "A manual Custom contract acquired inferred backing-storage requirements.");
            driver = driver.RunGeneratorsAndUpdateCompilation(replaced, out _, out diagnostics);
            Require(diagnostics.Any(item => item.Id == "LuminPack112" && item.Severity == DiagnosticSeverity.Error && item.GetMessage().Contains("Name", StringComparison.Ordinal)),
                "Reference-only property storage was silently discarded in " + mode + ".");
            Require(!diagnostics.Any(item => item.Id == "LuminPack112" && item.GetMessage().Contains("Double", StringComparison.Ordinal)), "A computed getter was treated as missing automatic storage.");
            var (fresh, _, freshDiagnostics) = Generate(replaced, parse);
            Require(Trees(driver).SequenceEqual(Trees(fresh), StringComparer.Ordinal) && Messages(diagnostics).SequenceEqual(Messages(freshDiagnostics), StringComparer.Ordinal),
                "Replacing complete metadata retained stale property output or diagnostics.");
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out var restored, out diagnostics);
            Require(!diagnostics.Any(item => item.Id == "LuminPack112"), "Restoring complete storage retained the missing-metadata diagnostic.");
            var (restoredFresh, _, _) = Generate(input, parse);
            Require(Trees(driver).SequenceEqual(Trees(restoredFresh), StringComparer.Ordinal), "Restoring complete metadata retained a stale omitted property.");
            Execute(restored);

            string safe = source.Replace("[assembly:LuminPackable(typeof(ImportedProperties.Record))]", "", StringComparison.Ordinal);
            safe = safe.Substring(0, safe.IndexOf("public static class Entry", StringComparison.Ordinal)) + "public static class Safe { public static string Write()=>LuminPackSerializer.SerializeJson(new ImportedProperties.Computed{Number=7}); }";
            var safeInput = input.RemoveAllSyntaxTrees().AddSyntaxTrees(CSharpSyntaxTree.ParseText(safe, parse)).ReplaceReference(complete, incomplete);
            var (_, safeOutput, safeDiagnostics) = Generate(safeInput, parse);
            Require(!safeDiagnostics.Any(item => item.Id == "LuminPack112"), "An unused imported type or computed getter acquired a storage error.");
            Emit(safeOutput);
        }
        Console.WriteLine("Passed imported automatic-property completeness, computed exclusion, four tiers and retained/fresh reference replacement.");
    }

    /// <summary>Runs both generators with the selected exact dependency metadata.</summary>
    /// <param name="input">The consumer compilation.</param>
    /// <param name="parse">Consumer language and target symbols.</param>
    /// <returns>The retained driver, generated output and diagnostics.</returns>
    private static (GeneratorDriver Driver, Compilation Output, System.Collections.Immutable.ImmutableArray<Diagnostic> Diagnostics) Generate(CSharpCompilation input, CSharpParseOptions parse)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        return (driver, output, diagnostics);
    }

    /// <summary>Emits either the implementation or standard reference metadata.</summary>
    /// <param name="input">The complete source or generated compilation.</param>
    /// <param name="options">Optional reference-only metadata emission.</param>
    /// <returns>The emitted assembly bytes.</returns>
    private static byte[] Emit(Compilation input, EmitOptions? options = null)
    {
        using var stream = new MemoryStream();
        var result = input.Emit(stream, options: options);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        return stream.ToArray();
    }

    /// <summary>Executes complete-metadata binary and JSON field validation.</summary>
    /// <param name="input">The generated consumer.</param>
    private static void Execute(Compilation input) => Assembly.Load(Emit(input)).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);

    /// <summary>Orders frozen generated sources for retained-versus-fresh comparison.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>The stable complete generated trees.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(result => result.HintName + "\n" + result.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Orders diagnostic identities and text across retained and fresh generator runs.</summary>
    /// <param name="diagnostics">The current generated diagnostics.</param>
    /// <returns>The diagnostic comparison keys.</returns>
    private static string[] Messages(IEnumerable<Diagnostic> diagnostics) => diagnostics.Select(item => item.Id + " " + item.GetMessage()).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a generation, metadata or runtime invariant failure.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
