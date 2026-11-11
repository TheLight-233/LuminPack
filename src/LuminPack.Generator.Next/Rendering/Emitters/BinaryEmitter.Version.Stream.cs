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

    /// <summary>Groups adjacent raw slots while retaining each variable field's length boundary.</summary>
    /// <param name="model">The complete versioned contract.</param>
    /// <param name="first">The first raw member.</param>
    /// <returns>The last raw member in this run.</returns>
    private static int VersionRunEnd(PayloadRenderModel model, int first)
    {
        int last = first;
        if (!model.Members[first].IsFixed || model.Members[first].Volatile || model.Version.Widths[first] is 0 or 255)
            return last;
        while (last + 1 < model.Members.Length && model.Members[last + 1].IsFixed && !model.Members[last + 1].Volatile && model.Version.Widths[last + 1] is > 0 and < 255)
            last++;
        return last;
    }

    /// <summary>Writes revision-2 fields with the legacy one-pass length back-patches and concrete child operations.</summary>
    /// <param name="model">The complete versioned contract.</param>
    /// <param name="w">The source writer.</param>
    /// <returns>The generated method statements.</returns>
    private static List<string> VersionStreamWriteBody(PayloadRenderModel model, CodeWriter w)
    {
        var body = new List<string> { "var value = input;" };
        if (!model.IsStruct)
        {
            body.Add("if (value is null)");
            body.Add("{");
            body.Add("writer.Cursor = " + w.A("LuminPackCode") + ".NullObject;");
            body.Add("writer.Advance(1);");
            body.Add("return;");
            body.Add("}");
            body.Add("");
        }
        string profile = model.DeclaredProfile == null ? Profile : ScopedProfile(w, model.DeclaredProfile);
        body.Add(model.Version.Schema + ".WritePrefix<" + profile + ">(ref writer);");
        for (int index = 0; index < model.Members.Length; index++)
        {
            RenderMember member = model.Members[index];
            int width = model.Version.Widths[index];
            string target = "value." + member.Name;
            body.Add("{");
            if (width == 255)
            {
                body.Add("int fieldLength = writer.ReserveVersionLength();");
                AddMemberWrite(body, model, member, w, target);
                body.Add("writer.CompleteVersionLength(fieldLength);");
            }
            else
            {
                string size = width == 0 ? w.A("Unsafe") + ".SizeOf<" + member.TypeName + ">()" : width.ToString(CultureInfo.InvariantCulture);
                if (width == 0)
                    body.Add("if (" + size + " >= 255) writer.WriteVersionLength(" + size + ");");
                body.Add("writer.EnsureAdditionalCapacity(" + size + ");");
                body.Add("writer.Write(in " + target + ");");
            }
            body.Add("}");
        }
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
            body.Add(w.A("LuminPackSerializer") + ".Serialize" + MemberArguments(member, profile) + "(ref writer, in " + target + ");");
        else
            body.Add(member.Formatter + ".Write<" + profile + ">(ref writer, in " + target + ");");
    }
}
