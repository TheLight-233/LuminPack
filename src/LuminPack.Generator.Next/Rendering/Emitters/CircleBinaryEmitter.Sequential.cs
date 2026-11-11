#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

internal static partial class CircleBinaryEmitter
{
    /// <summary>Writes sparse, runtime-sized or interleaved slots through the original one-pass framing operations.</summary>
    /// <param name="model">The reference-tracked payload.</param>
    /// <param name="w">The source writer.</param>
    /// <returns>The generated operation statements.</returns>
    private static List<string> SequentialWriteBody(PayloadRenderModel model, CodeWriter w)
    {
        var body = new List<string>();
        if (model.CircleGuarded)
            body.Add(w.A("LuminPackExceptionHelper") + ".EnsureSufficientStack();");
        body.Add("var value = input;");
        body.AddRange(model.WriteBefore);
        body.Add("if (value is null)");
        body.Add("{");
        body.Add("writer.WriteNullObjectHeader();");
        body.Add("return;");
        body.Add("}");
        if (model.Members.Any(member => member.Mirrored))
            body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref " +
                w.A("Unsafe") + ".AsRef(in value));");
        body.Add("int reference = writer.OptionState.TrackReference(value);");
        body.Add("if (reference < 0)");
        body.Add("{");
        body.Add("writer.WriteObjectReferenceId((uint)~reference);");
        body.Add("return;");
        body.Add("}");
        body.Add("writer.EnsureAdditionalCapacity(" + (model.CircleSlotCount + 6).ToString(CultureInfo.InvariantCulture) + ");");
        body.Add("writer.WriteObjectHeader(" + model.CircleSlotCount.ToString(CultureInfo.InvariantCulture) + ");");
        for (int slot = 0; slot < model.CircleSlotCount; slot++)
        {
            int index = Array.IndexOf(model.CircleSlots, slot);
            string width = index < 0 ? "0" : model.CircleWidths[index].ToString(CultureInfo.InvariantCulture);
            if (index >= 0 && model.CircleWidths[index] == 0)
            {
                string size = w.A("Unsafe") + ".SizeOf<" + model.Members[index].TypeName + ">()";
                width = "(" + size + " < 255 ? " + size + " : 255)";
            }
            body.Add("writer.Write((byte)" + width + ");");
        }
        body.Add("writer.WriteVarInt((uint)reference);");
        for (int index = 0; index < model.Members.Length; index++)
        {
            RenderMember member = model.Members[index];
            string target = Access(member);
            body.Add("{");
            if (model.CircleWidths[index] == 255)
            {
                body.Add("int fieldLength = writer.ReserveVersionLength();");
                BinaryEmitter.AddMemberWrite(body, model, member, w, target);
                body.Add("writer.CompleteVersionLength(fieldLength);");
            }
            else
            {
                string width = w.A("Unsafe") + ".SizeOf<" + member.TypeName + ">()";
                if (model.CircleWidths[index] == 0)
                    body.Add("if (" + width + " >= 255) writer.WriteVersionLength(" + width + ");");
                body.Add("writer.EnsureAdditionalCapacity(" + width + ");");
                body.Add("writer.Write(in " + target + ");");
            }
            body.Add("}");
        }
        body.AddRange(model.WriteAfter);
        return body;
    }

    /// <summary>Creates a reference shell before decoding members so cycles can resolve the registered instance.</summary>
    /// <param name="model">The analyzed construction contract.</param>
    /// <returns>The existing rental or selected-constructor expression.</returns>
    internal static string NewInstance(PayloadRenderModel model) => model.Rent ?? "new " + model.GlobalName + "(" +
        (model.ConstructorArguments == null ? string.Empty : string.Join(", ", model.ConstructorArguments.Select(_ => "default!"))) + ")";
}
