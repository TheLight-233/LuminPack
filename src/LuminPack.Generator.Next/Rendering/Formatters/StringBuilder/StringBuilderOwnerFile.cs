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
        SpecialOwnerFile.Render(set, operations, "StringBuilder", "global::System.Text.StringBuilder", true, (w, c) => BinaryWrite(w, c, set),
            BinaryRead, (w, c) => JsonWrite(w, c, set), (w, c) => JsonRead(w, c, set));

    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context, SharedFormatterSet set)
    {
        w.Line("if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        w.Line("int length = value.Length;");
        w.Line("int byteCount = checked(length * sizeof(char));");
        w.Line("writer.EnsureAdditionalCapacity(checked(sizeof(int) + byteCount));");
        w.Line("writer.WriteCollectionHeader(length);");
        if (set.FindFormatter("global::System.Text.StringBuilder")?.Supports("CanCopyStringBuilder") == true)
        {
            w.Line("var destination = global::System.Runtime.InteropServices.MemoryMarshal.Cast<byte, char>(writer.Buffer.Slice(writer.Position, byteCount));");
            w.Line("value.CopyTo(0, destination, length);");
            w.Line("writer.Advance(byteCount);");
        }
        else
        {
            w.Line("#if NET8_0_OR_GREATER");
            w.Line("foreach (var chunk in value.GetChunks())");
            w.Open();
            w.Line("var bytes = global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(chunk.Span);");
            w.Line("writer.Write(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(bytes), bytes.Length);");
            w.Close();
            w.Line("#else");
            w.Line("var bytes = global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(value.ToString().AsSpan());");
            w.Line("writer.Write(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(bytes), bytes.Length);");
            w.Line("#endif");
        }
        w.Line("writer.CheckBuffer();");
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
        w.Line("int bytes = checked(length * sizeof(char));");
        w.Line("var chars = global::System.Runtime.InteropServices.MemoryMarshal.Cast<byte, char>(reader.Buffer.Slice(reader.Position, bytes));");
        w.Line("value.Append(chars);");
        w.Line("reader.Advance(bytes);");
    }

    private static void JsonWrite(CodeWriter w, SpecialOwnerContext context, SharedFormatterSet set)
    {
        if (set.FindFormatter("global::System.Text.StringBuilder")?.Supports("CanWriteJsonStringBuilder") == true)
        {
            w.Line("writer.WriteStringBuilder<" + context.Profile + ">(value);");
            return;
        }
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNull<" + context.Profile + ">();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.Write<" + context.Profile + ">(value.ToString());");
    }

    private static void JsonRead(CodeWriter w, SpecialOwnerContext context, SharedFormatterSet set)
    {
        if (set.FindFormatter("global::System.Text.StringBuilder")?.Supports("CanReadJsonStringBuilder") == true)
        {
            w.Line("reader.ReadStringBuilder<" + context.Profile + ">(ref value);");
            return;
        }
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
