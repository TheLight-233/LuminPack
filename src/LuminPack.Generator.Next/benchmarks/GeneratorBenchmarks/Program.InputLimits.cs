using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Pipeline;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    /// <summary>Executes checked-copy contracts through trusted inline descendants under frozen root limits.</summary>
    /// <param name="references">The framework and runtime metadata.</param>
    /// <param name="parse">The consumer parse options.</param>
    /// <param name="coreDll">The runtime implementation used by the generated fixture.</param>
    /// <returns>Zero when every profile preserves checked reads, reuse and ordinary wire bytes.</returns>
    private static int VerifyInputLimits(MetadataReference[] references, CSharpParseOptions parse, string coreDll)
    {
        var tree = CSharpSyntaxTree.ParseText(InputLimitFixture, parse, "InputLimits.cs");
        var input = CSharpCompilation.Create("NextInputLimitContracts", new[] { tree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        foreach (GeneratorRunResult result in driver.GetRunResult().Results)
            if (result.Exception != null)
                return LuminPackExceptionHelper.ThrowInvalidOperation(result.Exception.ToString());
        foreach (Diagnostic diagnostic in diagnostics.Concat(output.GetDiagnostics()))
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                return LuminPackExceptionHelper.ThrowInvalidOperation(diagnostic.ToString());
        string generated = string.Concat(driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources)
            .Select(static file => file.SourceText.ToString()));
        if (!generated.Contains("LuminInputReads_", StringComparison.Ordinal))
            return LuminPackExceptionHelper.ThrowInvalidOperation("The fixture did not exercise a root-owned checked copy.");
        foreach (string type in new[] { "CircleAdjacent", "CircleGapped" })
        {
            string source = driver.GetRunResult().Results.Single().GeneratedSources.Single(file =>
                file.HintName == "Binary/LuminPackGen." + type + ".g.cs").SourceText.ToString();
            RequireIncremental(source.Contains(".ByteOffset(", StringComparison.Ordinal) &&
                source.Contains(".SizeOf<global::System.IntPtr>() == 8", StringComparison.Ordinal) &&
                source.Contains(".Advance(", StringComparison.Ordinal), type + " must retain its guarded fixed group and checked fallback.");
        }
        string versionWrite = driver.GetRunResult().Results.Single().GeneratedSources.Single(file =>
            file.HintName == "Binary/LuminPackGen.VersionedRuns.g.cs").SourceText.ToString();
        RequireIncremental(versionWrite.Contains(".ByteOffset(", StringComparison.Ordinal) &&
            versionWrite.Contains(".SizeOf<global::System.IntPtr>() == 8", StringComparison.Ordinal) &&
            versionWrite.Contains(".WriteReservedBlock(", StringComparison.Ordinal) &&
            versionWrite.Contains(".CompleteVersionLength(", StringComparison.Ordinal), "Versioned runs must retain guarded stores across variable slots.");
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        if (!emitted.Success)
            return LuminPackExceptionHelper.ThrowInvalidOperation(string.Join(Environment.NewLine, emitted.Diagnostics));
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(coreDll));
        Assembly assembly = Assembly.Load(image.ToArray());
        int checks = (int)assembly.GetType("InputLimitFixture")!.GetMethod("Exercise")!.Invoke(null, null)!;
        foreach (string edited in new[]
        {
            InputLimitFixture.Replace("MaxStringLength = 4", "MaxStringLength = 1"),
            InputLimitFixture.Replace("MaxStringLength = 4, MaxCollectionLength = 4", "UntrustedData = false"),
            InputLimitFixture.Replace("LuminPackOption(MaxStringLength = 4)]\npublic abstract partial class LimitedUnion",
                "LuminPackOption(UntrustedData = false)]\npublic abstract partial class LimitedUnion"),
            InputLimitFixture.Replace("LuminPackOption(UntrustedData = false)]\npublic sealed class TrustedVersionedFlag",
                "LuminPackOption(UntrustedData = true)]\npublic sealed class TrustedVersionedFlag"),
            InputLimitFixture,
        })
        {
            var changed = input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(edited, parse, "InputLimits.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(changed, out var retainedOutput, out var retainedDiagnostics);
            GeneratorDriver fresh = CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: parse)
                .RunGeneratorsAndUpdateCompilation(changed, out var freshOutput, out var freshDiagnostics);
            var actual = Sources(driver);
            var expected = Sources(fresh);
            RequireIncremental(actual.Count == expected.Count, "Input-limit edits changed the retained file count.");
            foreach (var file in expected)
                RequireIncremental(actual.TryGetValue(file.Key, out string? text) && text == file.Value,
                    "Input-limit edits retained stale checked source: " + file.Key);
            RequireIncremental(DiagnosticSnapshot(retainedOutput, retainedDiagnostics) == DiagnosticSnapshot(freshOutput, freshDiagnostics),
                "Input-limit edits retained stale diagnostics.");
            foreach (Diagnostic diagnostic in retainedDiagnostics.Concat(retainedOutput.GetDiagnostics()))
                RequireIncremental(diagnostic.Severity != DiagnosticSeverity.Error, diagnostic.ToString());
        }
        Console.WriteLine($"Next input-limit contracts: {checks} checks passed across six string profiles.");
        Console.WriteLine("Changed, disabled and restored input limits match fresh-driver source and diagnostics.");
        return 0;
    }

    private const string InputLimitFixture = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class TrustedLeaf
        {
            public int Number;
            public string Text;
            public bool Flag;
            public bool[] Bits;
        }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class StandaloneFlag { public bool Flag; }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class TrustedMiddle { public TrustedLeaf Leaf; }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class TrustedContainer { public TrustedMiddle Middle; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public sealed class LimitedRoot { public TrustedContainer Child { get; set; } }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public sealed class LimitedListRoot { public System.Collections.Generic.List<TrustedContainer> Children; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public sealed class LimitedArrayRoot { public TrustedContainer[] Children; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public sealed class LimitedDictionaryRoot { public System.Collections.Generic.Dictionary<int, TrustedContainer> Children; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public sealed class LimitedForwardedCollections
        {
            [LuminPackableObject] public int[][] Jagged;
            [LuminPackableObject] public List<int[]> Arrays;
            [LuminPackableObject] public bool[][] Flags;
            [LuminPackableObject] public List<string> Text;
        }
        [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(MaxStringLength = 4)]
        public partial class LimitedVersion { public string Text { get; set; } }
        [LuminPackable(GeneratorType.VersionTolerant)]
        public partial class EarlierVersion { public string Text { get; set; } public int Removed; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4)]
        public partial struct LimitedStruct { public string Text; public int[] Values; }
        [LuminPackable(GeneratorType.CircleReference), LuminPackOption(MaxStringLength = 4)]
        public partial class LimitedCircle { [LuminPackOrder(0)] public string Text { get; set; } }
        [LuminPackable(GeneratorType.CircleReference), LuminPackOption(MaxDepth = 2)]
        public partial class LimitedCycle { [LuminPackOrder(0)] public LimitedCycle Next; }
        [LuminPackable(GeneratorType.CircleReference)]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public partial class CircleAdjacent
        {
            [LuminPackOrder(0)] public int First;
            [LuminPackOrder(1)] public int Second;
            [LuminPackOrder(2)] public bool Flag;
            [LuminPackOrder(3)] public volatile int Last;
        }
        [LuminPackable(GeneratorType.CircleReference)]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public partial class CircleGapped
        {
            [LuminPackOrder(0)] public byte First;
            [LuminPackOrder(1)] public long Second;
        }
        [LuminPackable(GeneratorType.VersionTolerant)]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public sealed class VersionedRuns
        {
            public int First;
            public int Second;
            public int[] Items;
            public byte Flag;
            public long Last;
        }
        [LuminPackable(GeneratorType.VersionTolerant)]
        public sealed class VersionedVolatile
        {
            public int First;
            public volatile int Middle;
            public int Last;
        }
        [LuminPackable, LuminPackOption(MaxCollectionLength = 4)]
        public partial class LimitedCompressed { [LuminPackCompress] public int[] Values; }
        [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(MaxCollectionLength = 4)]
        public partial class LimitedVersionCompressed { [LuminPackCompress] public int[] Values; }
        [LuminPackable(GeneratorType.VersionTolerant)]
        public partial class EarlierVersionCompressed { [LuminPackCompress] public int[] Values; public int Removed; }
        [LuminPackable, LuminPackOption(MaxCollectionLength = 4)]
        public sealed class LimitedJaggedCompressed
        {
            [LuminPackableObject, LuminPackCompress] public int[][] Array;
            [LuminPackableObject, LuminPackCompress] public List<int[][]> List;
        }
        [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(MaxCollectionLength = 4)]
        public sealed class LimitedVersionJaggedCompressed
        {
            [LuminPackableObject, LuminPackCompress] public int[][] Array;
        }
        [LuminPackable(GeneratorType.VersionTolerant)]
        public sealed class EarlierVersionJaggedCompressed
        {
            [LuminPackableObject, LuminPackCompress] public int[][] Array;
            public int Removed;
        }
        [LuminPackable, LuminPackOption(MaxInputBytes = 5)]
        public partial class LimitedStream { public int Value; }
        [LuminPackable, LuminPackOption(UntrustedData = true)]
        public partial struct RejectedStruct { public object Value; }
        [LuminPackable, LuminPackUnion(0, typeof(RejectedCase)), LuminPackOption(UntrustedData = true)]
        public abstract partial class RejectedUnion { }
        [LuminPackable]
        public sealed partial class RejectedCase : RejectedUnion { public object Value; }
        [LuminPackable, LuminPackUnion(0, typeof(TrustedFlagCase)), LuminPackOption(UntrustedData = true)]
        public abstract partial class CheckedFlagUnion { }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed partial class TrustedFlagCase : CheckedFlagUnion { public bool Flag; }
        [LuminPackable, LuminPackUnion(0, typeof(TrustedTextCase)), LuminPackUnion(1, typeof(TrustedVersionCase)),
            LuminPackOption(MaxStringLength = 4)]
        public abstract partial class LimitedUnion { }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed partial class TrustedTextCase : LimitedUnion { public string Text { get; set; } }
        [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(UntrustedData = false)]
        public sealed partial class TrustedVersionCase : LimitedUnion { public string Text { get; set; } }
        [LuminPackable, LuminPackOption(MaxStringLength = 2)]
        public sealed class LimitedUnionMember { public LimitedUnion Value; }
        [LuminPackable, LuminPackOption(MaxStringLength = 2)]
        public sealed class LimitedUnionBatch { public LimitedUnion[] Values; }
        [LuminPackable, LuminPackOption(MaxStringLength = 2)]
        public sealed class LimitedUnionContainers
        {
            public List<LimitedUnion> List;
            public Dictionary<int, LimitedUnion> Map;
        }
        [LuminPackable, LuminPackUnion(0, typeof(TrackedCase)), LuminPackUnion(1, typeof(FixedTrackedCase)),
            LuminPackOption(MaxStringLength = 4, MaxCollectionLength = 4, MaxDepth = 2)]
        public abstract partial class LimitedTrackedUnion { }
        [LuminPackable(GeneratorType.CircleReference), LuminPackOption(UntrustedData = false)]
        public sealed partial class TrackedCase : LimitedTrackedUnion
        {
            [LuminPackOrder(0)] public string Text { get; set; }
            [LuminPackOrder(1)] public TrackedCase Next;
            [LuminPackOrder(2)] public int[] Values;
        }
        [LuminPackable(GeneratorType.CircleReference)]
        public sealed partial class FixedTrackedCase : LimitedTrackedUnion
        {
            [LuminPackOrder(0)] public int Number;
        }
        [LuminPackable, LuminPackUnion(0, typeof(TrustedGenericCase<string>)), LuminPackOption(MaxStringLength = 4)]
        public abstract partial class LimitedGenericUnion<T> { }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed partial class TrustedGenericCase<T> : LimitedGenericUnion<T> { public T Text { get; set; } }
        [LuminPackable, LuminPackUnion(0, typeof(TrustedStructCase)), LuminPackOption(MaxStringLength = 4)]
        public partial interface ILimitedStructUnion { }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public partial struct TrustedStructCase : ILimitedStructUnion { public string Text { get; set; } }
        [LuminPackable, LuminPackOption(MaxStringLength = 4)]
        public abstract partial class LimitedOpenUnion<T> where T : struct { }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed partial class TrustedOpenCase<T> : LimitedOpenUnion<T> where T : struct { public string Text; }
        [LuminPackable, LuminPackUnion(3, typeof(ScopedGenericCase<string>)), LuminPackUnion(257, typeof(ScopedGenericCase<int>))]
        public abstract partial class ScopedGenericUnion<T> { }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed partial class ScopedGenericCase<T> : ScopedGenericUnion<T> { public T Value; }
        [LuminPackable, LuminPackOption(MaxStringLength = 2)]
        public sealed class LimitedClosedUnion
        {
            public ScopedGenericUnion<string> Value;
            public List<ScopedGenericUnion<string>> Items;
            public LimitedOpenUnion<int> OpenCase;
        }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public partial class TrustedMirror { public string Text { get; set; } }
        [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(UntrustedData = false)]
        public partial class TrustedVersion { public string Text { get; set; } }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public partial struct TrustedStruct { public string Text { get; set; } }
        [LuminPackable(GeneratorType.CircleReference), LuminPackOption(UntrustedData = false)]
        public partial class TrustedCircle
        {
            [LuminPackOrder(0)] public string Text { get; set; }
            [LuminPackOrder(1)] public TrustedCircle Next;
        }
        [LuminPackable, LuminPackOption(MaxStringLength = 4)]
        public partial class LimitedNestedContracts
        {
            public TrustedMirror Mirror;
            public TrustedVersion Version;
            public TrustedStruct Structure;
            public TrustedCircle Circle;
        }
        [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(MaxStringLength = 4)]
        public partial class LimitedVersionParent { public TrustedMirror Child; }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class TrustedVersionedFlag
        {
            public bool Flag { get; set; }
        }
        [LuminPackable(GeneratorType.VersionTolerant)]
        public sealed class UnboundedVersionParent { public TrustedVersionedFlag Child; }
        [LuminPackable]
        public partial struct UnboundedStructParent { public TrustedVersionedFlag Child; }
        [LuminPackable]
        public sealed class UnboundedCollectionParent
        {
            public System.Collections.Generic.List<TrustedVersionedFlag> Children { get; set; }
        }
        [LuminPackable, LuminPackUnion(0, typeof(TrustedUnboundedCase))]
        public abstract partial class UnboundedUnion { }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed partial class TrustedUnboundedCase : UnboundedUnion
        {
            public string Text;
            public bool Flag;
        }
        [LuminPackable, LuminPackUnion(0, typeof(TrustedUnboundedGenericCase<bool>))]
        public abstract partial class UnboundedGenericUnion<T> { }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed partial class TrustedUnboundedGenericCase<T> : UnboundedGenericUnion<T> { public T Value; }
        [LuminPackable, LuminPackOption(UntrustedData = false)]
        public sealed class TrustedGenericValue<T> { public T Value; }
        [LuminPackable(GeneratorType.VersionTolerant)]
        public sealed class UnboundedGenericParent { public TrustedGenericValue<bool> Child; }
        [LuminPackable]
        public sealed class GenericInputRoot<T> { public T Value; public TrustedVersionedFlag Child; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4)]
        public sealed class BoundedGenericInputRoot<T> { public T Value; public TrustedMirror Child; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4)]
        public partial struct BoundedGenericStruct<T> { public T Value; public TrustedMirror Child; }
        [LuminPackable, LuminPackOption(MaxStringLength = 4)]
        public partial struct LimitedStructParent { public TrustedVersion Child; }
        public static class InputLimitFixture
        {
            public static int Exercise() => Check<LuminPackUtf16LengthOptions>() + Check<LuminPackUtf8LengthOptions>() +
                Check<LuminPackUtf16VarIntOptions>() + Check<LuminPackUtf8VarIntOptions>() +
                Check<LuminPackUtf16TokenOptions>() + Check<LuminPackUtf8TokenOptions>() + CheckAdditionalRoots() + CheckCircleGroups() + CheckUnbounded();

            private static int CheckUnbounded()
            {
                var child = new TrustedVersionedFlag { Flag = true };
                byte[] version = LuminPackSerializer.Serialize(new UnboundedVersionParent { Child = child });
                byte[] structure = LuminPackSerializer.Serialize(new UnboundedStructParent { Child = child });
                byte[] collection = LuminPackSerializer.Serialize(new UnboundedCollectionParent { Children = new() { child } });
                byte[] union = LuminPackSerializer.Serialize<UnboundedUnion>(new TrustedUnboundedCase { Text = "ok", Flag = true });
                if (!LuminPackSerializer.Deserialize<UnboundedVersionParent>(version).Child.Flag ||
                    !LuminPackSerializer.Deserialize<UnboundedStructParent>(structure).Child.Flag ||
                    !LuminPackSerializer.Deserialize<UnboundedCollectionParent>(collection).Children[0].Flag ||
                    !((TrustedUnboundedCase)LuminPackSerializer.Deserialize<UnboundedUnion>(union)).Flag)
                    throw new Exception("An unbounded checked read changed valid bytes.");
                version[^1] = structure[^1] = collection[^1] = union[^1] = 2;
                RejectRead(() => LuminPackSerializer.Deserialize<UnboundedVersionParent>(version));
                RejectRead(() => LuminPackSerializer.Deserialize<UnboundedStructParent>(structure));
                RejectRead(() => LuminPackSerializer.Deserialize<UnboundedCollectionParent>(collection));
                RejectRead(() => LuminPackSerializer.Deserialize<UnboundedUnion>(union));
                UnboundedGenericUnion<bool> generic = new TrustedUnboundedGenericCase<bool> { Value = true };
                byte[] genericBytes = LuminPackSerializer.Serialize(generic);
                byte[] profiled = LuminPackSerializer.Serialize<UnboundedGenericUnion<bool>, LuminPackUtf8VarIntOptions>(generic);
                if (!((TrustedUnboundedGenericCase<bool>)LuminPackSerializer.Deserialize<UnboundedGenericUnion<bool>>(genericBytes)).Value ||
                    !((TrustedUnboundedGenericCase<bool>)LuminPackSerializer.Deserialize<UnboundedGenericUnion<bool>, LuminPackUtf8VarIntOptions>(profiled)).Value)
                    throw new Exception("A closed generic case lost its valid Boolean.");
                genericBytes[^1] = profiled[^1] = 2;
                RejectRead(() => LuminPackSerializer.Deserialize<UnboundedGenericUnion<bool>>(genericBytes));
                RejectRead(() => LuminPackSerializer.Deserialize<UnboundedGenericUnion<bool>, LuminPackUtf8VarIntOptions>(profiled));
                var cases = new System.Collections.Generic.List<UnboundedGenericUnion<bool>> { generic };
                byte[] list = LuminPackSerializer.Serialize(cases);
                if (!((TrustedUnboundedGenericCase<bool>)LuminPackSerializer.Deserialize<System.Collections.Generic.List<UnboundedGenericUnion<bool>>>(list)[0]).Value)
                    throw new Exception("A closed generic case in a list lost its valid Boolean.");
                list[^1] = 2;
                RejectRead(() => LuminPackSerializer.Deserialize<System.Collections.Generic.List<UnboundedGenericUnion<bool>>>(list));
                byte[] parent = LuminPackSerializer.Serialize(new UnboundedGenericParent { Child = new() { Value = true } });
                if (!LuminPackSerializer.Deserialize<UnboundedGenericParent>(parent).Child.Value)
                    throw new Exception("A closed generic member lost its valid Boolean.");
                parent[^1] = 2;
                RejectRead(() => LuminPackSerializer.Deserialize<UnboundedGenericParent>(parent));
                var root = new GenericInputRoot<int> { Value = 17, Child = child };
                byte[] rootBytes = LuminPackSerializer.Serialize(root);
                if (LuminPackSerializer.Deserialize<GenericInputRoot<int>>(rootBytes).Value != 17 ||
                    !LuminPackSerializer.Deserialize<GenericInputRoot<int>>(rootBytes).Child.Flag)
                    throw new Exception("The closed generic root lost a field.");
                rootBytes[^1] = 2;
                RejectRead(() => LuminPackSerializer.Deserialize<GenericInputRoot<int>>(rootBytes));
                RejectRead(() => ReadRegistered<GenericInputRoot<int>>(rootBytes));
                var bounded = new BoundedGenericInputRoot<int> { Value = 23, Child = new TrustedMirror { Text = "ok" } };
                byte[] valid = LuminPackSerializer.Serialize<BoundedGenericInputRoot<int>, LuminPackUtf8VarIntOptions>(bounded);
                if (LuminPackSerializer.Deserialize<BoundedGenericInputRoot<int>, LuminPackUtf8VarIntOptions>(valid).Child.Text != "ok")
                    throw new Exception("The profiled generic root lost a valid string.");
                bounded.Child.Text = "oversized";
                byte[] oversized = LuminPackSerializer.Serialize<BoundedGenericInputRoot<int>, LuminPackUtf8VarIntOptions>(bounded);
                RejectRead(() => LuminPackSerializer.Deserialize<BoundedGenericInputRoot<int>, LuminPackUtf8VarIntOptions>(oversized));
                var boundedStruct = new BoundedGenericStruct<int> { Value = 29, Child = new TrustedMirror { Text = "ok" } };
                byte[] structBytes = LuminPackSerializer.Serialize(boundedStruct);
                if (LuminPackSerializer.Deserialize<BoundedGenericStruct<int>>(structBytes).Child.Text != "ok")
                    throw new Exception("The closed generic struct lost a valid string.");
                boundedStruct.Child.Text = "oversized";
                structBytes = LuminPackSerializer.Serialize(boundedStruct);
                RejectRead(() => LuminPackSerializer.Deserialize<BoundedGenericStruct<int>>(structBytes));
                return 23;
            }

            private static T ReadRegistered<T>(byte[] input) => LuminPackSerializer.Deserialize<T>(input);

            private static int CheckCircleGroups()
            {
                var adjacent = new CircleAdjacent { First = 1, Second = 2, Flag = true, Last = 3 };
                byte[] bytes = LuminPackSerializer.Serialize(adjacent);
                byte[] expected = { 4, 4, 4, 1, 4, 0, 1, 0, 0, 0, 2, 0, 0, 0, 1, 3, 0, 0, 0 };
                if (!bytes.AsSpan().SequenceEqual(expected)) throw new Exception("Adjacent CircleReference wire bytes changed.");
                var existing = new CircleAdjacent();
                var target = existing;
                LuminPackSerializer.Deserialize(bytes, ref target);
                if (!ReferenceEquals(existing, target) || target.First != 1 || target.Second != 2 || !target.Flag || target.Last != 3)
                    throw new Exception("Same-table grouped read changed reference reuse or fields.");
                bytes[14] = 2;
                RejectRead(() => LuminPackSerializer.Deserialize<CircleAdjacent>(bytes));
                for (int length = 6; length < 14; length++)
                {
                    byte[] truncated = bytes.AsSpan(0, length).ToArray();
                    try
                    {
                        LuminPackSerializer.Deserialize<CircleAdjacent>(truncated);
                        throw new Exception("A truncated CircleReference fixed run was accepted.");
                    }
                    catch (Exception error) when (error.GetType().Name == "LuminPackException" &&
                        error.Message.StartsWith("Span out of range", StringComparison.Ordinal)) { }
                }
                byte[] gapped = LuminPackSerializer.Serialize(new CircleGapped { First = 5, Second = 9 });
                byte[] gappedExpected = { 2, 1, 8, 0, 5, 9, 0, 0, 0, 0, 0, 0, 0 };
                var copy = LuminPackSerializer.Deserialize<CircleGapped>(gapped);
                if (!gapped.AsSpan().SequenceEqual(gappedExpected) || copy.First != 5 || copy.Second != 9)
                    throw new Exception("The gapped CircleReference fallback must keep packed bytes.");
                var runs = new VersionedRuns { First = 7, Second = 19, Items = new int[4096], Flag = 3, Last = 29 };
                runs.Items[^1] = 31;
                byte[] versioned = LuminPackSerializer.Serialize(runs);
                var retained = new VersionedRuns { Items = new int[4096] };
                var prior = retained;
                if (LuminPackSerializer.Deserialize(versioned, ref retained) != versioned.Length || !ReferenceEquals(prior, retained) ||
                    retained.First != 7 || retained.Second != 19 || retained.Items[^1] != 31 || retained.Flag != 3 || retained.Last != 29)
                    throw new Exception("Versioned fixed runs changed values, framing or reuse.");
                var snapshot = LuminPackSerializer.Deserialize<VersionedVolatile>(
                    LuminPackSerializer.Serialize(new VersionedVolatile { First = 37, Middle = 41, Last = 43 }));
                if (snapshot.First != 37 || snapshot.Middle != 41 || snapshot.Last != 43)
                    throw new Exception("The versioned volatile member lost its scalar snapshot.");
                return 14;
            }

            private static int CheckAdditionalRoots()
            {
                var version = new LimitedVersion { Text = "ok" };
                var structure = new LimitedStruct { Text = "ok", Values = new[] { 1, 2 } };
                var circle = new LimitedCircle { Text = "ok" };
                var compressed = new LimitedCompressed { Values = new[] { 1, 2 } };
                var earlier = new EarlierVersion { Text = "old", Removed = 42 };
                if (LuminPackSerializer.Deserialize<LimitedVersion>(LuminPackSerializer.Serialize(version)).Text != "ok" ||
                    LuminPackSerializer.Deserialize<LimitedVersion>(LuminPackSerializer.Serialize(earlier)).Text != "old" ||
                    LuminPackSerializer.Deserialize<LimitedStruct>(LuminPackSerializer.Serialize(structure)).Values[1] != 2 ||
                    LuminPackSerializer.Deserialize<LimitedCircle>(LuminPackSerializer.Serialize(circle)).Text != "ok" ||
                    LuminPackSerializer.Deserialize<LimitedCompressed>(LuminPackSerializer.Serialize(compressed)).Values[1] != 2)
                    throw new Exception("A checked root changed valid wire reads.");

                version.Text = structure.Text = circle.Text = "oversized";
                compressed.Values = new int[5];
                byte[] versionBytes = LuminPackSerializer.Serialize(version);
                byte[] structBytes = LuminPackSerializer.Serialize(structure);
                byte[] circleBytes = LuminPackSerializer.Serialize(circle);
                byte[] compressedBytes = LuminPackSerializer.Serialize(compressed);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedVersion>(versionBytes));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedStruct>(structBytes));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedCircle>(circleBytes));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedCompressed>(compressedBytes));
                earlier.Text = "oversized";
                byte[] earlierBytes = LuminPackSerializer.Serialize(earlier);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedVersion>(earlierBytes));
                structure.Text = "ok";
                structure.Values = new int[5];
                byte[] oversizedStruct = LuminPackSerializer.Serialize(structure);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedStruct>(oversizedStruct));
                var earlierCompressed = new EarlierVersionCompressed { Values = new[] { 1, 2 }, Removed = 42 };
                if (LuminPackSerializer.Deserialize<LimitedVersionCompressed>(LuminPackSerializer.Serialize(earlierCompressed)).Values[1] != 2)
                    throw new Exception("The evolved schema changed a valid compressed read.");
                earlierCompressed.Values = new int[5];
                byte[] oversizedCompressed = LuminPackSerializer.Serialize(earlierCompressed);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedVersionCompressed>(oversizedCompressed));

                var jagged = new LimitedJaggedCompressed { Array = new[] { new[] { 17, 18 }, null },
                    List = new List<int[][]> { new[] { System.Array.Empty<int>() }, new[] { new[] { 19 } } } };
                var jaggedCopy = LuminPackSerializer.Deserialize<LimitedJaggedCompressed>(LuminPackSerializer.Serialize(jagged));
                if (jaggedCopy.Array[0][1] != 18 || jaggedCopy.Array[1] != null || jaggedCopy.List[0][0].Length != 0 ||
                    jaggedCopy.List[1][0][0] != 19)
                    throw new Exception("A checked jagged compressed collection changed valid wire reads.");
                jagged.Array = new int[5][];
                byte[] oversizedJaggedArray = LuminPackSerializer.Serialize(jagged);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedJaggedCompressed>(oversizedJaggedArray));
                jagged.Array = null;
                jagged.List.AddRange(new int[3][][]);
                byte[] oversizedJaggedList = LuminPackSerializer.Serialize(jagged);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedJaggedCompressed>(oversizedJaggedList));
                jagged.List.Clear();
                jagged.Array = new[] { new int[5] };
                byte[] oversizedJaggedArrayLeaf = LuminPackSerializer.Serialize(jagged);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedJaggedCompressed>(oversizedJaggedArrayLeaf));
                jagged.Array = null;
                jagged.List.Add(new int[5][]);
                byte[] oversizedJaggedListChild = LuminPackSerializer.Serialize(jagged);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedJaggedCompressed>(oversizedJaggedListChild));
                jagged.List[0] = new[] { new int[5] };
                byte[] oversizedJaggedListLeaf = LuminPackSerializer.Serialize(jagged);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedJaggedCompressed>(oversizedJaggedListLeaf));

                var earlierJagged = new EarlierVersionJaggedCompressed { Array = new[] { new[] { 23 } }, Removed = 42 };
                if (LuminPackSerializer.Deserialize<LimitedVersionJaggedCompressed>(LuminPackSerializer.Serialize(earlierJagged)).Array[0][0] != 23)
                    throw new Exception("The evolved schema changed a valid jagged compressed read.");
                earlierJagged.Array = new[] { new int[5] };
                byte[] oversizedVersionJaggedLeaf = LuminPackSerializer.Serialize(earlierJagged);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedVersionJaggedCompressed>(oversizedVersionJaggedLeaf));

                var cycle = new LimitedCycle { Next = new LimitedCycle() };
                cycle.Next.Next = cycle;
                var cycleCopy = LuminPackSerializer.Deserialize<LimitedCycle>(LuminPackSerializer.Serialize(cycle));
                if (!ReferenceEquals(cycleCopy, cycleCopy.Next.Next))
                    throw new Exception("A back reference incorrectly consumed another level of depth.");
                cycle.Next.Next = new LimitedCycle();
                byte[] deepCycle = LuminPackSerializer.Serialize(cycle);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedCycle>(deepCycle));

                byte[] bytes = LuminPackSerializer.Serialize(new LimitedStream { Value = 42 });
                using var exact = new MemoryStream(bytes);
                if (LuminPackSerializer.DeserializeAsync<LimitedStream>(exact).GetAwaiter().GetResult().Value != 42)
                    throw new Exception("The stream read rejected its exact limit.");
                Array.Resize(ref bytes, bytes.Length + 4);
                using var oversized = new MemoryStream(bytes);
                RejectRead(() => LuminPackSerializer.DeserializeAsync<LimitedStream>(oversized).GetAwaiter().GetResult());
                if (oversized.Position != 6)
                    throw new Exception("The stream reader consumed more than the limit and one lookahead byte.");
                var rejected = new RejectedStruct { Value = 42 };
                RejectedUnion rejectedCase = new RejectedCase { Value = 42 };
                RejectContract(() => LuminPackSerializer.Serialize(rejected));
                RejectContract(() => LuminPackSerializer.Deserialize<RejectedStruct>(new byte[] { 1 }));
                RejectContract(() => LuminPackSerializer.SerializeJson(rejected));
                RejectContract(() => LuminPackSerializer.DeserializeJson<RejectedStruct>("{}"));
                RejectContract(() => LuminPackSerializer.Serialize(rejectedCase));
                RejectContract(() => LuminPackSerializer.Deserialize<RejectedUnion>(new byte[] { 0 }));
                RejectContract(() => LuminPackSerializer.SerializeJson(rejectedCase));
                RejectContract(() => LuminPackSerializer.DeserializeJson<RejectedUnion>("{}"));
                CheckedFlagUnion flag = new TrustedFlagCase { Flag = true };
                byte[] flagBytes = LuminPackSerializer.Serialize(flag);
                if (!((TrustedFlagCase)LuminPackSerializer.Deserialize<CheckedFlagUnion>(flagBytes)).Flag)
                    throw new Exception("A checked fixed Union case changed its valid value.");
                flagBytes[flagBytes.Length - 1] = 2;
                RejectRead(() => LuminPackSerializer.Deserialize<CheckedFlagUnion>(flagBytes));
                LimitedUnion textCase = new TrustedTextCase { Text = "ok" };
                LimitedUnion versionCase = new TrustedVersionCase { Text = "ok" };
                byte[] textCaseBytes = LuminPackSerializer.Serialize(textCase);
                byte[] versionCaseBytes = LuminPackSerializer.Serialize(versionCase);
                LimitedUnion existing = new TrustedTextCase();
                var original = existing;
                if (LuminPackSerializer.Deserialize(textCaseBytes, ref existing) != textCaseBytes.Length ||
                    !ReferenceEquals(original, existing) || ((TrustedTextCase)existing).Text != "ok" ||
                    ((TrustedVersionCase)LuminPackSerializer.Deserialize<LimitedUnion>(versionCaseBytes)).Text != "ok")
                    throw new Exception("The checked Union case changed framing or reuse.");
                ((TrustedTextCase)textCase).Text = "oversized";
                ((TrustedVersionCase)versionCase).Text = "oversized";
                byte[] oversizedTextCase = LuminPackSerializer.Serialize(textCase);
                byte[] oversizedVersionCase = LuminPackSerializer.Serialize(versionCase);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedUnion>(oversizedTextCase));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedUnion>(oversizedVersionCase));
                var cases = new System.Collections.Generic.List<LimitedUnion> { textCase };
                byte[] oversizedCases = LuminPackSerializer.Serialize(cases);
                RejectRead(() => LuminPackSerializer.Deserialize<System.Collections.Generic.List<LimitedUnion>>(oversizedCases));
                LimitedGenericUnion<string> generic = new TrustedGenericCase<string> { Text = "ok" };
                ILimitedStructUnion boxed = new TrustedStructCase { Text = "ok" };
                byte[] genericBytes = LuminPackSerializer.Serialize(generic);
                byte[] structCaseBytes = LuminPackSerializer.Serialize(boxed);
                if (((TrustedGenericCase<string>)LuminPackSerializer.Deserialize<LimitedGenericUnion<string>>(genericBytes)).Text != "ok" ||
                    ((TrustedStructCase)LuminPackSerializer.Deserialize<ILimitedStructUnion>(structCaseBytes)).Text != "ok")
                    throw new Exception("A checked generic or struct Union case changed valid framing.");
                ((TrustedGenericCase<string>)generic).Text = "oversized";
                boxed = new TrustedStructCase { Text = "oversized" };
                genericBytes = LuminPackSerializer.Serialize(generic);
                structCaseBytes = LuminPackSerializer.Serialize(boxed);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedGenericUnion<string>>(genericBytes));
                RejectRead(() => LuminPackSerializer.Deserialize<ILimitedStructUnion>(structCaseBytes));
                byte[] utf8Generic = LuminPackSerializer.Serialize<LimitedGenericUnion<string>, LuminPackUtf8VarIntOptions>(generic);
                byte[] utf8Struct = LuminPackSerializer.Serialize<ILimitedStructUnion, LuminPackUtf8VarIntOptions>(boxed);
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedGenericUnion<string>, LuminPackUtf8VarIntOptions>(utf8Generic));
                RejectRead(() => LuminPackSerializer.Deserialize<ILimitedStructUnion, LuminPackUtf8VarIntOptions>(utf8Struct));
                byte[] openValid = LuminPackSerializer.Serialize(NewOpenCase<int>("ok"));
                if (LuminPackSerializer.Deserialize<LimitedOpenUnion<int>>(openValid) == null)
                    throw new Exception("An open generic case changed its valid framing.");
                byte[] openOversized = LuminPackSerializer.Serialize(NewOpenCase<int>("oversized"));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedOpenUnion<int>>(openOversized));
                return 49 + CheckNestedContracts() + CheckForwardedCollections<LuminPackUtf16LengthOptions>() +
                    CheckForwardedCollections<LuminPackUtf8VarIntOptions>() +
                    CheckTrackedUnion<LuminPackUtf16LengthOptions>() + CheckTrackedUnion<LuminPackUtf8VarIntOptions>() +
                    CheckUnionMembers<LuminPackUtf16LengthOptions>() + CheckUnionMembers<LuminPackUtf8VarIntOptions>() +
                    CheckClosedUnion<LuminPackUtf16LengthOptions>() + CheckClosedUnion<LuminPackUtf8VarIntOptions>();
            }

            private static int CheckClosedUnion<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var item = new ScopedGenericCase<string> { Value = "ok" };
                var source = new LimitedClosedUnion { Value = item, Items = new() { item }, OpenCase = NewOpenCase<int>("ok") };
                byte[] bytes = Write();
                var candidate = new ScopedGenericCase<string>();
                var copy = new LimitedClosedUnion { Value = candidate };
                if (LuminPackSerializer.Deserialize<LimitedClosedUnion, TProfile>(bytes, ref copy) != bytes.Length ||
                    !ReferenceEquals(candidate, copy.Value) || candidate.Value != "ok" ||
                    ((ScopedGenericCase<string>)copy.Items[0]).Value != "ok" || ((TrustedOpenCase<int>)copy.OpenCase).Text != "ok")
                    throw new Exception("A closed generic Union read changed tag selection or reuse.");
                item.Value = "four";
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedClosedUnion, TProfile>(Write()));
                source.Value = null;
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedClosedUnion, TProfile>(Write()));
                source.Items.Clear();
                source.OpenCase = NewOpenCase<int>("four");
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedClosedUnion, TProfile>(Write()));
                return 4;

                byte[] Write() => LuminPackSerializer.Serialize<LimitedClosedUnion, TProfile>(source);
            }

            private static int CheckUnionMembers<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var item = new TrustedVersionCase { Text = "ok" };
                var source = new LimitedUnionMember { Value = item };
                var array = new LimitedUnionBatch { Values = new LimitedUnion[] { item, null } };
                var containers = new LimitedUnionContainers { List = new() { item }, Map = new() { [1] = item } };
                byte[] memberBytes = LuminPackSerializer.Serialize<LimitedUnionMember, TProfile>(source);
                var member = new LimitedUnionMember { Value = new TrustedVersionCase() };
                var candidate = member.Value;
                if (LuminPackSerializer.Deserialize<LimitedUnionMember, TProfile>(memberBytes, ref member) != memberBytes.Length ||
                    !ReferenceEquals(candidate, member.Value) || ((TrustedVersionCase)member.Value).Text != "ok" ||
                    LuminPackSerializer.Deserialize<LimitedUnionBatch, TProfile>(LuminPackSerializer.Serialize<LimitedUnionBatch, TProfile>(array))
                        .Values[1] != null)
                    throw new Exception("The checked Union member changed framing or case reuse.");
                byte[] containerBytes = WriteContainers();
                var containersCopy = LuminPackSerializer.Deserialize<LimitedUnionContainers, TProfile>(containerBytes);
                if (((TrustedVersionCase)containersCopy.List[0]).Text != "ok" || ((TrustedVersionCase)containersCopy.Map[1]).Text != "ok" ||
                    LuminPackSerializer.Deserialize<LimitedUnionContainers, TProfile>(containerBytes, ref containersCopy) != containerBytes.Length)
                    throw new Exception("The checked list or dictionary of Unions changed its read.");
                item.Text = "four";
                byte[] ordinary = LuminPackSerializer.Serialize<LimitedUnion, TProfile>(item);
                if (((TrustedVersionCase)LuminPackSerializer.Deserialize<LimitedUnion, TProfile>(ordinary)).Text != "four")
                    throw new Exception("The standalone Union lost its declared input scope.");
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedUnionMember, TProfile>(
                    LuminPackSerializer.Serialize<LimitedUnionMember, TProfile>(source)));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedUnionBatch, TProfile>(
                    LuminPackSerializer.Serialize<LimitedUnionBatch, TProfile>(array)));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedUnionContainers, TProfile>(WriteContainers()));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedUnionContainers, TProfile>(WriteContainers(), ref containersCopy));
                containers.List.Clear();
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedUnionContainers, TProfile>(WriteContainers()));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedUnionContainers, TProfile>(WriteContainers(), ref containersCopy));
                return 9;

                byte[] WriteContainers() => LuminPackSerializer.Serialize<LimitedUnionContainers, TProfile>(containers);
            }

            private static int CheckTrackedUnion<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var source = new TrackedCase { Text = "ok", Values = new[] { 17 } };
                source.Next = source;
                byte[] bytes = Write();
                LimitedTrackedUnion value = new TrackedCase();
                var candidate = value;
                if (LuminPackSerializer.Deserialize<LimitedTrackedUnion, TProfile>(bytes, ref value) != bytes.Length ||
                    !ReferenceEquals(candidate, value) || value is not TrackedCase copy ||
                    !ReferenceEquals(copy, copy.Next) || copy.Text != "ok" || copy.Values[0] != 17)
                    throw new Exception("The checked Union read changed reference tracking or reuse.");
                source.Text = "oversized";
                Reject();
                source.Text = "ok";
                source.Values = new int[5];
                Reject();
                source.Values = null;
                source.Next = new TrackedCase { Text = "next" };
                source.Next.Next = new TrackedCase { Text = "end" };
                Reject();
                byte[] fixedBytes = LuminPackSerializer.Serialize<LimitedTrackedUnion, TProfile>(new FixedTrackedCase { Number = 29 });
                var fixedCase = new FixedTrackedCase();
                value = fixedCase;
                if (LuminPackSerializer.Deserialize<LimitedTrackedUnion, TProfile>(fixedBytes, ref value) != fixedBytes.Length ||
                    !ReferenceEquals(fixedCase, value) || fixedCase.Number != 29)
                    throw new Exception("A fixed reference-tracked case lost its slot framing or reuse.");
                return 5;

                byte[] Write() => LuminPackSerializer.Serialize<LimitedTrackedUnion, TProfile>(source);
                void Reject() => RejectRead(() => LuminPackSerializer.Deserialize<LimitedTrackedUnion, TProfile>(Write()));
            }

            private static int CheckForwardedCollections<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var source = new LimitedForwardedCollections
                {
                    Jagged = new[] { new[] { 1 } },
                    Arrays = new List<int[]> { new[] { 2 } },
                    Flags = new[] { new[] { true } },
                    Text = new List<string> { "ok" },
                };
                byte[] bytes = Write();
                var copy = LuminPackSerializer.Deserialize<LimitedForwardedCollections, TProfile>(bytes);
                var jagged = copy.Jagged;
                var text = copy.Text;
                if (LuminPackSerializer.Deserialize<LimitedForwardedCollections, TProfile>(bytes, ref copy) != bytes.Length ||
                    !ReferenceEquals(jagged, copy.Jagged) || !ReferenceEquals(text, copy.Text) ||
                    copy.Jagged[0][0] != 1 || copy.Arrays[0][0] != 2 || !copy.Flags[0][0] || copy.Text[0] != "ok")
                    throw new Exception("Checked forwarded collections changed a valid read or its reuse.");
                source.Jagged[0] = new int[5];
                Reject();
                source.Jagged[0] = null;
                source.Arrays[0] = new int[5];
                Reject();
                source.Arrays[0] = null;
                source.Flags[0] = new bool[5];
                Reject();
                source.Flags[0] = null;
                source.Text[0] = "oversized";
                Reject();
                source.Text.Clear();
                source.Text.AddRange(new string[5]);
                Reject();
                return 6;

                byte[] Write() => LuminPackSerializer.Serialize<LimitedForwardedCollections, TProfile>(source);
                void Reject() => RejectRead(() => LuminPackSerializer.Deserialize<LimitedForwardedCollections, TProfile>(Write()));
            }

            private static int CheckNestedContracts()
            {
                var source = new LimitedNestedContracts
                {
                    Mirror = new TrustedMirror { Text = "ok" },
                    Version = new TrustedVersion { Text = "ok" },
                    Structure = new TrustedStruct { Text = "ok" },
                    Circle = new TrustedCircle { Text = "ok" },
                };
                source.Circle.Next = source.Circle;
                byte[] bytes = LuminPackSerializer.Serialize(source);
                var target = new LimitedNestedContracts { Mirror = new TrustedMirror(), Circle = new TrustedCircle() };
                var mirror = target.Mirror;
                var circle = target.Circle;
                if (LuminPackSerializer.Deserialize(bytes, ref target) != bytes.Length || !ReferenceEquals(mirror, target.Mirror) ||
                    !ReferenceEquals(circle, target.Circle) || !ReferenceEquals(circle, circle.Next) ||
                    target.Mirror.Text != "ok" || target.Version.Text != "ok" || target.Structure.Text != "ok" || target.Circle.Text != "ok")
                    throw new Exception("Nested checked reads changed framing or reuse.");
                source.Mirror.Text = "oversized";
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedNestedContracts>(LuminPackSerializer.Serialize(source)));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedVersionParent>(
                    LuminPackSerializer.Serialize(new LimitedVersionParent { Child = source.Mirror })));
                source.Mirror.Text = "ok";
                source.Version.Text = "oversized";
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedNestedContracts>(LuminPackSerializer.Serialize(source)));
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedStructParent>(
                    LuminPackSerializer.Serialize(new LimitedStructParent { Child = source.Version })));
                source.Version.Text = "ok";
                source.Structure.Text = "oversized";
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedNestedContracts>(LuminPackSerializer.Serialize(source)));
                source.Structure.Text = "ok";
                source.Circle.Text = "oversized";
                RejectRead(() => LuminPackSerializer.Deserialize<LimitedNestedContracts>(LuminPackSerializer.Serialize(source)));
                return 7;
            }

            private static LimitedOpenUnion<T> NewOpenCase<T>(string text) where T : struct =>
                new TrustedOpenCase<T> { Text = text };

            private static void RejectContract(Action action)
            {
                try { action(); }
                catch (NotSupportedException error) when (error.Message.Contains("untrusted-input option")) { return; }
                throw new Exception("An explicitly untrusted contract accepted an input-selected type.");
            }

            private static void RejectRead(Action read)
            {
                try { read(); }
                catch (InvalidDataException) { return; }
                throw new Exception("A root bypassed its declared input limit.");
            }

            private static int Check<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var value = new LimitedRoot { Child = NewContainer() };
                byte[] bytes = LuminPackSerializer.Serialize<LimitedRoot, TProfile>(value);
                byte[] trusted = LuminPackSerializer.Serialize<TrustedContainer, TProfile>(value.Child);
                if (!bytes.AsSpan(1).SequenceEqual(trusted))
                    throw new Exception("Checked copies changed ordinary wire bytes.");
                LimitedRoot copy = null;
                int consumed = LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref copy);
                if (consumed != bytes.Length || copy.Child.Middle.Leaf.Number != 17 || !copy.Child.Middle.Leaf.Flag || copy.Child.Middle.Leaf.Text != "ok" ||
                    copy.Child.Middle.Leaf.Bits.Length != 2 || copy.Child.Middle.Leaf.Bits[0])
                    throw new Exception("The checked copy changed a valid read.");
                var container = copy.Child;
                var middle = container.Middle;
                var leaf = middle.Leaf;
                var text = leaf.Text;
                LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref copy);
                if (!ReferenceEquals(container, copy.Child) || !ReferenceEquals(middle, copy.Child.Middle) ||
                    !ReferenceEquals(leaf, copy.Child.Middle.Leaf) || !ReferenceEquals(text, copy.Child.Middle.Leaf.Text))
                    throw new Exception("The checked copy lost object or equal-string reuse.");
                for (int iteration = 0; iteration < 16; iteration++)
                    LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref copy);
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int iteration = 0; iteration < 128; iteration++)
                    LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref copy);
                if (GC.GetAllocatedBytesForCurrentThread() != allocated)
                    throw new Exception("A warmed checked-copy read allocated while reusing its complete target.");
                foreach (int offset in new[] { 2, 3, bytes.Length - 7, bytes.Length - 1 })
                {
                    byte[] forged = (byte[])bytes.Clone();
                    forged[offset] = offset < 4 ? (byte)0 : (byte)2;
                    Reject<TProfile>(forged, "A trusted inline descendant bypassed a checked-copy marker or Boolean domain at " + offset);
                }
                var current = value.Child.Middle.Leaf;
                current.Text = "oversized";
                Reject<TProfile>(LuminPackSerializer.Serialize<LimitedRoot, TProfile>(value), "An inline string bypassed its root limit.");
                current.Text = "ok";
                current.Bits = new bool[5];
                Reject<TProfile>(LuminPackSerializer.Serialize<LimitedRoot, TProfile>(value), "An inline array bypassed its root limit.");
                current.Bits = new[] { false, true };
                value.Child.Middle = null;
                LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(LuminPackSerializer.Serialize<LimitedRoot, TProfile>(value), ref copy);
                if (copy.Child.Middle != null)
                    throw new Exception("A null inline record did not clear reused storage.");
                byte[] uncheckedBytes = (byte[])trusted.Clone();
                uncheckedBytes[uncheckedBytes.Length - 7] = 2;
                uncheckedBytes[uncheckedBytes.Length - 1] = 2;
                TrustedContainer uncheckedCopy = null;
                LuminPackSerializer.Deserialize<TrustedContainer, TProfile>(uncheckedBytes, ref uncheckedCopy);
                byte[] flagBytes = LuminPackSerializer.Serialize<StandaloneFlag, TProfile>(new StandaloneFlag { Flag = true });
                StandaloneFlag flagCopy = null;
                if (LuminPackSerializer.Deserialize<StandaloneFlag, TProfile>(flagBytes, ref flagCopy) != flagBytes.Length || !flagCopy.Flag)
                    throw new Exception("The trusted scalar reader did not advance through its Boolean.");
                return 13 + CheckCollections<TProfile>();
            }

            private static TrustedContainer NewContainer() => new TrustedContainer { Middle = new TrustedMiddle { Leaf =
                new TrustedLeaf { Number = 17, Text = "ok", Flag = true, Bits = new[] { false, true } } } };

            private static int CheckCollections<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var child = NewContainer();
                var list = new LimitedListRoot { Children = new System.Collections.Generic.List<TrustedContainer> { child } };
                var array = new LimitedArrayRoot { Children = new[] { child } };
                var dictionary = new LimitedDictionaryRoot { Children = new System.Collections.Generic.Dictionary<int, TrustedContainer> { [17] = child } };
                byte[] listBytes = LuminPackSerializer.Serialize<LimitedListRoot, TProfile>(list);
                byte[] arrayBytes = LuminPackSerializer.Serialize<LimitedArrayRoot, TProfile>(array);
                byte[] dictionaryBytes = LuminPackSerializer.Serialize<LimitedDictionaryRoot, TProfile>(dictionary);
                LimitedListRoot listCopy = null;
                LimitedArrayRoot arrayCopy = null;
                LimitedDictionaryRoot dictionaryCopy = null;
                if (LuminPackSerializer.Deserialize<LimitedListRoot, TProfile>(listBytes, ref listCopy) != listBytes.Length || !listCopy.Children[0].Middle.Leaf.Flag)
                    throw new Exception("A checked list changed a valid element read.");
                if (LuminPackSerializer.Deserialize<LimitedArrayRoot, TProfile>(arrayBytes, ref arrayCopy) != arrayBytes.Length || !arrayCopy.Children[0].Middle.Leaf.Flag)
                    throw new Exception("A checked array changed a valid element read.");
                if (LuminPackSerializer.Deserialize<LimitedDictionaryRoot, TProfile>(dictionaryBytes, ref dictionaryCopy) != dictionaryBytes.Length || !dictionaryCopy.Children[17].Middle.Leaf.Flag)
                    throw new Exception("A checked dictionary changed a valid element read.");
                var existingList = listCopy.Children;
                var existingArray = arrayCopy.Children;
                var existingDictionary = dictionaryCopy.Children;
                LuminPackSerializer.Deserialize<LimitedListRoot, TProfile>(listBytes, ref listCopy);
                LuminPackSerializer.Deserialize<LimitedArrayRoot, TProfile>(arrayBytes, ref arrayCopy);
                LuminPackSerializer.Deserialize<LimitedDictionaryRoot, TProfile>(dictionaryBytes, ref dictionaryCopy);
                if (!ReferenceEquals(existingList, listCopy.Children) || !ReferenceEquals(existingArray, arrayCopy.Children) ||
                    !ReferenceEquals(existingDictionary, dictionaryCopy.Children))
                    throw new Exception("Checked collection copies lost existing container reuse.");
                byte[][] payloads = new[] { listBytes, arrayBytes, dictionaryBytes };
                for (int kind = 0; kind < payloads.Length; kind++)
                {
                    byte[] payload = payloads[kind];
                    foreach (int offset in new[] { kind == 2 ? 11 : 7, payload.Length - 7, payload.Length - 1 })
                    {
                        byte[] forged = (byte[])payload.Clone();
                        forged[offset] = offset < 12 ? (byte)0 : (byte)2;
                        RejectCollection<TProfile>(forged, kind);
                    }
                }
                child.Middle.Leaf.Text = "oversized";
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedListRoot, TProfile>(list), 0);
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedArrayRoot, TProfile>(array), 1);
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedDictionaryRoot, TProfile>(dictionary), 2);
                child.Middle.Leaf.Text = "ok";
                child.Middle.Leaf.Bits = new bool[5];
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedListRoot, TProfile>(list), 0);
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedArrayRoot, TProfile>(array), 1);
                RejectCollection<TProfile>(LuminPackSerializer.Serialize<LimitedDictionaryRoot, TProfile>(dictionary), 2);
                list.Children[0] = null;
                array.Children[0] = null;
                dictionary.Children[17] = null;
                LuminPackSerializer.Deserialize<LimitedListRoot, TProfile>(LuminPackSerializer.Serialize<LimitedListRoot, TProfile>(list), ref listCopy);
                LuminPackSerializer.Deserialize<LimitedArrayRoot, TProfile>(LuminPackSerializer.Serialize<LimitedArrayRoot, TProfile>(array), ref arrayCopy);
                LuminPackSerializer.Deserialize<LimitedDictionaryRoot, TProfile>(LuminPackSerializer.Serialize<LimitedDictionaryRoot, TProfile>(dictionary), ref dictionaryCopy);
                if (listCopy.Children[0] != null || arrayCopy.Children[0] != null || dictionaryCopy.Children[17] != null)
                    throw new Exception("A checked collection did not clear a reused null element.");
                return 20;
            }

            private static void RejectCollection<TProfile>(byte[] bytes, int kind) where TProfile : struct, ILuminPackOptions
            {
                try
                {
                    if (kind == 0)
                    {
                        LimitedListRoot list = null;
                        LuminPackSerializer.Deserialize<LimitedListRoot, TProfile>(bytes, ref list);
                    }
                    else if (kind == 1)
                    {
                        LimitedArrayRoot array = null;
                        LuminPackSerializer.Deserialize<LimitedArrayRoot, TProfile>(bytes, ref array);
                    }
                    else
                    {
                        LimitedDictionaryRoot dictionary = null;
                        LuminPackSerializer.Deserialize<LimitedDictionaryRoot, TProfile>(bytes, ref dictionary);
                    }
                }
                catch (InvalidDataException) { return; }
                throw new Exception("A shared collection owner bypassed a checked inline descendant for kind " + kind);
            }

            private static void Reject<TProfile>(byte[] bytes, string message) where TProfile : struct, ILuminPackOptions
            {
                LimitedRoot target = null;
                try { LuminPackSerializer.Deserialize<LimitedRoot, TProfile>(bytes, ref target); }
                catch (InvalidDataException) { return; }
                throw new Exception(message);
            }
        }
        """;
}
