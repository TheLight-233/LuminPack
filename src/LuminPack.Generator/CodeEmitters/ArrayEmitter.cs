using System.Text;
using System.Text.RegularExpressions;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides array emitter functionality.</summary>
public static class ArrayEmitter
{
    /// <summary>Appends binary writing statements for array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (PortableStructEmitter.TryAppendCollection(sb, EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol), list: false, write: true)) return;
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        bool? elementIsUnmanaged = GetClosedElementIsUnmanaged(fieldData);
        bool useUnmanagedPath = elementIsUnmanaged ?? KnownValueTypes.Contains(baseTypeName);
        if (useUnmanagedPath)
        {
            sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.Write(-1); return; }");
            EmitterTypeTraits.AppendRawCollectionWrite(sb, EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol), baseTypeName, "value.AsSpan()", "value.Length");
            return;
        }
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();

        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.EnsureAdditionalCapacity(sizeof(int));");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (useUnmanagedPath)
        {
            sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + baseTypeName + ">()));");
            sb.AppendLine("            writer.DangerousWriteUnmanagedArray(ref index, value, out var offset);");
            sb.AppendLine("            writer.Advance(offset);");
            sb.AppendLine("            writer.CheckBuffer();");
            return;
        }

        if (elementIsUnmanaged is null && !baseTypeName.Contains("?"))
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + baseTypeName + ">())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.EnsureAdditionalCapacity(checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + baseTypeName + ">()));");
            sb.AppendLine("                writer.DangerousWriteUnmanagedArray(ref index, value, out var offset);");
            sb.AppendLine("                writer.Advance(offset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        else if (elementIsUnmanaged is null)
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + baseTypeName + ">())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.EnsureAdditionalCapacity(checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + baseTypeName + ">()));");
            sb.AppendLine("                writer.DangerousWriteUnmanagedArray(ref index, value, out var offset);");
            sb.AppendLine("                writer.Advance(offset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        sb.AppendLine("            writer.EnsureAdditionalCapacity(sizeof(int));");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Length);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (ref var item in value.AsSpan())");
        sb.AppendLine("            {");
        if ((fieldData.TypeSymbol as Microsoft.CodeAnalysis.IArrayTypeSymbol)?.ElementType.SpecialType != Microsoft.CodeAnalysis.SpecialType.System_String)
            sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (PortableStructEmitter.TryAppendCollection(sb, EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol), list: false, write: false)) return;
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        bool? elementIsUnmanaged = GetClosedElementIsUnmanaged(fieldData);
        bool useUnmanagedPath = elementIsUnmanaged ?? KnownValueTypes.Contains(baseTypeName);
        if (useUnmanagedPath)
        {
            sb.AppendLine("            int length = reader.ReadLength(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + baseTypeName + ">());");
            sb.AppendLine("            if (length < 0) { value = null; return; }");
            sb.AppendLine("            if (length == 0) { value = global::System.Array.Empty<" + baseTypeName + ">(); return; }");
            sb.AppendLine("            if (value is null || value.Length != length) value = LuminPackMarshal.AllocateUninitializedArray<" + baseTypeName + ">(length);");
            EmitterTypeTraits.AppendValidatedRawRead(sb, baseTypeName, "value.AsSpan()", "length");
            return;
        }
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();

        if (useUnmanagedPath)
        {
            sb.AppendLine(@"            reader.DangerousReadUnmanagedArray(ref index, ref value!, out var offset);");
            sb.AppendLine(@"            reader.Advance(offset);");
            return;
        }

        if (elementIsUnmanaged is null && !baseTypeName.Contains("?"))
        {
            sb.AppendLine($@"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{baseTypeName}>())");
            sb.AppendLine(@"            {");
            sb.AppendLine(@"                reader.DangerousReadUnmanagedArray(ref index, ref value!, out var offset);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                reader.Advance(offset);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                return;");
            sb.AppendLine(@"            }");
            sb.AppendLine();
        }
        else if (elementIsUnmanaged is null)
        {
            sb.AppendLine($@"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{baseTypeName}>())");
            sb.AppendLine(@"            {");
            sb.AppendLine(@"                reader.DangerousReadUnmanagedArray(ref index, ref value!, out var offset);");
            sb.AppendLine(@"                reader.Advance(offset);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                return;");
            sb.AppendLine(@"            }");
            sb.AppendLine();
        }

        sb.AppendLine(@"            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                value = null;");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                reader.Advance(4);");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"");
        sb.AppendLine(@"            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine(@"            if (length is 0)");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                value = Array.Empty<{baseTypeName}>();");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine(@"            if (value is null || value.Length != length)");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                value = LuminPackMarshal.AllocateUninitializedArray<{baseTypeName}>(length);");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine(@"            ref var first = ref LuminPackMarshal.GetNotNullArrayReference(value!);");
        sb.AppendLine(@"            for (nint i = 0; i < length; i++)");
        sb.AppendLine(@"            {");
        EmitterTypeTraits.AppendReusableElementRead(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData),
            "global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)", "                ");
        sb.AppendLine(@"            }");
    }

    /// <summary>Appends binary reading statements for array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateFreshDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (PortableStructEmitter.TryAppendCollection(sb, EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol), list: false, write: false)) return;
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine(@"            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                value = null;");
        sb.AppendLine();
        sb.AppendLine(@"                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine(@"            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine(@"            if (length is 0)");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                value = Array.Empty<{baseTypeName}>();");
        sb.AppendLine();
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine($@"            value = LuminPackMarshal.AllocateUninitializedArray<{baseTypeName}>(length);");
        sb.AppendLine(@"            ref var first = ref LuminPackMarshal.GetNotNullArrayReference(value!);");
        sb.AppendLine(@"            for (nint i = 0; i < length; i++)");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
        sb.AppendLine(@"            }");
    }

    /// <summary>Appends JSON writing statements for array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayStart();");
        if (EmitterTypeTraits.HasJsonElementWriter(elementType))
        {
            // Each element and its separator share one capacity reservation; the loop owns the first-element state.
            sb.AppendLine("            for (int index = 0; index < value.Length; index++)");
            sb.AppendLine("                writer.WriteElement(value[index], index != 0);");
            sb.AppendLine("            writer.WriteArrayEnd();");
            return;
        }

        sb.AppendLine("            bool isFirst = true;");
        sb.AppendLine("            foreach (ref var item in value.AsSpan())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine("                else isFirst = false;");
        sb.AppendLine("                writer.SetFirstElement(true);");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        string clearPooledArray = GetArrayPoolClearExpression(fieldData, elementType);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            " + elementType + "[]? buffer = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(4);");
        sb.AppendLine("            int count = 0;");
        sb.AppendLine("            int capacity = buffer.Length;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                while (true)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (!reader.Read())");
        sb.AppendLine("                        break;");
        sb.AppendLine();
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine();
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                        continue;");
        sb.AppendLine();
        sb.AppendLine("                    if (count >= capacity)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        int newCapacity = capacity * 2;");
        sb.AppendLine("                        " + elementType + "[] newBuffer = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(newCapacity);");
        sb.AppendLine("                        buffer.AsSpan(0, count).CopyTo(newBuffer);");
        sb.AppendLine("                        global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: " + clearPooledArray + ");");
        sb.AppendLine("                        buffer = newBuffer;");
        sb.AppendLine("                        capacity = newBuffer.Length;");
        sb.AppendLine("                    }");
        sb.AppendLine();
        sb.AppendLine("                    " + elementType + " item = default!;");
        sb.AppendLine("                    reader.ReadValue(ref item);");
        sb.AppendLine("                    buffer[count++] = item;");
        sb.AppendLine("                }");
        sb.AppendLine();
        // `new Element[count]` is not valid when Element itself is an array
        // (`new int[][count]`).  The parser's generic `new T[count]` maps to
        // Span<T>.ToArray() for the concrete generated element type.
        sb.AppendLine("                value = buffer.AsSpan(0, count).ToArray();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                if (buffer != null)");
        sb.AppendLine("                    global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: " + clearPooledArray + ");");
        sb.AppendLine("            }");
    }

    // ── Compress variants ────────────────────────────────────────────────────
    // For unmanaged element types: use DangerousWriteUnmanagedArrayWithCompress /
    // DangerousReadUnmanagedArrayWithCompress.
    // For reference element types: compress is not applicable; fall back to the
    // normal WriteValue / ReadValue loop (compress attribute is ignored silently
    // for elements that are not blittable).

    /// <summary>Appends binary writing statements for array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCodeWithCompress(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (PortableStructEmitter.TryAppendCollection(sb, EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol), list: false, write: true)) return;
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        bool? elementIsUnmanaged = GetClosedElementIsUnmanaged(fieldData);
        bool useUnmanagedPath = elementIsUnmanaged ?? KnownValueTypes.Contains(baseTypeName);
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();

        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.EnsureAdditionalCapacity(sizeof(int));");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();

        if (useUnmanagedPath)
        {
            sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + baseTypeName + ">()));");
            sb.AppendLine(baseTypeName.Contains("?")
                ? "            writer.DangerousWriteUnmanagedArray(ref index, value, out var offset);"
                : "            writer.DangerousWriteUnmanagedArrayWithCompress(ref index, value, out var offset);");
            sb.AppendLine("            writer.Advance(offset);");
            sb.AppendLine("            writer.CheckBuffer();");
            return;
        }

        if (elementIsUnmanaged is null && !baseTypeName.Contains("?"))
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + baseTypeName + ">())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.EnsureAdditionalCapacity(checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + baseTypeName + ">()));");
            sb.AppendLine("                writer.DangerousWriteUnmanagedArrayWithCompress(ref index, value, out var offset);");
            sb.AppendLine("                writer.Advance(offset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        else if (elementIsUnmanaged is null)
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + baseTypeName + ">())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.EnsureAdditionalCapacity(checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + baseTypeName + ">()));");
            sb.AppendLine("                writer.DangerousWriteUnmanagedArray(ref index, value, out var offset);");
            sb.AppendLine("                writer.Advance(offset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        // Reference-type elements: propagate WithCompress only when leaf element is a KnownValueType.
        // e.g. Guid[][] → leaf=Guid (known) → WriteValueWithCompress(Guid[])
        //      IFoo[]   → leaf=IFoo (unknown) → WriteValue(IFoo)
        //      Transform[][] → leaf=Transform (user-defined, unknown) → WriteValue(Transform[])
        string elementCallSerialize = KnownValueTypes.Contains(baseTypeName) || IsLeafTypeKnownValueType(baseTypeName)
            ? "writer.WriteValueWithCompress(item);"
            : "writer.WriteValue(in item);";
        sb.AppendLine();
        sb.AppendLine("            writer.EnsureAdditionalCapacity(sizeof(int));");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Length);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (ref var item in value.AsSpan())");
        sb.AppendLine("            {");
        if ((fieldData.TypeSymbol as Microsoft.CodeAnalysis.IArrayTypeSymbol)?.ElementType.SpecialType != Microsoft.CodeAnalysis.SpecialType.System_String)
            sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine($"                {elementCallSerialize}");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCodeWithCompress(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (PortableStructEmitter.TryAppendCollection(sb, EmitterTypeTraits.GetCollectionElementType(fieldData.TypeSymbol), list: false, write: false)) return;
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        bool? elementIsUnmanaged = GetClosedElementIsUnmanaged(fieldData);
        bool useUnmanagedPath = elementIsUnmanaged ?? KnownValueTypes.Contains(baseTypeName);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();

        if (useUnmanagedPath)
        {
            sb.AppendLine(baseTypeName.Contains("?")
                ? @"            reader.DangerousReadUnmanagedArray(ref index, ref value!, out var offset);"
                : @"            reader.DangerousReadUnmanagedArrayWithCompress(ref index, ref value!, out var offset);");
            sb.AppendLine(@"            reader.Advance(offset);");
            return;
        }

        if (elementIsUnmanaged is null && !baseTypeName.Contains("?"))
        {
            sb.AppendLine($@"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{baseTypeName}>())");
            sb.AppendLine(@"            {");
            sb.AppendLine(@"                reader.DangerousReadUnmanagedArrayWithCompress(ref index, ref value!, out var offset);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                reader.Advance(offset);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                return;");
            sb.AppendLine(@"            }");
            sb.AppendLine();
        }
        else if (elementIsUnmanaged is null)
        {
            sb.AppendLine($@"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{baseTypeName}>())");
            sb.AppendLine(@"            {");
            sb.AppendLine(@"                reader.DangerousReadUnmanagedArray(ref index, ref value!, out var offset);");
            sb.AppendLine(@"                reader.Advance(offset);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                return;");
            sb.AppendLine(@"            }");
            sb.AppendLine();
        }

        // Reference-type elements — normal loop
        sb.AppendLine(@"            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                value = null;");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                reader.Advance(4);");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"");
        sb.AppendLine(@"            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine(@"            if (length is 0)");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                value = Array.Empty<{baseTypeName}>();");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine(@"            if (value is null || value.Length != length)");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                value = LuminPackMarshal.AllocateUninitializedArray<{baseTypeName}>(length);");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        bool compressedElement = KnownValueTypes.Contains(baseTypeName) || IsLeafTypeKnownValueType(baseTypeName);
        sb.AppendLine(@"            ref var first = ref LuminPackMarshal.GetNotNullArrayReference(value!);");
        sb.AppendLine(@"            for (nint i = 0; i < length; i++)");
        sb.AppendLine(@"            {");
        EmitterTypeTraits.AppendReusableElementRead(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData),
            "global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)", "                ", compressedElement);
        sb.AppendLine(@"            }");
    }

    /// <summary>Obtains the closed element is unmanaged for the supplied input.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <returns>The result of the operation.</returns>
    private static bool? GetClosedElementIsUnmanaged(LuminLocalFieldData fieldData)
    {
        if (fieldData.TypeSymbol is not Microsoft.CodeAnalysis.IArrayTypeSymbol array)
        {
            return null;
        }

        return EmitterTypeTraits.TryGetIsUnmanaged(array.ElementType, out bool isUnmanaged)
            ? isUnmanaged
            : null;
    }

    /// <summary>Obtains the array pool clear expression for the supplied input.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetArrayPoolClearExpression(LuminLocalFieldData fieldData, string elementType)
    {
        bool? elementIsUnmanaged = GetClosedElementIsUnmanaged(fieldData);
        return elementIsUnmanaged.HasValue
            ? (elementIsUnmanaged.Value ? "false" : "true")
            : "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">()";
    }

}
