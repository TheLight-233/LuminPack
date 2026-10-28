using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects closed compound string candidates and their independent string inlining budget.</summary>
internal static class SerializerCompoundStringReuseRegression
{
    /// <summary>Executes profiles in every generation tier and verifies retained closed-contract changes.</summary>
    /// <param name="references">Platform and runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using Scalar = System.Int32;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable,LuminPackOption(StructLayout=LuminPackStructLayout.Packed)]
            public struct Portable { public byte Tag;public long Value; }
            public static class CompoundEntry
            {
                public static int Pair(ReadOnlySpan<byte> bytes,ref KeyValuePair<Scalar,string> value) => LuminPackSerializer.Deserialize(bytes,ref value);
                public static int Value(ReadOnlySpan<byte> bytes,ref ValueTuple<Scalar,string,bool> value) => LuminPackSerializer.Deserialize(bytes,ref value);
                public static int Reference(ReadOnlySpan<byte> bytes,ref Tuple<Scalar,string,bool> value) => LuminPackSerializer.Deserialize(bytes,ref value);
                public static void Check()
                {
                    CheckProfile<LuminPackUtf8LengthOptions>();
                    CheckProfile<LuminPackUtf8TokenOptions>();
                    CheckProfile<LuminPackUtf16LengthOptions>();
                    CheckProfile<LuminPackUtf16TokenOptions>();
                }
                private static void CheckProfile<T>() where T:struct,ILuminPackOptions
                {
                    string text = new string('x',17), candidate = new string('x',17);
                    var pair = new KeyValuePair<Scalar,string>(default(Scalar),text);
                    var target = new KeyValuePair<Scalar,string>(default(Scalar),candidate);
                    byte[] bytes = LuminPackSerializer.Serialize<T>(pair);
                    if(LuminPackSerializer.Deserialize<T>(bytes,ref target)!=bytes.Length || !ReferenceEquals(candidate,target.Value)) throw new Exception("Pair candidate changed.");
                    var tuple = new ValueTuple<Scalar,string,bool>(default(Scalar),text,true);
                    var value = new ValueTuple<Scalar,string,bool>(default(Scalar),candidate,false);
                    bytes=LuminPackSerializer.Serialize<T>(tuple);
                    if(LuminPackSerializer.Deserialize<T>(bytes,ref value)!=bytes.Length || !ReferenceEquals(candidate,value.Item2) || !value.Item3) throw new Exception("ValueTuple candidate changed.");
                    var reference = Tuple.Create(default(Scalar),text,true);
                    var receiver = Tuple.Create(default(Scalar),candidate,false);
                    object original=receiver;
                    bytes=LuminPackSerializer.Serialize<T>(reference);
                    if(LuminPackSerializer.Deserialize<T>(bytes,ref receiver)!=bytes.Length || !ReferenceEquals(candidate,receiver.Item2) || ReferenceEquals(original,receiver)) throw new Exception("Reference tuple construction changed.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            string text = source.Replace("MODE", mode, StringComparison.Ordinal);
            var compilation = CSharpCompilation.Create("CompoundReuse" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(text, parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckSelection(driver, selected: true);
            Execute(output);
            string changed = text.Replace("using Scalar = System.Int32;", "using Scalar = Portable;", StringComparison.Ordinal)
                .Replace(" || !ReferenceEquals(candidate,target.Value)", "", StringComparison.Ordinal)
                .Replace(" || !ReferenceEquals(candidate,value.Item2)", "", StringComparison.Ordinal)
                .Replace(" || !ReferenceEquals(candidate,receiver.Item2)", "", StringComparison.Ordinal);
            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(changed, parse, "Entry.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var editedOutput, out diagnostics);
            CheckSelection(driver, selected: false);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(edited))), "A scalar-to-portable edit retained stale compound reuse.");
            Execute(editedOutput);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out diagnostics);
            CheckSelection(driver, selected: true);
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(compilation))), "Restoring scalar compound storage retained stale exclusions.");
            Execute(output);
        }
        Console.WriteLine("Passed compound string candidates, profiles, four tiers and retained/fresh portable edits.");
    }

    /// <summary>Checks candidate capture and profile-correct outlined string forwarding in each selected root.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="selected">Whether the closed shape contains only supported scalars and strings.</param>
    private static void CheckSelection(GeneratorDriver driver, bool selected)
    {
        int roots = 0;
        foreach (var method in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                     .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                     .Where(method => method.Identifier.ValueText == "RootRead" && method.ParameterList.Parameters.Count == 2))
        {
            string type = method.ParameterList.Parameters[1].Type!.ToString();
            if (!type.Contains("KeyValuePair<", StringComparison.Ordinal) && !type.Contains("ValueTuple<", StringComparison.Ordinal) && !type.Contains("System.Tuple<", StringComparison.Ordinal)) continue;
            Require(method.ToString().Contains("__readCompoundString", StringComparison.Ordinal) == selected,
                "Compound string helper qualification differs: " + type);
            if (selected)
            {
                Require(method.ToString().Contains("NoInlining", StringComparison.Ordinal), "The compound string helper lost its independent inlining budget.");
                Require(method.ToString().Contains("stringReader.Read<__LuminProfile>", StringComparison.Ordinal), "The compound string helper lost its selected profile.");
            }
            roots++;
        }
        Require(roots == 3, "The compound fixture omitted a selected root.");
    }

    /// <summary>Creates a fresh driver for deterministic incremental comparisons.</summary>
    /// <param name="parse">The target language and symbols.</param>
    /// <returns>The independent driver.</returns>
    private static GeneratorDriver Create(CSharpParseOptions parse) => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Compiles and executes the complete consumer fixture.</summary>
    /// <param name="output">The generated consumer.</param>
    private static void Execute(Compilation output)
    {
        using var image = new MemoryStream();
        var result = output.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("CompoundEntry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Captures complete generated hint names and texts.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The sorted complete source snapshot.</returns>
    private static string[] Trees(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a focused invariant failure.</summary>
    /// <param name="condition">Whether the requirement holds.</param>
    /// <param name="message">The precise failed requirement.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
