using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides bit array emitter functionality.</summary>
public static class BitArrayEmitter
{
    /// <summary>Evaluates the bit-count header and word-rounded binary payload.</summary>
    /// <param name="fieldData">The BitArray metadata.</param>
    /// <param name="sb">The emitted statements.</param>
    public static void GenerateCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            evaluator += 4;");
        sb.AppendLine("            if (value is null) return;");
        // A nonnegative Int32 bit count needs at most 268435456 payload bytes.
        sb.AppendLine("            evaluator += unchecked((int)(((long)value.Length + 31) / 32 * 4));");
    }

    /// <summary>Appends binary writing statements for bit array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.Write(-1); return; }");
        sb.AppendLine("            int length = value.Length;");
        sb.AppendLine("            int words = (int)(((long)length + 31) >> 5);");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Code.BitArrayView>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        sb.AppendLine("#if NET10_0_OR_GREATER");
        sb.AppendLine("            writer.Write<byte>(view.m_array.AsSpan(0, unchecked(words * sizeof(int))), length);");
        sb.AppendLine("#else");
        sb.AppendLine("            writer.Write<int>(view.m_array.AsSpan(0, words), length);");
        sb.AppendLine("#endif");
        sb.AppendLine("#else");
        sb.AppendLine("            var storage = global::System.Buffers.ArrayPool<int>.Shared.Rent(global::System.Math.Max(words, 1));");
        sb.AppendLine("            try { if (words != 0) value.CopyTo(storage, 0); writer.Write<int>(storage.AsSpan(0, words), length); }");
        sb.AppendLine("            finally { global::System.Buffers.ArrayPool<int>.Shared.Return(storage); }");
        sb.AppendLine("#endif");
    }

    /// <summary>Appends binary reading statements for bit array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            int length = reader.ReadLength(0);");
        sb.AppendLine("            if (length < 0) { value = null; return; }");
        sb.AppendLine("            int words = (int)(((long)length + 31) >> 5);");
        sb.AppendLine("            reader.EnsureCollectionPayload(reader.Position, unchecked(words * sizeof(int)));");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            if (value is null || value.Length != length) value = new global::System.Collections.BitArray(length, false);");
        sb.AppendLine("            ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Code.BitArrayView>(ref value);");
        sb.AppendLine("#if NET10_0_OR_GREATER");
        sb.AppendLine("            var payload = view.m_array.AsSpan(0, unchecked(words * sizeof(int)));");
        sb.AppendLine("            reader.Read<byte>(payload);");
        // New byte storage requires unused bits to be clear, including old integer-format input.
        sb.AppendLine("            int extraBits = length & 31;");
        sb.AppendLine("            if (extraBits != 0) global::System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(payload)[words - 1] &= (int)((1u << extraBits) - 1);");
        sb.AppendLine("#else");
        sb.AppendLine("            reader.Read<int>(view.m_array.AsSpan(0, words));");
        sb.AppendLine("#endif");
        sb.AppendLine("#else");
        sb.AppendLine("            var storage = new int[words];");
        sb.AppendLine("            reader.Read<int>(storage.AsSpan());");
        sb.AppendLine("            value = new global::System.Collections.BitArray(storage) { Length = length };");
        sb.AppendLine("#endif");
    }

    /// <summary>Appends JSON writing statements for bit array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine();
        sb.AppendLine("            if (value.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = 0; i < value.Count; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteBool(value[i]);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for bit array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine();
        // The element count sizes the bit array directly; no temporary list is staged.
        sb.AppendLine("            int count = reader.CountArrayElements();");
        sb.AppendLine("            value = new global::System.Collections.BitArray(count);");
        sb.AppendLine("            for (int i = 0; i < count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                value[i] = reader.GetBoolean();");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.ConsumeArrayEnd();");
    }
}
