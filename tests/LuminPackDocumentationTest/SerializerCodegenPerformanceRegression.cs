using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects wire layout and dispatch while optimizing generated unmanaged field writes.</summary>
internal static class SerializerCodegenPerformanceRegression
{
    /// <summary>Checks repeated padded structs, Union input passing, and array storage references.</summary>
    /// <param name="references">Runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        VerifyCursorAliasStorage();
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
            public static class Entry
            {
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
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        var payload = methods.Single(method => method.Identifier.ValueText == "SerializeUnionAt" &&
            method.ParameterList.Parameters.Count == 4 && method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::FixedChild");
        var originalPayload = methods.Single(method => method.Identifier.ValueText == "SerializeUnion" &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::FixedChild");
        Require(originalPayload.Body!.Statements.Count == 1 && originalPayload.Body.ToString().Contains("SerializeUnionAt", StringComparison.Ordinal),
            "The original entry duplicated its fixed field implementation instead of publishing the shared result.");
        Require(payload.ParameterList.Parameters[1].Modifiers.Count == 0, "A class Union payload still forces an input stack address.");
        Require(payload.Body!.ToString().Contains("global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8", StringComparison.Ordinal) &&
            payload.Body.ToString().Contains("writer.WriteUnmanaged", StringComparison.Ordinal), "The 32-bit packed-view fallback was removed.");
        Require(payload.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(call => call.Expression.ToString().Contains("Unsafe.WriteUnaligned", StringComparison.Ordinal)) == 4,
            "Unmanaged structs no longer use independent direct field writes.");
        Require(payload.Body.ToString().Contains("Unsafe.ByteOffset", StringComparison.Ordinal) &&
            payload.Body.ToString().Contains("Unsafe.CopyBlockUnaligned", StringComparison.Ordinal), "The proven 64-bit adjacent-field bulk copy was removed.");
        var readPayload = methods.Single(method => method.Identifier.ValueText == "DeserializeUnion" &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::FixedChild");
        Require(readPayload.Body!.ToString().Contains("reader.ReadUnmanaged", StringComparison.Ordinal), "A fixed struct reader bypassed the shared architecture-compatible core.");
        Require(!readPayload.Body.ToString().Contains("Unsafe.CopyBlockUnaligned", StringComparison.Ordinal),
            "A Union payload acquired the additional raw destination copy instead of its established grouped read.");
        var fixedReader = methods.Single(method => method.Identifier.ValueText == "Deserialize" &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[0].Type!.ToString().Contains("LuminPackReader", StringComparison.Ordinal) &&
            method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::FixedChild");
        Require(fixedReader.Body!.ToString().Contains("Unsafe.ByteOffset", StringComparison.Ordinal) &&
            fixedReader.Body.ToString().Contains("Unsafe.CopyBlockUnaligned", StringComparison.Ordinal),
            "A raw field reader omitted the proven contiguous destination copy.");
        var listReader = methods.Single(method => method.Identifier.ValueText == "Deserialize" &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[0].Type!.ToString().Contains("LuminPackReader", StringComparison.Ordinal) &&
            method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::ListGraph");
        Require(!listReader.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(member => member.Name.Identifier.ValueText == "IsValueType"),
            "A known list element type expanded a second unreachable formatter graph.");
        Require(listReader.Body!.ToString().Contains("MemoryMarshal.GetArrayDataReference", StringComparison.Ordinal),
            "Fresh reference lists did not retain the target-supported intrinsic storage access.");
        Require(listReader.DescendantNodes().OfType<VariableDeclaratorSyntax>().Any(variable => variable.Identifier.ValueText.StartsWith("__luminSwitchCursor", StringComparison.Ordinal)) &&
            listReader.Body.ToString().Contains(".__luminSwitchCursor0", StringComparison.Ordinal),
            "Exclusive cursor storage was omitted or renamed a matching user field.");
        foreach (var entry in new[] { payload, fixedReader })
        {
            var architecture = entry.DescendantNodes().OfType<IfStatementSyntax>().Single(statement =>
                statement.Condition.ToString().StartsWith("global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8", StringComparison.Ordinal));
            Require(!architecture.Else!.Statement.ToString().Contains("Unsafe.WriteUnaligned", StringComparison.Ordinal) &&
                !architecture.Else.Statement.ToString().Contains("Unsafe.ReadUnaligned", StringComparison.Ordinal),
                "Typed fixed-value unsafe access escaped into the 32-bit IL2CPP path.");
        }
        var sequences = methods.Where(method => method.Identifier.ValueText == "Deserialize" &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type!.ToString().Contains("ReadOnlySequence", StringComparison.Ordinal)) &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type!.ToString().TrimEnd('?') is "global::Raw" or "global::LargeRaw")).ToArray();
        Require(sequences.Length >= 2, "The architecture regression did not retain concrete unmanaged sequence entries.");
        foreach (var sequence in sequences)
        {
            var branches = sequence.DescendantNodes().OfType<IfStatementSyntax>().Where(statement =>
                statement.Condition.ToString() == "global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8").ToArray();
            Require(branches.Length == 3 && branches.All(branch => branch.Else!.Statement.ToString().Contains("Unsafe.CopyBlockUnaligned", StringComparison.Ordinal) &&
                !branch.Else.Statement.ToString().Contains("Unsafe.ReadUnaligned", StringComparison.Ordinal)),
                "A direct, stack-staged, or pool-staged sequence bypasses the 32-bit block-copy path.");
        }
        foreach (string operation in new[] { "Serialize", "Deserialize" })
        {
            var entry = methods.Single(method => method.Identifier.ValueText == operation && method.ParameterList.Parameters.Count == 2 &&
                method.ParameterList.Parameters[0].Type!.ToString().Contains(operation == "Serialize" ? "LuminPackWriter" : "LuminPackReader", StringComparison.Ordinal) &&
                method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::Contiguous");
            Require(entry.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(call => call.Expression.ToString().EndsWith(".GetCurrentSpanIndex", StringComparison.Ordinal)) == 1,
                "A fixed object entry reloads its cursor between fields.");
        }
        Require(!payload.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
            call.Expression.ToString().Contains("EnsureAdditionalCapacity", StringComparison.Ordinal) ||
            call.Expression.ToString().Contains("EnsureReadable", StringComparison.Ordinal) ||
            call.Expression.ToString().Contains("CheckBuffer", StringComparison.Ordinal)),
            "A fixed-length field batch acquired a per-value buffer bounds check.");
        var volatileWriter = methods.Single(method => method.Identifier.ValueText == "Serialize" &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[0].Type!.ToString().Contains("LuminPackWriter", StringComparison.Ordinal) &&
            method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::VolatileGroup");
        Require(!volatileWriter.Body!.ToString().Contains("Unsafe.CopyBlockUnaligned", StringComparison.Ordinal), "A grouped memory copy bypassed a volatile field load.");
        var volatileReader = methods.Single(method => method.Identifier.ValueText == "Deserialize" &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[0].Type!.ToString().Contains("LuminPackReader", StringComparison.Ordinal) &&
            method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::VolatileGroup");
        Require(!volatileReader.Body!.ToString().Contains("Unsafe.CopyBlockUnaligned", StringComparison.Ordinal), "A grouped memory copy bypassed a volatile field store.");
        var dispatch = methods.Single(method => method.Identifier.ValueText.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal) &&
            method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "FixedChild" });
        Require(dispatch.Modifiers.Any(SyntaxKind.OverrideKeyword) && dispatch.Body!.ToString().Contains("WriteUnionHeader(11)", StringComparison.Ordinal) &&
            dispatch.Body.ToString().Contains("LuminPackSerializer.SerializeUnion", StringComparison.Ordinal), "The Union virtual/tag/payload mechanism changed.");
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
        var originalTree = compilation.SyntaxTrees.Single();
        var edited = compilation.ReplaceSyntaxTree(originalTree, originalTree.WithChangedText(
            Microsoft.CodeAnalysis.Text.SourceText.From(source.Replace("public volatile int Head;", "public int Head;", StringComparison.Ordinal))));
        var retained = driver.RunGenerators(edited).GetRunResult();
        var fresh = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGenerators(edited).GetRunResult();
        Require(retained.Results.SelectMany(result => result.GeneratedSources).Select(result => result.HintName + result.SourceText).Order(StringComparer.Ordinal)
            .SequenceEqual(fresh.Results.SelectMany(result => result.GeneratedSources).Select(result => result.HintName + result.SourceText).Order(StringComparer.Ordinal)),
            "A volatile modifier edit reused stale field-copy eligibility.");
        Console.WriteLine("Passed direct unmanaged batches, repeated padded structs, class Union register inputs, 32-bit fallback structure, and array reference semantics.");
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
        Require(generic.ToString().Contains("typeof(T).IsValueType", StringComparison.Ordinal) &&
            generic.ToString().Contains("reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first", StringComparison.Ordinal),
            "An undetermined generic list lost its reference/value storage dispatch.");
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

    /// <summary>Reports a code-generation or binary contract regression.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
