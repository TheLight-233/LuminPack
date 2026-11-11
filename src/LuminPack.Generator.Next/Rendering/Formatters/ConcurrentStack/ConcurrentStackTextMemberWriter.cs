#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the concurrent stack's inline record write for string elements.</summary>
internal static class ConcurrentStackTextMemberWriter
{
    /// <summary>Wraps the raw member writer so a string element writes its reversed snapshot through an isolated operation state.</summary>
    /// <param name="raw">The family's raw member writer.</param>
    /// <returns>The member writer used by records.</returns>
    internal static WriteFormatterMember Write(WriteFormatterMember raw) =>
        (List<string> body, RenderMember member, string owner, int depth, CodeWriter w, Dictionary<string, int> names,
            ref int ends, ref int cursors, ref int spans) =>
        {
            if (member.Scalar != "string")
            {
                raw(body, member, owner, depth, w, names, ref ends, ref cursors, ref spans);
                return;
            }
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
            ConcurrentStackTextOwnerFile.WriteLines(body, local);
            body.Add("operationState.CopyTo(ref writer);");
            body.Add(exit + ":");
            body.Add("\t;");
            body.Add("}");
        };
}
