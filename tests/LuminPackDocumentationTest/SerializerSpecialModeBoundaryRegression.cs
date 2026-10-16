using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Protects length-table boundaries shared by version-tolerant and reference-tracked objects.</summary>
internal static class SerializerSpecialModeBoundaryRegression
{
    /// <summary>Checks trailing schema changes, exact consumed lengths, gap sizes, and empty schemas.</summary>
    /// <param name="references">Runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            [LuminPackable(GeneratorType.VersionTolerant)] public class VersionShort { [LuminPackOrder(0)] public int First; }
            [LuminPackable(GeneratorType.VersionTolerant)] public class VersionLong
            {
                [LuminPackOrder(0)] public int First;
                [LuminPackOrder(2)] public string Last;
            }
            [LuminPackable(GeneratorType.VersionTolerant)] public class VersionEmpty { }
            [LuminPackable(GeneratorType.CircleReference)] public class CircleShort { [LuminPackOrder(0)] public int First; }
            [LuminPackable(GeneratorType.CircleReference)] public class CircleLong
            {
                [LuminPackOrder(0)] public int First;
                [LuminPackOrder(2)] public string Last;
            }
            [LuminPackable(GeneratorType.CircleReference)] public class CircleEmpty { }
            public static class SpecialModeEntry
            {
                public static void Check()
                {
                    var longVersion = new VersionLong { First = 17, Last = "trailing member" };
                    byte[] versionBytes = LuminPackSerializer.Serialize(longVersion);
                    VersionShort shortVersion = null;
                    if (LuminPackSerializer.Deserialize(versionBytes.AsSpan(), ref shortVersion) != versionBytes.Length || shortVersion.First != 17)
                        throw new Exception("VersionTolerant did not skip the complete unknown tail.");
                    var restoredVersion = LuminPackSerializer.Deserialize<VersionLong>(LuminPackSerializer.Serialize(new VersionShort { First = 19 }));
                    if (restoredVersion.First != 19 || restoredVersion.Last != null) throw new Exception("VersionTolerant missing tail did not receive defaults.");
                    if (LuminPackSerializer.Sizeof(longVersion) != versionBytes.Length) throw new Exception("VersionTolerant gap length was omitted from Sizeof.");
                    var longCircle = new CircleLong { First = 23, Last = "trailing member" };
                    byte[] circleBytes = LuminPackSerializer.Serialize(longCircle);
                    CircleShort shortCircle = null;
                    if (LuminPackSerializer.Deserialize(circleBytes.AsSpan(), ref shortCircle) != circleBytes.Length || shortCircle.First != 23)
                        throw new Exception("CircleReference did not skip the complete unknown tail.");
                    var restoredCircle = LuminPackSerializer.Deserialize<CircleLong>(LuminPackSerializer.Serialize(new CircleShort { First = 29 }));
                    if (restoredCircle.First != 29 || restoredCircle.Last != null) throw new Exception("CircleReference missing tail did not receive defaults.");
                    if (LuminPackSerializer.Sizeof(longCircle) != circleBytes.Length) throw new Exception("CircleReference gap length was omitted from Sizeof.");
                    var versionEmpty = new VersionEmpty();
                    var circleEmpty = new CircleEmpty();
                    if (LuminPackSerializer.Deserialize<VersionEmpty>(LuminPackSerializer.Serialize(versionEmpty)) == null ||
                        LuminPackSerializer.Deserialize<CircleEmpty>(LuminPackSerializer.Serialize(circleEmpty)) == null)
                        throw new Exception("An empty specialized schema did not round-trip.");
                    if (LuminPackSerializer.Sizeof(versionEmpty) != LuminPackSerializer.Serialize(versionEmpty).Length ||
                        LuminPackSerializer.Sizeof(circleEmpty) != LuminPackSerializer.Serialize(circleEmpty).Length)
                        throw new Exception("An empty specialized schema size changed.");
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse,
            preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        var input = CSharpCompilation.Create("SpecialModeBoundaries", [CSharpSyntaxTree.ParseText(source, parse, "SpecialModes.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(driver.GetRunResult().Results.All(result => result.Exception == null) && errors.Length == 0,
            string.Join(Environment.NewLine, errors.Select(error => error.ToString()).Concat(driver.GetRunResult().Results.Select(result => result.Exception?.ToString()))));
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        Assembly.Load(image.ToArray()).GetType("SpecialModeEntry")!.GetMethod("Check")!.Invoke(null, null);
        Console.WriteLine("Passed VersionTolerant/CircleReference trailing additions/removals, consumed bytes, gap size evaluation, and empty schemas.");
    }

    /// <summary>Reports a specialized-mode protocol regression.</summary>
    /// <param name="condition">The required contract.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
