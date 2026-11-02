using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

/// <summary>Emits framed records (version-tolerant and reference-tracked) like ordinary objects: one reservation, a local cursor.</summary>
internal static class ReservedRecordEmitter
{
    private const string Memory = "global::System.Runtime.CompilerServices.Unsafe";
    private const string WriterType = "global::LuminPack.Core.LuminPackWriter";

    /// <summary>Writes a header and its members: bounded members share one reservation and store at a local cursor.</summary>
    /// <param name="sb">The generated operation body.</param>
    /// <param name="fields">The wire members in slot order.</param>
    /// <param name="widths">Each member's table width: a raw width, 255 for a length-prefixed member, or null when known only at run time.</param>
    /// <param name="access">The member storage expression.</param>
    /// <param name="headerBound">The header's upper bound in bytes.</param>
    /// <param name="storeHeader">Emits, at the given indentation, the header store at <c>__cursor</c> and advances it.</param>
    /// <returns>Whether the record was emitted; records with members of run-time width or volatile members keep their original writer.</returns>
    /// <remarks>The header joins the first group. A variable member's four-byte length slot precedes it and is filled from the
    /// cursor distance after the member. A member without a computable bound publishes the cursor, keeps its original
    /// reserved-length write and resumes a new group.</remarks>
    internal static bool TryGenerate(StringBuilder sb, List<LuminDataField> fields, int?[] widths, Func<LuminDataField, string> access,
        string headerBound, Action<string> storeHeader)
    {
        const string indent = "            ";
        for (int index = 0; index < fields.Count; index++)
        {
            int kind = LuminPackCodeGenerator.ReservedKind(fields[index]);
            if (widths[index] is not int width || kind == 1 && width == 255 || kind != 1 && width != 255 || fields[index].IsVolatile) return false;
        }
        sb.AppendLine(indent + "ref byte __cursor = ref writer.GetCurrentSpanReference();");
        bool headerPending = true;
        int slot = 0;
        for (int index = 0; index <= fields.Count; index++)
        {
            int last = index;
            while (last < fields.Count && LuminPackCodeGenerator.ReservedKind(fields[last]) is > 0 and < 6) last++;
            // Members [index, last) form one reserved group; the header joins the first group even when it is empty.
            if (last > index || headerPending)
            {
                sb.AppendLine(indent + "{");
                var terms = new List<string>();
                if (headerPending) terms.Add(headerBound);
                for (int member = index; member < last; member++)
                {
                    var field = fields[member];
                    int kind = LuminPackCodeGenerator.ReservedKind(field);
                    string name = LuminPackCodeGenerator.RecordSnapshotName(field, member);
                    if (kind == 1) terms.Add(LuminPackCodeGenerator.GetUnmanagedFieldSize(field));
                    else if (kind == 2)
                    {
                        sb.AppendLine(indent + "    string " + name + " = " + access(field) + ";");
                        terms.Add("4 + writer.RecordBound(" + name + ")");
                    }
                    else
                    {
                        sb.AppendLine(indent + "    var " + name + " = " + WriterType + ".Capture(" + access(field) + ");");
                        terms.Add("4 + " + name + ".Bound");
                    }
                }
                sb.AppendLine(indent + "    __cursor = ref writer.ReserveRecord(ref __cursor, checked(" + string.Join(" + ", terms) + "));");
                if (headerPending) storeHeader(indent + "    ");
                headerPending = false;
                for (int member = index; member < last; member++)
                {
                    var field = fields[member];
                    int kind = LuminPackCodeGenerator.ReservedKind(field);
                    string name = LuminPackCodeGenerator.RecordSnapshotName(field, member);
                    if (kind == 1)
                    {
                        int end = member;
                        while (end + 1 < last && LuminPackCodeGenerator.ReservedKind(fields[end + 1]) == 1) end++;
                        LuminPackCodeGenerator.AppendReservedFixedGroup(sb, fields, member, end, access, indent + "    ");
                        member = end;
                        continue;
                    }
                    string length = "__length" + slot++;
                    sb.AppendLine(indent + "    // Field: " + field.Name);
                    sb.AppendLine(indent + "    ref byte " + length + " = ref __cursor;");
                    sb.AppendLine(indent + "    __cursor = ref " + Memory + ".Add(ref __cursor, 4);");
                    if (kind == 2) sb.AppendLine(indent + "    __cursor = ref writer.WriteReservedRecordString(ref __cursor, " + name + ");");
                    else if (kind is 3 or 4) sb.AppendLine(indent + "    __cursor = ref " + WriterType + ".WriteReservedSpanRecord(ref __cursor, " + name + ");");
                    else sb.AppendLine(indent + "    __cursor = ref " + WriterType + ".WriteReserved(ref __cursor, " + name + ");");
                    sb.AppendLine(indent + "    " + Memory + ".WriteUnaligned(ref " + length + ", (int)" + Memory + ".ByteOffset(ref " + Memory + ".Add(ref " + length + ", 4), ref __cursor));");
                }
                sb.AppendLine(indent + "}");
            }
            if (last >= fields.Count) break;
            // An unbounded member publishes the cursor and keeps its reserved-length write.
            var open = fields[last];
            sb.AppendLine(indent + "writer.CommitRecords(ref __cursor);");
            sb.AppendLine(indent + "{");
            sb.AppendLine(indent + "    int " + open.Name + "Length = writer.ReserveVersionLength();");
            sb.AppendLine(indent + "    writer." + (open.IsCompress ? "WriteValueWithCompress" : "WriteValue") + "(" + access(open) + ");");
            sb.AppendLine(indent + "    writer.CompleteVersionLength(" + open.Name + "Length);");
            sb.AppendLine(indent + "}");
            sb.AppendLine(indent + "__cursor = ref writer.GetCurrentSpanReference();");
            index = last;
        }
        sb.AppendLine(indent + "writer.CommitRecords(ref __cursor);");
        return true;
    }

    /// <summary>Splits a constant byte sequence into the disjoint words a fixed store or comparison uses.</summary>
    /// <param name="bytes">The constant bytes.</param>
    /// <param name="widest">The widest word: eight on 64-bit targets, four where typed access must stay within four bytes.</param>
    /// <returns>Each word's offset, width and little-endian value.</returns>
    internal static IEnumerable<(int Offset, int Width, ulong Value)> Words(byte[] bytes, int widest)
    {
        int offset = 0;
        foreach (int width in new[] { 8, 4, 2, 1 }.Where(width => width <= widest))
            for (; offset + width <= bytes.Length; offset += width)
            {
                ulong word = 0;
                for (int index = width - 1; index >= 0; index--) word = word << 8 | bytes[offset + index];
                yield return (offset, width, word);
            }
    }

    /// <summary>Names the unsigned type of a word width.</summary>
    /// <param name="width">One, two, four or eight bytes.</param>
    /// <returns>The C# keyword.</returns>
    internal static string WordType(int width) => width == 8 ? "ulong" : width == 4 ? "uint" : width == 2 ? "ushort" : "byte";

    /// <summary>Emits stores of constant bytes at a reference, as words of at most <paramref name="widest"/> bytes.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="bytes">The constant bytes.</param>
    /// <param name="target">The first destination byte.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <param name="widest">The widest word.</param>
    internal static void AppendStores(StringBuilder sb, byte[] bytes, string target, string indent, int widest)
    {
        foreach (var (offset, width, value) in Words(bytes, widest))
        {
            string location = offset == 0 ? "ref " + target : "ref " + Memory + ".Add(ref " + target + ", " + offset + ")";
            sb.AppendLine(indent + Memory + ".WriteUnaligned(" + location + ", (" + WordType(width) + ")0x" +
                value.ToString("X", System.Globalization.CultureInfo.InvariantCulture) + (width == 8 ? "UL" : "U") + ");");
        }
    }

    /// <summary>Builds an expression that is zero exactly when the bytes at a reference equal constant bytes.</summary>
    /// <param name="bytes">The constant bytes.</param>
    /// <param name="source">The first input byte.</param>
    /// <param name="widest">The widest word.</param>
    /// <returns>The XOR-OR difference expression.</returns>
    internal static string Difference(byte[] bytes, string source, int widest)
        => "(" + string.Join(" | ", Words(bytes, widest).Select(word =>
        {
            string location = word.Offset == 0 ? "ref " + source : "ref " + Memory + ".Add(ref " + source + ", " + word.Offset + ")";
            return "((ulong)" + Memory + ".ReadUnaligned<" + WordType(word.Width) + ">(" + location + ") ^ 0x" +
                word.Value.ToString("X", System.Globalization.CultureInfo.InvariantCulture) + "UL)";
        })) + ")";
}
