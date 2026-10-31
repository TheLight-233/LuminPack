using System;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

/// <summary>Emits mixed exact-size and upper-bound partition kernels without changing virtual fallbacks.</summary>
internal static class VariableUnionPartitionEmitter
{
    private const string Partition = "global::LuminPack.Code.LuminUnionPartition";
    private const string UnsafeType = "global::System.Runtime.CompilerServices.Unsafe";
    private const string Writer = "global::LuminPack.Core.LuminPackWriter";
    private const string Reader = "global::LuminPack.Core.LuminPackReader";
    private const string Options = "global::LuminPack.Option.ILuminPackOptions";
    private const string Record = "global::LuminPack.Code.LuminUnionPartitionRecord";
    private const string Method = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]";

    /// <summary>Publishes one root's state holder, compiled kernels and case loops.</summary>
    /// <param name="target">The partial root declaration builder.</param>
    /// <param name="padding">The declaration indentation.</param>
    /// <param name="partition">The complete partition contract.</param>
    internal static void Append(StringBuilder target, string padding, UnionPartitionPlan partition)
    {
        var plan = partition.Variable;
        var sb = new StringBuilder();
        string holder = "__LuminPackVariablePartition_" + plan.Suffix;
        string root = plan.Root;
        int count = plan.Cases.Count;
        sb.AppendLine("private static class " + holder + " {");
        sb.AppendLine("internal static readonly global::LuminPack.Code.LuminUnionPartitionMap Map = new global::LuminPack.Code.LuminUnionPartitionMap(new global::System.Type[] { " +
            string.Join(", ", plan.Cases.Select(item => "typeof(" + item.Type + ")")) + " }, new byte[] { " + string.Join(", ", plan.Cases.Select(item => item.Id)) + " }, new int[" + count + "]);");
        sb.AppendLine("[global::System.ThreadStatic] private static byte[]? successors;");
        sb.AppendLine("internal static global::System.Span<byte> Successors() { if (successors is null) { successors = new byte[" + (count + 2) + "]; global::System.MemoryExtensions.AsSpan(successors, 0, " + count + ").Fill(255); } return successors; }");
        sb.AppendLine("}");
        AppendWrite(sb, partition, holder);
        AppendRead(sb, partition, holder);
        for (int index = 0; index < count; index++)
        {
            AppendMeasure(sb, plan, index);
            AppendWriteGroup(sb, plan, index);
            AppendReadWidth(sb, plan, index);
        }
        foreach (string line in sb.ToString().Split('\n')) target.AppendLine(padding + line.TrimEnd('\r'));
    }

    /// <summary>Emits the reservation, grouped writes and optional stable compaction.</summary>
    /// <param name="sb">The root members.</param>
    /// <param name="partition">The complete partition contract.</param>
    /// <param name="holder">The root's thread-state holder.</param>
    private static void AppendWrite(StringBuilder sb, UnionPartitionPlan partition, string holder)
    {
        var plan = partition.Variable;
        string root = plan.Root, method = partition.WriteMethod;
        sb.AppendLine("public static bool " + method + "(ref " + Writer + " writer, global::System.ReadOnlySpan<" + root + "?> chunk) => " + method + "<global::LuminPack.Option.LuminPackDefaultOptions>(ref writer, chunk);");
        sb.AppendLine(Method);
        if (partition.SkipLocalsInit) sb.AppendLine("[global::System.Runtime.CompilerServices.SkipLocalsInit]");
        sb.AppendLine("public static bool " + method + "<__LuminProfile>(ref " + Writer + " writer, global::System.ReadOnlySpan<" + root + "?> chunk) where __LuminProfile : struct, " + Options + " {");
        sb.AppendLine("if (default(__LuminProfile).StringRecording != global::LuminPack.Option.LuminPackStringRecording.Length || (default(__LuminProfile).StringEncoding != global::LuminPack.Option.LuminPackStringEncoding.UTF16 && default(__LuminProfile).StringEncoding != global::LuminPack.Option.LuminPackStringEncoding.UTF8) || chunk.Length > " + Partition + ".ChunkLength) return false;");
        sb.AppendLine("var map = " + holder + ".Map;");
        sb.AppendLine("if (" + Partition + ".IsPredictable<" + root + ">(map, chunk, " + holder + ".Successors())) return false;");
        sb.AppendLine("global::System.Span<byte> cases = stackalloc byte[256]; global::System.Span<ushort> order = stackalloc ushort[256];");
        sb.AppendLine("global::System.Span<int> groups = stackalloc int[" + plan.Cases.Count + "]; groups.Clear();");
        sb.AppendLine("global::System.Span<" + Record + "> records = stackalloc " + Record + "[256];");
        sb.AppendLine("for (int i = 0; i < chunk.Length; i++) { if (chunk[i] is null) return false; int kind = map.Classify(chunk[i]!); if (kind < 0) return false; cases[i] = (byte)kind; groups[kind]++; }");
        sb.AppendLine(Partition + ".Order(cases.Slice(0, chunk.Length), groups, order);");
        sb.AppendLine("int first = 0;");
        for (int index = 0; index < plan.Cases.Count; index++)
            sb.AppendLine("if (!__LuminPackMeasurePartition" + index + "_" + plan.Suffix + "<__LuminProfile>(chunk, order, records, first, groups[" + index + "])) return false; first = groups[" + index + "];");
        sb.AppendLine("int start = writer.Position; long end = start; for (int i = 0; i < chunk.Length; i++) { if (end > int.MaxValue) return false; records[i].Offset = (int)end; end += records[i].Bound; }");
        sb.AppendLine("if (end > int.MaxValue || !writer.TryEnsurePartitionCapacity(end - start, " + (plan.HasObjectElements ? "true" : "false") + ")) return false;");
        sb.AppendLine("first = 0; bool compact = false;");
        for (int index = 0; index < plan.Cases.Count; index++)
            sb.AppendLine("compact |= __LuminPackWritePartition" + index + "_" + plan.Suffix + "<__LuminProfile>(ref writer, chunk, order, records, first, groups[" + index + "]); first = groups[" + index + "];");
        sb.AppendLine("if (compact) { int destination = start; for (int i = 0; i < chunk.Length; i++) { writer.Buffer.Slice(records[i].Offset, records[i].Actual).CopyTo(writer.Buffer.Slice(destination, records[i].Actual)); destination = checked(destination + records[i].Actual); } writer.Position = destination; } else writer.Position = (int)end;");
        sb.AppendLine("return true; }");
    }

    /// <summary>Emits the O(1) bound loop for one exact runtime case.</summary>
    /// <param name="sb">The root members.</param>
    /// <param name="plan">The eligible root.</param>
    /// <param name="index">The dense case index.</param>
    private static void AppendMeasure(StringBuilder sb, VariableUnionPayloadPlan plan, int index)
    {
        var item = plan.Cases[index];
        sb.AppendLine(Method);
        sb.AppendLine("private static bool __LuminPackMeasurePartition" + index + "_" + plan.Suffix + "<__LuminProfile>(scoped global::System.ReadOnlySpan<" + plan.Root + "?> chunk, scoped global::System.Span<ushort> order, scoped global::System.Span<" + Record + "> records, int first, int stop) where __LuminProfile : struct, " + Options + " {");
        sb.AppendLine("for (int k = first; k < stop; k++) { int i = order[k]; var value = " + UnsafeType + ".As<" + item.Type + ">(chunk[i]!); long width = 1L + " + item.Bound("value", "__LuminProfile") + "; if (width > int.MaxValue) return false; records[i].Bound = (int)width; } return true; }");
    }

    /// <summary>Emits a monomorphic write loop that retains actual widths for upper-bound records.</summary>
    /// <param name="sb">The root members.</param>
    /// <param name="plan">The eligible root.</param>
    /// <param name="index">The dense case index.</param>
    private static void AppendWriteGroup(StringBuilder sb, VariableUnionPayloadPlan plan, int index)
    {
        var item = plan.Cases[index];
        sb.AppendLine(Method);
        sb.AppendLine("private static bool __LuminPackWritePartition" + index + "_" + plan.Suffix + "<__LuminProfile>(scoped ref " + Writer + " writer, scoped global::System.ReadOnlySpan<" + plan.Root + "?> chunk, scoped global::System.Span<ushort> order, scoped global::System.Span<" + Record + "> records, int first, int stop) where __LuminProfile : struct, " + Options + " {");
        string write = item.FixedCursor
            ? "global::LuminPack.LuminPackSerializer.SerializeUnionAt(ref writer, " + UnsafeType + ".As<" + item.Type + ">(chunk[i]!), at, (byte)" + item.Id + ")"
            : "global::LuminPack.LuminPackSerializer.SerializeUnionPartitionAt<__LuminProfile>(ref writer, " + UnsafeType + ".As<" + item.Type + ">(chunk[i]!), at + 1, records[i].Bound - 1)";
        sb.AppendLine("bool compact = false; for (int k = first; k < stop; k++) { int i = order[k]; int at = records[i].Offset; writer.Buffer[at] = (byte)" + item.Id + "; int end = " + write + "; records[i].Actual = end - at; compact |= records[i].Actual != records[i].Bound; } return compact; }");
    }

    /// <summary>Emits bounded record discovery followed by unchanged checked case reads.</summary>
    /// <param name="sb">The root members.</param>
    /// <param name="partition">The complete partition contract.</param>
    /// <param name="holder">The root's thread-state holder.</param>
    private static void AppendRead(StringBuilder sb, UnionPartitionPlan partition, string holder)
    {
        var plan = partition.Variable;
        string method = partition.ReadMethod;
        sb.AppendLine("public static bool " + method + "(ref " + Reader + " reader, global::System.Span<" + plan.Root + "?> chunk) => " + method + "<global::LuminPack.Option.LuminPackDefaultOptions>(ref reader, chunk);");
        sb.AppendLine(Method);
        if (partition.SkipLocalsInit) sb.AppendLine("[global::System.Runtime.CompilerServices.SkipLocalsInit]");
        sb.AppendLine("public static bool " + method + "<__LuminProfile>(ref " + Reader + " reader, global::System.Span<" + plan.Root + "?> chunk) where __LuminProfile : struct, " + Options + " {");
        sb.AppendLine("if (default(__LuminProfile).StringRecording != global::LuminPack.Option.LuminPackStringRecording.Length || (default(__LuminProfile).StringEncoding != global::LuminPack.Option.LuminPackStringEncoding.UTF16 && default(__LuminProfile).StringEncoding != global::LuminPack.Option.LuminPackStringEncoding.UTF8) || chunk.Length < 32 || chunk.Length > 256) return false;");
        sb.AppendLine("var map = " + holder + ".Map; global::System.ReadOnlySpan<byte> input = reader.Buffer.Slice(reader.Position);");
        sb.AppendLine("global::System.Span<byte> cases = stackalloc byte[256]; global::System.Span<int> offsets = stackalloc int[256]; global::System.Span<ushort> order = stackalloc ushort[256]; global::System.Span<int> groups = stackalloc int[" + plan.Cases.Count + "]; groups.Clear(); int position = 0;");
        sb.AppendLine("for (int i = 0; i < chunk.Length; i++) { if (chunk[i] is not null || (uint)position >= (uint)input.Length) return false; int kind = map.TagCases[input[position]]; if (kind == 255) return false; cases[i] = (byte)kind; offsets[i] = position; int width;");
        sb.AppendLine("switch (kind) {");
        for (int index = 0; index < plan.Cases.Count; index++)
            sb.AppendLine("case " + index + ": if (!__LuminPackReadPartitionWidth" + index + "_" + plan.Suffix + "<__LuminProfile>(input.Slice(position + 1), out width)) return false; break;");
        sb.AppendLine("default: return false; } position += 1 + width; groups[kind]++; if (i == 31 && " + Partition + ".IsPredictableCases(cases.Slice(0, 32), " + holder + ".Successors())) return false; }");
        sb.AppendLine("reader.EnsureReadable(position); " + Partition + ".Order(cases.Slice(0, chunk.Length), groups, order); ref byte origin = ref reader.Cursor; int first = 0;");
        for (int index = 0; index < plan.Cases.Count; index++)
        {
            var item = plan.Cases[index];
            sb.AppendLine("for (int stop = groups[" + index + "]; first < stop; first++) { int i = order[first]; reader.Cursor = ref " + UnsafeType + ".Add(ref origin, offsets[i] + 1); " + item.Type + "? value = null; global::LuminPack.LuminPackSerializer.DeserializeUnion<__LuminProfile>(ref reader, ref value); chunk[i] = value; }");
        }
        sb.AppendLine("reader.Cursor = ref " + UnsafeType + ".Add(ref origin, position); return true; }");
    }

    /// <summary>Emits a nonallocating bounded parser for one case's framing.</summary>
    /// <param name="sb">The root members.</param>
    /// <param name="plan">The eligible root.</param>
    /// <param name="index">The dense case index.</param>
    private static void AppendReadWidth(StringBuilder sb, VariableUnionPayloadPlan plan, int index)
    {
        var item = plan.Cases[index];
        sb.AppendLine(Method);
        sb.AppendLine("private static bool __LuminPackReadPartitionWidth" + index + "_" + plan.Suffix + "<__LuminProfile>(global::System.ReadOnlySpan<byte> input, out int width) where __LuminProfile : struct, " + Options + " { width = 0;");
        sb.AppendLine("int position = 0;");
        foreach (var field in item.Fields)
        {
            if (field.Kind == 0)
                sb.AppendLine("if (input.Length - position < " + UnsafeType + ".SizeOf<" + field.Type + ">()) return false; position += " + UnsafeType + ".SizeOf<" + field.Type + ">();");
            else
            {
                sb.AppendLine("{ if (input.Length - position < 4) return false; int length = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(input.Slice(position, 4)); position += 4; long bytes;");
                if (field.Kind == 1)
                {
                    string profile = field.Profile ?? "__LuminProfile";
                    sb.AppendLine("if (default(" + profile + ").StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) { if (input.Length - position < 4) return false; position += 4; bytes = length == -1 ? 0 : length; } else bytes = length == -1 ? 0 : (length & 1) != 0 ? length >> 1 : length;");
                }
                else if (field.Kind is 2 or 3)
                    sb.AppendLine("bytes = length == -1 ? 0 : (long)length * " + UnsafeType + ".SizeOf<" + field.Element + ">();");
                else
                {
                    sb.AppendLine("if (length < -1 || length > input.Length - position) return false;");
                    sb.AppendLine("for (int element = 0; element < length; element++) { if ((uint)position >= (uint)input.Length) return false; int members = input[position++]; if (members == 255) continue; int payload; switch (members) {");
                    for (int members = 0; members <= field.ObjectMembers.Length; members++)
                        sb.AppendLine("case " + members + ": payload = " + (members == 0 ? "0" : string.Join(" + ", field.ObjectMembers.Take(members))) + "; break;");
                    sb.AppendLine("default: return false; } if (payload > input.Length - position) return false; position += payload; } bytes = 0;");
                }
                sb.AppendLine("if (length < -1 || bytes < 0 || bytes > input.Length - position) return false; position += (int)bytes; }");
            }
        }
        sb.AppendLine("width = position; return true; }");
    }
}
