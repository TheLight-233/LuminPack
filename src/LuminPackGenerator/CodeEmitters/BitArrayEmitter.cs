using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static class BitArrayEmitter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            ");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                ");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            int offset;");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Code.BitArrayView>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, view.m_length);");
        sb.AppendLine("            writer.WriteUnmanagedArrayWithOutHeader(ref index, view.m_array, view.m_array.Length, out offset);");
        sb.AppendLine("#else");
        sb.AppendLine("            var length = value.Length;");
        sb.AppendLine("            var wordCount = (length + 31) >> 5;");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, length);");
        sb.AppendLine("            var words = global::System.Buffers.ArrayPool<int>.Shared.Rent(global::System.Math.Max(wordCount, 1));");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                if (wordCount != 0) value.CopyTo(words, 0);");
        sb.AppendLine("                writer.WriteUnmanagedArrayWithOutHeader(ref index, words, wordCount, out offset);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                global::System.Buffers.ArrayPool<int>.Shared.Return(words);");
        sb.AppendLine("            }");
        sb.AppendLine("#endif");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.Advance(4 + offset);");
        sb.AppendLine("            writer.CheckBuffer();");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            ");
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                ");
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine("                ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            ");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            if (value is null || value.Length != length) value = new global::System.Collections.BitArray(length, false);");
        sb.AppendLine("            ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.BitArray, global::LuminPack.Code.BitArrayView>(ref value);");
        sb.AppendLine("            reader.ReadUnmanagedArray(ref index, ref view.m_array!, view.m_array.Length, out var offset);");
        sb.AppendLine("            reader.Advance(offset);");
        sb.AppendLine("#else");
        sb.AppendLine("            var wordCount = (length + 31) >> 5;");
        sb.AppendLine("            var words = new int[wordCount];");
        sb.AppendLine("            reader.ReadUnmanagedArray(ref index, ref words, wordCount, out var offset);");
        sb.AppendLine("            reader.Advance(offset);");
        sb.AppendLine("            value = new global::System.Collections.BitArray(words) { Length = length };");
        sb.AppendLine("#endif");
    }

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
        sb.AppendLine("            var tempList = new global::System.Collections.Generic.List<bool>();");
        sb.AppendLine();
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine();
        sb.AppendLine("                tempList.Add(reader.GetBoolean());");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (tempList.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Collections.BitArray(tempList.Count);");
        sb.AppendLine("                for (int i = 0; i < tempList.Count; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    value[i] = tempList[i];");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Collections.BitArray(0);");
        sb.AppendLine("            }");
    }
}
