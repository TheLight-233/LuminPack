#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the family's inline record write and its cursor publication boundaries.</summary>
internal static class ImmutableListMemberWriter
{
    /// <summary>Expands an immutable-list member's shared write body in place: the null collection header, then the counted
    /// storage copied through pooled memory.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The immutable-list member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    internal static void Write(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends)
    {
        string local = BinaryEmitter.RoleName(names, "value");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        body.Add("if (" + local + " is null)");
        body.Add("{");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add(w.A("Unsafe") + ".WriteUnaligned(ref writer.Cursor, " + w.A("LuminPackCode") + ".NullCollection);");
        body.Add("writer.Advance(4);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("int count = " + local + ".Count;");
        body.Add("var items = count == 0 ? null : global::System.Buffers.ArrayPool<" + member.Scalar +
            ">.Shared.Rent(count);");
        body.Add("try");
        body.Add("{");
        body.Add("if (count != 0)");
        body.Add("    " + local + ".CopyTo(items);");
        body.Add("writer.Write<" + member.Scalar + ">(new global::System.ReadOnlySpan<" + member.Scalar +
            ">(items, 0, count), count);");
        body.Add("}");
        body.Add("finally");
        body.Add("{");
        body.Add("if (items is not null)");
        body.Add("    global::System.Buffers.ArrayPool<" + member.Scalar + ">.Shared.Return(items, false);");
        body.Add("}");
        body.Add("");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
