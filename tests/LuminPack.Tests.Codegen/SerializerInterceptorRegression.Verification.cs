using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerInterceptorRegression
{
    /// <summary>Checks emitted IL rather than assuming a generated attribute changed the call.</summary>
    /// <param name="assembly">The compiled modern consumer.</param>
    private static void VerifyDirectCalls(Assembly assembly)
    {
        var entry = assembly.GetType("Entry")!;
        foreach (string name in new[] { "DirectWrite", "DirectRead", "DirectWrapper" })
        {
            var selected = CalledMethods(entry.GetMethod(name)!).Single(method => method.DeclaringType?.FullName == "LuminPack.LuminPackSerializer");
            Require(!selected.IsGenericMethod && selected.Name.StartsWith(name == "DirectWrapper" ? "Wrapper_" : "Intercept_", StringComparison.Ordinal),
                name + " retained a generic payload call in IL.");
            var target = CalledMethods(selected).Single();
            if (name == "DirectWrapper")
            {
                Require(target.Name.StartsWith("Intercept_", StringComparison.Ordinal) && !target.IsGenericMethod,
                    "A specialized wrapper retained payload dispatch in IL.");
                target = CalledMethods(target).Single();
            }
            Require(target.DeclaringType!.Name.StartsWith("LuminEntry_", StringComparison.Ordinal) && target.IsGenericMethod &&
                target.GetGenericArguments().Length == 1 && target.GetGenericArguments()[0].GetInterfaces().Any(type => type.FullName == "LuminPack.Option.ILuminPackOptions"),
                "An interceptor did not call its concrete convenience entry.");
        }
    }

    /// <summary>Reads call operands with complete instruction-width handling.</summary>
    /// <param name="method">The ordinary generated or source method.</param>
    /// <returns>Its emitted call targets in IL order.</returns>
    private static IEnumerable<MethodBase> CalledMethods(MethodBase method)
    {
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!).ToDictionary(code => unchecked((ushort)code.Value));
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
        for (int offset = 0; offset < il.Length;)
        {
            ushort key = il[offset++];
            if (key == 0xfe)
                key = (ushort)(0xfe00 | il[offset++]);
            var code = codes[key];
            if (code == OpCodes.Call || code == OpCodes.Callvirt)
                yield return method.Module.ResolveMethod(BitConverter.ToInt32(il, offset))!;
            offset += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                _ => 4
            };
        }
    }

    /// <summary>Compares a retained generator driver to fresh generation after location and nested-body edits.</summary>
    /// <param name="references">The current framework/runtime references.</param>
    /// <param name="il">Whether to check portable compiler mappings instead of native interception.</param>
    private static void VerifyIncremental(MetadataReference[] references, bool il = false)
    {
        var parse = il ? new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: ["NETSTANDARD2_1", "LUMINPACK_IL_REWRITE"]) :
            new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"])
                .WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        var enabled = parse;
        const string model = "using LuminPack.Attribute; [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)] " +
            "[LuminPackable] public partial class Item { public int Value; }";
        const string caller = "public static class Calls { public static byte[] Run(Item value) => Helpers.Save(value); }";
        const string wrappers = "public static class Helpers { public static int Count; public static byte[] Save<T>(T value) => Inner(value); " +
            "private static byte[] Inner<T>(T value) { Count += 1; return LuminPack.LuminPackSerializer.Serialize(value); } }";
        var input = CSharpCompilation.Create("InterceptorEdits", [Tree(model, "Models.cs"), Tree(caller, "Calls.cs"), Tree(wrappers, "Helpers.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver retained = Driver();
        string? previous = null;
        foreach (int edit in Enumerable.Range(0, 5))
        {
            if (edit == 1)
                input = input.ReplaceSyntaxTree(input.SyntaxTrees.Last(), Tree(wrappers.Replace("Count += 1", "Count += 2"), "Helpers.cs"));
            if (edit == 2)
                input = input.ReplaceSyntaxTree(input.SyntaxTrees.ElementAt(1), Tree("\n\n" + caller, "Calls.cs"));
            if (edit >= 3)
            {
                parse = edit == 4 ? enabled : il ? enabled.WithPreprocessorSymbols("NETSTANDARD2_1") : enabled.WithFeatures([]);
                input = input.RemoveAllSyntaxTrees().AddSyntaxTrees(input.SyntaxTrees.Select(tree => tree.WithRootAndOptions(tree.GetRoot(), parse)));
                retained = retained.WithUpdatedParseOptions(parse);
            }
            retained = retained.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            var fresh = Driver().RunGeneratorsAndUpdateCompilation(input, out _, out _);
            string snapshot = Sources(retained);
            Require(snapshot == Sources(fresh), "Retained interceptor output disagreed with fresh generation after edit " + edit + ".");
            Require(previous == null || previous != snapshot || il && edit == 2, "An interception backend, location or nested wrapper body remained stale.");
            Require(!output.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error),
                "Incremental interception failed compilation: " + string.Join(Environment.NewLine, output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error)));
            previous = snapshot;
        }

        SyntaxTree Tree(string text, string path) => CSharpSyntaxTree.ParseText(text, parse, path, Encoding.UTF8);
        GeneratorDriver Driver() => CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        string Sources(GeneratorDriver driver) => string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .OrderBy(source => source.HintName, StringComparer.Ordinal).Select(source => source.HintName + "\n" + source.SourceText));
    }
}
