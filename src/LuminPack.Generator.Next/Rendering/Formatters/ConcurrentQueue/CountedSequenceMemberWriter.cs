#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the family's inline record write and its cursor publication boundaries.</summary>
internal static class CountedSequenceMemberWriter
{
    /// <summary>Expands a counted sequence member's shared write body in place: the null collection header, then the counted
    /// enumeration of the queue or stack.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The queue or stack member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    /// <param name="depth">The inline nesting depth the element body continues.</param>
    internal static void Write(List<string> body, RenderMember member, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        string local = BinaryEmitter.RoleName(names, "value");
        // A payload element claims its null exit before the collection's own, as the sequence template numbers them.
        string elementExit = null;
        if (member.Nested != null)
        {
            elementExit = "end" + ends.ToString(CultureInfo.InvariantCulture);
            ends++;
        }
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
        body.Add("foreach (var item in " + local + ")");
        body.Add("{");
        if (member.Nested != null)
        {
            // A packable element expands its whole record body in place through the isolated state.
            body.Add("writer.CheckBuffer();");
            body.Add("count++;");
            body.Add("var v = item;");
            BinaryEmitter.AddSequenceElementWrite(body, member.Nested, "v", elementExit, "writer", depth, w, names, ref ends, ref cursors, ref spans);
        }
        // A string element writes through the operation's profile, whose own store checks the buffer; a raw element keeps
        // the explicit check the unchecked span store does not perform.
        else if (member.Scalar == "string")
        {
            string element = BinaryEmitter.RoleName(names, "value");
            body.Add("count++;");
            body.Add("var v = item;");
            body.Add("ref readonly string " + element + " = ref v;");
            body.Add("writer.Write<TLuminProfile>(" + element + ");");
        }
        // A Boolean element writes through a read-only alias, as the legacy element write does.
        else if (member.Scalar == "bool")
        {
            string element = BinaryEmitter.RoleName(names, "value");
            body.Add("writer.CheckBuffer();");
            body.Add("count++;");
            body.Add("var v = item;");
            body.Add("ref readonly bool " + element + " = ref v;");
            body.Add("writer.Write(in " + element + ");");
        }
        else
        {
            body.Add("writer.CheckBuffer();");
            body.Add("count++;");
            body.Add("var v = item;");
            body.Add("writer.Write(in v);");
        }
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
