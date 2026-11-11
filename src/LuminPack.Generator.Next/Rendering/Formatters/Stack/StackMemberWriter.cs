#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the collection's inline record write.</summary>
internal static class StackMemberWriter
{
    /// <summary>Writes a stack's storage span as one batch, as the original operation does.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="member">The collection member.</param>
    /// <param name="owner">The containing record expression.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="names">The shared local name counters.</param>
    /// <param name="ends">The shared exit label counter.</param>
    internal static void Write(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends)
    {
        string local = BinaryEmitter.RoleName(names, "value");
        string span = BinaryEmitter.RoleName(names, "span");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        BinaryEmitter.AddCollectionNull(body, local, exit, w, reserve: false, countMarker: false);
        body.Add("");
        body.Add("var " + span + " = " + w.A("LuminPackMarshal") + ".GetStackSpan(" + w.A("Unsafe") + ".AsRef(in " + local + "));");
        body.Add(w.A("LuminPackWriter") + ".Write(");
        body.Add("\twriter.Reserve(writer.Position, " + w.A("LuminPackWriter") + ".Size(" + span + ", " + span + ".Length, true, false)),");
        body.Add("\t" + span + ",");
        body.Add("\t" + span + ".Length,");
        body.Add("\ttrue,");
        body.Add("\tfalse,");
        body.Add("\tout var spanOffset);");
        body.Add("writer.Advance(spanOffset);");
        body.Add("writer.CheckBuffer();");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
