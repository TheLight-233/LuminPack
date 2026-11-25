extern alias ModelBasedAnalyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

internal static partial class SerializerIssueRegression
{
    /// <summary>Checks required fields and auto-properties across the retained construction and reuse paths.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunRequiredConstruction(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Diagnostics.CodeAnalysis;
            using LuminPack;
            using LuminPack.Attribute;

            public class Parent { public required string Label { get; init; } }
            [LuminPackable]
            public sealed class Positional : Parent
            {
                public required int Count;
                [LuminPackIgnore] public bool ObservedDefault;
                [LuminPackOnDeserializing] public void BeforeRead() => ObservedDefault = Label is null && Count == 0;
            }

            [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
            public sealed class Versioned { public required string Label { get; init; } = "initial"; }
            [LuminPackable]
            public struct Managed { public required string Label { get; init; } public required int Count; }
            [LuminPackable]
            public struct Raw { public required int Count; }

            [LuminPackable]
            public sealed class Constructed
            {
                public readonly int Count;
                public required string Label { get; init; }
                public Constructed(int count) => Count = count;
            }

            [LuminPackable]
            public sealed class OwnsRequired
            {
                public required string Label { get; init; }
                [LuminPackIgnore] public required string ApplicationState;
                [SetsRequiredMembers] public OwnsRequired(string label)
                {
                    Label = label.Trim();
                    ApplicationState = "constructor";
                }
            }

            [LuminPackable]
            public sealed class Pooled
            {
                public static int Rentals;
                public required int Count;
                [LuminPackIgnore] public required string ApplicationState;
                [LuminPackPoolRent] public static Pooled Rent()
                {
                    Rentals++;
                    return new Pooled { Count = 0, ApplicationState = "rent" };
                }
            }

            [LuminPackable, LuminPackOption(TrackReferences = true)]
            public sealed class Tracked
            {
                public required string Label { get; init; }
                public Tracked Next;
            }

            public static class Entry
            {
                public static void Check()
                {
                    Positional positional = new() { Label = "position", Count = 7 };
                    Positional positionalCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(positional), ref positionalCopy);
                    if (positionalCopy is not { Label: "position", Count: 7, ObservedDefault: true })
                        throw new Exception("The positional read changed its before-read construction or required fields.");
                    Positional retained = positionalCopy;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(new Positional { Label = "reuse", Count = 11 }), ref positionalCopy);
                    if (!ReferenceEquals(retained, positionalCopy) || positionalCopy is not { Label: "reuse", Count: 11 })
                        throw new Exception("The JSON read replaced the reusable instance.");
                    positionalCopy = null;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(positional), ref positionalCopy);
                    if (positionalCopy is not { Label: "position", Count: 7 })
                        throw new Exception("The positional JSON read did not construct inherited required storage.");

                    Versioned versioned = new() { Label = "version" };
                    Versioned versionCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(versioned), ref versionCopy);
                    if (versionCopy is not { Label: "version" }) throw new Exception("Version binary read lost the required property.");
                    versionCopy = null;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(versioned), ref versionCopy);
                    if (versionCopy is not { Label: "version" }) throw new Exception("Version JSON read lost the required property.");

                    Managed managed = new() { Label = "managed", Count = 13 };
                    Managed managedCopy = default;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(managed), ref managedCopy);
                    if (managedCopy is not { Label: "managed", Count: 13 }) throw new Exception("Managed value read lost required storage.");
                    managedCopy = default;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(managed), ref managedCopy);
                    if (managedCopy is not { Label: "managed", Count: 13 }) throw new Exception("Managed JSON read lost required storage.");

                    Raw rawCopy = default;
                    byte[] rawBytes = LuminPackSerializer.Serialize(new Raw { Count = 17 });
                    LuminPackSerializer.Deserialize(rawBytes, ref rawCopy);
                    if (rawBytes.Length != 4 || rawCopy.Count != 17) throw new Exception("Required initialization changed raw binary storage.");
                    rawCopy = default;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(new Raw { Count = 19 }), ref rawCopy);
                    if (rawCopy.Count != 19) throw new Exception("Raw JSON read lost the required field.");

                    Constructed constructed = new(23) { Label = "selected" };
                    Constructed constructedCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(constructed), ref constructedCopy);
                    if (constructedCopy is not { Count: 23, Label: "selected" }) throw new Exception("Binary read lost selected construction.");
                    constructedCopy = null;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(constructed), ref constructedCopy);
                    if (constructedCopy is not { Count: 23, Label: "selected" }) throw new Exception("JSON read lost selected construction.");

                    OwnsRequired supplied = new("ignored") { Label = "  decoded  " };
                    OwnsRequired suppliedCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(supplied), ref suppliedCopy);
                    if (suppliedCopy is not { Label: "decoded", ApplicationState: "constructor" })
                        throw new Exception("An initializer replaced constructor-owned values.");
                    suppliedCopy = null;
                    LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(supplied), ref suppliedCopy);
                    if (suppliedCopy is not { Label: "decoded", ApplicationState: "constructor" })
                        throw new Exception("A JSON initializer replaced constructor-owned values.");

                    Pooled pooled = new() { Count = 29, ApplicationState = "unserialized" };
                    Pooled pooledCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(pooled), ref pooledCopy);
                    if (pooledCopy is not { Count: 29, ApplicationState: "rent" } || Pooled.Rentals != 1)
                        throw new Exception("Required initialization bypassed or overwrote the rental.");

                    var tracked = new Tracked { Label = "cycle" };
                    tracked.Next = tracked;
                    Tracked trackedCopy = null;
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(tracked), ref trackedCopy);
                    if (trackedCopy.Label != "cycle" || !ReferenceEquals(trackedCopy, trackedCopy.Next))
                        throw new Exception("Required initialization changed reference registration.");
                }
            }
            """;
        Execute(references, source, "RequiredConstruction");
        VerifyRequiredConstructionDiagnostics(references);
        Console.WriteLine("Passed required construction, inherited storage, selected constructors, callbacks, reuse, pooling and tracking.");
    }

    /// <summary>Verifies that unsupported required assignments report diagnostics on their source declarations.</summary>
    /// <param name="references">Platform and runtime references.</param>
    private static void VerifyRequiredConstructionDiagnostics(MetadataReference[] references)
    {
        const string source = """
            using LuminPack.Attribute;
            [LuminPackable] public class Ignored
            {
                [LuminPackIgnore] public required string ApplicationState;
            }
            [LuminPackable] public class OwnsRequired
            {
                public required string ConstructorState { get; init; }
                public OwnsRequired(string constructorState) => ConstructorState = constructorState.Trim();
            }
            [LuminPackable] public class Early
            {
                public required string SeededBeforeRead { get; init; } = "seed";
            }
            [LuminPackable] public class CustomSetter
            {
                public required string Computed { get; init => field = value.Trim(); }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        var tree = CSharpSyntaxTree.ParseText(source, parse, "RequiredConstruction.cs");
        var input = TestCompilation.Create("RequiredConstructionDiagnostics", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(item => item.ToString())));
        var actual = diagnostics.Where(item => item.Id == "LuminPack124" && item.Location.SourceTree == tree)
            .Select(item => source.Substring(item.Location.SourceSpan.Start, item.Location.SourceSpan.Length))
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(new[] { "ApplicationState", "Computed", "ConstructorState", "SeededBeforeRead" }))
            throw new InvalidOperationException("Unexpected required member diagnostics: " + string.Join(", ", diagnostics.AsEnumerable()));
    }
}
