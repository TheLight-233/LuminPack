#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the family's inline record write and its cursor publication boundaries.</summary>
internal static class QueueMemberWriter
{
    /// <summary>Expands a queue member's shared write body in place: the null collection header, then the storage write through
    /// an isolated operation state whose cursor is published back to the record.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The queue member.</param>
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
        body.Add(w.A("LuminPackWriter") + " operationState = default;");
        body.Add("writer.CopyTo(ref operationState);");
        QueueTraversal.Write(body, member.Scalar, local, w.A("Unsafe"), w.A("MemoryMarshal"));
        body.Add("operationState.CopyTo(ref writer);");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
