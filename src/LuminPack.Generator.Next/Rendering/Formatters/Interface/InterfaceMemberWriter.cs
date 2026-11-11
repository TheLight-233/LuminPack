#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the interface families' inline record writes and their cursor publication boundaries.</summary>
internal static class InterfaceMemberWriter
{
    /// <summary>Expands a sequence interface member's write body in place.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The interface member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    internal static void Write(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends) => Emit(body, member, owner, w, names, ref ends, 0);

    /// <summary>Expands an immutable sequence interface member's counted write body in place.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The interface member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    internal static void WriteCounted(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends) => Emit(body, member, owner, w, names, ref ends, 2);

    /// <summary>Expands a set interface member's write body in place.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The interface member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    internal static void WriteSet(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends) => Emit(body, member, owner, w, names, ref ends, 1);

    private static void Emit(List<string> body, RenderMember member, string owner, CodeWriter w, Dictionary<string, int> names,
        ref int ends, int mode)
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
        if (mode == 1)
            InterfaceTraversal.WriteSet(body, member.Scalar, local, w.A("Unsafe"));
        else if (mode == 2)
            InterfaceTraversal.Counted(body, member.Scalar, local);
        else
            InterfaceTraversal.Write(body, member.Scalar, local, w.A("Unsafe"));
        body.Add("operationState.CopyTo(ref writer);");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
