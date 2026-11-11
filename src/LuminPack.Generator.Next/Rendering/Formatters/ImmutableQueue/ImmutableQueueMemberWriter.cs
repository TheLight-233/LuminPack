#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the family's inline record write and its cursor publication boundaries.</summary>
internal static class ImmutableQueueMemberWriter
{
    /// <summary>Expands an immutable-queue member's shared write body in place: the null collection header, then the two-stack
    /// traversal whose backwards stack may be written through a reversal lease.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The immutable-queue member.</param>
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
        string element = member.Scalar!;
        bool boolean = element == "bool";
        // The two traversed stacks and the enumerator fallback each isolate their element alias, as the presentation does.
        string firstAlias = null, secondAlias = null, thirdAlias = null, reversedAlias = null, checkedAlias = null;
        if (boolean)
        {
            firstAlias = BinaryEmitter.RoleName(names, "value");
            secondAlias = BinaryEmitter.RoleName(names, "value");
            thirdAlias = BinaryEmitter.RoleName(names, "value");
            reversedAlias = BinaryEmitter.RoleName(names, "value");
            checkedAlias = BinaryEmitter.RoleName(names, "value");
        }
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
        body.Add(w.A("LuminPackWriter") + " operationStateFallback = default;");
        body.Add("writer.CopyTo(ref operationState);");
        ImmutableQueueTraversal.Write(body, element, local, w, firstAlias, secondAlias, thirdAlias, reversedAlias, checkedAlias);
        body.Add("operationState.CopyTo(ref writer);");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
