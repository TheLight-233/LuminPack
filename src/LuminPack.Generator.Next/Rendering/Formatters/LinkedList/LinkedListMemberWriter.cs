#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the family's inline record write and its cursor publication boundaries.</summary>
internal static class LinkedListMemberWriter
{
    /// <summary>Expands a linked-list member's shared write body in place: the null collection header, then the counted node
    /// walk.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The linked-list member.</param>
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
        body.Add("int countPosition = writer.BeginCollection();");
        body.Add("int count = 0;");
        body.Add("var node = " + local + ".First;");
        body.Add("if (node is not null)");
        body.Add("{");
        body.Add("do");
        body.Add("{");
        if (member.Scalar != "string")
            body.Add("writer.CheckBuffer();");
        body.Add("count++;");
        body.Add("var v = node.Value;");
        // A Boolean element writes through a read-only alias, as the legacy element write does.
        if (member.Scalar == "string")
            body.Add("writer.Write<TLuminProfile>(v);");
        else if (member.Scalar == "bool")
        {
            string element = BinaryEmitter.RoleName(names, "value");
            body.Add("ref readonly bool " + element + " = ref v;");
            body.Add("writer.Write(in " + element + ");");
        }
        else
            body.Add("writer.Write(in v);");
        body.Add("node = node.Next;");
        body.Add("}");
        body.Add("while (node is not null);");
        body.Add("}");
        body.Add("");
        body.Add("writer.EndCollection(countPosition, count);");
        body.Add("writer.CheckBuffer();");
        body.Add("");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
