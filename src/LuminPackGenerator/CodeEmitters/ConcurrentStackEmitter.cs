using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides concurrent stack emitter functionality.</summary>
public static class ConcurrentStackEmitter

{
    /// <summary>Appends binary writing statements for concurrent stack values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        string clearArrayExpression = EmitterTypeTraits.GetArrayPoolClearExpression(fieldData, elementType);

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
        sb.AppendLine("            // reverse order in serialize");
        sb.AppendLine("            var count = value.Count;");
        sb.AppendLine($"            {elementType}[] rentArray = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(count);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var i = 0;");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    rentArray[i++] = item;");
        sb.AppendLine("                }");
        sb.AppendLine("                if (i != count) LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                for (i = i - 1; i >= 0; i--)");
                sb.AppendLine("                {");
                sb.AppendLine("                    writer.CheckBuffer();");
                sb.AppendLine("                    var v = rentArray[i];");
                sb.AppendLine("                    writer.WriteValue(in v);");
                sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(rentArray, clearArray: {clearArrayExpression});");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary reading statements for concurrent stack values.</summary>
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
        sb.AppendLine($"                value = new global::System.Collections.Concurrent.ConcurrentStack<{elementType}>();");
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
        sb.AppendLine("                value.Push(v);");
        sb.AppendLine("            }");
    }
}
