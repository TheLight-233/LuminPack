using System.Text;
using System.Text.RegularExpressions;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class ArrayFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
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
        if (KnownValueTypes.Contains(baseTypeName))
        {
            sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + baseTypeName + ">()));");
            sb.AppendLine("            writer.DangerousWriteUnmanagedArray(ref index, value, out var offset);");
            sb.AppendLine("            writer.Advance(offset);");
            sb.AppendLine("            writer.CheckBuffer();");
            return;
        }

        if (!baseTypeName.Contains("?"))
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
        else
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + baseTypeName + ">())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteArray(value);");
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
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();

        if (KnownValueTypes.Contains(baseTypeName))
        {
            sb.AppendLine(@"            reader.DangerousReadUnmanagedArray(ref index, ref value!, out var offset);");
            sb.AppendLine(@"            reader.Advance(offset);");
            return;
        }

        if (!baseTypeName.Contains("?"))
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
        else
        {
            sb.AppendLine($@"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{baseTypeName}>())");
            sb.AppendLine(@"            {");
            sb.AppendLine(@"                reader.ReadArray(ref value!);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                return;");
            sb.AppendLine(@"            }");
            sb.AppendLine();
        }

        sb.AppendLine(@"            if (!reader.TryReadCollectionHead(ref index, out var length))");
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
        sb.AppendLine(@"            ref var first = ref LuminPackMarshal.GetArrayReference(value);");
        sb.AppendLine(@"            for (nint i = 0; i < length; i++)");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
        sb.AppendLine(@"            }");
    }

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

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
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
        sb.AppendLine("                        global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">());");
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
        sb.AppendLine("                    global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">());");
        sb.AppendLine("            }");
    }

    // ── Compress variants ────────────────────────────────────────────────────
    // For unmanaged element types: use DangerousWriteUnmanagedArrayWithCompress /
    // DangerousReadUnmanagedArrayWithCompress.
    // For reference element types: compress is not applicable; fall back to the
    // normal WriteValue / ReadValue loop (compress attribute is ignored silently
    // for elements that are not blittable).

    public static void GenerateSerializeCodeWithCompress(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
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

        if (KnownValueTypes.Contains(baseTypeName))
        {
            sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + baseTypeName + ">()));");
            sb.AppendLine("            writer.DangerousWriteUnmanagedArrayWithCompress(ref index, value, out var offset);");
            sb.AppendLine("            writer.Advance(offset);");
            sb.AppendLine("            writer.CheckBuffer();");
            return;
        }

        if (!baseTypeName.Contains("?"))
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
        else
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + baseTypeName + ">())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteArray(value);");
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
        sb.AppendLine($"                {elementCallSerialize}");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCodeWithCompress(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();

        if (KnownValueTypes.Contains(baseTypeName))
        {
            sb.AppendLine(@"            reader.DangerousReadUnmanagedArrayWithCompress(ref index, ref value!, out var offset);");
            sb.AppendLine(@"            reader.Advance(offset);");
            return;
        }

        if (!baseTypeName.Contains("?"))
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
        else
        {
            sb.AppendLine($@"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{baseTypeName}>())");
            sb.AppendLine(@"            {");
            sb.AppendLine(@"                reader.ReadArray(ref value!);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                return;");
            sb.AppendLine(@"            }");
            sb.AppendLine();
        }

        // Reference-type elements — normal loop
        sb.AppendLine(@"            if (!reader.TryReadCollectionHead(ref index, out var length))");
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
        string elementCallDeserialize = KnownValueTypes.Contains(baseTypeName) || IsLeafTypeKnownValueType(baseTypeName)
            ? "reader.ReadValueWithCompress(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);"
            : "reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);";
        sb.AppendLine(@"            ref var first = ref LuminPackMarshal.GetArrayReference(value);");
        sb.AppendLine(@"            for (nint i = 0; i < length; i++)");
        sb.AppendLine(@"            {");
        sb.AppendLine($"                {elementCallDeserialize}");
        sb.AppendLine(@"            }");
    }
}
