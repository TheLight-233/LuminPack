using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerCompiledOptionRegression
{
    /// <summary>Checks declaration-only Union switches, legacy compatibility, profiles and retained-driver invalidation.</summary>
    /// <param name="parse">The consumer language and framework symbols.</param>
    /// <param name="references">The platform and runtime references.</param>
    private static void VerifyUnionOptions(CSharpParseOptions parse, MetadataReference[] references)
    {
        const string source = """
            using System; using LuminPack; using LuminPack.Attribute; using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable, SETTING, LuminPackUnion(1, typeof(Case))]
            public abstract partial class Root { }
            [LuminPackable] public sealed partial class Case : Root { public string Text = "hello"; }
            [LuminPackable, LuminPackOption(WideTag = false, SingleImplementation = false), LuminPackUnion(1, typeof(Small))]
            public partial interface INarrow { }
            [LuminPackable] public sealed partial class Small : INarrow { public int Id = 7; }
            public static class Entry {
                public static void Check() {
                    Root value = new Case();
                    var bytes = LuminPackSerializer.Serialize(value);
                    if (bytes[0] != 1) throw new Exception("Wide framing changed.");
                    Root copy = LuminPackSerializer.Deserialize<Root>(bytes);
                    var reused = copy;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (!ReferenceEquals(reused, copy) || ((Case)copy).Text != "hello") throw new Exception("Wide reuse changed.");
                    var utf8 = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(value);
                    if (utf8.Length != 14 || ((Case)LuminPackSerializer.Deserialize<Root,LuminPackUtf8LengthOptions>(utf8)).Text != "hello") throw new Exception("Union switches fixed the string profile.");
                    if (LuminPackSerializer.Serialize<Root>(null)[0] != 255) throw new Exception("Wide null changed.");
                    INarrow narrow = new Small();
                    if (LuminPackSerializer.Serialize(narrow)[0] != 1) throw new Exception("False option enabled wide framing.");
                    if (((Small)LuminPackSerializer.Deserialize<INarrow>(LuminPackSerializer.Serialize(narrow))).Id != 7) throw new Exception("Interface round trip changed.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
            foreach (string setting in new[] { "LuminPackOption(WideTag = true, SingleImplementation = true)",
                "LuminPackWideTag, LuminPackSingleImplementation",
                "LuminPackWideTag, LuminPackOption(WideTag = false, SingleImplementation = false)" })
                Execute("UnionOptions" + mode + setting.Length, source.Replace("MODE", mode).Replace("SETTING", setting), parse, references);

        const string invalid = """
            using LuminPack.Attribute;
            [assembly: LuminPackOption(WideTag = true)]
            [LuminPackable, LuminPackOption(SingleImplementation = true)] public partial struct Item {
                [LuminPackOption(WideTag = false)] public int Number;
                [LuminPackOption(SingleImplementation = false)] public int Other { get; set; }
            }
            """;
        var input = CSharpCompilation.Create("InvalidUnionOptions", [CSharpSyntaxTree.ParseText(invalid, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        var result = driver.RunGenerators(input).GetRunResult();
        if (result.Diagnostics.Count(diagnostic => diagnostic.Id == "LuminPack110") != 4)
            throw new InvalidOperationException("Union options accepted an assembly, struct or member scope.");

        var tree = CSharpSyntaxTree.ParseText(source.Replace("MODE", "Light").Replace("SETTING", "LuminPackOption(WideTag = false)"), parse);
        input = input.RemoveAllSyntaxTrees().AddSyntaxTrees(tree);
        driver = driver.RunGenerators(input);
        foreach (string setting in new[] { "true", "false" })
        {
            var edited = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(tree.ToString().Replace("WideTag = false", "WideTag = " + setting), parse));
            driver = driver.RunGenerators(edited);
            var root = edited.GetTypeByMetadataName("Root")!;
            if (TypeMetaChecker.TryCheckWideTagAttribute(root) != (setting == "true"))
                throw new InvalidOperationException("Union declaration switch did not change metadata.");
            GeneratorDriver fresh = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse).RunGenerators(edited);
            string[] actual = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName + "\n" + s.SourceText).Order().ToArray();
            string[] expected = fresh.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName + "\n" + s.SourceText).Order().ToArray();
            if (!actual.SequenceEqual(expected)) throw new InvalidOperationException("Union option retained output differs from fresh generation.");
        }
    }
}
