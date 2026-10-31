using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code.Core;

/// <summary>
/// Certifies a Union root for adaptive type-partitioned collection dispatch: every generated case is a narrow, fixed,
/// callback-free record from the fixed Union cursor analysis whose fresh read is its ordinary payload read.
/// </summary>
internal sealed class UnionPartitionPlan
{
    /// <summary>The fully qualified root type.</summary>
    internal readonly string Root;

    /// <summary>The root's generated slot suffix.</summary>
    internal readonly string Suffix;

    /// <summary>The cases and their narrow tags in tag order; the list index is the case index.</summary>
    internal readonly List<(int Id, string Type)> Cases;

    /// <summary>Captures an eligible root.</summary>
    /// <param name="root">The fully qualified root type.</param>
    /// <param name="suffix">The root's generated slot suffix.</param>
    /// <param name="cases">The eligible cases in tag order.</param>
    private UnionPartitionPlan(string root, string suffix, List<(int Id, string Type)> cases)
    {
        Root = root;
        Suffix = suffix;
        Cases = cases;
    }

    /// <summary>Obtains the plan of a collection element or Union root.</summary>
    /// <param name="type">The element type or Union root.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="metadata">The current emission settings.</param>
    /// <returns>The plan, or null for the original per-element dispatch.</returns>
    internal static UnionPartitionPlan Get(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        // C# 11 ref cursor reassignment and runtime type identity words are required; IL2CPP type handles are not identity words.
        if (!metadata.IsNet8 || (int)metadata.CSharpVersion < 1100 || metadata.IsForUnity ||
            metadata.ParseOptions.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", System.StringComparison.Ordinal))) return null;
        var cursor = FixedUnionCursorPlan.GetRoot(type, compilation, metadata);
        if (cursor == null || type is not INamedTypeSymbol root) return null;
        var data = LuminPackSourceGenerator.GetSerializationMetadata(root, compilation, metadata);
        if (!data.isUnion || data.IsWideTag || data.callBackMethods.Count != 0 || data.RentPoolMethod != null ||
            data.UnionMembers.Count is < 2 or > 64) return null;
        var cases = new List<(int Id, string Type)>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var member in data.UnionMembers.OrderBy(member => member.Id))
        {
            if (member.Id >= 250 || member.Type is not INamedTypeSymbol { IsAbstract: false, IsGenericType: false } named ||
                !cursor.Contains(named) || !seen.Add(named)) return null;
            var payload = LuminPackSourceGenerator.GetSerializationMetadata(named, compilation, metadata);
            if (payload.callBackMethods.Count != 0 || payload.RentPoolMethod != null ||
                payload.SelectedConstructor is { Parameters.Count: > 0 }) return null;
            cases.Add((member.Id, named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }
        return new UnionPartitionPlan(root.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), cursor.Suffix, cases);
    }

    /// <summary>Emits the root's partition kernels and its lazily built case map.</summary>
    /// <param name="sb">The builder receiving the root's members.</param>
    /// <param name="padding">The member indentation.</param>
    internal void AppendRootMembers(StringBuilder sb, string padding)
    {
        string map = "__LuminPackPartition_" + Suffix;
        string partition = "global::LuminPack.Code.LuminUnionPartition";
        string unsafeAs = "global::System.Runtime.CompilerServices.Unsafe.As";
        sb.AppendLine($"{padding}/// <summary>Builds the exact case map and fixed record widths used by collection partition dispatch.</summary>");
        sb.AppendLine($"{padding}private static class {map}");
        sb.AppendLine($"{padding}{{");
        sb.AppendLine($"{padding}    /// <summary>The root's case map.</summary>");
        sb.AppendLine($"{padding}    internal static readonly global::LuminPack.Code.LuminUnionPartitionMap Map = Create();");
        sb.AppendLine();
        sb.AppendLine($"{padding}    /// <summary>Per-thread successor statistics of the predictability probe, followed by its reused-verdict count.</summary>");
        sb.AppendLine($"{padding}    [global::System.ThreadStatic] internal static byte[]? Successors;");
        sb.AppendLine();
        sb.AppendLine($"{padding}    /// <summary>Measures each case's record width by writing an uninitialized instance once.</summary>");
        sb.AppendLine($"{padding}    /// <returns>The completed map.</returns>");
        sb.AppendLine($"{padding}    private static global::LuminPack.Code.LuminUnionPartitionMap Create()");
        sb.AppendLine($"{padding}    {{");
        sb.AppendLine($"{padding}        var buffer = global::LuminPack.Utility.LuminBufferWriterPool.Rent();");
        sb.AppendLine($"{padding}        try");
        sb.AppendLine($"{padding}        {{");
        sb.AppendLine($"{padding}            var writer = new global::LuminPack.Core.LuminPackWriter(buffer);");
        sb.AppendLine($"{padding}            var widths = new int[{Cases.Count}];");
        for (int index = 0; index < Cases.Count; index++)
            sb.AppendLine($"{padding}            widths[{index}] = global::LuminPack.LuminPackSerializer.SerializeUnionAt(ref writer, ({Cases[index].Type})global::System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof({Cases[index].Type})), 0, (byte){Cases[index].Id});");
        sb.AppendLine($"{padding}            buffer.CompleteWrite(0);");
        sb.AppendLine($"{padding}            return new global::LuminPack.Code.LuminUnionPartitionMap(new global::System.Type[] {{ {string.Join(", ", Cases.Select(item => "typeof(" + item.Type + ")"))} }},");
        sb.AppendLine($"{padding}                new byte[] {{ {string.Join(", ", Cases.Select(item => item.Id))} }}, widths);");
        sb.AppendLine($"{padding}        }}");
        sb.AppendLine($"{padding}        finally");
        sb.AppendLine($"{padding}        {{");
        sb.AppendLine($"{padding}            global::LuminPack.Utility.LuminBufferWriterPool.Return(buffer);");
        sb.AppendLine($"{padding}        }}");
        sb.AppendLine($"{padding}    }}");
        sb.AppendLine();
        sb.AppendLine($"{padding}    /// <summary>Gets the calling thread's successor statistics.</summary>");
        sb.AppendLine($"{padding}    /// <returns>A table with one entry per case and a final reused-verdict count.</returns>");
        sb.AppendLine($"{padding}    internal static global::System.Span<byte> GetSuccessors()");
        sb.AppendLine($"{padding}    {{");
        sb.AppendLine($"{padding}        var successors = Successors;");
        sb.AppendLine($"{padding}        if (successors is null) {{ successors = new byte[{Cases.Count + 1}]; successors.AsSpan(0, {Cases.Count}).Fill(0xFF); Successors = successors; }}");
        sb.AppendLine($"{padding}        return successors;");
        sb.AppendLine($"{padding}    }}");
        sb.AppendLine($"{padding}}}");
        sb.AppendLine();
        AppendKernel(sb, padding, write: true, map, partition, unsafeAs);
        AppendKernel(sb, padding, write: false, map, partition, unsafeAs);
    }

    /// <summary>Emits one direction's chunk kernel.</summary>
    /// <param name="sb">The builder receiving the kernel.</param>
    /// <param name="padding">The member indentation.</param>
    /// <param name="write">Whether the kernel writes values or reads records.</param>
    /// <param name="map">The case map holder.</param>
    /// <param name="partition">The runtime kernel type.</param>
    /// <param name="unsafeAs">The reinterpretation helper.</param>
    private void AppendKernel(StringBuilder sb, string padding, bool write, string map, string partition, string unsafeAs)
    {
        if (write)
        {
            sb.AppendLine($"{padding}/// <summary>Writes a collection chunk grouped by case when its case sequence is not predictable.</summary>");
            sb.AppendLine($"{padding}/// <param name=\"writer\">The active binary operation; its cursor is published at the chunk's end.</param>");
            sb.AppendLine($"{padding}/// <param name=\"chunk\">At most <see cref=\"global::LuminPack.Code.LuminUnionPartition.ChunkLength\"/> elements.</param>");
            sb.AppendLine($"{padding}/// <returns>False, without writing, when the caller must write the chunk element by element.</returns>");
        }
        else
        {
            sb.AppendLine($"{padding}/// <summary>Reads a chunk of fresh records grouped by case when its case sequence is not predictable.</summary>");
            sb.AppendLine($"{padding}/// <param name=\"reader\">The active binary operation; its cursor moves past the chunk only on success.</param>");
            sb.AppendLine($"{padding}/// <param name=\"chunk\">At most <see cref=\"global::LuminPack.Code.LuminUnionPartition.ChunkLength\"/> empty destination slots.</param>");
            sb.AppendLine($"{padding}/// <returns>False, without reading, when the caller must read the chunk element by element.</returns>");
        }
        sb.AppendLine($"{padding}[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine(write
            ? $"{padding}public static bool __LuminPackUnionPartitionWrite_{Suffix}(ref global::LuminPack.Core.LuminPackWriter writer, global::System.ReadOnlySpan<{Root}?> chunk)"
            : $"{padding}public static bool __LuminPackUnionPartitionRead_{Suffix}(ref global::LuminPack.Core.LuminPackReader reader, global::System.Span<{Root}?> chunk)");
        sb.AppendLine($"{padding}{{");
        sb.AppendLine($"{padding}    var map = {map}.Map;");
        if (write)
            sb.AppendLine($"{padding}    if ({partition}.IsPredictable<{Root}>(map, chunk, {map}.GetSuccessors())) return false;");
        else
        {
            sb.AppendLine($"{padding}    global::System.ReadOnlySpan<byte> input = reader.Buffer.Slice(reader.Position);");
            sb.AppendLine($"{padding}    if ({partition}.IsPredictable(map, input, chunk.Length, {map}.GetSuccessors())) return false;");
        }
        sb.AppendLine($"{padding}    global::System.Span<byte> cases = stackalloc byte[{partition}.ChunkLength];");
        sb.AppendLine($"{padding}    global::System.Span<int> offsets = stackalloc int[{partition}.ChunkLength];");
        sb.AppendLine($"{padding}    global::System.Span<ushort> order = stackalloc ushort[{partition}.ChunkLength];");
        sb.AppendLine($"{padding}    global::System.Span<int> groups = stackalloc int[{Cases.Count}];");
        if (write)
        {
            sb.AppendLine($"{padding}    int start = writer.Position;");
            sb.AppendLine($"{padding}    if (!{partition}.TryLayout<{Root}>(map, chunk, start, cases, offsets, groups, out int end)) return false;");
            sb.AppendLine($"{padding}    writer.EnsureAdditionalCapacity(end - start);");
        }
        else
            sb.AppendLine($"{padding}    if (!{partition}.TryLayout<{Root}>(map, input, chunk, cases, offsets, groups, out int end)) return false;");
        sb.AppendLine($"{padding}    {partition}.Order(cases.Slice(0, chunk.Length), groups, order);");
        if (!write) sb.AppendLine($"{padding}    ref byte origin = ref reader.Cursor;");
        sb.AppendLine($"{padding}    int index = 0;");
        for (int caseIndex = 0; caseIndex < Cases.Count; caseIndex++)
        {
            var (id, type) = Cases[caseIndex];
            sb.AppendLine($"{padding}    for (int stop = groups[{caseIndex}]; index < stop; index++)");
            sb.AppendLine($"{padding}    {{");
            sb.AppendLine($"{padding}        int item = order[index];");
            if (write)
                sb.AppendLine($"{padding}        global::LuminPack.LuminPackSerializer.SerializeUnionAt(ref writer, {unsafeAs}<{type}>(chunk[item]), offsets[item], (byte){id});");
            else
            {
                sb.AppendLine($"{padding}        reader.Cursor = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref origin, offsets[item] + 1);");
                sb.AppendLine($"{padding}        {type}? value = null;");
                sb.AppendLine($"{padding}        global::LuminPack.LuminPackSerializer.DeserializeUnion(ref reader, ref value);");
                sb.AppendLine($"{padding}        chunk[item] = value;");
            }
            sb.AppendLine($"{padding}    }}");
        }
        sb.AppendLine(write ? $"{padding}    writer.Position = end;" : $"{padding}    reader.Cursor = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref origin, end);");
        sb.AppendLine($"{padding}    return true;");
        sb.AppendLine($"{padding}}}");
        sb.AppendLine();
    }
}
