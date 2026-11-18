extern alias ModelBasedAnalyzer;

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelBasedGenerator = ModelBasedAnalyzer::LuminPack.SourceGenerator.LuminPackSourceGenerator;

/// <summary>Checks that binary entries shortcut only the storage and operation state their formatter graph permits.</summary>
internal static class SerializerBinaryEntryShapeRegression
{
    /// <summary>Compiles a Next consumer and checks the root routes and their wire behavior.</summary>
    /// <param name="references">The framework and runtime metadata.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            using LuminPack.Utility;

            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
            public struct NativeCell { public long X; public int Y; }

            [LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
            public struct PackedCell { public byte X; public long Y; }

            [LuminPackable] public sealed class PlainGraph { public List<string> Text; public PackedCell? Cell; }
            [LuminPackable] public sealed class ReadBoundaryCell { public int Number; }
            [LuminPackable] public sealed class TextReadRoot { public string Text; }
            [LuminPackable] public sealed class WideReadRecord
            {
                public int A; public int B; public int C; public int D;
                public int E; public int F; public int G; public int H;
            }
            [LuminPackable, LuminPackUnion(1, typeof(ReadCase))] public abstract partial class ReadUnion { }
            [LuminPackable] public sealed partial class ReadCase : ReadUnion { public int Number; }
            [LuminPackable] public sealed class UnionReadGraph { public ReadUnion Value; }
            [LuminPackable(GeneratorType.VersionTolerant)] public sealed class VersionCell { public int Value; }
            [LuminPackable] public sealed class VersionGraph { public List<VersionCell> Cells; }
            public sealed class ObservedCulture : global::System.Globalization.CultureInfo
            {
                public int Observations;
                public ObservedCulture() : base("en-US") { }
                public override string Name { get { Observations++; return base.Name; } }
            }
            [LuminPackable] public sealed class CapturedRecord
            {
                public int Id;
                public string Name;
                public List<int> Items;
                public uint[] Values;
            }

            [LuminPackable(GeneratorType.Custom)] public struct CustomCell
            {
                public int Value;
                public static void Serialize(ref LuminPackWriter writer, in CustomCell value) => writer.Write(value.Value);
                public static void Deserialize(ref LuminPackReader reader, ref CustomCell value)
                {
                    if (reader.Options == null) throw new Exception("Custom read lost its explicit operation state.");
                    value.Value = reader.Read<int>();
                }
            }
            [LuminPackable] public sealed class CustomGraph { public CustomCell Cell; }

            public static class Cases
            {
                public static int ReadList(ReadOnlySpan<byte> data, ref IList<int> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int ReadCollection(ReadOnlySpan<byte> data, ref ICollection<int> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int ReadOnlyList(ReadOnlySpan<byte> data, ref IReadOnlyList<int> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int ReadRecords(ReadOnlySpan<byte> data, ref List<ReadBoundaryCell> value) => LuminPackSerializer.Deserialize(data, ref value);
                public static int ReadCompact(ReadOnlySpan<byte> data, ref List<int> value) => LuminPackSerializer.Deserialize(data, ref value);

                private static void CheckCapturedRecord()
                {
                    var record = new CapturedRecord { Id = 31, Name = "wire", Items = new List<int> { 2, -7, 19 }, Values = new uint[] { 13, 17 } };
                    Span<byte> scratch = new byte[256];
                    var sequential = new LuminPackWriter(ref scratch);
                    sequential.WriteObjectHeader(4);
                    sequential.Write(record.Id);
                    sequential.Write(record.Name);
                    sequential.Write<int>(record.Items.ToArray(), record.Items.Count);
                    sequential.Write<uint>(record.Values, record.Values.Length);
                    var bytes = LuminPackSerializer.Serialize(record);
                    if (!bytes.AsSpan().SequenceEqual(sequential.GetSpan())) throw new Exception("Captured fields changed binary framing.");

                    using var buffer = new LuminBufferWriter(true);
                    ((global::System.Buffers.IBufferWriter<byte>)buffer).GetSpan(1)[0] = 0xA5;
                    buffer.Advance(1);
                    LuminPackSerializer.Serialize(record, buffer);
                    if (buffer.GetSpan()[0] != 0xA5 || !buffer.GetSpan().Slice(1).SequenceEqual(bytes))
                        throw new Exception("Captured fields changed the buffer's appended record.");

                    var retained = new List<int>(record.Items.Count);
                    var reused = new CapturedRecord { Items = retained };
                    if (LuminPackSerializer.Deserialize(bytes, ref reused) != bytes.Length || !ReferenceEquals(retained, reused.Items) ||
                        reused.Items[1] != -7 || reused.Values[1] != 17)
                        throw new Exception("Captured fields lost the normal scalar collection read or reuse contract.");
                    var json = LuminPackSerializer.SerializeJson(record);
                    var jsonCopy = LuminPackSerializer.DeserializeJson<CapturedRecord>(json);
                    if (jsonCopy.Items[2] != 19 || jsonCopy.Values[0] != 13)
                        throw new Exception("Captured fields lost their published JSON formatters.");
                }

                public static void Run()
                {
                    CheckCapturedRecord();
                    using var buffer = new LuminBufferWriter(true);
                    CheckTextRoots(buffer);
                    CheckFixedArray(buffer);
                    var raw = new NativeCell { X = -321, Y = 412 };
                    var rawBytes = LuminPackSerializer.Serialize(raw);
                    LuminPackSerializer.Serialize(raw, buffer);
                    if (!buffer.GetSpan().SequenceEqual(rawBytes)) throw new Exception("Raw root bytes changed.");
                    var result = LuminPackSerializer.Deserialize<NativeCell>(rawBytes);
                    if (result.X != raw.X || result.Y != raw.Y) throw new Exception("Raw read changed fields.");
                    bool failed = false;
                    try { LuminPackSerializer.Deserialize(rawBytes.AsSpan(1), ref result); }
                    catch { failed = true; }
                    if (!failed || result.X != raw.X || result.Y != raw.Y) throw new Exception("Raw read published a truncated value.");

                    Span<byte> scratch = new byte[128];
                    var direct = new LuminPackWriter(ref scratch);
                    var rawArray = new[] { raw, raw };
                    LuminPackSerializer.Serialize(ref direct, in rawArray);
                    if (!LuminPackSerializer.Serialize(rawArray).AsSpan().SequenceEqual(direct.GetSpan()))
                        throw new Exception("Raw array allocation changed its native binary framing.");

                    NativeCell? nullable = raw;
                    foreach (var cell in new NativeCell?[] { nullable, null })
                    {
                        var nullableWriter = new LuminPackWriter(ref scratch);
                        LuminPackSerializer.Serialize(ref nullableWriter, in cell);
                        buffer.ResetWrittenCount();
                        LuminPackSerializer.Serialize(cell, buffer);
                        if (!LuminPackSerializer.Serialize(cell).AsSpan().SequenceEqual(nullableWriter.GetSpan()) ||
                            !buffer.GetSpan().SequenceEqual(nullableWriter.GetSpan()) ||
                            LuminPackSerializer.Deserialize<NativeCell?>(buffer.GetSpan())?.Y != cell?.Y)
                            throw new Exception("Native nullable write or read changed its payload or marker.");
                    }

                    PackedCell? packed = new PackedCell { X = 23, Y = -4096 };
                    byte[] packedBytes = LuminPackSerializer.Serialize(packed.Value);
                    byte[] nullableBytes = LuminPackSerializer.Serialize(packed);
                    buffer.Reset();
                    LuminPackSerializer.Serialize(packed, buffer);
                    if (packedBytes.Length != 9 || nullableBytes.Length != 10 ||
                        !nullableBytes.AsSpan(1).SequenceEqual(packedBytes) || !nullableBytes.AsSpan().SequenceEqual(buffer.GetSpan()))
                        throw new Exception("Packed nullable width or marker changed.");
                    var received = LuminPackSerializer.Deserialize<PackedCell?>(nullableBytes);
                    if (received?.X != 23 || received?.Y != -4096) throw new Exception("Packed nullable read changed fields.");

                    var plain = new PlainGraph { Text = new List<string> { "abc" }, Cell = packed };
                    byte[] plainBytes = LuminPackSerializer.Serialize<PlainGraph, LuminPackUtf8TokenOptions>(plain);
                    PlainGraph reused = new PlainGraph { Text = new List<string>(), Cell = null };
                    if (LuminPackSerializer.Deserialize<PlainGraph, LuminPackUtf8TokenOptions>(plainBytes, ref reused) != plainBytes.Length ||
                        reused.Text.Count != 1 || reused.Text[0] != "abc" || reused.Cell?.Y != -4096)
                        throw new Exception("Explicit-profile read changed data or consumed width.");
                    byte[] nullObject = LuminPackSerializer.Serialize((ReadBoundaryCell)null);
                    var cellAlias = new ReadBoundaryCell { Number = 23 };
                    var existingCell = cellAlias;
                    if (LuminPackSerializer.Deserialize(nullObject, ref existingCell) != 1 ||
                        !ReferenceEquals(existingCell, cellAlias) ||
                        LuminPackSerializer.Deserialize<ReadBoundaryCell>(nullObject) is not null)
                        throw new Exception("The ordinary root null marker changed target publication.");
                    CheckNullReadOutlining(nullObject);
                    var version = new VersionGraph { Cells = new List<VersionCell> { new VersionCell { Value = 17 }, new VersionCell { Value = 31 } } };
                    var versionBytes = LuminPackSerializer.Serialize(version);
                    if (LuminPackSerializer.Deserialize<VersionGraph>(versionBytes).Cells[1].Value != 31)
                        throw new Exception("Nested version read changed data.");
                    var customBytes = LuminPackSerializer.Serialize(new CustomGraph { Cell = new CustomCell { Value = 71 } });
                    if (LuminPackSerializer.Deserialize<CustomGraph>(customBytes).Cell.Value != 71)
                        throw new Exception("Nested Custom read changed data.");
                }

                private static void CheckNullReadOutlining(byte[] nullObject)
                {
                    var text = LuminPackSerializer.Serialize(new TextReadRoot { Text = "value" });
                    if (LuminPackSerializer.Deserialize<TextReadRoot>(text).Text != "value")
                        throw new Exception("The compact string root changed its payload.");

                    var wide = LuminPackSerializer.Serialize(new WideReadRecord { A = -17, H = 39 });
                    var wideCopy = LuminPackSerializer.Deserialize<WideReadRecord>(wide);
                    if (wideCopy.A != -17 || wideCopy.H != 39)
                        throw new Exception("The outlined record root changed its payload.");

                    var graph = new UnionReadGraph { Value = new ReadCase { Number = 31 } };
                    var copy = LuminPackSerializer.Deserialize<UnionReadGraph>(LuminPackSerializer.Serialize(graph));
                    if (copy.Value is not ReadCase { Number: 31 })
                        throw new Exception("The outlined root changed Union dispatch.");

                    var existing = new PlainGraph();
                    var target = existing;
                    if (LuminPackSerializer.Deserialize<PlainGraph, LuminPackUtf8TokenOptions>(nullObject, ref target) != 1 ||
                        !ReferenceEquals(existing, target) ||
                        LuminPackSerializer.Deserialize<PlainGraph, LuminPackUtf8TokenOptions>(nullObject) is not null)
                        throw new Exception("The outlined root null marker changed target publication.");

                    bool failed = false;
                    try { LuminPackSerializer.Deserialize<PlainGraph, LuminPackUtf8TokenOptions>(ReadOnlySpan<byte>.Empty, ref target); }
                    catch { failed = true; }
                    if (!failed || !ReferenceEquals(existing, target))
                        throw new Exception("The outlined root accepted an empty input or changed its target.");
                }

                private static void CheckFixedArray(LuminBufferWriter buffer)
                {
                    foreach (var items in new[] {
                        new[] { new ReadBoundaryCell { Number = 21 }, new ReadBoundaryCell { Number = -37 } },
                        new[] { null, new ReadBoundaryCell { Number = 19 }, null },
                        null })
                    {
                        buffer.ResetWrittenCount();
                        LuminPackSerializer.Serialize(items, buffer);
                        var bytes = LuminPackSerializer.Serialize(items);
                        if (!buffer.GetSpan().SequenceEqual(bytes))
                            throw new Exception("The allocating fixed collection changed null markers or field bytes.");
                        var copy = LuminPackSerializer.Deserialize<ReadBoundaryCell[]>(bytes);
                        if (items is null ? copy is not null : copy.Length != items.Length)
                            throw new Exception("The allocating fixed collection changed its length.");
                        if (items is not null)
                            for (int i = 0; i < items.Length; i++)
                                if (copy[i]?.Number != items[i]?.Number)
                                    throw new Exception("The allocating fixed collection changed an element.");
                    }
                    buffer.ResetWrittenCount();
                }

                private static void CheckTextRoots(LuminBufferWriter buffer)
                {
                    Span<byte> scratch = new byte[256];
                    foreach (var text in new string[] { null, "", "short-\u00E9", new string('s', 32), new string('\u5B57', 32) })
                    {
                        var sequential = new LuminPackWriter(ref scratch);
                        sequential.Write(text);
                        LuminPackSerializer.Serialize(text, buffer);
                        if (!buffer.GetSpan().SequenceEqual(sequential.GetSpan()))
                            throw new Exception("A retained text root changed bytes.");
                        LuminPackSerializer.Serialize(text, buffer);
                        if (buffer.CurrentIndex != sequential.CurrentIndex * 2 ||
                            !buffer.GetSpan().Slice(sequential.CurrentIndex).SequenceEqual(sequential.GetSpan()))
                            throw new Exception("A retained text root changed its append behavior.");
                        buffer.ResetWrittenCount();
                    }
                    var uri = new Uri("https://example.com/\u77ED/absolute");
                    var uriWriter = new LuminPackWriter(ref scratch);
                    uriWriter.WriteObjectHeader(0);
                    uriWriter.Write(uri.OriginalString);
                    LuminPackSerializer.Serialize(uri, buffer);
                    if (!buffer.GetSpan().SequenceEqual(uriWriter.GetSpan()))
                        throw new Exception("The retained URI root changed bytes.");
                    buffer.ResetWrittenCount();
                    var culture = new ObservedCulture();
                    LuminPackSerializer.Serialize<global::System.Globalization.CultureInfo>(culture, buffer);
                    if (culture.Observations != 1)
                        throw new Exception("The retained culture root repeated the Name observation.");
                    buffer.ResetWrittenCount();
                    LuminPackSerializer.Serialize<string, LuminPackUtf8TokenOptions>("hé", buffer);
                    if (!buffer.GetSpan().SequenceEqual(new byte[] { 104, 195, 169, 0 }))
                        throw new Exception("An alternate text profile changed wire.");
                    buffer.ResetWrittenCount();
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview,
            preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"])
            .WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        var compilation = CSharpCompilation.Create("BinaryEntryShapeCases",
            [CSharpSyntaxTree.ParseText(source, parse, "BinaryEntryShapeCases.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ModelBasedGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var declarations = driver.GetRunResult().GeneratedTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        VerifyCollectionReadBoundaries(declarations);
        var capturedRoot = declarations.FirstOrDefault(method => method.Body != null &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().Contains("CapturedRecord", StringComparison.Ordinal) == true) &&
            method.Body.ToString().Contains(".ReserveRecord(", StringComparison.Ordinal));
        var capturedCalls = capturedRoot?.DescendantNodes(node => node is not LocalFunctionStatementSyntax)
            .OfType<InvocationExpressionSyntax>().ToArray() ?? [];
        var reservations = capturedCalls.Where(call => Operation(call) == "ReserveRecord").ToArray();
        // Object and Union payload framing differ in their header byte but share both snapshots and span stores.
        if (reservations.Length is < 1 or > 2 || capturedCalls.Count(call => Operation(call) == "Capture") != 2 ||
            capturedCalls.Count(call => Operation(call) == "WriteReservedSpanInline") != 2 ||
            reservations.Any(call => !call.ToString().Contains("ItemsSnapshot.Bound + ValuesSnapshot.Bound", StringComparison.Ordinal)))
            throw new InvalidOperationException("The positional root did not reserve its captured vector and list together.\n" + capturedRoot);
        static string? Operation(InvocationExpressionSyntax call) => (call.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText;
        var methods = declarations.Where(method => method.Identifier.ValueText.StartsWith("LuminIntercept_", StringComparison.Ordinal)).ToArray();
        VerifyNullReadBoundaries(methods);
        var native = methods.Where(method => method.Identifier.ValueText.StartsWith("LuminIntercept_NativeCell_", StringComparison.Ordinal)).ToArray();
        var fixedArray = methods.Single(method => method.ReturnType.ToString() == "byte[]" &&
            method.ParameterList.Parameters[0].Type!.ToString().EndsWith("ReadBoundaryCell[]", StringComparison.Ordinal));
        var plain = methods.Single(method => method.Identifier.ValueText.StartsWith("LuminIntercept_PlainGraph_", StringComparison.Ordinal) &&
            method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[0].Type!.ToString() == "ReadOnlySpan<byte>");
        if (!native.Any(method => method.ToString().Contains("LuminPackReader.ReadComplete", StringComparison.Ordinal)) ||
            !native.Any(method => method.ToString().Contains("AllocateUninitializedArray<byte>", StringComparison.Ordinal)) ||
            !native.Any(method => method.ToString().Contains("TryBeginFixedRoot", StringComparison.Ordinal)) ||
            !fixedArray.ToString().Contains("WriteFixedRoot(ref destination", StringComparison.Ordinal) ||
            !plain.ToString().Contains("new LuminPackReader(ref buffer)", StringComparison.Ordinal))
            throw new InvalidOperationException("A proven binary root did not select its direct entry.");
        foreach (string type in new[] { "VersionGraph", "CustomGraph" })
        {
            var stateful = methods.Single(method => method.Identifier.ValueText.StartsWith("LuminIntercept_" + type + "_", StringComparison.Ordinal) &&
                method.ParameterList.Parameters[0].Type!.ToString() == "ReadOnlySpan<byte>");
            if (!stateful.ToString().Contains("LuminPackReaderOptionalState.Lease", StringComparison.Ordinal))
                throw new InvalidOperationException(type + " lost its binary operation state.");
        }

        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        if (diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error) || !emitted.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Concat(emitted.Diagnostics)
                .Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("Cases")!.GetMethod("Run")!.Invoke(null, null);
        Console.WriteLine("Passed binary root entry shapes and collection read inlining boundaries.");
    }

    /// <summary>Checks where ordinary binary span roots separate the null marker from the main reader.</summary>
    /// <param name="interceptors">The generated interceptor method declarations.</param>
    private static void VerifyNullReadBoundaries(MethodDeclarationSyntax[] interceptors)
    {
        foreach (var (type, outlined) in new[]
        {
            ("ReadBoundaryCell", false), ("TextReadRoot", false), ("WideReadRecord", true),
            ("PlainGraph", true), ("UnionReadGraph", true),
        })
        {
            var reads = interceptors.Where(method => method.Identifier.ValueText.StartsWith("LuminIntercept_" + type + "_", StringComparison.Ordinal) &&
                method.ParameterList.Parameters[0].Type?.ToString() == "ReadOnlySpan<byte>").ToArray();
            if (reads.Length == 0 || reads.Any(method => method.DescendantNodes().OfType<LocalFunctionStatementSyntax>()
                    .Any(local => local.Identifier.ValueText == "ReadNonNull") != outlined))
                throw new InvalidOperationException(type + " has the wrong binary null read boundary.");
        }
    }

    /// <summary>Checks the inlining flags on the published binary collection bodies themselves.</summary>
    /// <param name="declarations">The generated Next method declarations.</param>
    private static void VerifyCollectionReadBoundaries(MethodDeclarationSyntax[] declarations)
    {
        foreach (string expectedType in new[] { "IList<int>", "ICollection<int>", "IReadOnlyList<int>", "List<ReadBoundaryCell>", "List<int>" })
        {
            // Method names depend on whether publication compacts the owner. Reading the length identifies the body,
            // excluding both the static facade and lightweight reader forwarding methods.
            var roots = declarations.Where(method => (method.Identifier.ValueText is "Read" or "RootRead") &&
                method.ParameterList.Parameters.Count == 2 &&
                method.ParameterList.Parameters[0].Type?.ToString().EndsWith("LuminPackReader", StringComparison.Ordinal) == true &&
                method.ParameterList.Parameters[1].Type?.DescendantNodesAndSelf().OfType<GenericNameSyntax>().Any(name =>
                    name.ToString().Replace("global::", "", StringComparison.Ordinal) == expectedType) == true &&
                method.Body?.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                    call.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == "ReadLength") == true)
                .ToArray();
            if (roots.Length != 1)
                throw new InvalidOperationException("Expected one binary read body for " + expectedType + ", found " + roots.Length + ".");
            string attributes = roots[0].AttributeLists.ToString();
            string expectedHint = expectedType == "List<int>" ? "AggressiveInlining" : "NoInlining";
            if (!attributes.Contains(expectedHint, StringComparison.Ordinal) || attributes.Contains("AggressiveOptimization", StringComparison.Ordinal))
                throw new InvalidOperationException(expectedType + " did not retain the expected binary read JIT boundary.");
        }
    }
}
