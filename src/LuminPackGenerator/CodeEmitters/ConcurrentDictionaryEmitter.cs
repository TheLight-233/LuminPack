using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides concurrent dictionary emitter functionality.</summary>
public static class ConcurrentDictionaryEmitter

{
    /// <summary>Appends binary writing statements for concurrent dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var count = value.Count;");
        sb.AppendLine("            var i = 0;");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                i++;");
        sb.AppendLine("                var key = item.Key;");
        sb.AppendLine("                writer.WriteValue(in key);");
        sb.AppendLine("                var itemValue = item.Value;");
        sb.AppendLine("                writer.WriteValue(in itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine();
        sb.AppendLine("            if (i != count)");
        sb.AppendLine("                LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();");
    }

    /// <summary>Appends binary reading statements for concurrent dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.Concurrent.ConcurrentDictionary<{GetFirstGeneric(fieldData.TypeName)}, {GetSecondGeneric(fieldData.TypeName)}>();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {GetFirstGeneric(fieldData.TypeName)} k = default!;");
        sb.AppendLine($"                {GetSecondGeneric(fieldData.TypeName)} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                value.TryAdd(k!, v);");
        sb.AppendLine("            }");
    }
}
