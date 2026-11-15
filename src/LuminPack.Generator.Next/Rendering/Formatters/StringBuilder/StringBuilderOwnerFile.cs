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
            BinaryRead, (w, c) => JsonWrite(w, c, set), (w, c) => JsonRead(w, c, set), binaryWriteEntries: MeasureRecord);

    /// <summary>Measures record.</summary>
    /// <param name="w">The generated source writer.</param>
    private static void MeasureRecord(CodeWriter w)
    {
        w.Line(w.Preserve);
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("internal static void MeasureRecord_StringBuilder<TLuminProfile>(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref global::System.Text.StringBuilder value)");
        w.Line("    where TLuminProfile : struct, " + w.A("ILuminPackOptions"));
        w.Open();
        w.Line("evaluator.Add(value is null ? sizeof(int) : checked(sizeof(int) + value.Length * sizeof(char)));");
        w.Close();
        w.Blank();
    }

    /// <summary>Handles binary write for <c>StringBuilderOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    /// <param name="set">The shared formatter owners and publication options.</param>
    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context, SharedFormatterSet set)
    {
        w.Line("if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        w.Line("int length = value.Length;");
        w.Line("int byteCount = checked(length * sizeof(char));");
        w.Line("writer.EnsureAdditionalCapacity(checked(sizeof(int) + byteCount));");
        w.Line("writer.WriteCollectionHeader(length);");
        if (set.FindFormatter("global::System.Text.StringBuilder")?.Supports("CanCopyStringBuilder") == true)
        {
            w.Line("#if NET8_0_OR_GREATER");
            w.Line("var destination = global::System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref global::System.Runtime.CompilerServices.Unsafe.As<byte, char>(ref writer.Cursor), length);");
            w.Line("#else");
            w.Line("var destination = global::System.Runtime.InteropServices.MemoryMarshal.Cast<byte, char>(writer.Buffer.Slice(writer.Position, byteCount));");
            w.Line("#endif");
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

    /// <summary>Handles binary read for <c>StringBuilderOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (!reader.TryReadCollectionHead(out int length))");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("int bytes = checked(length * sizeof(char));");
        w.Line("reader.EnsureCollectionPayload(reader.Position, bytes);");
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
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("var chars = global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref global::System.Runtime.CompilerServices.Unsafe.As<byte, char>(ref reader.Cursor), length);");
        w.Line("#else");
        w.Line("var chars = global::System.Runtime.InteropServices.MemoryMarshal.Cast<byte, char>(reader.Buffer.Slice(reader.Position, bytes));");
        w.Line("#endif");
        w.Line("value.Append(chars);");
        w.Line("reader.Advance(bytes);");
    }

    /// <summary>Handles JSON write for <c>StringBuilderOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    /// <param name="set">The shared formatter owners and publication options.</param>
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

    /// <summary>Handles JSON read for <c>StringBuilderOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    /// <param name="set">The shared formatter owners and publication options.</param>
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
