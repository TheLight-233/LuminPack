using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects fixed-cursor qualification, original fallback boundaries and incremental invalidation.</summary>
internal static class SerializerUnionCursorRegression
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Runtime.InteropServices;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Utility;
        [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable, LuminPackUnion(1, typeof(Fixed)), LuminPackUnion(2, typeof(Text)),
         LuminPackUnion(3, typeof(Private)), LuminPackUnion(4, typeof(Callback)),
         LuminPackUnion(5, typeof(Specialized)), LuminPackUnion(6, typeof(Property)), LuminPackUnion(7, typeof(Guarded))]
        public abstract partial class Root { }
        [LuminPackable] public sealed partial class Text : Root { public string Value; }
        [LuminPackable] public sealed partial class Private : Root { [LuminPackInclude] private int Number = 7; }
        [LuminPackable] public sealed partial class Callback : Root
        {
            public int Number;
            [LuminPackOnSerializing] public static void Before() { }
        }
        [LuminPackable] public sealed partial class Property : Root { public int Number { get; set; } }
        [LuminPackable, LuminPackOption(UntrustedData = true)] public sealed partial class Guarded : Root { public int Number; public string Note = ""; }
        [LuminPackable] public sealed partial class Specialized : Root { public global::System.Collections.Generic.List<int> Values = new(); }
        [LuminPackable, LuminPackWideTag, LuminPackUnion(60000, typeof(WideCase))] public abstract partial class Wide { }
        [LuminPackable] public sealed partial class WideCase : Wide { public int Number; }
        [LuminPackable, LuminPackUnion(1, typeof(GenericCase<int>))] public abstract partial class GenericRoot<T> { }
        [LuminPackable] public sealed partial class GenericCase<T> : GenericRoot<T> { public T Value; }
        [LuminPackable, LuminPackUnion(1, typeof(UnusedCase))] public abstract partial class UnusedRoot { }
        [LuminPackable] public sealed partial class UnusedCase : UnusedRoot { public int Number; }
        [LuminPackable, LuminPackUnion(1, typeof(BigCase))] public abstract partial class BigRoot { }
        [LuminPackable] public sealed partial class BigCase : BigRoot { public BigPayload Value; public int Tail; }
        [LuminPackable, StructLayout(LayoutKind.Sequential, Size = 20000)] public struct BigPayload { public int Marker; }
        [LuminPackable] public sealed class Graph { public Dictionary<int, Root> Values; }
        public static class Entry
        {
            public static void Check()
            {
                var graph = new Graph { Values = new Dictionary<int, Root> { [1] = new Fixed { Number = 11 }, [2] = new Text { Value = "text" }, [3] = new Guarded { Number = 29 } } };
                byte[] bytes = LuminPackSerializer.Serialize(graph);
                var copy = global::LuminPackUnitTest.ConcreteReads.Read<Graph>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Graph concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                if (((Fixed)copy.Values[1]).Number != 11 || ((Text)copy.Values[2]).Value != "text" || ((Guarded)copy.Values[3]).Number != 29 ||
                    !LuminPackSerializer.Serialize(copy).AsSpan().SequenceEqual(bytes)) throw new Exception("Mixed Union wire or field data changed.");
                _ = LuminPackSerializer.Serialize(new Dictionary<int, Wide> { [1] = new WideCase { Number = 19 } });
                _ = LuminPackSerializer.Serialize(new Dictionary<int, GenericRoot<int>> { [1] = new GenericCase<int> { Value = 23 } });
                var large = new List<BigRoot>
                {
                    new BigCase { Value = new BigPayload { Marker = 31 }, Tail = 32 },
                    new BigCase { Value = new BigPayload { Marker = 41 }, Tail = 42 }
                };
                using var largeDestination = new LuminBufferWriter(true);
                int initialCapacity = largeDestination.TotalLength;
                LuminPackSerializer.Serialize(in large, largeDestination);
                byte[] largeBytes = largeDestination.GetSpan().ToArray();
                var largeCopy = global::LuminPackUnitTest.ConcreteReads.Read<List<BigRoot>>(largeBytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref List<BigRoot> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
                if (largeDestination.TotalLength <= initialCapacity || largeBytes.Length != 40014 ||
                    ((BigCase)largeCopy[0]).Value.Marker != 31 || ((BigCase)largeCopy[0]).Tail != 32 ||
                    ((BigCase)largeCopy[1]).Value.Marker != 41 || ((BigCase)largeCopy[1]).Tail != 42 ||
                    !LuminPackSerializer.Serialize(largeCopy).AsSpan().SequenceEqual(largeBytes))
                    throw new Exception("Large fixed Union payload or buffer growth changed.");
            }
        }
        """;

    /// <summary>Checks automatic retention through each compatibility mode and with unsafe disabled.</summary>
    /// <param name="references">Current platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        VerifyCollectionDispatch(references);
        var modern = Compilation(references, "Full", allowUnsafe: true, modern: true);
        var modernDriver = Driver(modern: true).RunGeneratorsAndUpdateCompilation(modern, out var modernOutput, out _);
        VerifyCursorGuards(modernDriver, new[] { ("global::Fixed", 1) });
        Execute(modernOutput);

        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var before = Compilation(references, mode, true);
            var driver = Driver().RunGeneratorsAndUpdateCompilation(before, out var output, out var diagnostics);
            Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
            var shared = methods.Single(method => method.Identifier.ValueText == "SerializeUnionAt" &&
                method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::Fixed");
            Require(shared.ReturnType.ToString() == "int" &&
                !shared.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)),
                "The internal shared cursor body must return an absolute index without caller-facing XML.");
            Require(methods.Where(method => method.Identifier.ValueText == "SerializeUnionAt")
                .All(method => method.AttributeLists.ToString().Contains("AggressiveInlining", StringComparison.Ordinal)),
                "Fixed cursor kernels must retain caller-driven framing and architecture specialization.");
            Require(!methods.Any(method => method.Identifier.ValueText == "SerializeUnionAt" &&
                method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') is "global::Private" or "global::Callback" or "global::Specialized" or "global::Property"),
                "A private, callback, property or collection payload entered the raw cursor body.");
            var cursorSlots = methods.Where(method => method.Identifier.ValueText.StartsWith("LuminPackUnionSerializeCursor_", StringComparison.Ordinal)).ToArray();
            Require(cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Root" } && method.Modifiers.Any(SyntaxKind.VirtualKeyword)) &&
                cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Fixed" } && method.Modifiers.Any(SyntaxKind.OverrideKeyword)),
                "The fixed path omitted its virtual root or exact case override.");
            var fixedSlots = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Fixed" } &&
                method.Identifier.ValueText.StartsWith("LuminPackUnionSerialize_", StringComparison.Ordinal)).ToArray();
            Require(fixedSlots.Length == 6 && fixedSlots.All(method => method.Body!.ToString().Contains("SerializeUnionAt", StringComparison.Ordinal) &&
                method.Body.ToString().Contains("writer.CurrentIndex", StringComparison.Ordinal)),
                "The original virtual slots must publish the shared fixed tag and payload body for each binary profile.");
            var largeSlots = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "BigCase" } &&
                method.Identifier.ValueText.StartsWith("LuminPackUnionSerialize_", StringComparison.Ordinal)).ToArray();
            Require(largeSlots.Length == 6 && largeSlots.All(method => method.Body!.ToString().Contains("SerializeUnionAt", StringComparison.Ordinal)),
                "The large fixed-case fixture did not exercise the original-slot projection.");
            var variableSlots = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Text" or "Callback" or "Guarded" } &&
                method.Identifier.ValueText.StartsWith("LuminPackUnionSerialize_", StringComparison.Ordinal)).ToArray();
            Require(variableSlots.Length == 18 && variableSlots.All(method => method.Body!.ToString().Contains("WriteUnionHeader", StringComparison.Ordinal) &&
                !method.Body.ToString().Contains("SerializeUnionAt", StringComparison.Ordinal)),
                "Variable and callback cases must retain the original virtual operation path.");
            Require(!cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax parent && parent.Identifier.ValueText is
                "Wide" or "WideCase" or "GenericRoot" or "GenericCase"),
                "A wide/generic root acquired a cursor slot.");
            foreach (var forwarding in cursorSlots.Where(method => method.Parent is ClassDeclarationSyntax parent &&
                parent.Identifier.ValueText is "Private" or "Callback" or "Specialized" or "Property" or "Text" or "Guarded"))
                Require(forwarding.Body!.Statements.Count == 3 && forwarding.Body.Statements[0].ToString().Contains("writer.CurrentIndex = initialIndex", StringComparison.Ordinal) &&
                    forwarding.Body.Statements[1].ToString().Contains("LuminPackUnionSerialize_", StringComparison.Ordinal) &&
                    forwarding.Body.Statements[2].ToString().Contains("writer.Position", StringComparison.Ordinal),
                    "A non-fixed case bypassed publication or its original virtual operation.");
            Require(methods.Any(method => method.Body?.ToString().Contains("LuminPackUnionSerializeCursor_", StringComparison.Ordinal) == true &&
                SerializerExecutionAssertions.IsSerializerRootContainer(method.Parent)), "The retained dictionary did not reach the fixed virtual protocol.");
            Require(methods.SelectMany(method => method.DescendantNodes().OfType<AssignmentExpressionSyntax>()).Any(assignment =>
                assignment.Right is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1,
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CheckBuffer" } } check &&
                assignment.Left.ToString() == check.ArgumentList.Arguments[0].Expression.ToString()),
                "The dictionary must check and reuse its actual unpublished cursor rather than the stale shared index.");
            VerifyCursorGuards(driver, new[] { ("global::Fixed", 1) });
            Require(cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "UnusedRoot" }),
                "Automatic generation must retain the formatter slots of a declared Union even without a local serializer call.");
            Execute(output);

            var leaf = before.SyntaxTrees.Single(tree => tree.FilePath == "Fixed.cs");
            var after = before.ReplaceSyntaxTree(leaf, CSharpSyntaxTree.ParseText(leaf.ToString().Replace("public int Number", "public volatile int Number"),
                new CSharpParseOptions(LanguageVersion.CSharp13), "Fixed.cs"));
            var retained = driver.RunGeneratorsAndUpdateCompilation(after, out var updated, out diagnostics);
            var fresh = Driver().RunGenerators(after);
            Require(Files(retained).SequenceEqual(Files(fresh)), "A fixed-to-volatile edit retained stale nested Union cursor output.");
            Require(!retained.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Any(source =>
                source.SourceText.ToString().Contains("LuminPackUnionSerializeCursor_", StringComparison.Ordinal) && source.SourceText.ToString().Contains("globalRoot_", StringComparison.Ordinal)),
                "The last fixed case became volatile but its root cursor protocol survived.");
            Execute(updated);

            // Making a second case fixed must reuse its cursor kernel in tag order, retained exactly like fresh generation.
            var root = before.SyntaxTrees.Single(tree => tree.FilePath == "Root.cs");
            var widened = before.ReplaceSyntaxTree(root, CSharpSyntaxTree.ParseText(root.ToString().Replace("[LuminPackInclude] private int Number = 7;", "public int Number = 7;"),
                new CSharpParseOptions(LanguageVersion.CSharp13), "Root.cs"));
            var retainedWide = driver.RunGeneratorsAndUpdateCompilation(widened, out var widenedOutput, out diagnostics);
            Require(Files(retainedWide).SequenceEqual(Files(Driver().RunGenerators(widened))), "A newly fixed case retained a stale cursor call.");
            VerifyCursorGuards(retainedWide, new[] { ("global::Fixed", 1), ("global::Private", 3) });
            Execute(widenedOutput);

            // The dictionary guard depends on the root's declared string profile across retained compilations.
            var changedProfile = before.ReplaceSyntaxTree(root, CSharpSyntaxTree.ParseText(root.ToString().Replace(
                "public abstract partial class Root { }",
                "[LuminPackOption(StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF8, StringRecording = LuminPack.Option.LuminPackStringRecording.Token)] public abstract partial class Root { }"),
                new CSharpParseOptions(LanguageVersion.CSharp13), "Root.cs"));
            var retainedProfile = driver.RunGeneratorsAndUpdateCompilation(changedProfile, out var profileOutput, out diagnostics);
            Require(Files(retainedProfile).SequenceEqual(Files(Driver().RunGenerators(changedProfile))), "A changed Union root profile retained stale dictionary guard output.");
            Execute(profileOutput);
        }
        var safe = Driver().RunGeneratorsAndUpdateCompilation(Compilation(references, "Minimal", false), out var safeOutput, out _);
        Require(!safe.GetRunResult().Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "Unsafe-disabled cursor generation failed.");
        Execute(safeOutput);
        Console.WriteLine("Passed fixed Union cursor signatures, internal XML policy, large fixed growth, exact case dispatch on both targets, wide/generic fallback, automatic retention, unsafe-disabled output and retained/fresh volatility, widening and profile edits.");
    }

    /// <summary>Compiles modern fixed Union collections and verifies ordered per-element wire and round trips.</summary>
    /// <param name="references">Current platform and runtime references.</param>
    private static void VerifyCollectionDispatch(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable, LuminPackUnion(0, typeof(First)), LuminPackUnion(1, typeof(Second))]
            public abstract partial class CollectionRoot { public int Id; }
            [LuminPackable] public sealed partial class First : CollectionRoot { public long Value; }
            [LuminPackable] public sealed partial class Second : CollectionRoot { public int Value; }
            public static class Entry
            {
                public static void Check()
                {
                    foreach (int count in new[] { 64, 600 })
                    {
                        var random = new Random(197);
                        var values = new CollectionRoot[count];
                        for (int i = 0; i < count; i++) values[i] = random.Next(2) == 0
                            ? (CollectionRoot)new First { Id = i, Value = i * 71L }
                            : new Second { Id = i, Value = -i };
                        values[count - 1] = null;
                        byte[] array = LuminPackSerializer.Serialize(values);
                        byte[] list = LuminPackSerializer.Serialize(new List<CollectionRoot>(values));
                        byte[] immutable = LuminPackSerializer.Serialize(ImmutableArray.CreateRange(values));
                        if (!array.AsSpan().SequenceEqual(list) || !array.AsSpan().SequenceEqual(immutable))
                            throw new Exception("Union collection wire differs by container.");
                        var copies = new[] {
                            global::LuminPackUnitTest.ConcreteReads.Read<CollectionRoot[]>(array, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref CollectionRoot[] concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)),
                            global::LuminPackUnitTest.ConcreteReads.Read<List<CollectionRoot>>(list, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref List<CollectionRoot> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).ToArray(),
                            global::LuminPackUnitTest.ConcreteReads.Read<ImmutableArray<CollectionRoot>>(immutable, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref ImmutableArray<CollectionRoot> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget)).ToArray()
                        };
                        foreach (var copy in copies)
                            if (!LuminPackSerializer.Serialize(copy).AsSpan().SequenceEqual(array))
                                throw new Exception("Union collection order or payload changed.");
                    }
                }
            }
            """;
        var options = new CSharpParseOptions(LanguageVersion.CSharp13, preprocessorSymbols: new[] { "NET8_0_OR_GREATER" });
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            var compilation = TestCompilation.Create("UnionCollections_" + mode,
                new[] { CSharpSyntaxTree.ParseText(source.Replace("MODE", mode), options) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = CSharpGeneratorDriver.Create(new[] { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() },
                parseOptions: options).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            string generated = Generated(driver);
            Require(!generated.Contains("LuminPackUnionPartition", StringComparison.Ordinal) &&
                !generated.Contains("LuminUnionPartitionMap", StringComparison.Ordinal), "Fixed Union collections must use per-element dispatch.");
            Execute(output);
        }
    }

    /// <summary>Checks that a dictionary applies its root's exact comparisons and uses the fixed kernel only for eligible cases.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <param name="cases">The expected fixed cases and their tags, in tag order.</param>
    private static void VerifyCursorGuards(GeneratorDriver driver, (string Type, int Tag)[] cases)
    {
        var chains = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<IfStatementSyntax>())
            .Where(node => node.Parent is not ElseClauseSyntax &&
                node.Condition.ToString().Contains(" == typeof(global::Fixed)", StringComparison.Ordinal) &&
                node.Statement.ToString().Contains("nextIndex = ", StringComparison.Ordinal)).ToArray();
        Require(chains.Length != 0, "The dictionary lost its exact case chain.");
        foreach (var chain in chains)
        {
            var fixedArms = new List<IfStatementSyntax>();
            int count = 0;
            StatementSyntax? branch = chain;
            while (branch is IfStatementSyntax arm)
            {
                count++;
                if (arm.Statement.ToString().Contains("SerializeUnionAt", StringComparison.Ordinal))
                    fixedArms.Add(arm);
                branch = arm.Else?.Statement;
            }
            Require(count == 7 && branch is BlockSyntax &&
                branch.ToString().Contains("writer.CurrentIndex = nextIndex", StringComparison.Ordinal) &&
                branch.ToString().Contains("LuminPackUnionWriteUnmatched_", StringComparison.Ordinal),
                "The cursor chain must try all seven cases and publish the cursor before registration.");
            Require(fixedArms.Count == cases.Length, "A non-fixed case entered the fixed cursor kernel.");
            for (int index = 0; index < cases.Length; index++)
            {
                var (type, tag) = cases[index];
                Require(fixedArms[index].Condition is BinaryExpressionSyntax { Right: TypeOfExpressionSyntax typeOf } &&
                    typeOf.Type.ToString() == type && fixedArms[index].Statement.ToString().Contains("(byte)" + tag, StringComparison.Ordinal),
                    "The cursor chain lost or reordered " + type + ".");
            }
        }
    }

    /// <summary>Concatenates every generated source for structural checks.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The generated source text.</returns>
    private static string Generated(GeneratorDriver driver) => string.Join(Environment.NewLine, driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(source => source.SourceText.ToString()));

    /// <summary>Creates independent root and leaf trees so nested dependency guards must observe edits.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="mode">The actual generation mode.</param>
    /// <param name="allowUnsafe">Whether manual registration is available.</param>
    /// <param name="modern">Whether the target enables the .NET 8 dispatch tiers.</param>
    /// <returns>The unchanged mixed Union fixture.</returns>
    private static CSharpCompilation Compilation(MetadataReference[] references, string mode, bool allowUnsafe, bool modern = false)
        => TestCompilation.Create("UnionCursor_" + mode + "_" + Guid.NewGuid().ToString("N"), new[]
        {
            CSharpSyntaxTree.ParseText(Source.Replace("MODE", mode), ParseOptions(modern), "Root.cs"),
            CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public sealed partial class Fixed : Root { public int Number; }", ParseOptions(modern), "Fixed.cs")
        }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe, optimizationLevel: OptimizationLevel.Release));

    /// <summary>Creates a fresh pair of incremental generators under the fixture's language version.</summary>
    /// <param name="modern">Whether the target enables the .NET 8 dispatch tiers.</param>
    /// <returns>A driver that can be retained across a leaf edit.</returns>
    private static GeneratorDriver Driver(bool modern = false) => CSharpGeneratorDriver.Create(new[]
    {
        new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()
    }, parseOptions: ParseOptions(modern));

    /// <summary>Selects the parse options and target symbols for the test fixture.</summary>
    /// <param name="modern">Whether to select the modern .NET target symbols.</param>
    /// <returns>The options.</returns>
    private static CSharpParseOptions ParseOptions(bool modern)
        => new(LanguageVersion.CSharp13, preprocessorSymbols: modern ? new[] { "NET8_0_OR_GREATER" } : null);

    /// <summary>Orders the exact generated files for retained-versus-fresh comparisons.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>Stable file identities and complete source text.</returns>
    private static string[] Files(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .Select(source => source.HintName + "\n" + source.SourceText).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    /// <summary>Compiles and executes the actual generated mixed-case round trip.</summary>
    /// <param name="compilation">The completed consumer output.</param>
    private static void Execute(Compilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Reports a structural, documentation or runtime regression.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure explanation.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
