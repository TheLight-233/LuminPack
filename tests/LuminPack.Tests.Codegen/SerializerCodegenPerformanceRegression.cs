using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects wire layout and dispatch while optimizing generated unmanaged field writes.</summary>
internal static partial class SerializerCodegenPerformanceRegression
{
    /// <summary>Checks repeated padded structs, Union input passing, and array storage references.</summary>
    /// <param name="references">Runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        VerifyProvenLeafWrites(references);
        VerifyProvenObjectWrites(references);
        VerifyProvenProfileIdentity(references);
        VerifyCursorAliasStorage();
        VerifyCheckedReaderAdvances();
        VerifyNativeOffsets(references);
        SerializerPayloadAliasRegression.Run(references);
        const string source = """
            using System;
            using System.Buffers;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using LuminPack.Utility;
            using LuminPack.Code;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
            [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 16)] public struct Raw
            {
                [FieldOffset(0)] public byte Tag;
                [FieldOffset(8)] public long Number;
            }
            [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 600)] public struct LargeRaw
            {
                [FieldOffset(596)] public int Tail;
            }
            public enum Narrow : byte { Value = 231 }
            public sealed class Segment : ReadOnlySequenceSegment<byte>
            {
                public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;
                public Segment Append(ReadOnlyMemory<byte> memory)
                {
                    var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
                    Next = next;
                    return next;
                }
            }
            [LuminPackable, LuminPackUnion(11, typeof(FixedChild)), LuminPackUnion(13, typeof(TextChild))] public abstract partial class Root { }
            [LuminPackable] public sealed partial class FixedChild : Root
            {
                public int Head;
                public Raw First;
                public Raw Second;
                public long Tail;
            }
            [LuminPackable, LuminPackUnion(2, typeof(InnerFirst)), LuminPackUnion(3, typeof(InnerSecond))]
            public abstract partial class InnerRoot { }
            [LuminPackable] public sealed partial class InnerFirst : InnerRoot { public string Text; public int Number; }
            [LuminPackable] public sealed partial class InnerSecond : InnerRoot { public string Text; public int Number; }
            [LuminPackable] public sealed partial class TextChild : Root
            {
                public string Text;
                public InnerRoot Nested;
                public int __luminSwitchCursor0;
            }
            [LuminPackable] public sealed class Bulk
            {
                public int Head;
                public List<Raw> Values;
                public Raw[] Array;
                public List<int> Numbers;
                public int Tail;
            }
            [LuminPackable] public sealed class Nested { public FixedChild Value; public int Tail; }
            [LuminPackable] public struct ManagedElement { public string Text; public Root Value; }
            [LuminPackable] public sealed class ListGraph
            {
                public List<Root> First;
                public List<Root> Second;
                public List<ManagedElement> Managed;
                public int Tail;
            }
            [LuminPackable, StructLayout(LayoutKind.Sequential)] public sealed class Contiguous
            {
                public long Head;
                public Raw First;
                public Raw Second;
                public long Tail;
            }
            [LuminPackable] public sealed class VolatileGroup
            {
                public volatile int Head;
                public Raw First;
                public Raw Second;
            }
            [LuminPackable] public struct ManagedNullableBody { public string Text; }
            public static class Entry
            {
                public static int ReadNarrowPair(ReadOnlySpan<byte> bytes, ref KeyValuePair<byte,string> value) => LuminPackSerializer.Deserialize(bytes,ref value);
                public static int ReadWidePair(ReadOnlySpan<byte> bytes, ref KeyValuePair<Guid,string> value) => LuminPackSerializer.Deserialize(bytes,ref value);
                public static byte[] WriteNullable(int? value) => LuminPackSerializer.Serialize(value);
                public static byte[] WriteNullableGuid(Guid? value) => LuminPackSerializer.Serialize(value);
                public static byte[] WriteManagedNullable(ManagedNullableBody? value) => LuminPackSerializer.Serialize(value);
                public static void Check()
                {
                    Raw first = default, second = default;
                    Unsafe.InitBlockUnaligned(ref Unsafe.As<Raw, byte>(ref first), 0xA5, 16);
                    Unsafe.InitBlockUnaligned(ref Unsafe.As<Raw, byte>(ref second), 0x5A, 16);
                    first.Tag = 3; first.Number = 123456789;
                    second.Tag = 7; second.Number = -987654321;
                    var rawBytes = LuminPackSerializer.Serialize(first);
                    Raw rawCopy = default;
                    LuminPackSerializer.Deserialize(new ReadOnlySequence<byte>(rawBytes), ref rawCopy);
                    var start = new Segment(rawBytes.AsMemory(0, 3));
                    var end = start.Append(rawBytes.AsMemory(3));
                    LuminPackSerializer.Deserialize(new ReadOnlySequence<byte>(start, 0, end, end.Memory.Length), ref rawCopy);
                    if (!LuminPackSerializer.Serialize(rawCopy).AsSpan().SequenceEqual(rawBytes))
                        throw new Exception("A staged unmanaged sequence changed storage padding.");
                    var large = new LargeRaw { Tail = 857 };
                    var largeBytes = LuminPackSerializer.Serialize(large);
                    start = new Segment(largeBytes.AsMemory(0, 3));
                    end = start.Append(largeBytes.AsMemory(3));
                    LargeRaw largeCopy = default;
                    LuminPackSerializer.Deserialize(new ReadOnlySequence<byte>(start, 0, end, end.Memory.Length), ref largeCopy);
                    if (largeCopy.Tail != 857 || !LuminPackSerializer.Serialize(largeCopy).AsSpan().SequenceEqual(largeBytes))
                        throw new Exception("A pooled unmanaged sequence changed raw storage.");
                    if (LuminPackSerializer.Deserialize<Narrow>(LuminPackSerializer.Serialize(Narrow.Value)) != Narrow.Value ||
                        LuminPackSerializer.Deserialize<int>(LuminPackSerializer.Serialize(997)) != 997)
                        throw new Exception("The architecture-compatible scalar/enum core changed values.");
                    Root value = new FixedChild { Head = 31, First = first, Second = second, Tail = -71 };
                    byte[] expected = new byte[45];
                    expected[0] = 11;
                    BitConverter.TryWriteBytes(expected.AsSpan(1), 31);
                    MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref first, 1)).CopyTo(expected.AsSpan(5));
                    MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref second, 1)).CopyTo(expected.AsSpan(21));
                    BitConverter.TryWriteBytes(expected.AsSpan(37), -71L);
                    if (!LuminPackSerializer.Serialize(value).AsSpan().SequenceEqual(expected))
                        throw new Exception("A direct field batch changed padding, framing, or field order.");
                    var buffer = LuminBufferWriterPool.Rent();
                    try
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            buffer.ResetForReuse();
                            LuminPackSerializer.Serialize(value, buffer);
                            if (!buffer.GetSpan().SequenceEqual(expected)) throw new Exception("Reusable Union output changed.");
                        }
                    }
                    finally { LuminBufferWriterPool.Return(buffer); }
                    Root copy = null;
                    if (LuminPackSerializer.Deserialize(expected, ref copy) != expected.Length ||
                        !LuminPackSerializer.Serialize(copy).AsSpan().SequenceEqual(expected))
                        throw new Exception("Padded repeated structs failed to round trip.");
                    var nested = new Nested { Value = (FixedChild)value, Tail = 41 };
                    if (!LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<Nested>(LuminPackSerializer.Serialize(nested))).AsSpan().SequenceEqual(LuminPackSerializer.Serialize(nested)))
                        throw new Exception("A nested header batch changed the payload.");
                    var lists = new ListGraph
                    {
                        First = new List<Root> { value, null, new TextChild
                        {
                            Text = "outer", Nested = new InnerSecond { Text = "inner", Number = 107 }, __luminSwitchCursor0 = 109
                        } },
                        Second = new List<Root> { null, value },
                        Managed = new List<ManagedElement>
                        {
                            new ManagedElement { Text = "managed", Value = value },
                            new ManagedElement { Text = null, Value = null }
                        },
                        Tail = 103
                    };
                    var listBytes = LuminPackSerializer.Serialize(lists);
                    var listCopy = LuminPackSerializer.Deserialize<ListGraph>(listBytes);
                    if (listCopy.First.Count != 3 || listCopy.First[1] != null || listCopy.Second[0] != null ||
                        listCopy.First[2] is not TextChild { Text: "outer", Nested: InnerSecond { Text: "inner", Number: 107 }, __luminSwitchCursor0: 109 } ||
                        listCopy.Managed[0].Text != "managed" || listCopy.Managed[1].Value != null || listCopy.Tail != 103 ||
                        !LuminPackSerializer.Serialize(listCopy).AsSpan().SequenceEqual(listBytes))
                        throw new Exception("Fresh reference or managed-value list elements changed.");
                    var existingList = new List<Root> { value, value, value };
                    var originalList = existingList;
                    var rootListBytes = LuminPackSerializer.Serialize(lists.First);
                    LuminPackSerializer.Deserialize(rootListBytes, ref existingList);
                    if (!ReferenceEquals(existingList, originalList) || existingList.Count != 3 || existingList[1] != null ||
                        !LuminPackSerializer.Serialize(existingList).AsSpan().SequenceEqual(rootListBytes))
                        throw new Exception("The fresh-list optimization changed reusable list identity or null elements.");
                    var adjacent = new Contiguous { Head = 97, First = first, Second = second, Tail = -101 };
                    var adjacentBytes = LuminPackSerializer.Serialize(adjacent);
                    byte[] adjacentExpected = new byte[49];
                    adjacentExpected[0] = 4;
                    BitConverter.TryWriteBytes(adjacentExpected.AsSpan(1), 97L);
                    MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref first, 1)).CopyTo(adjacentExpected.AsSpan(9));
                    MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref second, 1)).CopyTo(adjacentExpected.AsSpan(25));
                    BitConverter.TryWriteBytes(adjacentExpected.AsSpan(41), -101L);
                    if (!adjacentBytes.AsSpan().SequenceEqual(adjacentExpected))
                        throw new Exception("An adjacent raw copy included a gap or omitted a member's padding.");
                    var adjacentCopy = LuminPackSerializer.Deserialize<Contiguous>(adjacentBytes);
                    if (!LuminPackSerializer.Serialize(adjacentCopy).AsSpan().SequenceEqual(adjacentExpected))
                        throw new Exception("A typed field read changed unmanaged struct padding.");
                    var existing = new Contiguous { Head = -1, Tail = -2 };
                    var original = existing;
                    LuminPackSerializer.Deserialize(adjacentBytes, ref existing);
                    if (!ReferenceEquals(existing, original) || !LuminPackSerializer.Serialize(existing).AsSpan().SequenceEqual(adjacentExpected))
                        throw new Exception("A cursor optimization changed merge identity or field commit.");
                    var items = new Raw[3000];
                    for (int i = 0; i < items.Length; i++) items[i] = i % 2 == 0 ? first : second;
                    var bulk = new Bulk { Head = 53, Values = new List<Raw>(items), Array = items, Numbers = new List<int>(new int[20000]), Tail = 61 };
                    var bytes = LuminPackSerializer.Serialize(bulk);
                    if (bytes.Length != 1 + 4 + 4 + 48000 + 4 + 48000 + 4 + 80000 + 4)
                        throw new Exception("A large raw collection changed framing or padding.");
                    var restored = LuminPackSerializer.Deserialize<Bulk>(bytes);
                    var roundTrip = LuminPackSerializer.Serialize(restored);
                    if (restored.Head != 53 || restored.Tail != 61 || restored.Values.Count != 3000 || restored.Array.Length != 3000 ||
                        restored.Numbers.Count != 20000 || !roundTrip.AsSpan().SequenceEqual(bytes))
                    {
                        int different = 0;
                        while (different < Math.Min(bytes.Length, roundTrip.Length) && bytes[different] == roundTrip[different]) different++;
                        throw new Exception($"Raw collection mismatch: head={restored.Head}, tail={restored.Tail}, list={restored.Values.Count}, array={restored.Array.Length}, numbers={restored.Numbers.Count}, bytes={bytes.Length}/{roundTrip.Length}, first difference={different}.");
                    }
                    var repeated = new List<Bulk> { bulk, bulk };
                    if (LuminPackSerializer.Deserialize<List<Bulk>>(LuminPackSerializer.Serialize(repeated)).Count != 2)
                        throw new Exception("Deferred collection cursors lost their capacity reservation.");
                    bool rejected = false;
                    try { LuminPackSerializer.Deserialize<Bulk>(bytes.AsSpan(0, 32)); }
                    catch (Exception exception) when (exception.GetType().Name == "LuminPackException") { rejected = true; }
                    if (!rejected) throw new Exception("A truncated raw collection was copied beyond its input.");
                    int[] numbers = { 17, 23 };
                    if (LuminPackMarshal.GetArrayReference(numbers) != 17 ||
                        LuminPackMarshal.GetNotNullArrayReference(numbers) != 17 ||
                        !Unsafe.IsNullRef(ref LuminPackMarshal.GetArrayReference<int>((int[])null)))
                        throw new Exception("Single-dimensional array references changed.");
                    ref int empty = ref LuminPackMarshal.GetArrayReference(Array.Empty<int>());
                    if (MemoryMarshal.CreateSpan(ref empty, 0).Length != 0) throw new Exception("Empty array reference changed.");
                    object[] covariant = new string[] { "same" };
                    if (!ReferenceEquals(LuminPackMarshal.GetArrayReference(covariant), covariant[0]))
                        throw new Exception("Covariant array references changed.");
                    var multidimensional = Array.CreateInstance(typeof(int), new[] { 2, 3 }, new[] { 4, 7 });
                    multidimensional.SetValue(29, 4, 7);
                    if (Unsafe.As<byte, int>(ref LuminPackMarshal.GetArrayReference<int>(multidimensional)) != 29)
                        throw new Exception("Non-zero-based multidimensional array references changed.");
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET10_0_OR_GREATER", "NET11_0_OR_GREATER"]);
        var compilation = CSharpCompilation.Create("CodegenPerformance", [CSharpSyntaxTree.ParseText(source, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        VerifyFreshListTemplates(compilation);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        if (Environment.GetEnvironmentVariable("LUMINPACK_MECHANISM_CAPTURE") is { Length: > 0 } capture)
        {
            Directory.CreateDirectory(capture);
            foreach (var file in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources))
                File.WriteAllText(Path.Combine(capture, file.HintName), file.SourceText.ToString());
        }
        MethodDeclarationSyntax Root(string name, string payload) => methods.Single(method =>
            method.Identifier.ValueText == name && method.ParameterList.Parameters.Count == 2 &&
            method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::" + payload);
        var payload = Root("RootWriteUnion", "FixedChild");
        var fixedWriter = methods.Single(method => method.Identifier.ValueText == "SerializeUnionAt" &&
            method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::FixedChild");
        var fixedReader = Root("RootRead", "FixedChild");
        var unionReader = Root("RootReadUnion", "FixedChild");
        foreach (var reader in new[] { fixedReader, unionReader, Root("RootRead", "Contiguous") })
            VerifyFixedReadPrechecks(reader);
        Require(payload.ParameterList.Parameters[1].Modifiers.Count == 0, "A class Union payload still forces an input stack address.");
        Require(payload.Body!.ToString().Contains("SerializeUnionAt", StringComparison.Ordinal) &&
            fixedWriter.Body!.ToString().Contains("Unsafe.ByteOffset", StringComparison.Ordinal) &&
            fixedWriter.Body.ToString().Contains("LuminPackMarshal.CopyBlockUnaligned", StringComparison.Ordinal),
            "The proven adjacent-field copy is missing from its complete write operation.");
        Require(fixedReader.Body!.ToString().Contains("Unsafe.ByteOffset", StringComparison.Ordinal) &&
            fixedReader.Body.ToString().Contains(".Read(ref fixedStorage", StringComparison.Ordinal),
            "A constructed fixed object omitted its proven destination copy.");
        Require(!unionReader.Body!.ToString().Contains("fixedStorage", StringComparison.Ordinal),
            "A Union reader acquired an additional constructed-storage copy.");
        foreach (var entry in new[] { fixedWriter, fixedReader })
        {
            var architecture = entry.DescendantNodes().OfType<IfStatementSyntax>().Single(statement =>
                statement.Condition.ToString().Contains("Unsafe.SizeOf<global::System.IntPtr>() == 8", StringComparison.Ordinal) &&
                statement.Condition.ToString().Contains("Unsafe.ByteOffset", StringComparison.Ordinal));
            // Field-wise fallbacks may spell their reads inline, but every typed access keeps its own architecture gate;
            // a value of at most four bytes is allowed on 32-bit, matching the writer's scalar store.
            Require(architecture.Else!.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(call => call.Expression.ToString().EndsWith("Unaligned", StringComparison.Ordinal) ||
                        call.Expression.ToString().Contains("Unsafe.ReadUnaligned", StringComparison.Ordinal) ||
                        call.Expression.ToString().Contains("Unsafe.WriteUnaligned", StringComparison.Ordinal))
                    .Where(call => !call.Expression.ToString().Contains("CopyBlockUnaligned", StringComparison.Ordinal))
                    .All(call => call.Ancestors().TakeWhile(node => node != architecture.Else).OfType<IfStatementSyntax>().Any(gate =>
                        gate.Condition.ToString().StartsWith("Unsafe.SizeOf<global::System.IntPtr>() == 8", StringComparison.Ordinal) &&
                        gate.Statement.Contains(call))),
                "Fixed copies lost their address proof or bypassed the scalar architecture core.");
        }
        foreach (var entry in methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" } &&
            method.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeUnion" or "DeserializeUnion" &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[0].Type!.ToString().Contains("LuminPack", StringComparison.Ordinal)))
            Require(entry.Body?.Statements.Count == 1 || entry.ExpressionBody != null,
                "A public state entry duplicates its execution body instead of forwarding to its root.");
        foreach (string name in new[] { "RootWrite", "RootRead" })
        {
            var entry = Root(name, "Contiguous");
            Require(!entry.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                call.Expression.ToString().Contains("GetCurrentSpanOffset", StringComparison.Ordinal) ||
                call.Expression.ToString().Contains("FlushCurrentIndex", StringComparison.Ordinal)),
                "A fixed object caller still maintains a second cursor.");
        }
        foreach (string type in new[] { "int?", "global::System.Guid?" })
        {
            var entry = methods.Single(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" } &&
                method.Identifier.ValueText == "Serialize" && method.TypeParameterList?.Parameters.Count == 1 &&
                method.ParameterList.Parameters.Count == 1 && method.ParameterList.Parameters[0].Type!.ToString() == type);
            Require(entry.Body!.ToString().Contains("value.HasValue ? 1 + Unsafe.SizeOf<", StringComparison.Ordinal) &&
                !entry.Body.ToString().Contains("directEvaluator", StringComparison.Ordinal) &&
                !entry.Body.ToString().Contains("LuminBufferWriterPool", StringComparison.Ordinal) &&
                entry.Body.ToString().Contains("ref writer, in value", StringComparison.Ordinal),
                "A fixed Nullable scalar retained redundant evaluation/pool fallback or bypassed its existing root.");
        }
        var managedNullable = methods.Single(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" } &&
            method.Identifier.ValueText == "Serialize" && method.TypeParameterList?.Parameters.Count == 1 &&
            method.ParameterList.Parameters.Count == 1 && method.ParameterList.Parameters[0].Type!.ToString() == "global::ManagedNullableBody?");
        Require(managedNullable.Body!.ToString().Contains("LuminBufferWriterPool.Rent", StringComparison.Ordinal),
            "A state-dependent Nullable acquired a raw scalar convenience entry.");
        Require(!new[] { payload, fixedWriter }.SelectMany(method => method.DescendantNodes().OfType<InvocationExpressionSyntax>()).Any(call =>
            call.Expression.ToString().Contains("EnsureReadable", StringComparison.Ordinal) ||
            call.Expression.ToString().Contains("CheckBuffer", StringComparison.Ordinal)),
            "A fixed field group acquired a per-value buffer bounds check.");
        foreach (var reservation in fixedWriter.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => call.Expression.ToString().Contains("EnsureAdditionalCapacity", StringComparison.Ordinal)))
            Require(reservation.Ancestors().OfType<IfStatementSyntax>().Any(gate =>
                gate.Condition is BinaryExpressionSyntax condition && condition.IsKind(SyntaxKind.GreaterThanExpression) &&
                condition.Right.ToString() == "4096" && condition.Left.ToString().Contains("Unsafe.SizeOf", StringComparison.Ordinal) &&
                gate.Statement is BlockSyntax block && block.Statements[0].ToString().StartsWith("writer.Position =", StringComparison.Ordinal)),
                "A fixed Union capacity reservation lost its size guard or cursor publication.");
        foreach (string name in new[] { "RootWrite", "RootRead" })
            Require(!Root(name, "VolatileGroup").DescendantNodes().OfType<VariableDeclaratorSyntax>().Any(variable =>
                    variable.Identifier.ValueText == "fixedStorage" && variable.Initializer!.ToString().Contains(".Head", StringComparison.Ordinal)),
                "A grouped memory copy bypassed a volatile field access.");
        var listReader = Root("RootRead", "ListGraph");
        Require(!listReader.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.Name.Identifier.ValueText == "IsValueType"),
            "A known list field retained an unreachable type-dispatch graph.");
        Require(listReader.Body!.ToString().Contains(".Read<", StringComparison.Ordinal),
            "A root collection field does not use its complete closed extension.");
        var sequences = methods.Where(method => method.TypeParameterList?.Parameters.Count == 1 && method.Identifier.ValueText == "Deserialize" &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type!.ToString().Contains("ReadOnlySequence", StringComparison.Ordinal)) &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type!.ToString().TrimEnd('?') is "global::Raw" or "global::LargeRaw")).ToArray();
        Require(sequences.Length >= 2, "The architecture regression lost its concrete unmanaged sequence entries.");
        foreach (var sequence in sequences)
        {
            // The staged value read adds the writer's at-most-four-byte allowance to its architecture gate; the
            // wider fixture types still keep the 32-bit block-copy fallback.
            var branches = sequence.DescendantNodes().OfType<IfStatementSyntax>().Where(statement =>
                statement.Condition.ToString().StartsWith("global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8",
                    StringComparison.Ordinal)).ToArray();
            Require(branches.Length == 3 && branches.All(branch => branch.Else!.Statement.ToString().Contains("LuminPackMarshal.CopyBlockUnaligned", StringComparison.Ordinal) &&
                !branch.Else.Statement.ToString().Contains("Unsafe.ReadUnaligned", StringComparison.Ordinal)),
                "A direct or staged sequence bypasses the 32-bit block-copy path.");
        }
        var dispatch = methods.Single(method => method.TypeParameterList == null && !method.Identifier.ValueText.Contains("_Options_", StringComparison.Ordinal) &&
            method.Identifier.ValueText.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal) &&
            method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "FixedChild" });
        Require(dispatch.Modifiers.Any(SyntaxKind.OverrideKeyword) && dispatch.Body!.ToString().Contains("writer.CurrentIndex =", StringComparison.Ordinal) &&
            dispatch.Body.ToString().Contains("LuminPackSerializer.SerializeUnionAt(ref writer, this, writer.CurrentIndex, (byte)11)", StringComparison.Ordinal),
            "The original fixed Union virtual slot lost its tag, shared payload or final cursor publication.");
        VerifyPairPreaccess(methods);
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
        var originalTree = compilation.SyntaxTrees.Single();
        var edited = compilation.ReplaceSyntaxTree(originalTree, originalTree.WithChangedText(
            Microsoft.CodeAnalysis.Text.SourceText.From(source.Replace("public volatile int Head;", "public int Head;", StringComparison.Ordinal))));
        var retained = driver.RunGenerators(edited).GetRunResult();
        var fresh = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGenerators(edited).GetRunResult();
        Require(retained.Results.SelectMany(result => result.GeneratedSources).Select(result => result.HintName + result.SourceText).Order(StringComparer.Ordinal)
            .SequenceEqual(fresh.Results.SelectMany(result => result.GeneratedSources).Select(result => result.HintName + result.SourceText).Order(StringComparer.Ordinal)),
            "A volatile modifier edit reused stale field-copy eligibility.");
        Console.WriteLine("Passed direct unmanaged batches, repeated padded structs, class Union register inputs, 32-bit fallback structure, and array reference semantics.");
    }

    /// <summary>Checks that both pair scalar projections validate before any typed or block-copy access.</summary>
    /// <param name="methods">The generated complete fixture methods.</param>
    private static void VerifyPairPreaccess(IEnumerable<MethodDeclarationSyntax> methods)
    {
        var pairs = methods.Where(method => method.Identifier.ValueText == "RootRead" && method.ParameterList.Parameters.Count == 2 &&
            method.ParameterList.Parameters[1].Type!.ToString().Contains("KeyValuePair<", StringComparison.Ordinal)).ToArray();
        Require(pairs.Length >= 2, "The pair preaccess fixture lost its scalar roots.");
        int checkedLoads = 0;
        foreach (var method in pairs)
        foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
                     call.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText is "ReadUnaligned" or "CopyBlockUnaligned"))
        {
            var statement = call.Ancestors().OfType<StatementSyntax>().First();
            var block = statement.Parent as BlockSyntax;
            if (block == null || !block.Statements.Any(item => item.ToString().Contains("EnsureReadable", StringComparison.Ordinal)))
            {
                statement = statement.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault() ?? statement;
                block = statement.Parent as BlockSyntax;
            }
            Require(block != null && block.Statements.TakeWhile(item => !ReferenceEquals(item, statement))
                .Any(item => item.ToString().Contains("EnsureReadable", StringComparison.Ordinal)),
                "A pair scalar projection accesses bytes before its preaccess range check: " + call);
            checkedLoads++;
        }
        Require(checkedLoads >= 3, "The pair preaccess fixture did not cover narrow and architecture-gated wide loads.");
    }

    /// <summary>Checks target feature gates and preserves both storage paths for unconstrained generic elements.</summary>
    /// <param name="compilation">The fixture compilation supplying list element symbols.</param>
    private static void VerifyFreshListTemplates(CSharpCompilation compilation)
    {
        VerifyDictionaryHeaderTemplates(compilation);
        foreach (string memberName in new[] { "First", "Managed" })
        {
            var field = (IFieldSymbol)compilation.GetTypeByMetadataName("ListGraph")!.GetMembers(memberName).Single();
            var metadata = new LuminPack.Code.LuminLocalFieldData
            {
                TypeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), TypeSymbol = field.Type
            };
            var legacy = new System.Text.StringBuilder();
            LuminPack.SourceGenerator.CodeEmitters.ListEmitter.GenerateFreshDeserializeCode(metadata, legacy, useArrayDataIntrinsic: false);
            Require(!legacy.ToString().Contains("MemoryMarshal.GetArrayDataReference", StringComparison.Ordinal) &&
                !legacy.ToString().Contains(".IsValueType", StringComparison.Ordinal),
                "A known legacy list element retained an unreachable path or an unavailable intrinsic.");
        }
        var open = new LuminPack.Code.LuminLocalFieldData
        {
            TypeName = "global::System.Collections.Generic.List<T>",
            TypeSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")
        };
        var generic = new System.Text.StringBuilder();
        LuminPack.SourceGenerator.CodeEmitters.ListEmitter.GenerateFreshDeserializeCode(open, generic, useArrayDataIntrinsic: true);
        Require(!generic.ToString().Contains("RuntimeHelpers.IsReferenceOrContainsReferences<T>()", StringComparison.Ordinal) &&
            !generic.ToString().Contains("reader.Read<T>(span)", StringComparison.Ordinal) &&
            generic.ToString().Contains("reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first", StringComparison.Ordinal),
            "An open generic list bypassed its possible Custom formatter contract: " + generic);
    }

    /// <summary>Restricts cached dictionary string widths to element operations that cannot replace writer state.</summary>
    /// <param name="compilation">Runtime references and existing managed model symbols.</param>
    private static void VerifyDictionaryHeaderTemplates(CSharpCompilation compilation)
    {
        var symbols = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            [LuminPackable(GeneratorType.Custom)] public struct HeaderCustom { public int Value; }
            [LuminPackable(GeneratorType.VersionTolerant)] public struct HeaderVersioned { [LuminPackOrder(2)] public int Value; }
            public class HeaderShapes
            {
                public Dictionary<string, long> Stable;
                public Dictionary<string, string> Both;
                public Dictionary<string, FixedChild> Managed;
                public Dictionary<string, HeaderCustom> Custom;
                public Dictionary<LargeRaw, FixedChild> RawKey;
                public Dictionary<FixedChild, LargeRaw> RawValue;
                public Dictionary<HeaderCustom, string> CustomKey;
                public Dictionary<HeaderVersioned, string> VersionedKey;
                public Dictionary<string, HeaderVersioned> VersionedValue;
            }
            """, (CSharpParseOptions)compilation.SyntaxTrees.First().Options));
        var errors = symbols.GetSemanticModel(symbols.SyntaxTrees.Last()).GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(diagnostic => diagnostic.ToString())));
        foreach (string memberName in new[] { "Stable", "Both", "Managed", "Custom", "RawKey", "RawValue", "CustomKey", "VersionedKey", "VersionedValue" })
        {
            var field = (IFieldSymbol)symbols.GetTypeByMetadataName("HeaderShapes")!.GetMembers(memberName).Single();
            var metadata = new LuminPack.Code.LuminLocalFieldData
            {
                TypeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), TypeSymbol = field.Type
            };
            var text = new System.Text.StringBuilder();
            LuminPack.SourceGenerator.CodeEmitters.DictionaryEmitter.GenerateSerializeCode(metadata, text);
            Require(text.ToString().Contains("int stringRecordLength = writer.StringRecordLength();", StringComparison.Ordinal) ==
                (memberName is "Stable" or "Both"), "Dictionary string headers escaped their state-stability proof for " + memberName + ".");
            string generated = text.ToString();
            Require(generated.Contains("byte>(ref entry.Key)", StringComparison.Ordinal) == (memberName is "RawKey") &&
                generated.Contains("byte>(ref entry.Value)", StringComparison.Ordinal) == (memberName is "Stable" or "RawValue"),
                "Independent raw entry copying bypassed a formatter contract or required both raw members for " + memberName + ".");
        }
    }

    /// <summary>Preserves ref reassignment, name observations and nested bindings while removing generated cursor aliases.</summary>
    private static void VerifyCursorAliasStorage()
    {
        var aliasType = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.BinaryEntryCursorAliases", throwOnError: true)!;
        foreach (string state in new[] { "writer", "reader" })
        {
            var rewrite = aliasType.GetMethod(state == "writer" ? "Rewrite" : "RewriteReader", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (var (text, remove, marked) in new[]
            {
                ("{ ref var __lpCursor = ref writer.GetCurrentSpanOffset(); writer.WriteCollectionHeader(ref __lpCursor, 2); __lpCursor += 4; }", true, true),
                ("{ ref int __lpCursor = ref writer.GetCurrentSpanOffset(); __lpCursor += 4; }", true, true),
                ("{ ref int __lpCursor = ref writer.GetCurrentSpanOffset(); __lpCursor = ref other; __lpCursor += 4; }", false, true),
                ("{ ref int __lpCursor = ref writer.GetCurrentSpanOffset(); var name = nameof(__lpCursor); __lpCursor += 4; }", false, true),
                ("{ { ref int __lpCursor = ref writer.GetCurrentSpanOffset(); __lpCursor += 4; } { ref int __lpCursor = ref other; __lpCursor += 8; } }", false, true),
                ("{ ref int __lpCursor = ref writer.GetCurrentSpanOffset(); writer = ref other; __lpCursor += 4; }", false, true),
                ("{ ref int __lpCursor = ref writer.GetCurrentSpanOffset(); __lpCursor += 4; }", false, false)
            })
            {
                var block = (BlockSyntax)SyntaxFactory.ParseStatement(text.Replace("writer", state, StringComparison.Ordinal));
                if (marked)
                    block = block.ReplaceTokens(block.DescendantTokens().Where(token => token.ValueText == "__lpCursor"),
                        (token, _) => token.WithAdditionalAnnotations(new SyntaxAnnotation("LuminPack.ExpandedIdentity")));
                var result = (BlockSyntax)rewrite.Invoke(null, [block])!;
                Require(result.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Any(statement => statement.Declaration.Type is RefTypeSyntax) != remove,
                    "Cursor alias rewriting changed a name-sensitive, rebound or unmarked storage reference.");
                if (remove)
                    Require(result.DescendantNodes().OfType<IdentifierNameSyntax>().All(identifier => identifier.Identifier.ValueText != "__lpCursor"),
                        "A removed cursor alias retained an unbound use.");
                else
                    Require(result.ToFullString() == block.ToFullString(), "A guarded cursor body changed its original bindings.");
            }
        }
    }

    /// <summary>Preserves the approved all-build Reader check when cursor advances are lowered for either target.</summary>
    private static void VerifyCheckedReaderAdvances()
    {
        var projection = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.BinaryCursorProjection", throwOnError: true)!;
        foreach (bool pointer in new[] { false, true })
        foreach (bool allowUnsafe in new[] { false, true })
        {
            var rewrite = (CSharpSyntaxRewriter)Activator.CreateInstance(projection,
                BindingFlags.Instance | BindingFlags.NonPublic, binder: null, [pointer, allowUnsafe], culture: null)!;
            var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
                "static void Read(ref LuminPackReader reader) { reader.Advance(4); reader.Advance(GetCount()); reader.Advance(source.Length); }")!;
            var result = (MethodDeclarationSyntax)rewrite.Visit(method)!;
            var calls = result.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
            if (pointer && !allowUnsafe)
            {
                Require(calls.Count(call => call.Expression.ToString() == "reader.Advance") == 3,
                    "A pointer-disabled Reader lost its checked Advance helper.");
                continue;
            }
            var block = result.Body!.Statements[0] as BlockSyntax;
            Require(block?.Statements.Count == 2 && block.Statements[0].ToString() == "reader.EnsureReadable(4);" &&
                block.Statements[1].ToString().Contains("reader.Cursor", StringComparison.Ordinal),
                "A lowered Reader cursor advance bypassed its all-build range check.");
            Require(calls.Count(call => call.Expression.ToString() == "reader.Advance") == 2,
                "A side-effecting or property-based advance width no longer uses its original checked helper.");
        }
    }

    /// <summary>Retains full native offsets and signed backwards addressing under checked consumer compilation.</summary>
    /// <param name="references">The runtime and compiler platform references.</param>
    private static void VerifyNativeOffsets(MetadataReference[] references)
    {
        var projection = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.NativeOffsetProjection", throwOnError: true)!;
        var rewrite = (CSharpSyntaxRewriter)Activator.CreateInstance(projection, nonPublic: true)!;
        const string source = """
            using System;
            using System.Runtime.CompilerServices;
            public static class OffsetProbe
            {
                public static nint Integer(ref byte origin, int index) => Unsafe.ByteOffset(ref origin, ref Unsafe.Add(ref origin, index));
                public static nint Native(ref byte origin, nint index) => Unsafe.ByteOffset(ref origin, ref Unsafe.Add(ref origin, index));
                public static nint Product(ref byte origin, nint index) => Unsafe.ByteOffset(ref origin, ref Unsafe.Add(ref origin, index * 2));
                public static nint Local(ref byte origin) { nint index = (nint)1 << 33; return Unsafe.ByteOffset(ref origin, ref Unsafe.Add(ref origin, index)); }
                public static nint Backwards(ref byte origin, int distance) => Unsafe.ByteOffset(ref origin, ref Unsafe.Add(ref origin, -distance));
                public static nint Constant(ref byte origin) => Unsafe.ByteOffset(ref origin, ref Unsafe.Add(ref origin, 4));
                public static void Verify()
                {
                    byte origin = 0;
                    if (Integer(ref origin, 37) != 37 || Backwards(ref origin, 3) != -3 || Constant(ref origin) != 4) throw new Exception("Signed or integer offset changed.");
                    if (IntPtr.Size == 8)
                    {
                        nint wide = (nint)1 << 33;
                        if (Native(ref origin, wide) != wide || Product(ref origin, wide) != wide * 2 || Local(ref origin) != wide) throw new Exception("Native offset was truncated.");
                    }
                }
            }
            """;
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var result = rewrite.Visit(root)!;
        Require(rewrite.Visit(result)!.ToFullString() == result.ToFullString(), "Native offset projection is not idempotent.");
        foreach (bool overflow in new[] { false, true })
        {
            var compilation = CSharpCompilation.Create("OffsetProbe_" + Guid.NewGuid().ToString("N"), [CSharpSyntaxTree.Create((CSharpSyntaxNode)result)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, checkOverflow: overflow));
            using var image = new MemoryStream();
            var emitted = compilation.Emit(image);
            Require(emitted.Success, "Native offset probe failed: " + string.Join(Environment.NewLine, emitted.Diagnostics));
            var assembly = Assembly.Load(image.ToArray());
            assembly.GetType("OffsetProbe")!.GetMethod("Verify")!.Invoke(null, null);
        }
    }

    /// <summary>Rejects direct fixed cursor reads that validate only after assigning their destination fields.</summary>
    /// <param name="method">The generated fixed-field or Union read root.</param>
    private static void VerifyFixedReadPrechecks(MethodDeclarationSyntax method)
    {
        var cursors = method.DescendantNodes().OfType<LocalDeclarationStatementSyntax>()
            .Where(statement => statement.Declaration.Variables.Any(variable => variable.Identifier.ValueText.Contains("fixedCursor", StringComparison.Ordinal))).ToArray();
        Require(cursors.Length != 0, "The fixed read regression lost its field-wise fallback.");
        foreach (var cursor in cursors)
        {
            var block = cursor.Parent as BlockSyntax;
            int index = block?.Statements.IndexOf(cursor) ?? -1;
            // Either the range is checked before the cursor is captured, or the capture only takes the position and the
            // next statement is the checked Advance (or its lowered EnsureReadable block) that precedes every load.
            bool checkedBefore = index > 0 && IsEnsureReadable(block!.Statements[index - 1]);
            bool checkedAfterCapture = index >= 0 && index + 1 < block!.Statements.Count &&
                cursor.Declaration.Variables.All(variable => variable.Initializer?.Value is RefExpressionSyntax
                {
                    Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetCurrentSpanReference" } }
                } or RefExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Cursor" } }) &&
                IsCheckedAdvance(block.Statements[index + 1]);
            Require(checkedBefore || checkedAfterCapture,
                "A generated fixed field/batch can read or change storage before its all-build range check.");
        }

        static bool IsEnsureReadable(StatementSyntax statement) => statement is ExpressionStatementSyntax
            { Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "EnsureReadable" } } };

        static bool IsCheckedAdvance(StatementSyntax statement) => statement is ExpressionStatementSyntax
            { Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax, Name.Identifier.ValueText: "Advance" } } } ||
            statement is BlockSyntax { Statements.Count: > 0 } lowered && IsEnsureReadable(lowered.Statements[0]);
    }

    /// <summary>Reports a code-generation or binary contract regression.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
