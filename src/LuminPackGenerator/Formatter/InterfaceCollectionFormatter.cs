using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class InterfaceEnumerableFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var nullIndex = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref nullIndex);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (value is " + elementType + "[] array)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArray(array);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (value is global::System.Collections.Generic.List<" + elementType + "> list)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteSpan(global::LuminPack.Code.LuminPackMarshal.GetListSpan(list, list.Count));");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (global::LuminPack.Internal.EnumerableEx.TryGetNonEnumeratedCountEx(value, out var count))");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    writer.WriteValue(in v);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                // write to tempbuffer(because we don't know length so can't write header)");
        sb.AppendLine("                var tempBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    var tempWriter = new LuminPackWriter(writer.OptionState);");
        sb.AppendLine("                    tempWriter.SetWriteBuffer(tempBuffer);");
        sb.AppendLine();
        sb.AppendLine("                    count = 0;");
        sb.AppendLine("                    foreach (var item in value)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        count++;");
        sb.AppendLine("                        var v = item;");
        sb.AppendLine("                        tempWriter.WriteValue(in v);");
        sb.AppendLine("                    }");
        sb.AppendLine();
        sb.AppendLine("                    tempWriter.Flush();");
        sb.AppendLine();
        sb.AppendLine("                    // write to parameter writer.");
        sb.AppendLine("                    writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                    writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                    tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine("                    LuminBufferWriterPool.Return(tempBuffer);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        InterfaceCollectionFormatterHelper.AppendArrayDeserialize(sb, elementType);
    }
}

public static class InterfaceCollectionFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        InterfaceCollectionFormatterHelper.AppendCollectionSerialize(sb, elementType);
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        InterfaceCollectionFormatterHelper.AppendArrayDeserialize(sb, elementType);
    }
}

public static class InterfaceReadOnlyCollectionFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        InterfaceCollectionFormatterHelper.AppendCollectionSerialize(sb, elementType);
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        InterfaceCollectionFormatterHelper.AppendArrayDeserialize(sb, elementType);
    }
}

public static class InterfaceListFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var temp = item;");
        sb.AppendLine("                writer.WriteValue(in temp);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            var list = new global::System.Collections.Generic.List<" + elementType + ">(length);");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                list.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = list;");
    }
}

public static class InterfaceReadOnlyListFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        InterfaceCollectionFormatterHelper.AppendCollectionSerialize(sb, elementType);
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);

        InterfaceCollectionFormatterHelper.AppendArrayDeserialize(sb, elementType);
    }
}

public static class InterfaceDictionaryFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var valueType = GetSecondGeneric(fieldData.TypeName);

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
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var valueType = GetSecondGeneric(fieldData.TypeName);

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
        sb.AppendLine($"            var dict = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>();");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {keyType} k = default!;");
        sb.AppendLine($"                {valueType} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                dict.Add(k!, v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = dict;");
    }
}

public static class InterfaceReadOnlyDictionaryFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var valueType = GetSecondGeneric(fieldData.TypeName);

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
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var valueType = GetSecondGeneric(fieldData.TypeName);

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
        sb.AppendLine($"            var dict = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>();");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {keyType} k = default!;");
        sb.AppendLine($"                {valueType} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                dict.Add(k!, v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = dict;");
    }
}

public static class InterfaceLookupFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);

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
        sb.AppendLine($"                global::System.Collections.Generic.IEnumerable<{elementType}> elements = item;");
        sb.AppendLine("                writer.WriteValue(in elements);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);

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
        sb.AppendLine($"            var dict = new global::System.Collections.Generic.Dictionary<{keyType}, global::System.Linq.IGrouping<{keyType}, {elementType}>>();");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {keyType} key = default!;");
        sb.AppendLine($"                global::System.Collections.Generic.IEnumerable<{elementType}> values = default!;");
        sb.AppendLine("                reader.ReadValue(ref key);");
        sb.AppendLine("                reader.ReadValue(ref values);");
        sb.AppendLine("                if (key != null && values != null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    var grouping = new global::LuminPack.Utility.LuminPackGrouping<{keyType}, {elementType}>(key, values);");
        sb.AppendLine("                    dict.Add(key, grouping);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine($"            value = new global::LuminPack.Utility.LuminPackLookup<{keyType}, {elementType}>(dict);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
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
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::System.Linq.IGrouping<" + keyType + ", " + elementType + "> grouping = item;");
        sb.AppendLine("                writer.WriteValue(in grouping);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            var dict = new global::System.Collections.Generic.Dictionary<" + keyType + ", global::System.Linq.IGrouping<" + keyType + ", " + elementType + ">>();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                global::System.Linq.IGrouping<" + keyType + ", " + elementType + "> grouping = default!;");
        sb.AppendLine("                reader.ReadValue(ref grouping);");
        sb.AppendLine("                dict.Add(grouping.Key, grouping);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = new global::LuminPack.Utility.LuminPackLookup<" + keyType + ", " + elementType + ">(dict);");
    }
}

public static class InterfaceGroupingFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);

        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteValue(value.Key);");
        sb.AppendLine($"            global::System.Collections.Generic.IEnumerable<{elementType}> elements = value;");
        sb.AppendLine("            writer.WriteValue(in elements);");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);

        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadObjectHead(ref index))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            {keyType} key = default!;");
        sb.AppendLine($"            global::System.Collections.Generic.IEnumerable<{elementType}> values = default!;");
        sb.AppendLine("            reader.ReadValue(ref key);");
        sb.AppendLine("            reader.ReadValue(ref values);");
        sb.AppendLine();
        if (fieldData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol grouping &&
            !grouping.TypeArguments[0].IsValueType)
        {
            sb.AppendLine("            if (key is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(key));");
        }
        sb.AppendLine("            if (values is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(values));");
        sb.AppendLine();
        sb.AppendLine($"            value = new global::LuminPack.Utility.LuminPackGrouping<{keyType}, {elementType}>(key, values);");
    }

    public static void GenerateCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += 1;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            " + keyType + " key = value.Key;");
        sb.AppendLine("            evaluator.CalculateOffset(ref key);");
        sb.AppendLine("            global::System.Collections.Generic.IEnumerable<" + elementType + "> elements = value;");
        sb.AppendLine("            evaluator.CalculateOffset(ref elements);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteObjectStart();");
        sb.AppendLine("            writer.WritePropertyName(\"Key\");");
        sb.AppendLine("            var key = value.Key;");
        sb.AppendLine("            writer.WriteValue(in key);");
        sb.AppendLine("            writer.WritePropertyName(\"Elements\");");
        sb.AppendLine("            global::System.Collections.Generic.IEnumerable<" + elementType + "> elements = value;");
        sb.AppendLine("            writer.WriteValue(in elements);");
        sb.AppendLine("            writer.WriteObjectEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeObjectStart();");
        sb.AppendLine("            " + keyType + " key = default!;");
        sb.AppendLine("            global::System.Collections.Generic.IEnumerable<" + elementType + "> elements = default!;");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
        sb.AppendLine("                {");
        sb.AppendLine("                    string propertyName = reader.ReadString();");
        sb.AppendLine("                    reader.Read();");
        sb.AppendLine("                    if (propertyName == \"Key\")");
        sb.AppendLine("                        reader.ReadValue(ref key);");
        sb.AppendLine("                    else if (propertyName == \"Elements\")");
        sb.AppendLine("                        reader.ReadValue(ref elements);");
        sb.AppendLine("                    else");
        sb.AppendLine("                        reader.Skip();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            value = new global::LuminPack.Utility.LuminPackGrouping<" + keyType + ", " + elementType + ">(key!, elements!);");
    }
}

public static class InterfaceSetFormatter
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
        sb.AppendLine($"            var set = new global::System.Collections.Generic.HashSet<{elementType}>(length);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                set.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = set;");
    }
}

public static class InterfaceReadOnlySetFormatter
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
        sb.AppendLine($"            var set = new global::System.Collections.Generic.HashSet<{elementType}>(length);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                set.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = set;");
    }
}

internal static class InterfaceCollectionFormatterHelper
{
// This is the source-generator form of the interface collection helpers.
// The emitted code retains its array/List fast paths without runtime dispatch.
    public static void AppendCollectionSerialize(StringBuilder sb, string elementType)
    {
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (value is " + elementType + "[] array)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArray(array);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (value is global::System.Collections.Generic.List<" + elementType + "> list)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteSpan(global::LuminPack.Code.LuminPackMarshal.GetListSpan(list, list.Count));");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ref var collectionIndex = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            writer.WriteCollectionHeader(ref collectionIndex, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                writer.WriteValue(in v);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void AppendArrayDeserialize(StringBuilder sb, string elementType)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                reader.Advance(sizeof(int));");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.Advance(sizeof(int));");
        sb.AppendLine("            if (length == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = global::System.Array.Empty<" + elementType + ">();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            var array = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<" + elementType + ">(length);");
        sb.AppendLine("            ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetArrayReference(array);");
        sb.AppendLine("            for (nint i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = array;");
    }
}
