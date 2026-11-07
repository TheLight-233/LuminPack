using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableQueueConstructionRegression
{
    /// <summary>Verifies exclusive builder generation and invalidated runtime capabilities in every tier.</summary>
    /// <param name="references">The actual consumer metadata.</param>
    internal static void RunBuilders(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var initial = CSharpCompilation.Create("BuilderCases" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(BuilderSource.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var (driver, output) = Generate(initial, parse);
            CheckBuilders(driver, output, available: true);
            foreach (string declaration in new[]
            {
                "public static class LuminImmutableBuilders { }",
                "public static class LuminImmutableBuilders { public static System.Collections.Immutable.ImmutableDictionary<K,V>.Builder RentDictionary<K,V>() => null; }",
                "public static class LuminImmutableBuilders { public static System.Collections.Immutable.ImmutableDictionary<K,V>.Builder RentDictionary<K,V>() where V:class => null; public static void ReturnDictionary<K,V>(System.Collections.Immutable.ImmutableDictionary<K,V>.Builder b) where V:class { } }",
                "internal static class LuminImmutableBuilders { public static System.Collections.Immutable.ImmutableDictionary<K,V>.Builder RentDictionary<K,V>() => null; public static void ReturnDictionary<K,V>(System.Collections.Immutable.ImmutableDictionary<K,V>.Builder b) { } }"
            })
            {
                var changed = initial.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace LuminPack.Utility { " + declaration + " }", parse));
                driver = driver.RunGeneratorsAndUpdateCompilation(changed, out output, out var diagnostics);
                Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
                CheckBuilders(driver, output, available: false);
                var (fresh, _) = Generate(changed, parse);
                Require(Trees(driver).SequenceEqual(Trees(fresh)), "Builder capability edits retained stale generated ownership.");
            }
            driver = driver.RunGeneratorsAndUpdateCompilation(initial, out output, out _);
            CheckBuilders(driver, output, available: true);
            var (restored, _) = Generate(initial, parse);
            Require(Trees(driver).SequenceEqual(Trees(restored)), "Restoring builder APIs retained stale construction.");
        }
        Console.WriteLine("Passed immutable builders, binary/UTF8/UTF16, four tiers, missing/invalid/restored APIs and retained/fresh generation.");
    }

    /// <summary>Checks the complete ownership pair and executes all affected generated collection families.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="output">The generated consumer compilation.</param>
    /// <param name="available">Whether the actual runtime exposes the complete rental contract.</param>
    private static void CheckBuilders(GeneratorDriver driver, Compilation output, bool available)
    {
        string generated = string.Join("\n", Trees(driver));
        foreach (string family in new[] { "Dictionary", "SortedDictionary", "HashSet", "SortedSet" })
        {
            Require(generated.Contains("LuminImmutableBuilders.Rent" + family, StringComparison.Ordinal) == available, "Wrong rental capability for " + family);
            Require(generated.Contains("LuminImmutableBuilders.Return" + family, StringComparison.Ordinal) == available, "Missing cleanup capability for " + family);
        }
        using var image = new MemoryStream();
        var result = output.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("BuilderCases")!.GetMethod("Check")!.Invoke(null, null);
    }

    private const string BuilderSource = """
        using System;
        using System.Linq;
        using System.Text;
        using System.Collections.Immutable;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable] public struct BuilderKey : IEquatable<BuilderKey>, IComparable<BuilderKey>
        {
            public int Value;
            public int CompareTo(BuilderKey other) => Value.CompareTo(other.Value);
            public bool Equals(BuilderKey other) => Value == other.Value;
            public override bool Equals(object other) => other is BuilderKey key && Equals(key);
            public override int GetHashCode() => 0;
        }
        [LuminPackable] public sealed class BuilderGraph
        {
            public ImmutableDictionary<BuilderKey,int> Hash;
            public ImmutableSortedDictionary<BuilderKey,int> Sorted;
            public IImmutableDictionary<BuilderKey,int> Contract;
            public ImmutableHashSet<BuilderKey> Set;
            public ImmutableSortedSet<BuilderKey> SortedSet;
            public IImmutableSet<BuilderKey> SetContract;
            public int Tail;
        }
        public static class BuilderCases
        {
            public static void Check()
            {
                foreach(int count in new[]{0,1,2,17})
                {
                    var keys=Enumerable.Range(0,count).Select(i=>new BuilderKey{Value=i}).ToArray();
                    var graph=new BuilderGraph{Hash=keys.ToImmutableDictionary(k=>k,k=>k.Value),Sorted=keys.ToImmutableSortedDictionary(k=>k,k=>k.Value),Set=keys.ToImmutableHashSet(),SortedSet=keys.ToImmutableSortedSet(),Tail=917};
                    graph.Contract=graph.Hash;graph.SetContract=graph.Set;
                    var binary=LuminPackSerializer.Serialize(graph);
                    var utf8=Encoding.UTF8.GetBytes(LuminPackSerializer.SerializeJson<BuilderGraph, LuminPackUtf8LengthOptions>(graph));
                    var utf16=Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson<BuilderGraph, LuminPackUtf16LengthOptions>(graph));
                    for(int format=0;format<3;format++)
                    {
                        BuilderGraph first=null, second=null;
                        for(int pass=0;pass<2;pass++)
                        {
                            BuilderGraph result=null;
                            if(format==0)LuminPackSerializer.Deserialize(binary,ref result);
                            else if(format==1)LuminPackSerializer.DeserializeJson<BuilderGraph, LuminPackUtf8LengthOptions>(utf8,ref result);
                            else LuminPackSerializer.DeserializeJson<BuilderGraph, LuminPackUtf16LengthOptions>(utf16,ref result);
                            if(result.Tail!=917 || result.Hash.Count!=count || result.Sorted.Count!=count || result.Contract.Count!=count || result.Set.Count!=count || result.SortedSet.Count!=count || result.SetContract.Count!=count)throw new Exception("Builder result framing changed.");
                            foreach(var key in keys)if(result.Hash[key]!=key.Value || result.Sorted[key]!=key.Value || result.Contract[key]!=key.Value || !result.Set.Contains(key) || !result.SortedSet.Contains(key) || !result.SetContract.Contains(key))throw new Exception("Builder contents changed.");
                            if(pass==0)first=result;else second=result;
                        }
                        if(first.Hash.Count!=count || first.Set.Count!=count || first.SortedSet.Count!=count)throw new Exception("A returned builder mutated a published result.");
                    }
                }
            }
        }
        """;
}
