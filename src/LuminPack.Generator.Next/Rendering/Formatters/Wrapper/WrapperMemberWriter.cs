#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the wrapper families' inline record writes and their cursor publication boundaries.</summary>
internal static class WrapperMemberWriter
{
    /// <summary>Expands an object-model wrapper member's write body in place.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The wrapper member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    internal static void Write(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends) => Emit(body, member, owner, w, names, ref ends, false);

    /// <summary>Expands a collection-builder member's write body in place.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The builder member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    internal static void WriteBuilder(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends) => Emit(body, member, owner, w, names, ref ends, true);

    /// <summary>Handles emit for <c>WrapperMemberWriter</c>.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="names">The names supplied to <c>Emit</c>.</param>
    /// <param name="ends">The ends supplied to <c>Emit</c>.</param>
    /// <param name="builder">The builder receiving the result.</param>
    private static void Emit(List<string> body, RenderMember member, string owner, CodeWriter w, Dictionary<string, int> names,
        ref int ends, bool builder)
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
        if (builder)
            WrapperTraversal.WriteBuilder(body, member.Scalar, member.TypeName, local);
        else
            WrapperTraversal.Write(body, member.Scalar, local);
        body.Add("operationState.CopyTo(ref writer);");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
