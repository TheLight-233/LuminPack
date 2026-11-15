using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.TimeZoneInfo</c>.</summary>
/// <remarks>Both forms store the runtime's serialized snapshot text, which the cache resolves back to a cached instance on
/// read. A failed cache lookup falls back to parsing the snapshot.</remarks>
internal static class TimeZoneInfoOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations) =>
        SpecialOwnerFile.Render(set, operations, "TimeZoneInfo", "global::System.TimeZoneInfo", true, BinaryWrite,
            BinaryRead, JsonWrite, JsonRead);

    /// <summary>Handles binary write for <c>TimeZoneInfoOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("string? text = value is null ? null : global::LuminPack.Code.LuminPackTimeZoneInfoCache.GetText(value);");
        w.Line("writer.WriteTimeZoneText<" + context.Profile + ">(text);");
    }

    /// <summary>Handles binary read for <c>TimeZoneInfoOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (!reader.TryReadObjectHead())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var candidate = reader.GetTimeZoneCandidate<" + context.Profile + ">();");
        w.Line("if (reader.TryReadTimeZoneInfo<" + context.Profile + ">(candidate, out var cached))");
        w.Open();
        w.Line("value = cached;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var source = reader.Read<" + context.Profile + ">(candidate?.Text);");
        w.Line("if (string.IsNullOrEmpty(source))");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("value = global::LuminPack.Code.LuminPackTimeZoneInfoCache.Parse(source, candidate);");
    }

    /// <summary>Handles JSON write for <c>TimeZoneInfoOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    private static void JsonWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNull<" + context.Profile + ">();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.Write<" + context.Profile + ">(global::LuminPack.Code.LuminPackTimeZoneInfoCache.GetText(value));");
    }

    /// <summary>Handles JSON read for <c>TimeZoneInfoOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    private static void JsonRead(CodeWriter w, SpecialOwnerContext context)
    {
        // The original read returns a JSON null before either path.
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        if (context.Supports("CanReadJsonTimeZoneInfo"))
        {
            w.Close();
            w.Blank();
            w.Line("value = reader.ReadTimeZoneInfo<" + context.Profile + ">();");
            return;
        }
        w.Close();
        w.Blank();
        w.Line("var source = reader.Read<string, " + context.Profile + ">();");
        w.Line("value = global::LuminPack.Code.LuminPackTimeZoneInfoCache.Parse(source);");
    }
}
