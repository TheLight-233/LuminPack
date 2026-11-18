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
            JsonRead, binaryWriteEntries: MeasureRecord);

    /// <summary>Emits the exact width used by allocating binary entries.</summary>
    /// <param name="w">The formatter source writer.</param>
    private static void MeasureRecord(CodeWriter w)
    {
        w.Line(w.Preserve);
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("internal static void MeasureRecord_Version<TLuminProfile>(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref global::System.Version value)");
        w.Line("    where TLuminProfile : struct, " + w.A("ILuminPackOptions"));
        w.Open();
        w.Line("evaluator.Add(value is null ? 1 : 17);");
        w.Close();
        w.Blank();
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("internal static void WriteFixedRoot_Version(ref byte record, global::System.Version value)");
        w.Open();
        w.Line("record = 4;");
        w.Line("LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref record, 1), value.Major);");
        w.Line("LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref record, 5), value.Minor);");
        w.Line("LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref record, 9), value.Build);");
        w.Line("LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref record, 13), value.Revision);");
        w.Close();
        w.Blank();
    }

    /// <summary>Emits the fixed binary record writer.</summary>
    /// <param name="w">The formatter source writer.</param>
    /// <param name="context">The aliases and profile of the owning formatter.</param>
    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNullObjectHeader();");
        w.Line("return;");
        w.Close();
        w.Blank();
        const string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("if (" + unsafeType + ".SizeOf<global::System.IntPtr>() == 8)");
        w.Open();
        w.Line("WriteFixedRoot_Version(ref writer.Cursor, value);");
        w.Line("writer.Advance(17);");
        w.Line("return;");
        w.Close();
        w.Line("#endif");
        w.Line("writer.WriteObjectHeader(4);");
        w.Line("writer.Write(value.Major);");
        w.Line("writer.Write(value.Minor);");
        w.Line("writer.Write(value.Build);");
        w.Line("writer.Write(value.Revision);");
    }

    /// <summary>Emits the checked binary component reads and original BCL construction.</summary>
    /// <param name="w">The formatter source writer.</param>
    /// <param name="context">The aliases and profile of the owning formatter.</param>
    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context)
    {
        const string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
        w.Line("int major, minor, build, revision;");
        w.Line("#if NET8_0_OR_GREATER");
        // The complete window proves both the marker and every component. The fallback preserves short-read cursor progress.
        w.Line("ref byte record = ref reader.PeekInput(17, out bool complete);");
        w.Line("if (" + unsafeType + ".SizeOf<global::System.IntPtr>() == 8 && complete)");
        w.Open();
        w.Line("if (record == global::LuminPack.Code.LuminPackCode.NullObject)");
        w.Open();
        w.Line("reader.Cursor = ref " + unsafeType + ".Add(ref record, 1);");
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Line("reader.Cursor = ref " + unsafeType + ".Add(ref record, 17);");
        w.Line("major = LuminPackReader.Read<int>(ref " + unsafeType + ".Add(ref record, 1));");
        w.Line("minor = LuminPackReader.Read<int>(ref " + unsafeType + ".Add(ref record, 5));");
        w.Line("build = LuminPackReader.Read<int>(ref " + unsafeType + ".Add(ref record, 9));");
        w.Line("revision = LuminPackReader.Read<int>(ref " + unsafeType + ".Add(ref record, 13));");
        w.Close();
        w.Line("else");
        w.Line("#endif");
        w.Open();
        w.Line("if (!reader.TryReadObjectHead())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Line("major = reader.Read<int>();");
        w.Line("minor = reader.Read<int>();");
        w.Line("build = reader.Read<int>();");
        w.Line("revision = reader.Read<int>();");
        w.Close();
        w.Line("if (value is not null && value.Major == major && value.Minor == minor &&");
        w.Line("    value.Build == build && value.Revision == revision)");
        w.Line("    return;");
        w.Line("if (revision == -1)");
        w.Open();
        w.Line("value = build == -1 ? new global::System.Version(major, minor) : new global::System.Version(major, minor, build);");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("value = new global::System.Version(major, minor, build, revision);");
        w.Close();
    }

    /// <summary>Emits the JSON version writer and its compatible text fallback.</summary>
    /// <param name="w">The formatter source writer.</param>
    /// <param name="context">The aliases and profile of the owning formatter.</param>
    private static void JsonWrite(CodeWriter w, SpecialOwnerContext context)
    {
        if (context.Supports("CanWriteJsonVersion"))
        {
            w.Line("writer.WriteVersion<" + context.Profile + ">(value);");
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

    /// <summary>Emits the JSON version reader and its compatible text fallback.</summary>
    /// <param name="w">The formatter source writer.</param>
    /// <param name="context">The aliases and profile of the owning formatter.</param>
    private static void JsonRead(CodeWriter w, SpecialOwnerContext context)
    {
        if (context.Supports("CanReadJsonVersion"))
        {
            w.Line("value = reader.ReadVersion<" + context.Profile + ">();");
            return;
        }
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("value = global::System.Version.Parse(reader.Read<string, " + context.Profile + ">());");
    }
}
