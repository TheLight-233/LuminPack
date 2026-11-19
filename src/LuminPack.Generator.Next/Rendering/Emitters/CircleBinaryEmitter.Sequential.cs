#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

internal static partial class CircleBinaryEmitter
{
    /// <summary>Writes sparse, runtime-sized or interleaved slots through the original one-pass framing operations.</summary>
    /// <param name="model">The reference-tracked payload.</param>
    /// <param name="w">The source writer.</param>
    /// <returns>The generated operation statements.</returns>
    /// <param name="header">Receives the null and reference exits before the record.</param>
    private static List<string> SequentialWriteBody(PayloadRenderModel model, CodeWriter w, List<string> header)
    {
        var body = new List<string>();
        if (model.CircleGuarded)
            header.Add(w.A("LuminPackExceptionHelper") + ".EnsureSufficientStack();");
        header.Add("var value = input;");
        header.AddRange(model.WriteBefore);
        header.Add("if (value is null)");
        header.Add("{");
        header.Add("writer.WriteNullObjectHeader();");
        header.Add("return;");
        header.Add("}");
        header.Add("int reference = writer.OptionState.TrackReference(value);");
        header.Add("if (reference < 0)");
        header.Add("{");
        header.Add("writer.WriteObjectReferenceId((uint)~reference);");
        header.Add("return;");
        header.Add("}");
        if (model.Members.Any(member => member.Mirrored))
            body.Add("ref var local = ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref " +
                w.A("Unsafe") + ".AsRef(in value));");
        int slotCount = model.CircleSlotCount;
        body.Add("writer.EnsureAdditionalCapacity(" + WidthText(slotCount + 6) + ");");
        body.Add("writer.WriteObjectHeader(" + WidthText(slotCount) + ");");
        int nextField = 0;
        for (int slot = 0; slot < slotCount; slot++)
        {
            int index = nextField < model.CircleSlots.Length && model.CircleSlots[nextField] == slot ? nextField++ : -1;
            string width = index < 0 ? "0" : WidthText(model.CircleWidths[index]);
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
                int last = FixedRunEnd(model, index, model.Members.Length - 1, read: false, out int runWidth);
                if (last > index)
                {
                    string owner = member.Mirrored ? "local" : "value";
                    body.Add("writer.EnsureAdditionalCapacity(" + WidthText(runWidth) + ");");
                    body.Add("if (" + BinaryEmitter.Proof(w, model.Members, index, last, owner, readOnly: true) + ")");
                    body.Add("\twriter.Write(ref " + BinaryEmitter.Bytes(w, member, owner, readOnly: true) + ", " + WidthText(runWidth) + ");");
                    body.Add("else");
                    body.Add("{");
                    for (int fieldIndex = index; fieldIndex <= last; fieldIndex++)
                        body.Add("writer.Write(in " + Access(model.Members[fieldIndex]) + ");");
                    body.Add("}");
                    body.Add("}");
                    index = last;
                    continue;
                }
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
