using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Version</c>.</summary>
/// <remarks>The binary record stores four raw components behind a four-member object header; the JSON form round-trips the
/// version through its invariant text.</remarks>
internal static class VersionOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations) =>
        SpecialOwnerFile.Render(set, operations, "Version", "global::System.Version", true, BinaryWrite, BinaryRead, JsonWrite,
            JsonRead);

    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNullObjectHeader();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.WriteObjectHeader(4);");
        w.Line("writer.Write(value.Major);");
        w.Line("writer.Write(value.Minor);");
        w.Line("writer.Write(value.Build);");
        w.Line("writer.Write(value.Revision);");
    }

    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (!reader.TryReadObjectHead())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("int major = reader.Read<int>();");
        w.Line("int minor = reader.Read<int>();");
        w.Line("int build = reader.Read<int>();");
        w.Line("int revision = reader.Read<int>();");
        w.Line("if (revision == -1)");
        w.Open();
        w.Line("value = build == -1 ? new global::System.Version(major, minor) : new global::System.Version(major, minor, build);");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("value = new global::System.Version(major, minor, build, revision);");
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
        w.Line("value = global::System.Version.Parse(reader.Read<string, " + context.Profile + ">());");
    }
}
