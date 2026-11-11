using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Uri</c>.</summary>
/// <remarks>The binary record stores the original string behind a memberless object header; the JSON form reads and writes
/// the same string directly.</remarks>
internal static class UriOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations) =>
        SpecialOwnerFile.Render(set, operations, "Uri", "global::System.Uri", true, BinaryWrite, BinaryRead, JsonWrite, JsonRead);

    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNullObjectHeader();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.WriteObjectHeader(0);");
        w.Line("writer.Write<" + context.Profile + ">(value.OriginalString);");
    }

    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (!reader.TryReadObjectHead())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var str = reader.Read<" + context.Profile + ">(null);");
        w.Line("value = str == null ? null : new global::System.Uri(str, global::System.UriKind.RelativeOrAbsolute);");
    }

    private static void JsonWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNull<" + context.Profile + ">();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.Write<" + context.Profile + ">(value.OriginalString);");
    }

    private static void JsonRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("value = new global::System.Uri(reader.Read<string, " + context.Profile + ">() ?? string.Empty, global::System.UriKind.RelativeOrAbsolute);");
    }
}
