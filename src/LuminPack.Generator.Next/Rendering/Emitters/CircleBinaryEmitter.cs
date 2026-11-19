#nullable disable
using System.Collections.Generic;
using System.Globalization;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Wraps a positional or version-tolerant record with its operation's object identity.</summary>
internal static class CircleBinaryEmitter
{
    /// <summary>Writes null and back references inline and forwards a first occurrence to its record body.</summary>
    internal static List<string> WriteHeader(PayloadRenderModel model, CodeWriter w, string record)
    {
        var body = new List<string> { "var value = input;" };
        body.AddRange(model.WriteBefore);
        body.Add("if (value is null)");
        body.Add("{");
        body.Add("writer.EnsureAdditionalCapacity(1);");
        body.Add("writer.Cursor = " + w.A("LuminPackCode") + ".NullObject;");
        body.Add("writer.Advance(1);");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        body.Add("int reference = writer.OptionState.TrackReference(value);");
        body.Add("if (reference < 0)");
        body.Add("{");
        body.Add("writer.WriteObjectReferenceId((uint)~reference);");
        body.Add("return;");
        body.Add("}");
        body.Add(record + "(ref writer, value, reference);");
        return body;
    }

    /// <summary>Writes the member-count marker and explicit identifier of a first occurrence.</summary>
    internal static void PrependNewHeader(PayloadRenderModel model, List<string> body, string room)
    {
        string count = model.Members.Length.ToString(CultureInfo.InvariantCulture);
        body.InsertRange(0, new[]
        {
            "writer.EnsureAdditionalCapacity(" + room + ");",
            "writer.WriteObjectHeader(" + count + ");",
            "writer.WriteVarInt((uint)reference);",
        });
    }

    /// <summary>Resolves a back reference through the checked inlined header from the reference writer and reader.</summary>
    internal static List<string> ReadHeader(PayloadRenderModel model, CodeWriter w, string record)
    {
        var body = new List<string>();
        body.AddRange(model.ReadBefore);
        body.Add("reader.EnsureReadable(1);");
        body.Add("if (reader.PeekInput(1, out _) == " + w.A("LuminPackCode") + ".ReferenceId)");
        body.Add("{");
        body.Add("reader.Advance(1);");
        body.Add("value = " + w.A("LuminPackInputPolicy") + ".ReadReference<" + model.GlobalName +
            ">(reader.OptionState.GetObjectReference(reader.ReadVarIntUInt32()));");
        body.Add("return;");
        body.Add("}");
        body.Add(record + "(ref reader, ref value);");
        return body;
    }

    /// <summary>Registers an instance before reading members so that a cycle can resolve that instance.</summary>
    internal static void ReadNewRecord(PayloadRenderModel model, CodeWriter w, List<string> body, string record)
    {
        string exceptions = w.A("LuminPackExceptionHelper");
        body.Add("if (!reader.TryReadObjectHead(out byte marker))");
        body.Add("{");
        body.Add("value = null;");
        body.Add("return;");
        body.Add("}");
        body.Add("if (marker > 249)");
        body.Add("\t" + exceptions + ".ThrowFormatException(\"Invalid object reference header.\");");
        body.Add("uint id = reader.ReadVarIntUInt32();");

        if (model.Version == null)
        {
            if (model.SequenceBase == null)
            {
                body.Add("reader.EnsureReadable(1);");
                body.Add("if (reader.PeekInput(1, out _) == " + w.A("LuminPackCode") + ".NullObject)");
            }
            else
            {
                body.Add("reader.EnsureReadable(4);");
                body.Add("if (" + w.A("LuminPackReader") + ".Read<int>(ref reader.PeekInput(4, out _)) == -1)");
            }
            body.Add("\t" + exceptions + ".ThrowFormatException(\"A reference definition must contain a non-null record.\");");
        }

        body.Add("var instance = value;");
        body.Add("if (instance is null || !reader.OptionState.ClaimReference(id, instance))");
        body.Add("{");
        if (model.RentByRef)
        {
            body.Add("ref var rented = ref " + model.Rent + ";");
            body.Add("instance = rented;");
        }
        else
            body.Add("instance = " + NewInstance(model) + ";");
        body.Add("reader.OptionState.AddObjectReference(id, instance);");
        body.Add("}");
        body.Add(record + "(ref reader, ref instance);");
        body.Add("value = instance;");
    }

    /// <summary>Creates the reference shell using the declaration's pool or constructor.</summary>
    internal static string NewInstance(PayloadRenderModel model) => model.Rent ?? model.ReferenceConstruction;
}
