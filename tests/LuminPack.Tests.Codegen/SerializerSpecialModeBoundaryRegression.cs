extern alias ModelBasedAnalyzer;

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Protects versioned member boundaries and reference tracking across both object formats.</summary>
internal static class SerializerSpecialModeBoundaryRegression
{
    /// <summary>Checks schema evolution and reference identity under the selected format and declaration scope.</summary>
    /// <param name="references">Runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)] public class VersionShort { [LuminPackOrder(0)] public int First; }
            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)] public class VersionLong
            {
                [LuminPackOrder(0)] public int First;
                [LuminPackOrder(2)] public string Last;
            }
            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)] public class VersionEmpty { }
            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)]
            public class CircleShort { public int First; }
            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)] public class CircleLong
            {
                [LuminPackOrder(0)] public int First;
                [LuminPackOrder(2)] public string Last;
            }
            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)] public class CircleEmpty { }
            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Default, TrackReferences = true)]
            public class TrackedPositional
            {
                public int Value;
                public TrackedPositional Next;
                [LuminPackable] public sealed class Nested { public int Value; }
            }
            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
            public class VersionedScope
            {
                [LuminPackable] public sealed class Nested { public int Value; }
            }
            [LuminPackable] public sealed class UntrackedDerived : TrackedPositional { public byte Tail; }
            public static class SpecialModeEntry
            {
                public static void Check()
                {
                    var longVersion = new VersionLong { First = 17, Last = "trailing member" };
                    byte[] versionBytes = LuminPackSerializer.Serialize(longVersion);
                    VersionShort shortVersion = null;
                    if (LuminPackSerializer.Deserialize(versionBytes.AsSpan(), ref shortVersion) != versionBytes.Length || shortVersion.First != 17)
                        throw new Exception("VersionTolerant did not skip the complete unknown tail.");
                    var restoredVersion = global::LuminPackUnitTest.ConcreteReads.Read<VersionLong>(LuminPackSerializer.Serialize(new VersionShort { First = 19 }), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref VersionLong concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                    if (restoredVersion.First != 19 || restoredVersion.Last != null) throw new Exception("VersionTolerant missing tail did not receive defaults.");
                    var longCircle = new CircleLong { First = 23, Last = "trailing member" };
                    byte[] circleBytes = LuminPackSerializer.Serialize(longCircle);
                    CircleShort shortCircle = null;
                    if (LuminPackSerializer.Deserialize(circleBytes.AsSpan(), ref shortCircle) != circleBytes.Length || shortCircle.First != 23)
                        throw new Exception("CircleReference did not skip the complete unknown tail.");
                    var restoredCircle = global::LuminPackUnitTest.ConcreteReads.Read<CircleLong>(LuminPackSerializer.Serialize(new CircleShort { First = 29 }), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref CircleLong concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                    if (restoredCircle.First != 29 || restoredCircle.Last != null) throw new Exception("CircleReference missing tail did not receive defaults.");
                    var positional = new TrackedPositional { Value = 37 };
                    positional.Next = positional;
                    byte[] positionalBytes = LuminPackSerializer.Serialize(positional);
                    TrackedPositional positionalCopy = null;
                    if (LuminPackSerializer.Deserialize(positionalBytes, ref positionalCopy) != positionalBytes.Length ||
                        positionalCopy.Value != positional.Value || !ReferenceEquals(positionalCopy, positionalCopy.Next))
                        throw new Exception("The positional record did not retain its cycle.");
                    if (!LuminPackSerializer.Serialize(new TrackedPositional.Nested { Value = 41 }).AsSpan()
                        .SequenceEqual(new byte[] { 1, 41, 0, 0, 0 }))
                        throw new Exception("A containing class passed its tracking declaration to a nested class.");
                    if (LuminPackSerializer.Serialize(new UntrackedDerived { Value = 43, Tail = 47 }).Length != 7)
                        throw new Exception("A base class passed its tracking declaration to its derived class.");
                    if (!LuminPackSerializer.Serialize(new VersionedScope.Nested { Value = 53 }).AsSpan()
                        .SequenceEqual(new byte[] { 1, 53, 0, 0, 0 }))
                        throw new Exception("A containing class passed its generation mode to a nested class.");

                    var versionEmpty = new VersionEmpty();
                    var circleEmpty = new CircleEmpty();
                    if (global::LuminPackUnitTest.ConcreteReads.Read<VersionEmpty>(LuminPackSerializer.Serialize(versionEmpty), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref VersionEmpty concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)) == null ||
                        global::LuminPackUnitTest.ConcreteReads.Read<CircleEmpty>(LuminPackSerializer.Serialize(circleEmpty), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref CircleEmpty concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)) == null)
                        throw new Exception("An empty specialized schema did not round-trip.");
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse,
            preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        var input = TestCompilation.Create("SpecialModeBoundaries", [CSharpSyntaxTree.ParseText(source, parse, "SpecialModes.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(driver.GetRunResult().Results.All(result => result.Exception == null) && errors.Length == 0,
            string.Join(Environment.NewLine, errors.Select(error => error.ToString()).Concat(driver.GetRunResult().Results.Select(result => result.Exception?.ToString()))));
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        Assembly.Load(image.ToArray()).GetType("SpecialModeEntry")!.GetMethod("Check")!.Invoke(null, null);
        VerifyTrackingOptionScope(references, parse);
        VerifyGenerationOptionScope(references, parse);
        VerifyRetiredImportedMarker(references, parse);
        Console.WriteLine("Passed versioned evolution, tracked Default and Version records, and generation option scope.");
    }

    /// <summary>Prevents an imported record from silently selecting the default when its legacy constructor cannot bind.</summary>
    /// <param name="references">The framework and current runtime metadata.</param>
    /// <param name="parse">The consumer's parser settings.</param>
    private static void VerifyRetiredImportedMarker(MetadataReference[] references, CSharpParseOptions parse)
    {
        var core = typeof(LuminPack.Attribute.LuminPackableAttribute).Assembly.GetName();
        const string legacy = """
            namespace LuminPack.Attribute
            {
                public enum GeneratorType : byte { VersionTolerant = 1 }
                public sealed class LuminPackableAttribute : System.Attribute
                {
                    public LuminPackableAttribute(GeneratorType mode) { }
                }
            }
            """;
        var framework = references.Where(reference => Path.GetFileName(reference.Display) != core.Name + ".dll").ToArray();
        var legacyCore = Emit(CSharpCompilation.Create(core.Name, [CSharpSyntaxTree.ParseText(
            $"[assembly: System.Reflection.AssemblyVersion(\"{core.Version}\")]\n" + legacy, parse)], framework,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        var legacyLibrary = Emit(CSharpCompilation.Create("LegacyMarkers", [CSharpSyntaxTree.ParseText("""
            using LuminPack.Attribute;
            [LuminPackable(GeneratorType.VersionTolerant)] public class Foreign { public int Value; }
            """, parse)], framework.Append(legacyCore), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        var consumer = TestCompilation.Create("ImportedMarker", [CSharpSyntaxTree.ParseText("""
            using LuminPack.Attribute;
            [assembly: LuminPackable(typeof(Foreign))]
            """, parse, "ImportedMarker.cs")], references.Append(legacyLibrary).ToArray());
        var result = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGenerators(consumer).GetRunResult();
        Require(result.Results.All(item => item.Exception == null) &&
            result.Diagnostics.Count(d => d.Id == "LuminPack113" && d.GetMessage().Contains("Foreign", StringComparison.Ordinal)) == 1,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));

        static MetadataReference Emit(CSharpCompilation input)
        {
            using var image = new MemoryStream();
            var emitted = input.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
            return MetadataReference.CreateFromImage(image.ToArray());
        }
    }

    /// <summary>Requires valid declaration-owned modes and the correct marker constructor for each scope.</summary>
    /// <param name="references">The platform and runtime references.</param>
    /// <param name="parse">The consumer's parser settings.</param>
    private static void VerifyGenerationOptionScope(MetadataReference[] references, CSharpParseOptions parse)
    {
        const string source = """
            using LuminPack.Attribute;
            [assembly: LuminPackable]
            [assembly: LuminPackable(typeof(Local))]
            [assembly: LuminPackOption(GeneratorType = GeneratorType.Version)]
            public class Local { }
            [LuminPackable(typeof(Local))] public class WrongMarker { }
            [LuminPackable, LuminPackable] public class DuplicateMarker { }
            [LuminPackOption(GeneratorType = GeneratorType.Version)] public class MissingMarker { }
            [LuminPackable, LuminPackOption(GeneratorType = (GeneratorType)3)] public class InvalidMode { }
            [LuminPackable]
            public class Members
            {
                [LuminPackOption(GeneratorType = GeneratorType.Default)] public int Field;
            }
            """;
        var input = TestCompilation.Create("GenerationOptionScope", [CSharpSyntaxTree.ParseText(source, parse, "GenerationOptionScope.cs")], references);
        var result = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGenerators(input).GetRunResult();
        var errors = result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(result.Results.All(item => item.Exception == null) &&
            errors.Count(d => d.Id == "LuminPack110") == 4 && errors.Count(d => d.Id == "LuminPack113") == 4 && errors.Length == 8,
            string.Join(Environment.NewLine, errors.Select(error => error.ToString()).Concat(result.Results.Select(item => item.Exception?.ToString()))));
    }

    /// <summary>Requires the tracking option to appear only on a class declaration.</summary>
    /// <param name="references">The platform and runtime references.</param>
    /// <param name="parse">The consumer's parser settings.</param>
    private static void VerifyTrackingOptionScope(MetadataReference[] references, CSharpParseOptions parse)
    {
        const string source = """
            using LuminPack.Attribute;
            [assembly: LuminPackOption(TrackReferences = true)]
            [LuminPackable, LuminPackOption(TrackReferences = true)] public struct Value { }
            [LuminPackable, LuminPackOption(TrackReferences = true)] public partial interface IValue { }
            [LuminPackable] public class Members
            {
                [LuminPackOption(TrackReferences = true)] public int Field;
                [LuminPackOption(TrackReferences = true)] public int Property { get; set; }
            }
            """;
        var input = TestCompilation.Create("TrackingOptionScope", [CSharpSyntaxTree.ParseText(source, parse)], references);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse);
        var result = driver.RunGenerators(input).GetRunResult();
        var locations = result.Diagnostics.Where(diagnostic => diagnostic.Id == "LuminPack110")
            .Select(diagnostic => diagnostic.Location.SourceSpan).Distinct().ToArray();
        Require(result.Results.All(item => item.Exception is null) && locations.Length == 5,
            "The tracking option was not rejected at every unsupported declaration scope.");
    }

    /// <summary>Reports a specialized-mode protocol regression.</summary>
    /// <param name="condition">The required contract.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
