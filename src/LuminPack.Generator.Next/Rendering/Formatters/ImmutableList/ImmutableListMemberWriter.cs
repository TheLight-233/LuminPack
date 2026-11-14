#nullable disable
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Routes raw immutable-list members to their shared formatter.</summary>
internal static class ImmutableListMemberWriter
{
    /// <summary>Writes a raw immutable-list member through its profile-independent binary owner.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The immutable-list member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    internal static void Write(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends)
    {
        body.Add(member.BinaryFormatter + ".Write(ref writer, " + owner + "." + member.Name + ");");
    }
}
