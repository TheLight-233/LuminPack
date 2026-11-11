#nullable disable
using System.Collections.Generic;
using System.Globalization;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Owns the collection's inline record write.</summary>
internal static class SortedSetMemberWriter
{
    /// <summary>Writes a sorted set of raw scalars through the tree storage helper.</summary>
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
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        BinaryEmitter.AddCollectionNull(body, local, exit, w, reserve: true, countMarker: false);
        body.Add("");
        body.Add("global::LuminPack.Utility.LuminTreeStorage.WriteRaw(ref writer, " + local + ");");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }
}
