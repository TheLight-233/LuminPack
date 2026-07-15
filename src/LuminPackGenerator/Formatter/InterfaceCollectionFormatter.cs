using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class InterfaceEnumerableFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine("            if (global::LuminPack.Parsers.InterfaceCollectionParserUtils.TrySerializeOptimized<IEnumerable<T?>, T?>(ref writer, ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value))) return;");
        sb.AppendLine();
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value.TryGetNonEnumeratedCountEx(out var count))");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    writer.WriteValue(v);");
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
        sb.AppendLine("                        tempWriter.WriteValue(v);");
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
        
        sb.AppendLine($"            value = reader.ReadArray<{elementType}>();");
    }
}

public static class InterfaceCollectionFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine($"            global::LuminPack.Parsers.InterfaceCollectionParserUtils.SerializeCollection<global::System.Collections.Generic.ICollection<{elementType}>, {elementType}>(ref writer, ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine($"            value = global::LuminPack.Parsers.InterfaceCollectionParserUtils.ReadList<{elementType}>(ref reader);");
    }
}

public static class InterfaceReadOnlyCollectionFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine($"            global::LuminPack.Parsers.InterfaceCollectionParserUtils.SerializeReadOnlyCollection<global::System.Collections.Generic.IReadOnlyCollection<{elementType}>, {elementType}>(ref writer, ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine($"            value = global::LuminPack.Parsers.InterfaceCollectionParserUtils.ReadList<{elementType}>(ref reader);");
    }
}

public static class InterfaceListFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine($"            global::LuminPack.Parsers.InterfaceCollectionParserUtils.SerializeCollection<global::System.Collections.Generic.IList<{elementType}>, {elementType}>(ref writer, ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine($"            value = global::LuminPack.Parsers.InterfaceCollectionParserUtils.ReadList<{elementType}>(ref reader);");
    }
}

public static class InterfaceReadOnlyListFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine($"            global::LuminPack.Parsers.InterfaceCollectionParserUtils.SerializeReadOnlyCollection<global::System.Collections.Generic.IReadOnlyList<{elementType}>, {elementType}>(ref writer, ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine($"            value = global::LuminPack.Parsers.InterfaceCollectionParserUtils.ReadList<{elementType}>(ref reader);");
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
        sb.AppendLine($"                writer.WriteValue<global::System.Collections.Generic.IEnumerable<{elementType}>>(item);");
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
        sb.AppendLine($"                reader.ReadValue<global::System.Collections.Generic.IEnumerable<{elementType}>>(ref values);");
        sb.AppendLine("                if (key != null && values != null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    var grouping = new global::LuminPack.Code.Grouping<{keyType}, {elementType}>(key, values);");
        sb.AppendLine("                    dict.Add(key, grouping);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine($"            value = new global::LuminPack.Code.Lookup<{keyType}, {elementType}>(dict);");
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
        sb.AppendLine($"            writer.WriteValue<global::System.Collections.Generic.IEnumerable<{elementType}>>(value); // write as IEnumerable<TElement>");
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
        sb.AppendLine($"            reader.ReadValue<global::System.Collections.Generic.IEnumerable<{elementType}>>(ref values);");
        sb.AppendLine();
        sb.AppendLine("            if (key is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(key));");
        sb.AppendLine("            if (values is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(values));");
        sb.AppendLine();
        sb.AppendLine($"            value = new global::LuminPack.Code.Grouping<{keyType}, {elementType}>(key, values);");
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
        sb.AppendLine("                writer.WriteValue(v);");
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
        sb.AppendLine("                writer.WriteValue(v);");
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
