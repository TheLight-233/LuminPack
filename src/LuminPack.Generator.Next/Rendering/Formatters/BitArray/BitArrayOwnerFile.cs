using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Collections.BitArray</c>.</summary>
/// <remarks>The binary record is the word-rounded bit storage behind the bit count; the JSON form is an array of booleans.</remarks>
internal static class BitArrayOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations) =>
        SpecialOwnerFile.Render(set, operations, "BitArray", "global::System.Collections.BitArray", true, BinaryWrite,
            BinaryRead, JsonWrite, JsonRead, outlineJson: true, binaryWriteEntries: MeasureRecord);

    /// <summary>Measures record.</summary>
    /// <param name="w">The generated source writer.</param>
    private static void MeasureRecord(CodeWriter w)
    {
        w.Line(w.Preserve);
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("internal static void MeasureRecord_BitArray<TLuminProfile>(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref global::System.Collections.BitArray value)");
        w.Line("    where TLuminProfile : struct, " + w.A("ILuminPackOptions"));
        w.Open();
        w.Line("int words = value is null ? 0 : (int)(((uint)value.Length + 31) >> 5);");
        w.Line("evaluator.Add(sizeof(int) + words * sizeof(int));");
        w.Close();
        w.Blank();
    }

    /// <summary>Handles binary write for <c>BitArrayOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value is null) { writer.EnsureAdditionalCapacity(sizeof(int)); writer.WriteNullCollectionHeader(); return; }");
        w.Line("int length = value.Length;");
        w.Line("int words = (int)(((uint)length + 31) >> 5);");
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Code.BitArrayView>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        w.Line("#if NET10_0_OR_GREATER");
        w.Line("writer.Write<byte>(new global::System.ReadOnlySpan<byte>(view.m_array, 0, unchecked(words * sizeof(int))), length);");
        w.Line("#else");
        w.Line("writer.Write<int>(new global::System.ReadOnlySpan<int>(view.m_array, 0, words), length);");
        w.Line("#endif");
        w.Line("#else");
        if (context.Supports("CanWriteBitArrayPayload"))
            w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteBitArrayPayload(ref writer, value)) return;");
        w.Line("var storage = words == 0 ? null : global::System.Buffers.ArrayPool<int>.Shared.Rent(words);");
        w.Line("try { if (words != 0) value.CopyTo(storage, 0); writer.Write<int>(new global::System.ReadOnlySpan<int>(storage, 0, words), length); }");
        w.Line("finally { if (storage is not null) global::System.Buffers.ArrayPool<int>.Shared.Return(storage); }");
        w.Line("#endif");
    }

    /// <summary>Handles binary read for <c>BitArrayOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("int length = reader.ReadLength(0);");
        w.Line("if (length < 0) { value = null; return; }");
        w.Line("int words = (int)(((uint)length + 31) >> 5);");
        w.Line("int bytes = unchecked(words * sizeof(int));");
        w.Line("reader.EnsureCollectionPayload(reader.Position, bytes);");
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("if (value is null || value.Length != length) value = new global::System.Collections.BitArray(length, false);");
        w.Line("ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Code.BitArrayView>(ref value);");
        w.Line("#if NET10_0_OR_GREATER");
        w.Line("var payload = new global::System.Span<byte>(view.m_array, 0, bytes);");
        w.Line("ref byte first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(payload);");
        w.Line("if (bytes != 0) reader.Read(ref first, bytes);");
        w.Line("int extraBits = length & 31;");
        w.Line("if (extraBits != 0)");
        w.Open();
        w.Line("ref byte lastWord = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, (nint)(uint)(bytes - sizeof(int)));");
        w.Line("int mask = (int)((1u << extraBits) - 1);");
        w.Line("int last = global::LuminPack.Core.LuminPackReader.Read<int>(ref lastWord);");
        w.Line("global::System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref lastWord, last & mask);");
        w.Close();
        w.Line("#else");
        w.Line("var payload = new global::System.Span<int>(view.m_array, 0, words);");
        w.Line("ref int first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(payload);");
        w.Line("if (bytes != 0) reader.Read(ref global::System.Runtime.CompilerServices.Unsafe.As<int, byte>(ref first), bytes);");
        w.Line("#endif");
        w.Line("#else");
        if (context.Supports("CanCreateBitArrayPayload"))
        {
            w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryCreateBitArrayPayload(length, out var fresh, out var payload))");
            w.Open();
            w.Line("reader.Read<byte>(payload);");
            w.Line("fresh!.Length = length;");
            w.Line("value = fresh;");
            w.Line("return;");
            w.Close();
        }
        w.Line("var storage = words == 0 ? global::System.Array.Empty<int>() : new int[words];");
        w.Line("reader.Read<int>(new global::System.Span<int>(storage));");
        w.Line("value = new global::System.Collections.BitArray(storage) { Length = length };");
        w.Line("#endif");
    }

    /// <summary>Handles JSON write for <c>BitArrayOwnerFile</c>.</summary>
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
        w.Line("writer.WriteArrayStart<" + context.Profile + ">();");
        w.Line("if (value.Count != 0)");
        w.Open();
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("writer.Write<" + context.Profile + ">(value[index]);");
        w.Close();
        w.Line("while (++index < value.Count);");
        w.Close();
        w.Line("writer.WriteArrayEnd<" + context.Profile + ">();");
    }

    /// <summary>Handles JSON read for <c>BitArrayOwnerFile</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The context in scope for the operation.</param>
    private static void JsonRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("reader.TryConsumeArrayStart<" + context.Profile + ">();");
        w.Line("int count = reader.CountArrayElements<" + context.Profile + ">();");
        w.Line("value = new global::System.Collections.BitArray(count);");
        w.Line("if (count != 0)");
        w.Open();
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("reader.Read<" + context.Profile + ">();");
        w.Line("value[index] = reader.Read<bool, " + context.Profile + ">();");
        w.Close();
        w.Line("while (++index < count);");
        w.Close();
        w.Line("reader.ConsumeArrayEnd<" + context.Profile + ">();");
    }
}
