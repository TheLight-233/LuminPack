#nullable disable
using System.Collections.Generic;
using System.Globalization;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

internal static partial class BinaryEmitter
{
    /// <summary>Retains complete-record capacity and completion behavior around a one-pass write.</summary>
    /// <param name="model">The declaration-owned bound.</param>
    /// <param name="body">The root write statements.</param>
    /// <returns>The capacity-guarded write.</returns>
    internal static List<string> ApplyBoundedWrite(PayloadRenderModel model, List<string> body)
    {
        if (model.Bounded == null)
            return body;
        int value = body.FindIndex(line => line == "var value = input;");
        body.InsertRange(value + 1, new[] { "int boundedStart = writer.Position;",
            "writer.EnsureAdditionalCapacity(" + model.Bounded.Value.ToString(CultureInfo.InvariantCulture) + ");" });
        for (int index = 0; index < body.Count; index++)
            if (body[index] == "return;")
                body[index] = "goto end0;";
        AddBoundExit(body, model, union: model.IsStruct);
        return body;
    }

    /// <summary>Calls an existing member operation from a state-published variable field boundary.</summary>
    /// <param name="body">The receiving statement list.</param>
    /// <param name="model">The enclosing declaration.</param>
    /// <param name="member">The concrete member operation.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="target">The addressable input expression.</param>
    internal static void AddMemberWrite(List<string> body, PayloadRenderModel model, RenderMember member, CodeWriter w, string target)
    {
        string profile = WriteProfile(model, w, member);
        body.Add("writer.CheckBuffer();");
        if (member.IsFixed)
        {
            body.Add("writer.EnsureAdditionalCapacity(" + w.A("Unsafe") + ".SizeOf<" + member.TypeName + ">());");
            body.Add("writer.Write(in " + target + ");");
        }
        else if (member.Kind == RenderMemberKind.Text)
            body.Add("writer.Write<" + profile + ">(" + target + ");");
        else if (member.Kind == RenderMemberKind.Parameter)
            body.Add(w.A("LuminPackSerializer") + ".FormatterDispatch.Serialize" + MemberArguments(member, profile) + "(ref writer, in " + target + ");");
        else
            body.Add(member.Formatter + ".Write<" + profile + ">(ref writer, in " + target + ");");
    }
}
