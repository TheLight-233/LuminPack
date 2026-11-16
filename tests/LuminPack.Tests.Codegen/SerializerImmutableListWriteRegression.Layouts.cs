using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Injects unavailable layout facts and traps field access before a collection probe can touch storage.</summary>
    /// <param name="references">The actual runtime and platform references used by the projected source.</param>
    internal static void RunLayoutProbes(MetadataReference[] references)
    {
        using var resource = typeof(SerializerImmutableListWriteRegression).Assembly.GetManifestResourceStream("TestSupport.CollectionViews.cs")!;
        using var reader = new StreamReader(resource);
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var probes = CSharpSyntaxTree.ParseText(reader.ReadToEnd(), parse).GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .Where(type => type.Identifier.ValueText is "SortedListStorage" or "PriorityQueueStorage")
            .Select(type => type.ToString().Replace("LuminFieldLayout.Field<", "ProbeTrap.Field<", StringComparison.Ordinal));
        string source = "using System; using System.Collections.Generic; namespace CollectionProbe; public static class LuminPackCollectionAccess {\n" +
            string.Join("\n", probes) + "\n}\n" + """
            public static class ProbeTrap
            {
                public static int Calls;
                public static ref T Field<T>(object instance, int offset) { Calls++; throw new Exception("Unexpected field access."); }
                public static ref T Field<T>(ref byte storage, int offset) { Calls++; throw new Exception("Unexpected field access."); }
            }
            """;
        var cases = new[]
        {
            (Family: "SortedListStorage", Offset: "SortedListKeys"),
            (Family: "SortedListStorage", Offset: "SortedListValues"),
            (Family: "SortedListStorage", Offset: "SortedListSize"),
            (Family: "PriorityQueueStorage", Offset: "PriorityQueueNodes"),
            (Family: "PriorityQueueStorage", Offset: "PriorityQueueSize")
        };
        // These probes verify the unavailable-offset guard before any storage access.
        string offsets = string.Join("\n", cases.Select(test => $"internal static readonly int {test.Offset} = 0;"));
        source += "\ninternal static class LuminCollectionLayouts {\n" + offsets + "\n}";
        var failures = new List<string>();
        foreach (var test in cases)
        {
            string projected = source.Replace("LuminCollectionLayouts." + test.Offset, "(-1)", StringComparison.Ordinal);
            var compilation = CSharpCompilation.Create("LayoutProbe" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(projected, parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            using var image = new MemoryStream(); var result = compilation.Emit(image);
            Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            var assembly = System.Reflection.Assembly.Load(image.ToArray());
            var nested = assembly.GetType("CollectionProbe.LuminPackCollectionAccess")!.GetNestedType(test.Family, System.Reflection.BindingFlags.NonPublic)!;
            bool available = (bool)nested.GetField("Available", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
            int calls = (int)assembly.GetType("CollectionProbe.ProbeTrap")!.GetField("Calls")!.GetValue(null)!;
            if (available || calls != 0) failures.Add(test.Offset + ": available=" + available + ", field accesses=" + calls);
        }
        Require(failures.Count == 0, "Unavailable offsets did not stop the probes: " + string.Join("; ", failures));
        Console.WriteLine("Passed collection layout probes with each unavailable offset and zero attempted field accesses.");
    }
}
