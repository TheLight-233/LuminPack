using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides interface immutable stack emitter functionality.</summary>
public static class InterfaceImmutableStackEmitter
{
    /// <summary>Appends binary writing statements for interface immutable stack values.</summary>
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
        sb.AppendLine("            var tempBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var tempWriter = new LuminPackWriter(tempBuffer, writer.OptionState);");
        sb.AppendLine();
        sb.AppendLine("                var count = 0;");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    tempWriter.WriteValue(in v);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(tempBuffer);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary reading statements for interface immutable stack values.</summary>
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableStack<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length is 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableStack.Create(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var rentArray = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(length);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = length - 1; i >= 0; i--)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    reader.ReadValue(ref rentArray[i]);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (rentArray.Length == length)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableStack.Create(rentArray);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableStack.CreateRange((new global::System.ArraySegment<{elementType}>(rentArray, 0, length)).AsEnumerable());");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(rentArray, clearArray: {EmitterTypeTraits.GetArrayPoolClearExpression(fieldData, elementType)});");
        sb.AppendLine("            }");
    }
}
