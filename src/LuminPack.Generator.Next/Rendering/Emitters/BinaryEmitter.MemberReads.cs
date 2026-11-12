#nullable disable
using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

internal static partial class BinaryEmitter
{
    /// <summary>Reads a staged or reused member through its existing scalar or concrete static owner contract.</summary>
    /// <param name="body">The receiving method statements.</param>
    /// <param name="model">The enclosing payload facts.</param>
    /// <param name="member">The member's compiled operation.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="state">The reader expression.</param>
    /// <param name="target">The addressable destination expression.</param>
    internal static void AddMemberRead(List<string> body, PayloadRenderModel model, RenderMember member, CodeWriter w,
        string state, string target)
    {
        string profile = ReadProfile(model, w, member);
        if (member.Kind == RenderMemberKind.Text)
        {
            body.Add(state + ".Read<" + profile + ">(ref " + target + ");");
            return;
        }
        if (member.IsFixed)
        {
            body.Add(target + " = " + (member.IsBoolean && !model.Trusted
                ? w.A("LuminPackInputPolicy") + ".ReadBoolean(ref " + state + ")"
                : state + ".Read<" + member.Scalar + ">()") + ";");
            return;
        }
        if (member.Kind == RenderMemberKind.Parameter)
        {
            AddParameterReset(body, member, w, state, target);
            body.Add(w.A("LuminPackSerializer") + ".Deserialize" + MemberArguments(member, profile) + "(ref " + state + ", ref " + target + "!);");
            return;
        }
        if (member.Kind == RenderMemberKind.Array && member.Scalar == "bool" && !model.Trusted)
        {
            body.Add(w.A("LuminPackInputPolicy") + ".ReadBooleans(ref " + state + ", ref " + target + "!);");
            AddCollectionCheck(body, member, target);
            return;
        }
        // As the original object read, a null marker clears the member and the member's own read still runs, raising its
        // deserializing callbacks before it consumes the marker.
        if (member.Kind == RenderMemberKind.Object || member.NullObjectGuard)
        {
            body.Add("if (" + state + ".PeekIsNullObject())");
            body.Add("\t" + target + " = default!;");
        }
        body.Add(member.BinaryFormatter + ".Read<" + profile + ">(ref " + state + ", ref " + target + "!);");
        AddCollectionCheck(body, member, target);
    }
}
