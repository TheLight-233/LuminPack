using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides hash set emitter functionality.</summary>
public static class HashSetEmitter

{
    /// <summary>Appends binary writing statements for hash set values.</summary>
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
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            nuint setIndex = 0;");
        sb.AppendLine("            var setView = LuminPackMarshal.GetHashSetView(Unsafe.AsRef(in value));");
        sb.AppendLine("            ref var arrayRef = ref LuminPackMarshal.GetArrayReference(setView._entries);");
        sb.AppendLine();
        sb.AppendLine("            while ((uint) setIndex < (uint) LuminPackMarshal.GetHashSetEntryCount(setView))");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                ref var local = ref Unsafe.Add(ref arrayRef, setIndex++);");
        sb.AppendLine("                if (LuminPackMarshal.IsHashTableEntryActive(local.HashCode, local.Next))");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteValue(local.Value);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for hash set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);

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
        sb.AppendLine("            if (length == 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value ??= new global::System.Collections.Generic.HashSet<{elementType}>(0);");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            value = new global::System.Collections.Generic.HashSet<{elementType}>(length);");
        sb.AppendLine();
        sb.AppendLine("            var _setView = LuminPackMarshal.GetHashSetView(value);");
        sb.AppendLine("            ref var _entryRef = ref LuminPackMarshal.GetArrayReference(_setView._entries);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var _entry = ref Unsafe.Add(ref _entryRef, (nint)(uint)i);");
        sb.AppendLine("                reader.ReadValue(ref _entry.Value!);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            LuminPackMarshal.RebuildHashSetBuckets(_setView, length);");
    }
}
