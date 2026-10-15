using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides immutable sorted set emitter functionality.</summary>
public static class ImmutableSortedSetEmitter
{
    /// <summary>Appends binary writing statements for immutable sorted set values.</summary>
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
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                writer.WriteValue(in v);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for immutable sorted set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine($"            global::System.Collections.Generic.IComparer<{elementType}>? keyComparer = null;");
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
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableSortedSet<{elementType}>.Empty;");
        sb.AppendLine("                if (keyComparer != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = value.WithComparer(keyComparer);");
        sb.AppendLine("                }");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableSortedSet.Create(keyComparer, item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableSortedSet.CreateBuilder<{elementType}>(keyComparer);");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                builder.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}
