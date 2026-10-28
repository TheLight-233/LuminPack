using System;
using System.Linq;
using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;
/// <summary>
/// Emits serialization code for multidimensional arrays. The emitted code keeps
/// the binary object-header/dimensions/flat-element layout. JSON includes lengths,
/// lower bounds and flat values so empty and nonzero-based shapes remain lossless.
/// </summary>
public static class MultiDimensionalArrayEmitter
{
    /// <summary>Determines whether every element uses the raw unmanaged storage contract of the matching one-dimensional array.</summary>
    /// <param name="fieldData">The metadata describing the multidimensional array.</param>
    /// <param name="elementType">The element type name.</param>
    /// <returns>Whether the elements are proven reference-free raw storage, excluding Nullable values and unresolved generics.</returns>
    private static bool HasRawElements(LuminLocalFieldData fieldData, string elementType)
    {
        var element = EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol);
        if (element is Microsoft.CodeAnalysis.INamedTypeSymbol { OriginalDefinition.SpecialType: Microsoft.CodeAnalysis.SpecialType.System_Nullable_T })
            return false;
        return element != null && EmitterTypeTraits.TryGetIsUnmanaged(element, out bool isUnmanaged)
            ? isUnmanaged
            : CodeEmitterRegistry.KnownValueTypes.Contains(elementType);
    }

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
            sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(" + (5 + rank * 4) + " + " + PortableStructEmitter.PayloadSize(portable, "value.Length") + "));");
        AppendBinaryShapeValidation(sb, rank);
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
            PortableStructEmitter.AppendPayload(sb, portable, elementType, "first", "value.Length", write: true);
            sb.AppendLine("            writer.CheckBuffer();");
            return;
        }
        if (HasRawElements(fieldData, elementType))
        {
            // Raw elements are stored row-major and contiguously, exactly the order of the per-element loop, so the
            // complete payload is reserved once at the collection boundary and copied as one block.
            sb.AppendLine("            int payloadBytes = checked(value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">());");
            sb.AppendLine("            writer.EnsureAdditionalCapacity(payloadBytes);");
            sb.AppendLine("            writer.Write(ref global::System.Runtime.CompilerServices.Unsafe.As<" + elementType + ", byte>(ref first), payloadBytes);");
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
        if (portable != null)
        {
            sb.AppendLine("            reader.EnsureCollectionPayload(index, " + (4 + rank * 4) + ");");
            sb.AppendLine("            reader.ReadUnmanaged(ref index, " + JoinOutDimensions(rank) + ");");
            sb.AppendLine("            reader.Advance(" + (rank * 4) + ");");
        }
        else
        {
            // The checked Advance validates the complete dimension block before the cursor moves; the dimensions are
            // then loaded from the captured position.
            sb.AppendLine("            ref byte dimensions = ref reader.GetCurrentSpanReference();");
            sb.AppendLine("            reader.Advance(" + (rank * 4) + ");");
            for (int i = 0; i < rank; i++)
                sb.AppendLine("            global::LuminPack.Core.LuminPackReader.Read(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref dimensions, " + (i * 4) + "), out int dimension" + i + ");");
        }
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidCollection();");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            long expectedLength = " + string.Join(" || ", System.Linq.Enumerable.Range(0, rank).Select(i => "dimension" + i + " == 0")) + " ? 0 : 1;");
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
        if (portable != null) sb.AppendLine("            reader.EnsureCollectionPayload(index, checked(" + PortableStructEmitter.PayloadMinimum(portable, "length") + "));");
        // Raw elements are copied as one block, so the complete variable-length payload is validated before the allocation.
        else if (HasRawElements(fieldData, elementType))
            sb.AppendLine("            reader.EnsureCollectionPayload(index, checked(length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">()));");
        if (portable != null) PortableStructEmitter.AppendReadValidation(sb, portable, "length", "index");
        if (EmitterTypeTraits.IsKnownScalar(EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol)) &&
            HasRawElements(fieldData, elementType))
        {
            string dimensions = string.Join(" || ", System.Linq.Enumerable.Range(0, rank).Select(i =>
                "value.GetLength(" + i + ") != dimension" + i + " || value.GetLowerBound(" + i + ") != 0"));
            sb.AppendLine("            if (value is null || " + dimensions + ")");
            sb.AppendLine("                value = new " + elementType + "[" + JoinDimensions(rank) + "];");
        }
        else
        {
            sb.AppendLine("            value = new " + elementType + "[" + JoinDimensions(rank) + "];");
        }
        sb.AppendLine("            ref var first = ref global::System.Runtime.CompilerServices.Unsafe.As<byte, " + elementType + ">(ref global::LuminPack.Code.LuminPackMarshal.DangerousGetArrayDataReference<" + elementType + ">(value));");
        if (portable != null)
        {
            PortableStructEmitter.AppendPayload(sb, portable, elementType, "first", "length", write: false);
            return;
        }
        if (HasRawElements(fieldData, elementType))
        {
            // Preserve the complete payload validation and checked Reader operation for the row-major copy.
            sb.AppendLine("            reader.Read(ref global::System.Runtime.CompilerServices.Unsafe.As<" + elementType + ", byte>(ref first), unchecked(length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">()));");
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
        sb.AppendLine("            writer.WriteObjectStart();");
        sb.AppendLine("            writer.WritePropertyName(\"Lengths\");");
        sb.AppendLine("            writer.WriteArrayStart();");
        for (int i = 0; i < rank; i++) sb.AppendLine("            writer.WriteInt(value.GetLength(" + i + "));");
        sb.AppendLine("            writer.WriteArrayEnd();");
        sb.AppendLine("            writer.WritePropertyName(\"LowerBounds\");");
        sb.AppendLine("            writer.WriteArrayStart();");
        for (int i = 0; i < rank; i++) sb.AppendLine("            writer.WriteInt(value.GetLowerBound(" + i + "));");
        sb.AppendLine("            writer.WriteArrayEnd();");
        sb.AppendLine("            writer.WritePropertyName(\"Values\");");
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            ref var first = ref global::System.Runtime.CompilerServices.Unsafe.As<byte, " + elementType + ">(ref global::LuminPack.Code.LuminPackMarshal.DangerousGetArrayDataReference<" + elementType + ">(value));");
        sb.AppendLine("            for (int i = 0; i < value.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var item = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, (nint)(uint)i);");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
        sb.AppendLine("            writer.WriteObjectEnd();");
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
        sb.AppendLine("            reader.TryConsumeObjectStart();");
        sb.AppendLine("            int[] lengths = null, lowerBounds = null;");
        sb.AppendLine("            bool hasValues = false;");
        sb.AppendLine("            var items = new global::System.Collections.Generic.List<" + elementType + ">();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd) break;");
        JsonPropertyNameEmitter.Append(sb, "Lengths", "LowerBounds", "Values");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                if (property == 1 || property == 2)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.TryConsumeArrayStart();");
        sb.AppendLine("                    var shape = new int[" + rank + "];");
        sb.AppendLine("                    for (int i = 0; i < shape.Length; i++) { reader.Read(); shape[i] = reader.ReadInt(); }");
        sb.AppendLine("                    reader.ConsumeArrayEnd();");
        sb.AppendLine("                    if (property == 1) lengths = shape; else lowerBounds = shape;");
        sb.AppendLine("                }");
        sb.AppendLine("                else if (property == 3)");
        sb.AppendLine("                {");
        sb.AppendLine("                    hasValues = true;");
        sb.AppendLine("                    items.Clear();");
        sb.AppendLine("                    reader.TryConsumeArrayStart();");
        sb.AppendLine("                    while (reader.Read())");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd) break;");
        sb.AppendLine("                        " + elementType + " item = default!;");
        sb.AppendLine("                        reader.ReadValue(ref item);");
        sb.AppendLine("                        items.Add(item);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                else reader.Skip();");
        sb.AppendLine("            }");
        sb.AppendLine("            if (lengths is null || lowerBounds is null || !hasValues) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"A multidimensional JSON object requires Lengths, LowerBounds and Values properties.\");");
        sb.AppendLine("            long expectedLength = global::System.Array.IndexOf(lengths, 0) >= 0 ? 0 : 1;");
        sb.AppendLine("            for (int i = 0; i < lengths.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (lengths[i] < 0) global::LuminPack.Code.LuminPackExceptionHelper.ThrowNegativeMultiDimensionalArrayDimension();");
        sb.AppendLine("                expectedLength *= lengths[i];");
        sb.AppendLine("                if (expectedLength > int.MaxValue) global::LuminPack.Code.LuminPackExceptionHelper.ThrowMultiDimensionalArrayDimensionsTooLarge();");
        sb.AppendLine("            }");
        sb.AppendLine("            if (expectedLength != items.Count) global::LuminPack.Code.LuminPackExceptionHelper.ThrowMultiDimensionalArrayLengthMismatch();");
        sb.AppendLine("#if NET9_0_OR_GREATER");
        sb.AppendLine("            value = (" + fieldData.TypeName + ")global::System.Array.CreateInstanceFromArrayType(typeof(" + fieldData.TypeName + "), lengths, lowerBounds);");
        sb.AppendLine("#else");
        sb.AppendLine("            value = (" + fieldData.TypeName + ")global::System.Array.CreateInstance(typeof(" + elementType + "), lengths, lowerBounds);");
        sb.AppendLine("#endif");
        sb.AppendLine("            ref var first = ref global::System.Runtime.CompilerServices.Unsafe.As<byte, " + elementType + ">(ref global::LuminPack.Code.LuminPackMarshal.DangerousGetArrayDataReference<" + elementType + ">(value));");
        sb.AppendLine("            for (nint i = 0; i < items.Count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::System.Runtime.CompilerServices.Unsafe.Add(ref first, (nint)(uint)i) = items[(int)i];");
        sb.AppendLine("            }");
    }

    /// <summary>Evaluates the existing zero-based binary shape and flat element payload.</summary>
    /// <param name="fieldData">The multidimensional array metadata.</param>
    /// <param name="sb">The emitted statements.</param>
    public static void GenerateCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        int rank = GetRank(fieldData.TypeName);
        sb.AppendLine("            evaluator += 1;");
        sb.AppendLine("            if (value is null) return;");
        AppendBinaryShapeValidation(sb, rank);
        sb.AppendLine("            evaluator += " + (4 + rank * 4) + ";");
        var portable = PortableStructPlan.TryCreate(EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol));
        if (portable != null)
        {
            sb.AppendLine("            evaluator += checked(" + PortableStructEmitter.PayloadSize(portable, "value.Length") + ");");
            return;
        }
        string elementType = GetElementType(fieldData.TypeName);
        if (HasRawElements(fieldData, elementType))
        {
            // The writer copies raw elements as one row-major block, whatever framing their per-value formatter adds.
            sb.AppendLine("            evaluator += checked(value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">());");
            return;
        }
        sb.AppendLine("            foreach (var element in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var item = element;");
        sb.AppendLine("                evaluator.CalculateOffset(ref item);");
        sb.AppendLine("            }");
    }

    /// <summary>Rejects bounds absent from the existing binary shape record before writing or evaluation.</summary>
    /// <param name="sb">The emitted statements.</param>
    /// <param name="rank">The array rank.</param>
    private static void AppendBinaryShapeValidation(StringBuilder sb, int rank)
    {
        for (int dimension = 0; dimension < rank; dimension++)
            sb.AppendLine("            if (value.GetLowerBound(" + dimension + ") != 0) global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Binary multidimensional arrays require zero lower bounds.\");");
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
