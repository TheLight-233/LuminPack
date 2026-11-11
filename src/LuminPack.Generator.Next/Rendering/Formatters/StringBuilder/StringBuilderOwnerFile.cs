using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Text.StringBuilder</c>.</summary>
/// <remarks>The binary record is the counted UTF-16 character payload; the JSON form is the builder text. An existing
/// instance is cleared and reused, which keeps the legacy pooling behavior.</remarks>
internal static class StringBuilderOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations) =>
        SpecialOwnerFile.Render(set, operations, "StringBuilder", "global::System.Text.StringBuilder", true, BinaryWrite,
            BinaryRead, JsonWrite, JsonRead);

    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value is null) { writer.WriteNullCollectionHeader(); return; }");
        w.Line("int length = value.Length;");
        w.Line("string text = value.ToString();");
        w.Line("writer.Write<char>(text.AsSpan(), length);");
    }

    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (!reader.TryReadCollectionHead(out int length))");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("reader.EnsureCollectionPayload(reader.Position, checked(length * sizeof(char)));");
        w.Line("if (value is null)");
        w.Open();
        w.Line("value = new global::System.Text.StringBuilder(length);");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("value.Clear();");
        w.Line("value.EnsureCapacity(length);");
        w.Close();
        w.Blank();
        w.Line("if (length != 0)");
        w.Open();
        w.Line("var chars = new char[length];");
        w.Line("reader.Read<char>(chars);");
        w.Line("value.Append(chars, 0, length);");
        w.Close();
    }

    private static void JsonWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNull<" + context.Profile + ">();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.Write<" + context.Profile + ">(value.ToString());");
    }

    private static void JsonRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var str = reader.Read<string, " + context.Profile + ">();");
        w.Line("if (value == null)");
        w.Open();
        w.Line("value = new global::System.Text.StringBuilder(str);");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("value.Clear();");
        w.Line("value.Append(str);");
        w.Close();
    }
}
