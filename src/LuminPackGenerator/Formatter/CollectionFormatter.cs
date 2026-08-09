using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class ListFormatter

{
    /// <summary>
    /// Shared JSON template for the mutable collection formatters in this file.
    /// It emits the original token loop while using generated static extensions
    /// for element serialization.
    /// </summary>
    public static void GenerateJsonEnumerableSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetCollectionBaseType(fieldData.TypeName);
        bool hasCount = baseType != "global::System.Collections.Generic.IEnumerable";
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayStart();");
        if (hasCount)
        {
            sb.AppendLine("            if (value.Count == 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteArrayEnd();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        sb.AppendLine("            bool isFirst = true;");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine("                else isFirst = false;");
        sb.AppendLine("                writer.SetFirstElement(true);");
        sb.AppendLine("                var temp = item;");
        sb.AppendLine("                writer.WriteValue(in temp);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonEnumerableDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string type = fieldData.TypeName;
        string baseType = GetCollectionBaseType(type);
        string elementType = GetFirstGeneric(type);
        bool stack = baseType == "global::System.Collections.Generic.Stack";
        bool readOnly = baseType is "global::System.Collections.ObjectModel.ReadOnlyCollection" or
            "global::System.Collections.ObjectModel.ReadOnlyObservableCollection";
        bool interfaceType = baseType is "global::System.Collections.Generic.IEnumerable" or
            "global::System.Collections.Generic.ICollection" or
            "global::System.Collections.Generic.IReadOnlyCollection" or
            "global::System.Collections.Generic.IList" or
            "global::System.Collections.Generic.IReadOnlyList";
        bool setInterface = baseType is "global::System.Collections.Generic.ISet" or
            "global::System.Collections.Generic.IReadOnlySet";

        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine();

        if (stack || readOnly || interfaceType || setInterface)
        {
            sb.AppendLine("            var tempList = new global::System.Collections.Generic.List<" + elementType + ">();");
        }
        else if (baseType == "global::System.Collections.Concurrent.BlockingCollection")
        {
            sb.AppendLine("            value = new " + type + "();");
        }
        else
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                value = new " + type + "();");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                value.Clear();");
            sb.AppendLine("            }");
        }
        sb.AppendLine();
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine();
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        if (stack || readOnly || interfaceType || setInterface)
        {
            sb.AppendLine("                tempList.Add(item);");
        }
        else if (baseType == "global::System.Collections.Generic.LinkedList")
        {
            sb.AppendLine("                value.AddLast(item);");
        }
        else if (baseType == "global::System.Collections.Generic.Queue" ||
                 baseType == "global::System.Collections.Concurrent.ConcurrentQueue")
        {
            sb.AppendLine("                value.Enqueue(item);");
        }
        else if (baseType == "global::System.Collections.Concurrent.ConcurrentStack")
        {
            sb.AppendLine("                value.Push(item);");
        }
        else
        {
            sb.AppendLine("                value.Add(item);");
        }
        sb.AppendLine("            }");

        if (stack)
        {
            sb.AppendLine("            value = new " + type + "();");
            sb.AppendLine("            for (int i = tempList.Count - 1; i >= 0; i--)");
            sb.AppendLine("            {");
            sb.AppendLine("                value.Push(tempList[i]);");
            sb.AppendLine("            }");
        }
        else if (readOnly)
        {
            if (baseType == "global::System.Collections.ObjectModel.ReadOnlyCollection")
            {
                sb.AppendLine("            value = new global::System.Collections.ObjectModel.ReadOnlyCollection<" + elementType + ">(tempList);");
            }
            else
            {
                sb.AppendLine("            value = new global::System.Collections.ObjectModel.ReadOnlyObservableCollection<" + elementType + ">(new global::System.Collections.ObjectModel.ObservableCollection<" + elementType + ">(tempList));");
            }
        }
        else if (interfaceType)
        {
            sb.AppendLine("            value = tempList;");
        }
        else if (setInterface)
        {
            sb.AppendLine("            value = new global::System.Collections.Generic.HashSet<" + elementType + ">(tempList);");
        }
    }

    private static string GetCollectionBaseType(string typeName)
    {
        int separator = typeName.IndexOf('<');
        return separator < 0 ? typeName : typeName.Substring(0, separator);
    }

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
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        sb.AppendLine();

        if (KnownValueTypes.Contains(elementType))
        {
            sb.AppendLine("            writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
            sb.AppendLine("            writer.Advance(spanOffset);");
            sb.AppendLine("            writer.CheckBuffer();");
            return;
        }

        if (!elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
            sb.AppendLine("                writer.Advance(spanOffset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteSpan(span);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("            writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (ref var item in span)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteValue(in item!);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        GenerateDeserializeCodeCore(fieldData, sb, freshValue: false);
    }

    public static void GenerateFreshDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        GenerateDeserializeCodeCore(fieldData, sb, freshValue: true);
    }

    private static void GenerateDeserializeCodeCore(
        LuminLocalFieldData fieldData, StringBuilder sb, bool freshValue)
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
        if (freshValue)
        {
            sb.AppendLine($"            var items = reader.CreateFreshListArray<{elementType}>(length, out value);");
        }
        else
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                value = new global::System.Collections.Generic.List<{elementType}>(length);");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (value.Count == length)");
            sb.AppendLine("            {");
            sb.AppendLine("                value.Clear();");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
        }
        if (KnownValueTypes.Contains(elementType))
        {
            if (freshValue)
                sb.AppendLine("            var span = items.AsSpan();");
            sb.AppendLine("            reader.DangerousReadUnmanagedSpan(ref index, ref span, out var spanOffset);");
            sb.AppendLine("            reader.Advance(spanOffset);");
            return;
        }

        if (!elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            if (freshValue)
                sb.AppendLine("                var span = items.AsSpan();");
            sb.AppendLine("                reader.DangerousReadUnmanagedSpan(ref index, ref span, out var spanOffset);");
            sb.AppendLine("                reader.Advance(spanOffset);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            if (freshValue)
                sb.AppendLine("                var span = items.AsSpan();");
            sb.AppendLine("                reader.Advance(4);");
            sb.AppendLine("                reader.ReadSpan(ref index, length, ref span);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // The unmanaged fast paths consume the collection header themselves and report an
        // offset including that header.  The recursive static path starts after the header.
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        if (freshValue)
        {
            sb.AppendLine("            ref var first = ref LuminPackMarshal.DangerousGetArrayDataReference(items);");
            sb.AppendLine($"            if (!typeof({elementType}).IsValueType)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (nint i = 0; i < length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {elementType} item = default!;");
            sb.AppendLine("                    reader.ReadValue(ref item);");
            sb.AppendLine("                    global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i) = item;");
            sb.AppendLine("                }");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            for (nint i = 0; i < length; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("            if (span.IsEmpty)");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span);");
            sb.AppendLine("            for (nint i = 0; i < span.Length; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
            sb.AppendLine("            }");
        }
    }

    // 鈹€鈹€ Compress variants 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public static void GenerateSerializeCodeWithCompress(LuminLocalFieldData fieldData, StringBuilder sb)
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
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        sb.AppendLine();

        if (KnownValueTypes.Contains(elementType))
        {
            sb.AppendLine("            writer.DangerousWriteUnmanagedSpanWithCompress(ref index, span, out var spanOffset);");
            sb.AppendLine("            writer.Advance(spanOffset);");
            return;
        }

        if (!elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.DangerousWriteUnmanagedSpanWithCompress(ref index, span, out var spanOffset);");
            sb.AppendLine("                writer.Advance(spanOffset);");
            sb.AppendLine("                writer.CheckBuffer();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteSpan(span);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // Reference elements: propagate WithCompress only when leaf element is a KnownValueType.
        string elementCallSerialize = KnownValueTypes.Contains(elementType) || IsLeafTypeKnownValueType(elementType)
            ? "writer.WriteValueWithCompress(item!);"
            : "writer.WriteValue(in item!);";
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (ref var item in span)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementCallSerialize}");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCodeWithCompress(LuminLocalFieldData fieldData, StringBuilder sb)
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
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.Generic.List<{elementType}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else if (value.Count == length)");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
        sb.AppendLine();

        if (KnownValueTypes.Contains(elementType))
        {
            // index still points at [count:4] 鈥?DangerousRead reads count interally, spanOffset = 8+compressedLe

            sb.AppendLine("            reader.DangerousReadUnmanagedSpanWithCompress(ref index, ref span, out var spanOffset);");
            sb.AppendLine("            LuminPackMarshal.SetListSize(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
            sb.AppendLine("            reader.Advance(spanOffset);");
            return;
        }

        if (!elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            // index still at [count:4] 鈥?corect starting position for DangerousRead
            sb.AppendLine("                reader.DangerousReadUnmanagedSpanWithCompress(ref index, ref span, out var spanOffset);");
            sb.AppendLine("                LuminPackMarshal.SetListSize(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
            sb.AppendLine("                reader.Advance(spanOffset);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                reader.Advance(4);");
            sb.AppendLine("                reader.ReadSpan(ref index, length, ref span);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // Reference path only: now advance past the count header

        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();

        // Reference elements: propagate WithCompress only when leaf element is a KnownValueType.
        string elementCallDeserialize = KnownValueTypes.Contains(elementType) || IsLeafTypeKnownValueType(elementType)
            ? "reader.ReadValueWithCompress(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);"
            : "reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);";
        sb.AppendLine("            if (span.IsEmpty)");
        sb.AppendLine("            {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span);");
        sb.AppendLine("            for (nint i = 0; i < span.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementCallDeserialize}");
        sb.AppendLine("            }");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArrayEnd();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(value);");
        sb.AppendLine("            bool isFirst = true;");
        sb.AppendLine("            foreach (ref var item in span)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine("                else isFirst = false;");
        sb.AppendLine("                writer.SetFirstElement(true);");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Collections.Generic.List<" + elementType + ">();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine();
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                value.Add(item);");
        sb.AppendLine("            }");
    }
}

public static class DictionaryFormatter

{
    public static void GenerateJsonDictionarySerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteArrayStart();");
        sb.AppendLine("                    var key = item.Key;");
        sb.AppendLine("                    writer.WriteValue(in key);");
        sb.AppendLine("                    var itemValue = item.Value;");
        sb.AppendLine("                    writer.WriteValue(in itemValue);");
        sb.AppendLine("                    writer.WriteArrayEnd();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDictionaryDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string type = fieldData.TypeName;
        string baseType = GetDictionaryBaseType(type);
        string keyType = GetFirstGeneric(type);
        string valueType = GetSecondGeneric(type);
        bool interfaceType = baseType is "global::System.Collections.Generic.IDictionary" or "global::System.Collections.Generic.IReadOnlyDictionary";

        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (interfaceType)
        {
            sb.AppendLine("            var dictionary = new global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">();");
        }
        else
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("                value = new " + type + "();");
            sb.AppendLine("            else");
            sb.AppendLine("                value.Clear();");
        }
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayStart)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        sb.AppendLine("                if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.ReadValue(ref key);");
        sb.AppendLine("                    if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        reader.ReadValue(ref itemValue);");
        sb.AppendLine("                }");
        sb.AppendLine("                while (reader.Read())");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine("                }");
        if (interfaceType)
        {
            sb.AppendLine("                dictionary.Add(key, itemValue);");
        }
        else if (baseType == "global::System.Collections.Concurrent.ConcurrentDictionary")
        {
            sb.AppendLine("                value.TryAdd(key, itemValue);");
        }
        else
        {
            sb.AppendLine("                value.Add(key, itemValue);");
        }
        sb.AppendLine("            }");
        if (interfaceType)
        {
            sb.AppendLine("            value = dictionary;");
        }
    }

    private static string GetDictionaryBaseType(string typeName)
    {
        int separator = typeName.IndexOf('<');
        return separator < 0 ? typeName : typeName.Substring(0, separator);
    }

    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        bool unmanagedPair = fieldData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol dictionaryType &&
                             dictionaryType.TypeArguments.Length == 2 &&
                             dictionaryType.TypeArguments[0].IsUnmanagedType &&
                             dictionaryType.TypeArguments[1].IsUnmanagedType;
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            int count = value.Count;");
        if (unmanagedPair)
        {
            sb.AppendLine($"            writer.EnsureAdditionalCapacity(checked(sizeof(int) + count * (global::System.Runtime.CompilerServices.Unsafe.SizeOf<{keyType}>() + global::System.Runtime.CompilerServices.Unsafe.SizeOf<{valueType}>())));");
        }
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine("            writer.Advance(4);");
        if (unmanagedPair)
        {
            sb.AppendLine("            var dictionaryView = LuminPackMarshal.GetDictionaryView(value);");
            sb.AppendLine("            if (dictionaryView._count == count)");
            sb.AppendLine("            {");
            sb.AppendLine("                int version = dictionaryView._version;");
            sb.AppendLine("                ref var entryRef = ref LuminPackMarshal.GetArrayReference(dictionaryView._entries);");
            sb.AppendLine("                for (nint i = 0; i < count; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryRef, i);");
            sb.AppendLine("                    int entryOffset = index;");
            sb.AppendLine("                    entryOffset += writer.WriteUnmanaged(ref entryOffset, entry.Key);");
            sb.AppendLine("                    entryOffset += writer.WriteUnmanaged(ref entryOffset, entry.Value);");
            sb.AppendLine("                    writer.Advance(entryOffset - index);");
            sb.AppendLine("                }");
            sb.AppendLine("                if (dictionaryView._version != version)");
            sb.AppendLine("                    throw new global::System.InvalidOperationException(\"Collection was modified during serialization.\");");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        if (unmanagedPair)
        {
            sb.AppendLine("                int entryOffset = index;");
            sb.AppendLine("                entryOffset += writer.WriteUnmanaged(ref entryOffset, item.Key);");
            sb.AppendLine("                entryOffset += writer.WriteUnmanaged(ref entryOffset, item.Value);");
            sb.AppendLine("                writer.Advance(entryOffset - index);");
        }
        else
        {
            sb.AppendLine("                " + keyType + " key = item.Key;");
            sb.AppendLine("                writer.WriteValue(in key);");
            sb.AppendLine("                " + valueType + " itemValue = item.Value;");
            sb.AppendLine("                writer.WriteValue(in itemValue);");
        }
        sb.AppendLine("            }");
        if (!unmanagedPair)
        {
            sb.AppendLine("            writer.CheckBuffer();");
        }
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        GenerateDeserializeCodeCore(fieldData, sb, freshValue: false);
    }

    public static void GenerateFreshDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        GenerateDeserializeCodeCore(fieldData, sb, freshValue: true);
    }

    private static void GenerateDeserializeCodeCore(
        LuminLocalFieldData fieldData, StringBuilder sb, bool freshValue)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        bool unmanagedPair = fieldData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol dictionaryType &&
                             dictionaryType.TypeArguments.Length == 2 &&
                             dictionaryType.TypeArguments[0].IsUnmanagedType &&
                             dictionaryType.TypeArguments[1].IsUnmanagedType;

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
        if (freshValue)
        {
            sb.AppendLine($"                value = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(0);");
        }
        else
        {
            sb.AppendLine($"                value ??= new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(0);");
            sb.AppendLine("                value.Clear();");
        }
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var result = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(length);");
        sb.AppendLine("            var dictionaryView = LuminPackMarshal.GetDictionaryView(result);");
        sb.AppendLine("            ref var entryRef = ref LuminPackMarshal.GetArrayReference(dictionaryView._entries);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryRef, (nint)(uint)i);");
        if (unmanagedPair)
        {
            sb.AppendLine("                int entryOffset = index;");
            sb.AppendLine("                entryOffset += reader.ReadUnmanaged(ref entryOffset, out entry.Key);");
            sb.AppendLine("                entryOffset += reader.ReadUnmanaged(ref entryOffset, out entry.Value);");
            sb.AppendLine("                reader.Advance(entryOffset - index);");
        }
        else
        {
            sb.AppendLine("                reader.ReadValue(ref entry.Key!);");
            sb.AppendLine("                reader.ReadValue(ref entry.Value!);");
        }
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (!LuminPackMarshal.TryRebuildFreshDictionaryBuckets(dictionaryView, length))");
        sb.AppendLine("            {");
        sb.AppendLine($"                var fallback = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(length);");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref entryRef, (nint)(uint)i);");
        sb.AppendLine("                    fallback.Add(entry.Key, entry.Value);");
        sb.AppendLine("                }");
        sb.AppendLine("                result = fallback;");
        sb.AppendLine("            }");
        sb.AppendLine("            value = result;");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                nuint dictIndex = 0;");
        sb.AppendLine("                var dictView = LuminPackMarshal.GetDictionaryView(value);");
        sb.AppendLine("                ref var arrayRef = ref LuminPackMarshal.GetArrayReference(dictView._entries);");
        sb.AppendLine("                while (dictIndex < (uint)dictView._count)");
        sb.AppendLine("                {");
        sb.AppendLine("                    ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref arrayRef, dictIndex++);");
        sb.AppendLine("                    if (entry.Next >= -1)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        writer.WriteArrayStart();");
        sb.AppendLine("                        var key = entry.Key;");
        sb.AppendLine("                        writer.WriteValue(in key);");
        sb.AppendLine("                        var itemValue = entry.Value;");
        sb.AppendLine("                        writer.WriteValue(in itemValue);");
        sb.AppendLine("                        writer.WriteArrayEnd();");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("                value = new global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">();");
        sb.AppendLine("            else");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayStart)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        sb.AppendLine("                if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.ReadValue(ref key);");
        sb.AppendLine("                    if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        reader.ReadValue(ref itemValue);");
        sb.AppendLine("                }");
        sb.AppendLine("                while (reader.Read())");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine("                }");
        sb.AppendLine("                value[key] = itemValue;");
        sb.AppendLine("            }");
    }
}

public static class StackFormatter

{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                index += 4;");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            var span = LuminPackMarshal.GetStackSpan(Unsafe.AsRef(in value));");
        sb.AppendLine("            writer.WriteSpan(ref index, span);");
        sb.AppendLine("#else");
        sb.AppendLine("            var size = value.Count;");
        sb.AppendLine($"            var payloadLength = global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{GetFirstGeneric(fieldData.TypeName)}>() ? 0 : checked(size * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{GetFirstGeneric(fieldData.TypeName)}>());");
        sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(sizeof(int) + payloadLength));");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, size);");
        sb.AppendLine("            writer.Advance(sizeof(int));");
        sb.AppendLine("            if (size == 0) return;");
        sb.AppendLine($"            var buffer = global::System.Buffers.ArrayPool<{GetFirstGeneric(fieldData.TypeName)}>.Shared.Rent(size);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                value.CopyTo(buffer, 0);");
        sb.AppendLine($"                if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{GetFirstGeneric(fieldData.TypeName)}>())");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::System.Array.Reverse(buffer, 0, size);");
        sb.AppendLine("                    ref var destination = ref writer.GetSpanReference(index);");
        sb.AppendLine($"                    ref var source = ref global::System.Runtime.CompilerServices.Unsafe.As<{GetFirstGeneric(fieldData.TypeName)}, byte>(ref buffer[0]);");
        sb.AppendLine("                    global::System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref destination, ref source, (uint)payloadLength);");
        sb.AppendLine("                    writer.Advance(payloadLength);");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("                for (var i = size - 1; i >= 0; i--) writer.WriteValue(buffer[i]);");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Buffers.ArrayPool<{GetFirstGeneric(fieldData.TypeName)}>.Shared.Return(buffer, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{GetFirstGeneric(fieldData.TypeName)}>());");
        sb.AppendLine("            }");
        sb.AppendLine("#endif");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
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
        sb.AppendLine($"                value = new global::System.Collections.Generic.Stack<{GetFirstGeneric(fieldData.TypeName)}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            var span = LuminPackMarshal.GetStackSpan(value, length);");
        sb.AppendLine("            reader.ReadSpan(ref index, length, ref span);");
        sb.AppendLine("#else");
        sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{GetFirstGeneric(fieldData.TypeName)}>())");
        sb.AppendLine("            {");
        sb.AppendLine($"                var itemSize = global::System.Runtime.CompilerServices.Unsafe.SizeOf<{GetFirstGeneric(fieldData.TypeName)}>();");
        sb.AppendLine("                for (var i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    {GetFirstGeneric(fieldData.TypeName)} item = default!;");
        sb.AppendLine($"                    global::System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref global::System.Runtime.CompilerServices.Unsafe.As<{GetFirstGeneric(fieldData.TypeName)}, byte>(ref item), ref reader.GetSpanReference(index), (uint)itemSize);");
        sb.AppendLine("                    reader.Advance(itemSize);");
        sb.AppendLine("                    value.Push(item);");
        sb.AppendLine("                }");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {GetFirstGeneric(fieldData.TypeName)} item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                value.Push(item);");
        sb.AppendLine("            }");
        sb.AppendLine("#endif");
    }
}

public static class QueueFormatter

{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.EnsureAdditionalCapacity(sizeof(int));");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                index += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var size = value.Count;");
        sb.AppendLine("            if (size == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.EnsureAdditionalCapacity(sizeof(int));");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, 0);");
        sb.AppendLine("                index += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var payloadLength = global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>()");
        sb.AppendLine("                ? 0");
        sb.AppendLine($"                : checked(size * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>());");
        sb.AppendLine();
        sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(sizeof(int) * 4 + payloadLength));");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, size);");
        sb.AppendLine("            index += sizeof(int);");
        sb.AppendLine("            writer.WriteUnmanaged(0);");
        sb.AppendLine("            index += sizeof(int);");
        sb.AppendLine("            writer.WriteUnmanaged(0);");
        sb.AppendLine("            index += sizeof(int);");
        sb.AppendLine("            writer.WriteUnmanaged(size);");
        sb.AppendLine("            index += sizeof(int);");
        sb.AppendLine();
        sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine("            {");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("                global::LuminPack.Code.LuminPackMarshal.GetQueueSize(value, out _, out var head, out _);");
        sb.AppendLine("                var storage = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value);");
        sb.AppendLine("                var firstLength = global::System.Math.Min(size, storage.Length - head);");
        sb.AppendLine("                var firstSegment = storage.Slice(head, firstLength);");
        sb.AppendLine("                var secondSegment = storage.Slice(0, size - firstLength);");
        sb.AppendLine("                ref var destination = ref writer.GetSpanReference(index);");
        sb.AppendLine($"                var firstBytes = checked(firstSegment.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>());");
        sb.AppendLine("                if (firstBytes != 0)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    ref var source = ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref firstSegment.GetPinnableReference()!);");
        sb.AppendLine("                    global::System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref destination, ref source, (uint)firstBytes);");
        sb.AppendLine("                }");
        sb.AppendLine("                var secondBytes = payloadLength - firstBytes;");
        sb.AppendLine("                if (secondBytes != 0)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    ref var source = ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref secondSegment.GetPinnableReference()!);");
        sb.AppendLine("                    global::System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref destination, firstBytes), ref source, (uint)secondBytes);");
        sb.AppendLine("                }");
        sb.AppendLine("#else");
        sb.AppendLine($"                var buffer = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(size);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    value.CopyTo(buffer, 0);");
        sb.AppendLine("                    ref var destination = ref writer.GetSpanReference(index);");
        sb.AppendLine($"                    ref var source = ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref buffer[0]);");
        sb.AppendLine("                    global::System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref destination, ref source, (uint)payloadLength);");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine($"                    global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(buffer, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>());");
        sb.AppendLine("                }");
        sb.AppendLine("#endif");
        sb.AppendLine("                index += payloadLength;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            global::LuminPack.Code.LuminPackMarshal.GetQueueSize(value, out _, out var referenceHead, out _);");
        sb.AppendLine("            var referenceStorage = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value);");
        sb.AppendLine("            var referenceFirstLength = global::System.Math.Min(size, referenceStorage.Length - referenceHead);");
        sb.AppendLine("            var first = referenceStorage.Slice(referenceHead, referenceFirstLength);");
        sb.AppendLine("            var second = referenceStorage.Slice(0, size - referenceFirstLength);");
        sb.AppendLine("            foreach (ref var item in first)");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("            foreach (ref var item in second)");
        sb.AppendLine("                writer.WriteValue(in item);");
        sb.AppendLine("#else");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var current = item;");
        sb.AppendLine("                writer.WriteValue(in current);");
        sb.AppendLine("            }");
        sb.AppendLine("#endif");
        sb.AppendLine("            writer.CheckBuffer();");
    }

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
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.Generic.Queue<{GetFirstGeneric(fieldData.TypeName)}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("                value.EnsureCapacity(length);");
        sb.AppendLine("#endif");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 0)");
        sb.AppendLine("                return;");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanaged(out int storedHead);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanaged(out int storedTail);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanaged(out int storedSize);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            if (storedSize != length || storedHead < 0 || storedTail < 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidRange(storedSize, length);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            var span = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value, length);");
        sb.AppendLine("            var normalizedTail = global::LuminPack.Code.LuminPackMarshal.GetQueueSpan(value).Length == length ? 0 : length;");
        sb.AppendLine();
        sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var dest = ref global::LuminPack.Code.LuminPackMarshal.GetReference(ref span.GetPinnableReference());");
        sb.AppendLine();
        sb.AppendLine($"                var srcLength = checked(length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>());");
        sb.AppendLine();
        sb.AppendLine("                reader.EnsureReadable(index, srcLength);");
        sb.AppendLine("                global::System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref dest, ref reader.GetSpanReference(index), (uint)srcLength);");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(srcLength);");
        sb.AppendLine();
        sb.AppendLine("                global::LuminPack.Code.LuminPackMarshal.SetQueueSize(value, normalizedTail, 0, length);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.ReadValue(ref span[i]);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            global::LuminPack.Code.LuminPackMarshal.SetQueueSize(value, normalizedTail, 0, length);");
        sb.AppendLine("#else");
        sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine("            {");
        sb.AppendLine($"                var itemSize = global::System.Runtime.CompilerServices.Unsafe.SizeOf<{elementType}>();");
        sb.AppendLine("                for (var i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    {elementType} item = default!;");
        sb.AppendLine($"                    global::System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(ref global::System.Runtime.CompilerServices.Unsafe.As<{elementType}, byte>(ref item), ref reader.GetSpanReference(index), (uint)itemSize);");
        sb.AppendLine("                    reader.Advance(itemSize);");
        sb.AppendLine("                    value.Enqueue(item);");
        sb.AppendLine("                }");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                value.Enqueue(item);");
        sb.AppendLine("            }");
        sb.AppendLine("#endif");
    }
}

public static class HashSetFormatter

{
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
        sb.AppendLine("            while ((uint) setIndex < (uint) setView._count)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var local = ref Unsafe.Add(ref arrayRef, setIndex++);");
        sb.AppendLine("                if (local.Next >= -1)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteValue(local.Value);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

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
        sb.AppendLine("                ref var _entry = ref Unsafe.Add(ref _entryRef, i);");
        sb.AppendLine("                reader.ReadValue(ref _entry.Value!);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            LuminPackMarshal.RebuildHashSetBuckets(_setView, length);");
    }
}

public static class ConcurrentDictionaryFormatter

{
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
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var count = value.Count;");
        sb.AppendLine("            var i = 0;");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                i++;");
        sb.AppendLine("                var key = item.Key;");
        sb.AppendLine("                writer.WriteValue(in key);");
        sb.AppendLine("                var itemValue = item.Value;");
        sb.AppendLine("                writer.WriteValue(in itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine();
        sb.AppendLine("            if (i != count)");
        sb.AppendLine("                LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
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
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.Concurrent.ConcurrentDictionary<{GetFirstGeneric(fieldData.TypeName)}, {GetSecondGeneric(fieldData.TypeName)}>();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {GetFirstGeneric(fieldData.TypeName)} k = default!;");
        sb.AppendLine($"                {GetSecondGeneric(fieldData.TypeName)} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                value.TryAdd(k!, v);");
        sb.AppendLine("            }");
    }
}

public static class SortedDictionaryFormatter

{
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
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteValue(item.Key);");
        sb.AppendLine("                writer.WriteValue(item.Value);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
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
        sb.AppendLine($"                value = new global::System.Collections.Generic.SortedDictionary<{GetFirstGeneric(fieldData.TypeName)}, {GetSecondGeneric(fieldData.TypeName)}>();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {GetFirstGeneric(fieldData.TypeName)} k = default!;");
        sb.AppendLine($"                {GetSecondGeneric(fieldData.TypeName)} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                value.Add(k!, v);");
        sb.AppendLine("            }");
    }
}

public static class LinkedListFormatter

{
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

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
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
        sb.AppendLine($"                value = new global::System.Collections.Generic.LinkedList<{GetFirstGeneric(fieldData.TypeName)}>();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {GetFirstGeneric(fieldData.TypeName)} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                value.AddLast(v);");
        sb.AppendLine("            }");
    }
}

public static class SortedSetFormatter

{
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

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
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
        sb.AppendLine($"                value = new global::System.Collections.Generic.SortedSet<{GetFirstGeneric(fieldData.TypeName)}>();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {GetFirstGeneric(fieldData.TypeName)} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                value.Add(v);");
        sb.AppendLine("            }");
    }
}

public static class SortedListFormatter

{
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
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteValue(item.Key);");
        sb.AppendLine("                writer.WriteValue(item.Value);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
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
        sb.AppendLine($"                value = new global::System.Collections.Generic.SortedList<{GetFirstGeneric(fieldData.TypeName)}, {GetSecondGeneric(fieldData.TypeName)}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {GetFirstGeneric(fieldData.TypeName)} k = default!;");
        sb.AppendLine($"                {GetSecondGeneric(fieldData.TypeName)} v = default!;");
        sb.AppendLine($"                reader.ReadValue(ref k);");
        sb.AppendLine($"                reader.ReadValue(ref v);");
        sb.AppendLine("                value.Add(k!, v);");
        sb.AppendLine("            }");
    }
}

public static class BlockingCollectionFormatter

{
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
        sb.AppendLine($"            value = new global::System.Collections.Concurrent.BlockingCollection<{elementType}>();");
        sb.AppendLine();
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                value.Add(v);");
        sb.AppendLine("            }");
    }
}

public static class ConcurrentBagFormatter

{
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

public static class ConcurrentQueueFormatter

{
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
        sb.AppendLine($"                value = new global::System.Collections.Concurrent.ConcurrentQueue<{elementType}>();");
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
        sb.AppendLine("                value.Enqueue(v);");
        sb.AppendLine("            }");
    }
}

public static class ConcurrentStackFormatter

{
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
        sb.AppendLine("                    var v = rentArray[i];");
        sb.AppendLine("                    writer.WriteValue(in v);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(rentArray, clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>());");
        sb.AppendLine("            }");
    }

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

public static class CollectionFormatter

{
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
        sb.AppendLine("            var depth = 0;");
        sb.AppendLine();
        sb.AppendLine("            var list = global::LuminPack.Code.LuminPackCollectionAccess.GetUnderlyingIList(value, ref depth);");
        sb.AppendLine();
        sb.AppendLine("            if (list != null)");
        sb.AppendLine("            {");
        CollectionFormatterHelper.AppendStaticListSerialize(sb);
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var temp = item;");
        sb.AppendLine("                    writer.WriteValue(in temp);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
    }

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
        sb.AppendLine($"                value = new global::System.Collections.ObjectModel.Collection<{elementType}>(new global::System.Collections.Generic.List<{elementType}>(length));");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var list = LuminPackMarshal.As<global::System.Collections.ObjectModel.Collection<{elementType}>, CollectionView<{elementType}>>(ref value);");
        sb.AppendLine();
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(list.items!, length);");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadSpan(ref index, length, ref span);");
    }
}

public static class ObservableCollectionFormatter

{
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
        sb.AppendLine("            var depth = 0;");
        sb.AppendLine();
        sb.AppendLine("            var list = global::LuminPack.Code.LuminPackCollectionAccess.GetUnderlyingIList(value, ref depth);");
        sb.AppendLine();
        sb.AppendLine("            if (list != null)");
        sb.AppendLine("            {");
        CollectionFormatterHelper.AppendStaticListSerialize(sb);
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var temp = item;");
        sb.AppendLine("                    writer.WriteValue(in temp);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
    }

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
        sb.AppendLine($"                value = new global::System.Collections.ObjectModel.ObservableCollection<{elementType}>();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var list = LuminPackMarshal.As<global::System.Collections.ObjectModel.ObservableCollection<{elementType}>, ObservableCollectionView<{elementType}>>(ref value);");
        sb.AppendLine();
        sb.AppendLine($"            list.items = new global::System.Collections.Generic.List<{elementType}>(length);");
        sb.AppendLine();
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(list.items!, length);");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadSpan(ref index, length, ref span);");
    }
}

public static class ReadOnlyCollectionFormatter

{
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
        sb.AppendLine("            var depth = 0;");
        sb.AppendLine();
        sb.AppendLine("            var list = global::LuminPack.Code.LuminPackCollectionAccess.GetUnderlyingIList(value, ref depth);");
        sb.AppendLine();
        sb.AppendLine("            if (list != null)");
        sb.AppendLine("            {");
        CollectionFormatterHelper.AppendStaticListSerialize(sb);
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var temp = item;");
        sb.AppendLine("                    writer.WriteValue(in temp);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        sb.AppendLine($"            var array = reader.ReadArray<{elementType}>();");
        sb.AppendLine();
        sb.AppendLine("            if (array is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.ObjectModel.ReadOnlyCollection<{elementType}>(array);");
        sb.AppendLine("            }");
    }
}

public static class ReadOnlyObservableCollectionFormatter

{
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
        sb.AppendLine("            var depth = 0;");
        sb.AppendLine();
        sb.AppendLine("            var list = global::LuminPack.Code.LuminPackCollectionAccess.GetUnderlyingIList(value, ref depth);");
        sb.AppendLine();
        sb.AppendLine("            if (list != null)");
        sb.AppendLine("            {");
        CollectionFormatterHelper.AppendStaticListSerialize(sb);
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var temp = item;");
        sb.AppendLine("                    writer.WriteValue(in temp);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        sb.AppendLine($"            var array = reader.ReadArray<{elementType}>();");
        sb.AppendLine();
        sb.AppendLine("            if (array is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.ObjectModel.ReadOnlyObservableCollection<{elementType}>(new global::System.Collections.ObjectModel.ObservableCollection<{elementType}>(array));");
        sb.AppendLine("            }");
    }
}

public static class ReadOnlyCollectionBuilderFormatter

{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                var index = writer.CurrentIndex;");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var list = LuminPackMarshal.As<global::System.Collections.Immutable.ReadOnlyCollectionBuilder<{elementType}>, global::System.Collections.Generic.List<{elementType}>>(ref value);");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteSpan(LuminPackMarshal.GetListSpan(ref list));");
    }

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
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.Immutable.ReadOnlyCollectionBuilder<{elementType}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else if (value.Count == length)");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var list = LuminPackMarshal.As<global::System.Collections.Immutable.ReadOnlyCollectionBuilder<{elementType}>, global::System.Collections.Generic.List<{elementType}>>(ref value);");
        sb.AppendLine();
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(ref list, length);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            reader.ReadSpan(ref index, length, ref span);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArrayEnd();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            var list = LuminPackMarshal.As<global::System.Collections.Immutable.ReadOnlyCollectionBuilder<" + elementType + ">, global::System.Collections.Generic.List<" + elementType + ">(ref value);");
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(list);");
        sb.AppendLine("            bool isFirst = true;");
        sb.AppendLine("            foreach (ref var item in span)");
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
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Collections.Immutable.ReadOnlyCollectionBuilder<" + elementType + ">();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                value.Add(item);");
        sb.AppendLine("            }");
    }
}

public static class PriorityQueueFormatter

{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        var priorityType = GetSecondGeneric(fieldData.TypeName);

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
        sb.AppendLine("            foreach (var item in value.UnorderedItems)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteValue(item.Element);");
        sb.AppendLine("                writer.WriteValue(item.Priority);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        var priorityType = GetSecondGeneric(fieldData.TypeName);

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
        sb.AppendLine($"                value = new global::System.Collections.Generic.PriorityQueue<{elementType}, {priorityType}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} element = default!;");
        sb.AppendLine($"                {priorityType} priority = default!;");
        sb.AppendLine("                reader.ReadValue(ref element);");
        sb.AppendLine("                reader.ReadValue(ref priority);");
        sb.AppendLine("                value.Enqueue(element, priority);");
        sb.AppendLine("            }");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArrayEnd();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            foreach (var item in value.UnorderedItems)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArrayStart();");
        sb.AppendLine("                var element = item.Element;");
        sb.AppendLine("                writer.WriteValue(in element);");
        sb.AppendLine("                writer.WriteByteRaw((byte)',');");
        sb.AppendLine("                var priority = item.Priority;");
        sb.AppendLine("                writer.WriteValue(in priority);");
        sb.AppendLine("                writer.WriteArrayEnd();");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        var priorityType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Collections.Generic.PriorityQueue<" + elementType + ", " + priorityType + ">();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayStart)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                " + elementType + " element = default!;");
        sb.AppendLine("                reader.ReadValue(ref element);");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                " + priorityType + " priority = default!;");
        sb.AppendLine("                reader.ReadValue(ref priority);");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                value.Enqueue(element, priority);");
        sb.AppendLine("            }");
    }
}

internal static class CollectionFormatterHelper
{
    // Underlying-list fast path emitted with static element calls.
    public static void AppendStaticListSerialize(StringBuilder sb)
    {
        sb.AppendLine("                var span = global::LuminPack.Code.LuminPackMarshal.GetListSpan(list, list.Count);");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                foreach (ref var item in span)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteValue(in item!);");
        sb.AppendLine("                }");
        sb.AppendLine("                writer.CheckBuffer();");
    }
}
