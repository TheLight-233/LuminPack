using System;
using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Emits tuple components and recursively nested eighth-slot Rest storage.</summary>
public static class TupleEmitter
{
    /// <summary>Determines whether the supplied value has the tuple classification.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool IsTuple(string typeName) => typeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal) ||
        typeName.StartsWith("global::System.ValueTuple<", StringComparison.Ordinal);

    /// <summary>Appends binary writing statements for tuple values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (EmitterTypeTraits.CanUseUnmanagedSerialization(fieldData.TypeSymbol))
        {
            UnmanagedEmitter.GenerateSerializeCode(fieldData, sb);
            return;
        }
        bool referenceTuple = fieldData.TypeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal);
        int count = GetGenericArguments(fieldData.TypeName).Length;
        if (referenceTuple)
        {
            sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
            sb.AppendLine("                writer.Advance(1);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            writer.WriteObjectHeader(" + count + ");");
        }
        for (int i = 1; i <= count; i++)
            sb.AppendLine("            writer.WriteValue(value." + (i == 8 ? "Rest" : "Item" + i) + ");");
    }

    /// <summary>Appends binary reading statements for tuple values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (EmitterTypeTraits.CanUseUnmanagedSerialization(fieldData.TypeSymbol))
        {
            UnmanagedEmitter.GenerateDeserializeCode(fieldData, sb);
            return;
        }
        bool referenceTuple = fieldData.TypeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal);
        string[] args = GetGenericArguments(fieldData.TypeName);
        if (referenceTuple)
        {
            sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
            sb.AppendLine("            if (!reader.TryReadObjectHead())");
            sb.AppendLine("            {");
            sb.AppendLine("                value = null;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        var tuple = fieldData.TypeSymbol as Microsoft.CodeAnalysis.INamedTypeSymbol;
        bool reusable = tuple != null && EmitterTypeTraits.IsDirectRecord(tuple);
        for (int i = 0; i < args.Length; i++)
        {
            string seed = "default!";
            if (reusable && tuple.TypeArguments[i].SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String)
            {
                string member = i == 7 ? "Rest" : "Item" + (i + 1);
                seed = referenceTuple ? "value is null ? default! : value." + member : "value." + member;
            }
            sb.AppendLine("            " + args[i] + " item" + i + " = " + seed + ";");
            sb.AppendLine("            reader.ReadValue(ref item" + i + ");");
        }
        sb.AppendLine("            value = new " + fieldData.TypeName + "(" + JoinItems(args.Length) + ");");
    }

    /// <summary>Appends JSON writing statements for tuple values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        bool referenceTuple = fieldData.TypeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal);
        int count = GetGenericArguments(fieldData.TypeName).Length;
        if (referenceTuple)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNull();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            writer.WriteArrayStart();");
        for (int i = 1; i <= count; i++)
        {
            if (i > 1) sb.AppendLine("            writer.WriteByteRaw((byte)',');");
            sb.AppendLine("            writer.SetFirstElement(true);");
            sb.AppendLine("            var item" + i + " = value." + (i == 8 ? "Rest" : "Item" + i) + ";");
            sb.AppendLine("            writer.WriteValue(in item" + i + ");");
        }
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for tuple values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        bool referenceTuple = fieldData.TypeName.StartsWith("global::System.Tuple<", StringComparison.Ordinal);
        string[] args = GetGenericArguments(fieldData.TypeName);
        if (referenceTuple)
        {
            sb.AppendLine("            if (reader.IsNull())");
            sb.AppendLine("            {");
            sb.AppendLine("                value = null;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        for (int i = 0; i < args.Length; i++)
        {
            sb.AppendLine("            reader.Read();");
            sb.AppendLine("            " + args[i] + " item" + i + " = default!;");
            sb.AppendLine("            reader.ReadValue(ref item" + i + ");");
        }
        sb.AppendLine("            reader.Read();");
        sb.AppendLine("            value = new " + fieldData.TypeName + "(" + JoinItems(args.Length) + ");");
    }

    /// <summary>Performs the join items step for the current operation.</summary>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string JoinItems(int count)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("item").Append(i);
        }
        return sb.ToString();
    }
}
