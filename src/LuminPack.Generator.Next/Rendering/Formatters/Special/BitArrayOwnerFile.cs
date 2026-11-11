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
            BinaryRead, JsonWrite, JsonRead);

    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value is null) { writer.WriteNullCollectionHeader(); return; }");
        w.Line("int length = value.Length;");
        w.Line("int words = (int)(((long)length + 31) >> 5);");
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Code.BitArrayView>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        w.Line("#if NET10_0_OR_GREATER");
        w.Line("writer.Write<byte>(view.m_array.AsSpan(0, unchecked(words * sizeof(int))), length);");
        w.Line("#else");
        w.Line("writer.Write<int>(view.m_array.AsSpan(0, words), length);");
        w.Line("#endif");
        w.Line("#else");
        w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteBitArrayPayload(ref writer, value)) return;");
        w.Line("var storage = words == 0 ? null : global::System.Buffers.ArrayPool<int>.Shared.Rent(words);");
        w.Line("try { if (words != 0) value.CopyTo(storage, 0); writer.Write<int>(storage.AsSpan(0, words), length); }");
        w.Line("finally { if (storage is not null) global::System.Buffers.ArrayPool<int>.Shared.Return(storage); }");
        w.Line("#endif");
    }

    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("int length = reader.ReadLength(0);");
        w.Line("if (length < 0) { value = null; return; }");
        w.Line("int words = (int)(((long)length + 31) >> 5);");
        w.Line("reader.EnsureCollectionPayload(reader.Position, unchecked(words * sizeof(int)));");
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("if (value is null || value.Length != length) value = new global::System.Collections.BitArray(length, false);");
        w.Line("ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Code.BitArrayView>(ref value);");
        w.Line("#if NET10_0_OR_GREATER");
        w.Line("var payload = view.m_array.AsSpan(0, unchecked(words * sizeof(int)));");
        w.Line("reader.Read<byte>(payload);");
        w.Line("int extraBits = length & 31;");
        w.Line("if (extraBits != 0) global::System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(payload)[words - 1] &= (int)((1u << extraBits) - 1);");
        w.Line("#else");
        w.Line("reader.Read<int>(view.m_array.AsSpan(0, words));");
        w.Line("#endif");
        w.Line("#else");
        w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryCreateBitArrayPayload(length, out var fresh, out var payload))");
        w.Open();
        w.Line("reader.Read<byte>(payload);");
        w.Line("fresh!.Length = length;");
        w.Line("value = fresh;");
        w.Line("return;");
        w.Close();
        w.Line("var storage = words == 0 ? global::System.Array.Empty<int>() : new int[words];");
        w.Line("reader.Read<int>(storage.AsSpan());");
        w.Line("value = new global::System.Collections.BitArray(storage) { Length = length };");
        w.Line("#endif");
    }

    private static void JsonWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNull<" + context.Profile + ">();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.WriteArrayStart<" + context.Profile + ">();");
        w.Line("for (int i = 0; i < value.Count; i++)");
        w.Open();
        w.Line("writer.Write<" + context.Profile + ">(value[i]);");
        w.Close();
        w.Line("writer.WriteArrayEnd<" + context.Profile + ">();");
    }

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
        w.Line("for (int i = 0; i < count; i++)");
        w.Open();
        w.Line("value[i] = reader.Read<bool, " + context.Profile + ">();");
        w.Close();
        w.Line("reader.ConsumeArrayEnd<" + context.Profile + ">();");
    }
}
