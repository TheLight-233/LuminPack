#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

internal static partial class BinaryEmitter
{
    /// <summary>Recognizes callback-free positional class records whose complete wire width is known.</summary>
    /// <param name="model">The candidate class record.</param>
    /// <returns>Whether the write may reserve and publish its record once.</returns>
    internal static bool CanReserveFixedObject(PayloadRenderModel model) =>
        model.Net8 && !model.IsStruct && model.TypeParameters.Length == 0 && !model.UnionRoot && !model.Circle &&
        model.Rejection == null && model.Version == null && model.Bounded == null && model.Mirror == null &&
        !model.HasCallbacks && model.Rent == null && model.Members.Length != 0 &&
        model.Members.All(member => member.IsFixed && !member.Volatile && !member.Mirrored && !member.Property);

    /// <summary>Writes one fixed class through a single checked reservation and cursor publication.</summary>
    /// <param name="model">The class record.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="count">The one-byte member marker.</param>
    /// <returns>The write statements.</returns>
    private static List<string> ReservedFixedObject(PayloadRenderModel model, CodeWriter w, string count)
    {
        var body = new List<string>
        {
            "var value = input;",
            "ref byte cursor = ref writer.Cursor;",
            "if (value is null)",
            "{",
            "cursor = ref writer.ReserveRecord(ref cursor, 1);",
            "cursor = (byte)255;",
            "writer.Cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, 1);",
            "return;",
            "}",
            "cursor = ref writer.ReserveRecord(ref cursor, " + FixedObjectWidth(model) + ");",
        };
        WriteFixedObject(model, w, body, "value", "cursor", count);
        body.Add("writer.Cursor = ref cursor;");
        return body;
    }

    /// <summary>Returns the complete non-null record width, including its member marker.</summary>
    /// <param name="model">The fixed class record.</param>
    /// <returns>The generated checked width.</returns>
    internal static string FixedObjectWidth(PayloadRenderModel model) =>
        "checked(1 + " + Width(model.Members, 0, model.Members.Length - 1) + ")";

    /// <summary>Stores a complete non-null fixed class at a cursor with reserved room.</summary>
    /// <param name="model">The fixed class record.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="body">The receiving statement list.</param>
    /// <param name="value">The non-null class instance.</param>
    /// <param name="cursor">The cursor over proven writable storage.</param>
    /// <param name="count">The one-byte member marker.</param>
    internal static void WriteFixedObject(PayloadRenderModel model, CodeWriter w, List<string> body, string value, string cursor, string count)
    {
        RenderMember[] members = model.Members;
        int last = members.Length - 1;
        string width = Width(members, 0, last);
        string unsafeType = w.A("Unsafe");
        body.Add(cursor + " = (byte)" + count + ";");
        body.Add(cursor + " = ref " + unsafeType + ".Add(ref " + cursor + ", 1);");
        if (last > 0)
        {
            body.Add("if (" + Proof(w, members, 0, last, value, readOnly: true) + ")");
            body.Add("\t" + w.A("LuminPackMarshal") + ".CopyBlockUnaligned(ref " + cursor + ", ref " +
                Bytes(w, members[0], value, readOnly: true) + ", (uint)(" + width + "));");
            body.Add("else");
            body.Add("{");
        }
        AddFixedStores(w, members, 0, last, cursor, body, value);
        if (last > 0)
            body.Add("}");
        body.Add(cursor + " = ref " + unsafeType + ".Add(ref " + cursor + ", " + width + ");");
    }
}
