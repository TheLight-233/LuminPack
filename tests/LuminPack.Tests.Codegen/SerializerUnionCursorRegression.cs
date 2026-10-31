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
        [LuminPackable, LuminPackOption(UntrustedData = true)] public sealed partial class Guarded : Root { public int Number; }
        [LuminPackable(GeneratorType.VersionTolerant)] public partial struct Versioned { [LuminPackOrder(1)] public int Number; }
        [LuminPackable] public sealed partial class Specialized : Root { public Versioned Value; }
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
                var copy = LuminPackSerializer.Deserialize<Graph>(bytes);
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
                var largeCopy = LuminPackSerializer.Deserialize<List<BigRoot>>(largeBytes);
                if (largeDestination.TotalLength <= initialCapacity || largeBytes.Length != 40014 ||
                    ((BigCase)largeCopy[0]).Value.Marker != 31 || ((BigCase)largeCopy[0]).Tail != 32 ||
                    ((BigCase)largeCopy[1]).Value.Marker != 41 || ((BigCase)largeCopy[1]).Tail != 42 ||
                    !LuminPackSerializer.Serialize(largeCopy).AsSpan().SequenceEqual(largeBytes))
                    throw new Exception("Large fixed Union payload or buffer growth changed.");
            }
        }
        """;

    /// <summary>Checks all pruning modes and the registration-free unsafe-disabled configuration.</summary>
    /// <param name="references">Current platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        VerifyVariablePartitions(references);
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
            Require(!methods.Any(method => method.Identifier.ValueText == "SerializeUnionAt" &&
                method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') is "global::Private" or "global::Callback" or "global::Specialized" or "global::Property"),
                "A private, callback, property or versioned payload entered the raw cursor body.");
            var cursorSlots = methods.Where(method => method.Identifier.ValueText.StartsWith("__LuminPackUnionSerializeCursor_", StringComparison.Ordinal)).ToArray();
            Require(cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Root" } && method.Modifiers.Any(SyntaxKind.VirtualKeyword)) &&
                cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Fixed" } && method.Modifiers.Any(SyntaxKind.OverrideKeyword)),
                "The fixed path omitted its virtual root or exact case override.");
            var fixedSlots = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Fixed" } &&
                method.Identifier.ValueText.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal)).ToArray();
            Require(fixedSlots.Length == 6 && fixedSlots.All(method => method.Body!.ToString().Contains("SerializeUnionAt", StringComparison.Ordinal) &&
                method.Body.ToString().Contains("writer.CurrentIndex", StringComparison.Ordinal)),
                "The original virtual slots must publish the shared fixed tag and payload body for each binary profile.");
            var largeSlots = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "BigCase" } &&
                method.Identifier.ValueText.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal)).ToArray();
            Require(largeSlots.Length == 6 && largeSlots.All(method => method.Body!.ToString().Contains("SerializeUnionAt", StringComparison.Ordinal)),
                "The large fixed-case fixture did not exercise the original-slot projection.");
            var variableSlots = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Text" or "Callback" or "Guarded" } &&
                method.Identifier.ValueText.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal)).ToArray();
            Require(variableSlots.Length == 18 && variableSlots.All(method => method.Body!.ToString().Contains("WriteUnionHeader", StringComparison.Ordinal) &&
                !method.Body.ToString().Contains("SerializeUnionAt", StringComparison.Ordinal)),
                "Variable, callback and external-input-policy cases must retain the original virtual operation path.");
            Require(!cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax parent && parent.Identifier.ValueText is
                "Wide" or "WideCase" or "GenericRoot" or "GenericCase"),
                "A wide/generic root acquired a cursor slot.");
            foreach (var forwarding in cursorSlots.Where(method => method.Parent is ClassDeclarationSyntax parent &&
                parent.Identifier.ValueText is "Private" or "Callback" or "Specialized" or "Property" or "Text" or "Guarded"))
                Require(forwarding.Body!.Statements.Count == 3 && forwarding.Body.Statements[0].ToString().Contains("writer.CurrentIndex = initialIndex", StringComparison.Ordinal) &&
                    forwarding.Body.Statements[1].ToString().Contains("__LuminPackUnionSerialize_", StringComparison.Ordinal) &&
                    forwarding.Body.Statements[2].ToString().Contains("writer.Position", StringComparison.Ordinal),
                    "A non-fixed case bypassed publication or its original virtual operation.");
            Require(methods.Any(method => method.Body?.ToString().Contains("__LuminPackUnionSerializeCursor_", StringComparison.Ordinal) == true &&
                SerializerExecutionAssertions.IsSerializerRootContainer(method.Parent)), "The retained dictionary did not reach the fixed virtual protocol.");
            Require(methods.SelectMany(method => method.DescendantNodes().OfType<AssignmentExpressionSyntax>()).Any(assignment =>
                assignment.Right is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1,
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CheckBuffer" } } check &&
                assignment.Left.ToString() == check.ArgumentList.Arguments[0].Expression.ToString()),
                "The dictionary must check and reuse its actual unpublished cursor rather than the stale shared index.");
            VerifyCaseChain(Generated(driver), new[] { ("global::Fixed", 1) });
            if (mode == "Minimal") Require(!cursorSlots.Any(method => method.Parent is ClassDeclarationSyntax parent && parent.Identifier.ValueText is "UnusedRoot" or "UnusedCase"),
                "Minimal retained an unused Union cursor family.");
            Execute(output);

            var leaf = before.SyntaxTrees.Single(tree => tree.FilePath == "Fixed.cs");
            var after = before.ReplaceSyntaxTree(leaf, CSharpSyntaxTree.ParseText(leaf.ToString().Replace("public int Number", "public volatile int Number"),
                new CSharpParseOptions(LanguageVersion.CSharp13), "Fixed.cs"));
            var retained = driver.RunGeneratorsAndUpdateCompilation(after, out var updated, out diagnostics);
            var fresh = Driver().RunGenerators(after);
            Require(Files(retained).SequenceEqual(Files(fresh)), "A fixed-to-volatile edit retained stale nested Union cursor output.");
            Require(!retained.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Any(source =>
                source.SourceText.ToString().Contains("__LuminPackUnionSerializeCursor_", StringComparison.Ordinal) && source.SourceText.ToString().Contains("globalRoot_", StringComparison.Ordinal)),
                "The last fixed case became volatile but its root cursor protocol survived.");
            Execute(updated);

            // Making a second case fixed must extend the exact-case chain in tag order, retained exactly like fresh generation.
            var root = before.SyntaxTrees.Single(tree => tree.FilePath == "Root.cs");
            var widened = before.ReplaceSyntaxTree(root, CSharpSyntaxTree.ParseText(root.ToString().Replace("[LuminPackInclude] private int Number = 7;", "public int Number = 7;"),
                new CSharpParseOptions(LanguageVersion.CSharp13), "Root.cs"));
            var retainedWide = driver.RunGeneratorsAndUpdateCompilation(widened, out var widenedOutput, out diagnostics);
            Require(Files(retainedWide).SequenceEqual(Files(Driver().RunGenerators(widened))), "A newly fixed case retained a stale exact-case chain.");
            VerifyCaseChain(Generated(retainedWide), new[] { ("global::Fixed", 1), ("global::Private", 3) });
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
        Console.WriteLine("Passed fixed Union cursor signatures, internal XML policy, large fixed growth, public/private/property/callback/versioned guards, exact-case call-site chains, wide/generic fallback, four pruning modes, unsafe-disabled output and retained/fresh volatility, widening and profile edits.");
    }

    /// <summary>Checks the dictionary call site tests exactly the fixed cases in tag order before falling back to the cursor slot.</summary>
    /// <param name="generated">The complete generated source.</param>
    /// <param name="cases">The expected fixed cases and their tags, in tag order.</param>
    private static void VerifyCaseChain(string generated, (string Type, int Tag)[] cases)
    {
        // Source expansion reflows statements and may rename locals, so compare normalized text with captured names.
        string text = System.Text.RegularExpressions.Regex.Replace(generated, @"\s+", " ");
        var slot = System.Text.RegularExpressions.Regex.Match(text, @"else nextIndex = (\w+)\.__LuminPackUnionSerializeCursor_\w+\((?:ref )?(\w+), nextIndex\);");
        Require(slot.Success, "The dictionary lost its cursor slot fallback.");
        string value = slot.Groups[1].Value, state = slot.Groups[2].Value;
        int start = text.LastIndexOf("if (" + value + " is null)", slot.Index, StringComparison.Ordinal);
        Require(start >= 0, "The dictionary lost its null Union record.");
        string chain = text.Substring(start, slot.Index - start);
        int position = 0;
        foreach (var (type, tag) in cases)
        {
            string test = "else if (" + value + ".GetType() == typeof(" + type + ")) nextIndex = global::LuminPack.LuminPackSerializer.SerializeUnionAt(ref " + state +
                ", global::System.Runtime.CompilerServices.Unsafe.As<" + type + ">(" + value + "), nextIndex, (byte)" + tag + ");";
            int found = chain.IndexOf(test, position, StringComparison.Ordinal);
            Require(found >= 0, "The exact-case chain lost or reordered " + type + ".");
            position = found + test.Length;
        }
        Require(chain.Split("GetType() == typeof(").Length - 1 == cases.Length, "A non-fixed case entered the exact-case chain.");
    }

    /// <summary>Concatenates every generated source for structural checks.</summary>
    /// <param name="driver">The completed generation.</param>
    /// <returns>The generated source text.</returns>
    private static string Generated(GeneratorDriver driver) => string.Join(Environment.NewLine, driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(source => source.SourceText.ToString()));

    /// <summary>Creates independent root and leaf trees so nested dependency guards must observe edits.</summary>
    /// <param name="references">Platform and runtime references.</param>
    /// <param name="mode">The actual generation mode.</param>
    /// <param name="allowUnsafe">Whether manual registration is available.</param>
    /// <returns>The unchanged mixed Union fixture.</returns>
    private static CSharpCompilation Compilation(MetadataReference[] references, string mode, bool allowUnsafe)
        => CSharpCompilation.Create("UnionCursor_" + mode + "_" + Guid.NewGuid().ToString("N"), new[]
        {
            CSharpSyntaxTree.ParseText(Source.Replace("MODE", mode), new CSharpParseOptions(LanguageVersion.CSharp13), "Root.cs"),
            CSharpSyntaxTree.ParseText("[LuminPack.Attribute.LuminPackable] public sealed partial class Fixed : Root { public int Number; }", new CSharpParseOptions(LanguageVersion.CSharp13), "Fixed.cs")
        }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe, optimizationLevel: OptimizationLevel.Release));

    /// <summary>Creates a fresh pair of incremental generators under the fixture's language version.</summary>
    /// <returns>A driver that can be retained across a leaf edit.</returns>
    private static GeneratorDriver Driver() => CSharpGeneratorDriver.Create(new[]
    {
        new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()
    }, parseOptions: new CSharpParseOptions(LanguageVersion.CSharp13));

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

    /// <summary>Checks exact and upper-bound variable partitions against ordinary records and their checked readers.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void VerifyVariablePartitions(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            using LuminPack.Utility;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable, LuminPackUnion(0, typeof(TextCase)), LuminPackUnion(1, typeof(ArrayCase)), LuminPackUnion(2, typeof(ScalarCase))]
            public abstract partial class VariableRoot { public int Id; }
            [LuminPackable] public sealed partial class TextCase : VariableRoot { public string? Text; }
            [LuminPackable] public sealed partial class ArrayCase : VariableRoot { public int[]? Values; }
            [LuminPackable] public sealed partial class ScalarCase : VariableRoot { public long Value; }
            public sealed class Foreign : VariableRoot { }
            [LuminPackable] public sealed class FixedElement { public int X; public double Y; }
            [LuminPackable, LuminPackUnion(0, typeof(ObjectCase)), LuminPackUnion(1, typeof(ObjectTextCase))]
            public abstract partial class ObjectRoot { public int Id; }
            [LuminPackable] public sealed partial class ObjectCase : ObjectRoot { public List<FixedElement>? Values; }
            [LuminPackable] public sealed partial class ObjectTextCase : ObjectRoot { public string? Text; }
            [LuminPackable, LuminPackUnion(0, typeof(UnsupportedCase)), LuminPackUnion(1, typeof(UnsupportedTextCase))]
            public abstract partial class UnsupportedRoot { }
            [LuminPackable] public sealed partial class UnsupportedCase : UnsupportedRoot { public string[]? Values; }
            [LuminPackable] public sealed partial class UnsupportedTextCase : UnsupportedRoot { public string? Text; }
            public static class Entry
            {
                private delegate bool WriteChunk(ref LuminPackWriter writer, ReadOnlySpan<VariableRoot> values);
                public static void Check()
                {
                    foreach (int count in new[] { 0, 1, 31, 32, 64, 255, 256, 257, 600 })
                    foreach (bool predictable in new[] { false, true })
                    {
                        var random = new Random(197);
                        var values = new List<VariableRoot>();
                        for (int i = 0; i < count; i++)
                            values.Add((predictable ? i % 3 : random.Next(3)) switch
                            {
                                0 => new TextCase { Id = i, Text = i % 5 == 0 ? null : new string(i % 2 == 0 ? 'a' : '\u4e2d', i % 81) },
                                1 => new ArrayCase { Id = i, Values = i % 7 == 0 ? null : Enumerable.Range(i, i % 31).ToArray() },
                                _ => new ScalarCase { Id = i, Value = -i * 71L }
                            });
                        CheckValues(values, false, !predictable && count == 64);
                        CheckValues(values, true, !predictable && count == 64);
                        var varint = LuminPackSerializer.Serialize<LuminPackUtf16VarIntOptions>(values);
                        if (!LuminPackSerializer.Serialize<LuminPackUtf16VarIntOptions>(LuminPackSerializer.Deserialize<List<VariableRoot>, LuminPackUtf16VarIntOptions>(varint)).AsSpan().SequenceEqual(varint)) throw new Exception("VarInt fallback changed.");
                        if (count > 1) { var saved = values[1]; values[1] = null!; CheckValues(values, false); values[1] = saved; }
                    }
                    VariableRoot raw = new TextCase { Id = 7, Text = "abc" };
                    byte[] rawBytes = LuminPackSerializer.Serialize(raw);
                    if (rawBytes.Length != 15 || BitConverter.ToInt32(rawBytes, 5) != 6) throw new Exception("Partition UTF-16 was compacted.");
                    byte[] legacy = new byte[] { 0, 7, 0, 0, 0, 7, 0, 0, 0, 97, 98, 99 };
                    if (LuminPackSerializer.Deserialize<VariableRoot>(legacy) is not TextCase { Id: 7, Text: "abc" }) throw new Exception("An existing compact Union record is no longer readable.");
                    var concrete = LuminPackSerializer.Serialize(new TextCase { Id = 7, Text = "abc" });
                    if (concrete.Length != 15 || BitConverter.ToInt32(concrete, 5) != 6) throw new Exception("Concrete case policy differs.");
                    UnsupportedRoot unsupported = new UnsupportedTextCase { Text = "abc" };
                    if (LuminPackSerializer.Serialize(unsupported).Length != 8) throw new Exception("Unsupported root lost its compact string policy.");
                    var objects = new List<ObjectRoot>();
                    var kinds = new Random(31);
                    for (int i = 0; i < 300; i++) objects.Add(kinds.Next(2) == 0
                        ? new ObjectCase { Id = i, Values = i % 11 == 0 ? null : new List<FixedElement> { null!, new() { X = i, Y = i + 0.5 }, null! } }
                        : new ObjectTextCase { Id = i, Text = new string('x', i % 17) });
                    byte[] objectBytes = LuminPackSerializer.Serialize(objects);
                    var expected = new List<byte>(BitConverter.GetBytes(objects.Count));
                    foreach (var item in objects) expected.AddRange(LuminPackSerializer.Serialize(item));
                    if (!objectBytes.AsSpan().SequenceEqual(expected.ToArray()) || !LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<List<ObjectRoot>>(objectBytes)).AsSpan().SequenceEqual(objectBytes)) throw new Exception("Fixed-object upper-bound compaction changed records.");
                    var foreign = new List<VariableRoot> { raw, new Foreign() };
                    bool rejected = false;
                    try { LuminPackSerializer.Serialize(foreign); } catch { rejected = true; }
                    if (!rejected) throw new Exception("Foreign fallback was bypassed.");
                }
                private static void CheckValues(List<VariableRoot> values, bool utf8, bool requirePartition = false)
                {
                    byte[] bytes = utf8 ? LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(values) : LuminPackSerializer.Serialize(values);
                    var expected = new List<byte>(BitConverter.GetBytes(values.Count));
                    foreach (var value in values) expected.AddRange(utf8 ? LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(value) : LuminPackSerializer.Serialize(value));
                    if (!bytes.AsSpan().SequenceEqual(expected.ToArray())) throw new Exception("Variable partition wire mismatch.");
                    var copy = utf8 ? LuminPackSerializer.Deserialize<List<VariableRoot>, LuminPackUtf8LengthOptions>(bytes) : LuminPackSerializer.Deserialize<List<VariableRoot>>(bytes);
                    byte[] again = utf8 ? LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(copy) : LuminPackSerializer.Serialize(copy);
                    if (!bytes.AsSpan().SequenceEqual(again)) throw new Exception("Variable partition roundtrip mismatch.");
                    var array = values.ToArray();
                    byte[] arrayBytes = utf8 ? LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(array) : LuminPackSerializer.Serialize(array);
                    var immutable = ImmutableArray.CreateRange(values);
                    byte[] immutableBytes = utf8 ? LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(immutable) : LuminPackSerializer.Serialize(immutable);
                    if (!arrayBytes.AsSpan().SequenceEqual(bytes) || !immutableBytes.AsSpan().SequenceEqual(bytes)) throw new Exception("Contiguous collection policy mismatch.");
                    if (values.Count == 64 && values.All(item => item is not null))
                    {
                        var definition = typeof(VariableRoot).GetMethods().Single(method => method.Name.StartsWith("__LuminPackUnionPartitionVariableWrite_") && method.IsGenericMethodDefinition);
                        var kernel = (WriteChunk)definition.MakeGenericMethod(utf8 ? typeof(LuminPackUtf8LengthOptions) : typeof(LuminPackDefaultOptions)).CreateDelegate(typeof(WriteChunk));
                        using var buffer = new LuminBufferWriter(true);
                        var writer = new LuminPackWriter(buffer); writer.EnsureAdditionalCapacity(4); writer.Write(values.Count);
                        bool accepted = false;
                        for (int attempt = 0; attempt < (requirePartition ? 64 : 1); attempt++)
                        {
                            writer.Position = 4; accepted = kernel(ref writer, values.ToArray());
                            if (accepted) break;
                            if (writer.Position != 4) throw new Exception("Declined kernel advanced the cursor.");
                        }
                        if (requirePartition && !accepted) throw new Exception("Random variable chunk never used ATPD.");
                        if (accepted) { buffer.CompleteWrite(writer.Position); if (!buffer.GetSpan().SequenceEqual(bytes)) throw new Exception("Direct partition kernel mismatch."); }
                    }
                    if (values.Count != 0)
                    {
                        bool rejected = false;
                        try { if (utf8) LuminPackSerializer.Deserialize<List<VariableRoot>, LuminPackUtf8LengthOptions>(bytes[..^1]); else LuminPackSerializer.Deserialize<List<VariableRoot>>(bytes[..^1]); } catch { rejected = true; }
                        if (!rejected) throw new Exception("Truncated partition was accepted.");
                        var target = copy!; object? first = target[0];
                        if (utf8) LuminPackSerializer.Deserialize<List<VariableRoot>, LuminPackUtf8LengthOptions>(bytes, ref target); else LuminPackSerializer.Deserialize(bytes, ref target);
                        if (!ReferenceEquals(first, target[0])) throw new Exception("Existing Union instance was replaced.");
                    }
                    if (values.Count == 600)
                    {
                        using var output = new LuminBufferWriter(true);
                        byte[] prefix = new byte[32700]; Array.Fill(prefix, (byte)0xa5);
                        var writer = new LuminPackWriter(output); writer.EnsureAdditionalCapacity(prefix.Length); prefix.CopyTo(writer.Buffer); writer.Position = prefix.Length; output.CompleteWrite(writer.Position);
                        if (utf8) LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(values, output); else LuminPackSerializer.Serialize(values, output);
                        if (!output.GetSpan().Slice(0, prefix.Length).SequenceEqual(prefix) || !output.GetSpan().Slice(prefix.Length).SequenceEqual(bytes)) throw new Exception("Appending a variable partition across growth corrupted output.");
                    }
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.CSharp13, preprocessorSymbols: new[] { "NET8_0_OR_GREATER", "NET9_0_OR_GREATER" });
            var input = CSharpCompilation.Create("VariablePartitions_" + mode + "_" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(source.Replace("MODE", mode), parse) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = CSharpGeneratorDriver.Create(new[] { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() }, parseOptions: parse)
                .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            string generated = string.Join("\n", Files(driver));
            Require(generated.Contains("__LuminPackUnionPartitionVariableWrite_", StringComparison.Ordinal) &&
                generated.Contains("SerializeUnionPartitionAt", StringComparison.Ordinal) && generated.Contains("reader.EnsureReadable(position)", StringComparison.Ordinal),
                "Variable partition kernels or checked read framing are missing.");
            Execute(output);
            var originalTree = input.SyntaxTrees.Single();
            string editedSource = originalTree.ToString().Replace("public long Value; }", "public long Value; [LuminPackOnSerializing] public static void Before() { } }");
            var edited = input.ReplaceSyntaxTree(originalTree, CSharpSyntaxTree.ParseText(editedSource, parse));
            var retained = driver.RunGeneratorsAndUpdateCompilation(edited, out var editedOutput, out _);
            Require(!editedOutput.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "A sibling callback edit broke the compiled fallback.");
            var fresh = CSharpGeneratorDriver.Create(new[] { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() }, parseOptions: parse).RunGenerators(edited);
            Require(Files(retained).SequenceEqual(Files(fresh)), "A sibling payload edit left stale ATPD or raw-string eligibility in retained generation.");
        }
    }

    /// <summary>Reports a structural, documentation or runtime regression.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure explanation.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
