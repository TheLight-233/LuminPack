using System;
using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;
/// <summary>
/// Emits serialization code for multidimensional arrays. The emitted code keeps
/// the object-header/dimensions/flat-element layout and JSON rectangular-array
/// validation, but dispatches every element through generated extensions.
/// </summary>
public static class MultiDimensionalArrayEmitter
{
    /// <summary>Determines whether the supplied value has the multi dimensional array classification.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool IsMultiDimensionalArray(string typeName)
    {
        int open = typeName.LastIndexOf('[');
        return open >= 0 && typeName.EndsWith("]", StringComparison.Ordinal) &&
            typeName.AsSpan(open).IndexOf(',') >= 0;
    }

    /// <summary>Appends binary writing statements for multi dimensional array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        int rank = GetRank(fieldData.TypeName);
        string elementType = GetElementType(fieldData.TypeName);
        var portable = PortableStructPlan.TryCreate(EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol));
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        if (portable != null) sb.AppendLine("                writer.EnsureAdditionalCapacity(1);");
        sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine("                writer.Advance(1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        if (portable != null)
            sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(" + (5 + rank * 4) + " + value.Length * " + portable.WireSize + "));");
        sb.AppendLine("            writer.WriteObjectHeader(ref index, 0);");
        sb.AppendLine("            writer.Advance(1);");
        for (int i = 0; i < rank; i++)
            sb.AppendLine("            var dimension" + i + " = value.GetLength(" + i + ");");
        sb.AppendLine("            writer.WriteUnmanaged(ref index, " + JoinDimensions(rank) + ");");
        sb.AppendLine("            writer.Advance(" + (rank * 4) + ");");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Length);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            ref var first = ref global::System.Runtime.CompilerServices.Unsafe.As<byte, " + elementType + ">(ref global::LuminPack.Code.LuminPackMarshal.DangerousGetArrayDataReference<" + elementType + ">(value));");
        if (portable != null)
        {
            PortableStructEmitter.AppendPayload(sb, portable, elementType, "global::System.Runtime.CompilerServices.Unsafe.Add(ref first, __portableItem)", "value.Length", write: true);
            sb.AppendLine("            writer.CheckBuffer();");
            return;
        }
        sb.AppendLine("            for (nint i = 0; i < value.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                ref " + elementType + " item = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i);");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for multi dimensional array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        int rank = GetRank(fieldData.TypeName);
        string elementType = GetElementType(fieldData.TypeName);
        var portable = PortableStructPlan.TryCreate(EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol));
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        if (portable != null) sb.AppendLine("            reader.EnsureCollectionPayload(index, 1);");
        sb.AppendLine("            if (!reader.TryReadObjectHead(ref index))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                reader.Advance(1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.Advance(1);");
        if (portable != null) sb.AppendLine("            reader.EnsureCollectionPayload(index, " + (4 + rank * 4) + ");");
        sb.AppendLine("            reader.ReadUnmanaged(ref index, " + JoinOutDimensions(rank) + ");");
        sb.AppendLine("            reader.Advance(" + (rank * 4) + ");");
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidCollection();");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            long expectedLength = 1;");
        for (int i = 0; i < rank; i++)
        {
            sb.AppendLine("            if (dimension" + i + " < 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowNegativeMultiDimensionalArrayDimension();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            expectedLength *= dimension" + i + ";");
            sb.AppendLine("            if (expectedLength > int.MaxValue)");
            sb.AppendLine("            {");
            sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowMultiDimensionalArrayDimensionsTooLarge();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            if (length != (int)expectedLength)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowMultiDimensionalArrayLengthMismatch();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        if (portable != null) sb.AppendLine("            reader.EnsureCollectionPayload(index, checked(length * " + portable.WireSize + "));");
        sb.AppendLine("            value = new " + elementType + "[" + JoinDimensions(rank) + "];");
        sb.AppendLine("            ref var first = ref global::System.Runtime.CompilerServices.Unsafe.As<byte, " + elementType + ">(ref global::LuminPack.Code.LuminPackMarshal.DangerousGetArrayDataReference<" + elementType + ">(value));");
        if (portable != null)
        {
            PortableStructEmitter.AppendPayload(sb, portable, elementType, "global::System.Runtime.CompilerServices.Unsafe.Add(ref first, __portableItem)", "length", write: false);
            return;
        }
        sb.AppendLine("            for (nint i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref " + elementType + " item = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i);");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends JSON writing statements for multi dimensional array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        int rank = GetRank(fieldData.TypeName);
        string elementType = GetElementType(fieldData.TypeName);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        for (int i = 0; i < rank; i++)
            sb.AppendLine("            var dimension" + i + " = value.GetLength(" + i + ");");
        AppendJsonWriteLevel(sb, rank, 0, elementType, "            ");
    }

    /// <summary>Appends JSON reading statements for multi dimensional array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        int rank = GetRank(fieldData.TypeName);
        string elementType = GetElementType(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            var items = new global::System.Collections.Generic.List<" + elementType + ">();");
        for (int i = 0; i < rank; i++)
        {
            sb.AppendLine("            var dimension" + i + " = 0;");
            if (i != 0)
                sb.AppendLine("            var hasDimension" + i + " = false;");
        }
        AppendJsonReadLevel(sb, rank, 0, elementType, "            ");
        sb.AppendLine("            value = new " + elementType + "[" + JoinDimensions(rank) + "];");
        sb.AppendLine("            ref var first = ref global::System.Runtime.CompilerServices.Unsafe.As<byte, " + elementType + ">(ref global::LuminPack.Code.LuminPackMarshal.DangerousGetArrayDataReference<" + elementType + ">(value));");
        sb.AppendLine("            for (nint i = 0; i < items.Count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i) = items[(int)i];");
        sb.AppendLine("            }");
    }

    /// <summary>Appends JSON writing statements for multi dimensional array values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="rank">The rank used by this operation.</param>
    /// <param name="level">The level used by this operation.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void AppendJsonWriteLevel(StringBuilder sb, int rank, int level, string elementType, string indent)
    {
        sb.AppendLine(indent + "writer.WriteArrayStart();");
        if (level == rank - 1)
        {
            sb.AppendLine(indent + "bool isFirst = true;");
            sb.AppendLine(indent + "for (int index" + level + " = 0; index" + level + " < dimension" + level + "; index" + level + "++)");
            sb.AppendLine(indent + "{");
            sb.AppendLine(indent + "    if (!isFirst) writer.WriteByteRaw((byte)',');");
            sb.AppendLine(indent + "    else isFirst = false;");
            sb.AppendLine(indent + "    writer.SetFirstElement(true);");
            sb.AppendLine(indent + "    " + elementType + " item = value[" + JoinIndexes(rank) + "];");
            sb.AppendLine(indent + "    writer.WriteValue(in item);");
            sb.AppendLine(indent + "}");
        }
        else
        {
            sb.AppendLine(indent + "for (int index" + level + " = 0; index" + level + " < dimension" + level + "; index" + level + "++)");
            sb.AppendLine(indent + "{");
            AppendJsonWriteLevel(sb, rank, level + 1, elementType, indent + "    ");
            sb.AppendLine(indent + "}");
        }
        sb.AppendLine(indent + "writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for multi dimensional array values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="rank">The rank used by this operation.</param>
    /// <param name="level">The level used by this operation.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void AppendJsonReadLevel(StringBuilder sb, int rank, int level, string elementType, string indent)
    {
        sb.AppendLine(indent + "var count" + level + " = 0;");
        sb.AppendLine(indent + "while (reader.Read())");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine(indent + "        break;");
        sb.AppendLine(indent + "    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine(indent + "        continue;");
        if (level == rank - 1)
        {
            sb.AppendLine(indent + "    " + elementType + " item = default!;");
            sb.AppendLine(indent + "    reader.ReadValue(ref item);");
            sb.AppendLine(indent + "    items.Add(item);");
        }
        else
        {
            sb.AppendLine(indent + "    reader.TryConsumeArrayStart();");
            AppendJsonReadLevel(sb, rank, level + 1, elementType, indent + "    ");
        }
        sb.AppendLine(indent + "    count" + level + "++;");
        sb.AppendLine(indent + "}");
        if (level == 0)
        {
            sb.AppendLine(indent + "dimension0 = count0;");
        }
        else
        {
            sb.AppendLine(indent + "if (!hasDimension" + level + ")");
            sb.AppendLine(indent + "{");
            sb.AppendLine(indent + "    dimension" + level + " = count" + level + ";");
            sb.AppendLine(indent + "    hasDimension" + level + " = true;");
            sb.AppendLine(indent + "}");
            sb.AppendLine(indent + "else if (dimension" + level + " != count" + level + ")");
            sb.AppendLine(indent + "{");
            sb.AppendLine(indent + "    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"JSON rows must have equal lengths for a rectangular array\");");
            sb.AppendLine(indent + "}");
        }
    }

    /// <summary>Obtains the rank for the supplied input.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    private static int GetRank(string typeName)
    {
        int open = typeName.LastIndexOf('[');
        int rank = 1;
        for (int i = open; i < typeName.Length; i++)
        {
            if (typeName[i] == ',') rank++;
        }
        return rank;
    }

    /// <summary>Obtains the element type for the supplied input.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetElementType(string typeName) => typeName.Substring(0, typeName.LastIndexOf('['));
    /// <summary>Performs the join dimensions step for the current operation.</summary>
    /// <param name="rank">The rank used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string JoinDimensions(int rank) => Join(rank, "dimension");
    /// <summary>Performs the join out dimensions step for the current operation.</summary>
    /// <param name="rank">The rank used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string JoinOutDimensions(int rank)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < rank; i++)
        {
            if (i != 0) sb.Append(", ");
            sb.Append("out int dimension").Append(i);
        }
        return sb.ToString();
    }
    /// <summary>Performs the join indexes step for the current operation.</summary>
    /// <param name="rank">The rank used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string JoinIndexes(int rank) => Join(rank, "index");

    /// <summary>Performs the join step for the current operation.</summary>
    /// <param name="rank">The rank used by this operation.</param>
    /// <param name="prefix">The prefix used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string Join(int rank, string prefix)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < rank; i++)
        {
            if (i != 0) sb.Append(", ");
            sb.Append(prefix).Append(i);
        }
        return sb.ToString();
    }
}
