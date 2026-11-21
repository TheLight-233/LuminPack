extern alias ModelBasedAnalyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

internal static partial class SerializerIssueRegression
{
    /// <summary>Verifies inferred record contracts, transitive Union cases and declaration-owned generation modes.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunDerivedTypeInclusion(MetadataReference[] references)
    {
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]

            [LuminPackable, LuminPackOption(IncludeDerivedTypes = true)]
            public abstract partial class IncludedRoot { public int Header; }
            public abstract partial class IntermediateBranch : IncludedRoot { public int Middle; }
            public abstract partial class EmptyBranch : IncludedRoot { }
            public sealed partial class IncludedLeaf : IntermediateBranch { public int Tail; }
            public sealed partial class IncludedDirect : IncludedRoot { public int Count; }

            [LuminPackable, LuminPackOption(IncludeDerivedTypes = true, GeneratorType = GeneratorType.Version)]
            public class VersionedParent { public int First; }
            public sealed class InferredDefaultChild : VersionedParent { public int Last; }
            [LuminPackOption(GeneratorType = GeneratorType.Version)]
            public sealed class InferredVersionChild : VersionedParent { public int Last; }

            [LuminPackable, LuminPackOption(IncludeDerivedTypes = true)]
            public abstract partial class IncludedGenericRoot<T> { }
            public sealed partial class IncludedGenericCase<T> : IncludedGenericRoot<T> { public string Value; public int Count; }
            public sealed partial class IncludedClosedCase : IncludedGenericRoot<int> { public int Value; public int Count; }
            public interface IOpenIncludedCase { int Count { get; } }
            public sealed partial class IncludedOpenCase<T> : IncludedGenericRoot<T>, IOpenIncludedCase
            {
                public int Count { get; set; }
            }

            [LuminPackable]
            public abstract partial class OrdinaryRoot { public int Header; }
            [LuminPackable]
            public sealed partial class OrdinaryCase : OrdinaryRoot { }
            public sealed partial class OptedOutCase : OrdinaryRoot { }

            public static class Entry
            {
                private static IncludedGenericRoot<T> CreateOpenCase<T>(int count) => new IncludedOpenCase<T> { Count = count };

                public static void Check()
                {
                    IncludedRoot root = new IncludedLeaf { Header = 3, Middle = 5, Tail = 7 };
                    IncludedRoot rootCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(root), ref rootCopy);
                    if (rootCopy is not IncludedLeaf { Header: 3, Middle: 5, Tail: 7 })
                        throw new Exception("The transitive Union case lost its inherited binary storage.");
                    rootCopy = null;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(root), ref rootCopy);
                    if (rootCopy is not IncludedLeaf { Header: 3, Middle: 5, Tail: 7 })
                        throw new Exception("The transitive Union case lost its inherited JSON storage.");

                    IntermediateBranch branch = (IncludedLeaf)root;
                    IntermediateBranch branchCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(branch), ref branchCopy);
                    if (branchCopy is not IncludedLeaf { Header: 3, Middle: 5, Tail: 7 })
                        throw new Exception("The inferred intermediate Union did not retain its concrete descendant.");
                    branchCopy = null;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(branch), ref branchCopy);
                    if (branchCopy is not IncludedLeaf { Header: 3, Middle: 5, Tail: 7 })
                        throw new Exception("The inferred intermediate JSON Union did not retain its concrete descendant.");

                    var direct = new IncludedDirect { Header = 11, Count = 13 };
                    IncludedDirect directCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(direct), ref directCopy);
                    if (directCopy is not { Header: 11, Count: 13 })
                        throw new Exception("The inferred direct case lacked a binary root.");
                    directCopy = null;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(direct), ref directCopy);
                    if (directCopy is not { Header: 11, Count: 13 })
                        throw new Exception("The inferred direct case lacked a JSON root.");

                    var plain = new InferredDefaultChild { First = 17, Last = 19 };
                    byte[] plainBytes = LuminPackSerializer.Serialize(plain);
                    InferredDefaultChild plainCopy = null;
                    LuminPackSerializer.Deserialize(plainBytes, ref plainCopy);
                    if (plainBytes.Length != 9 || plainBytes[0] != 2 || plainCopy is not { First: 17, Last: 19 })
                        throw new Exception("The inferred child inherited its parent's Version mode.");
                    var versioned = new InferredVersionChild { First = 23, Last = 29 };
                    byte[] versionedBytes = LuminPackSerializer.Serialize(versioned);
                    InferredVersionChild versionedCopy = null;
                    LuminPackSerializer.Deserialize(versionedBytes, ref versionedCopy);
                    if (versionedBytes.Length <= plainBytes.Length || versionedCopy is not { First: 23, Last: 29 })
                        throw new Exception("The inferred child lost its own Version mode.");

                    IncludedGenericRoot<string> generic = new IncludedGenericCase<string> { Value = "generic", Count = 31 };
                    IncludedGenericRoot<string> genericCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(generic), ref genericCopy);
                    if (genericCopy is not IncludedGenericCase<string> { Value: "generic", Count: 31 })
                        throw new Exception("The inferred generic case did not close under its binary Union root.");
                    genericCopy = null;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(generic), ref genericCopy);
                    if (genericCopy is not IncludedGenericCase<string> { Value: "generic", Count: 31 })
                        throw new Exception("The inferred generic case did not close under its JSON Union root.");

                    IncludedGenericRoot<int> closed = new IncludedClosedCase { Value = 37, Count = 41 };
                    IncludedGenericRoot<int> closedCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(closed), ref closedCopy);
                    if (closedCopy is not IncludedClosedCase { Value: 37, Count: 41 })
                        throw new Exception("The inferred case with a closed generic base did not round-trip.");

                    IncludedGenericRoot<long> open = CreateOpenCase<long>(47);
                    IncludedGenericRoot<long> openCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(open), ref openCopy);
                    if (openCopy is not IOpenIncludedCase { Count: 47 })
                        throw new Exception("The inferred open case was missing from binary Union dispatch.");
                    openCopy = null;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(open), ref openCopy);
                    if (openCopy is not IOpenIncludedCase { Count: 47 })
                        throw new Exception("The inferred open case was missing from JSON Union dispatch.");

                    OrdinaryRoot ordinary = new OrdinaryCase { Header = 43 };
                    OrdinaryRoot ordinaryCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(ordinary), ref ordinaryCopy);
                    if (ordinaryCopy is not OrdinaryCase { Header: 43 })
                        throw new Exception("The generator lost an explicitly marked Union case.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Minimal" })
            Execute(references, source.Replace("MODE", mode, StringComparison.Ordinal), "IncludeDerivedTypes" + mode, InspectDerivedTypeInclusion);
        VerifyDerivedInclusionInvalidation(references);
        Console.WriteLine("Passed inferred records, transitive Union cases, default opt-out, declaration-owned options and incremental invalidation.");
    }

    /// <summary>Checks that an empty abstract branch and a descendant without opt-in do not acquire contracts.</summary>
    /// <param name="result">The generated consumer source.</param>
    private static void InspectDerivedTypeInclusion(GeneratorDriverRunResult result)
    {
        var generated = result.Results.SelectMany(item => item.GeneratedSources).ToArray();
        if (generated.Any(item => item.HintName.Contains("EmptyBranch", StringComparison.Ordinal)))
            throw new InvalidOperationException("An abstract branch without concrete cases acquired a formatter.");
        if (generated.Any(item => item.SourceText.ToString().Contains("OptedOutCase", StringComparison.Ordinal)))
            throw new InvalidOperationException("An unmarked descendant without an opt-in base appeared in generated source.");
    }

    /// <summary>Checks that editing only the base syntax tree updates inferred contracts for unchanged descendants.</summary>
    /// <param name="references">Platform and runtime references.</param>
    private static void VerifyDerivedInclusionInvalidation(MetadataReference[] references)
    {
        const string before = """
            using LuminPack.Attribute;
            [LuminPackable] public class ToggleBase { public int Head; }
            """;
        const string derived = """
            public sealed class ToggleLeaf : ToggleBase { public int Tail; }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        var baseTree = CSharpSyntaxTree.ParseText(before, parse, "Base.cs");
        var leafTree = CSharpSyntaxTree.ParseText(derived, parse, "Leaf.cs");
        var input = TestCompilation.Create("DerivedInclusionInvalidation", [baseTree, leafTree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var enabledTree = baseTree.WithChangedText(SourceText.From(before.Replace("[LuminPackable]",
            "[LuminPackable, LuminPackOption(IncludeDerivedTypes = true)]", StringComparison.Ordinal)));
        var enabled = input.ReplaceSyntaxTree(baseTree, enabledTree);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse);
        foreach (var (compilation, expected) in new[] { (input, false), (enabled, true), (input, false) })
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            var errors = output.GetDiagnostics().Concat(diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(item => item.ToString())));
            bool hasLeaf = driver.GetRunResult().Results.SelectMany(item => item.GeneratedSources)
                .Any(item => item.HintName.Contains("ToggleLeaf", StringComparison.Ordinal));
            if (hasLeaf != expected)
                throw new InvalidOperationException("Editing the base opt-in did not update its unchanged descendant.");
        }
    }
}
