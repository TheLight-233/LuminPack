using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides concurrent bag emitter functionality.</summary>
public static class ConcurrentBagEmitter

{
    /// <summary>Appends binary writing statements for concurrent bag values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

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
        sb.AppendLine("            var count = value.Count;");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var i = 0;");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                i++;");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                writer.WriteValue(in v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine();
        sb.AppendLine("            if (i != count)");
        sb.AppendLine("                LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();");
    }

    /// <summary>Appends binary reading statements for concurrent bag values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

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
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.Concurrent.ConcurrentBag<{elementType}>();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                value.Add(v);");
        sb.AppendLine("            }");
    }
}
