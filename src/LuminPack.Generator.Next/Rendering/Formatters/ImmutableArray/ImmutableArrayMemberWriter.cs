#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the family's inline record write and its cursor publication boundaries.</summary>
internal static class ImmutableArrayMemberWriter
{
    /// <summary>Expands an immutable-array member's shared write body in place: the default marker, then the storage span.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The immutable-array member.</param>
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
        body.Add("if (" + local + ".IsDefault)");
        body.Add("{");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add(w.A("Unsafe") + ".WriteUnaligned(ref writer.Cursor, " + w.A("LuminPackCode") + ".NullCollection);");
        body.Add("writer.Advance(4);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        if (member.Nested != null)
        {
            // A packable element expands its whole record body in place through the isolated state.
            body.Add("global::System.ReadOnlySpan<" + member.Scalar + "> items = " + local + ".AsSpan();");
            body.Add("writer.EnsureAdditionalCapacity(4);");
            body.Add("writer.Write(items.Length);");
            body.Add("for (int i = 0; i < items.Length; i++)");
            body.Add("{");
            body.Add("writer.CheckBuffer();");
            BinaryEmitter.AddSequenceElementWrite(body, member.Nested, "items[i]!", elementExit, "writer", depth, w, names, ref ends, ref cursors, ref spans);
            body.Add("}");
            body.Add("");
            body.Add("writer.CheckBuffer();");
        }
        // A string span writes its element count and the records through one profiled call; a raw span stores in place.
        else if (member.Scalar == "string")
        {
            body.Add("global::System.ReadOnlySpan<string> items = " + local + ".AsSpan();");
            body.Add("writer.EnsureAdditionalCapacity(4);");
            body.Add("writer.Write(items.Length);");
            body.Add("writer.WriteStrings<TLuminProfile>(items);");
            body.Add("writer.CheckBuffer();");
        }
        else
        {
            body.Add("global::System.ReadOnlySpan<" + member.Scalar + "> items = " + local + ".AsSpan();");
            body.Add("writer.Write<" + member.Scalar + ">(items, items.Length);");
        }
        body.Add("");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
