using System.Text;

namespace LuminPack.SourceGenerator.Formatter;

public static class InterfaceEnumerableFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        if (TrySerializeOptimized<IEnumerable<T?>, T?>(ref writer, ref value)) return;");
        sb.AppendLine();
        sb.AppendLine("        ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (value.TryGetNonEnumeratedCountEx(out var count))");
        sb.AppendLine("        {");
        sb.AppendLine("            var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref writer, ref v);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            // write to tempbuffer(because we don't know length so can't write header)");
        sb.AppendLine("            var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var tempWriter = new LuminPackWriter(writer.OptionState);");
        sb.AppendLine("                tempWriter.SetWriteBuffer(tempBuffer);");
        sb.AppendLine();
        sb.AppendLine("                count = 0;");
        sb.AppendLine("                var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    parser.Serialize(ref tempWriter, ref v);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                tempWriter.Flush();");
        sb.AppendLine();
        sb.AppendLine("                // write to parameter writer.");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        value = reader.ReadArray<T?>();");
    }
}

public static class InterfaceCollectionFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        SerializeCollection<ICollection<T?>, T?>(ref writer, ref value);");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        value = ReadList<T?>(ref reader);");
    }
}

public static class InterfaceReadOnlyCollectionFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        SerializeReadOnlyCollection<IReadOnlyCollection<T?>, T?>(ref writer, ref value);");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        value = ReadList<T?>(ref reader);");
    }
}

public static class InterfaceListFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        SerializeCollection<IList<T?>, T?>(ref writer, ref value);");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        value = ReadList<T?>(ref reader);");
    }
}

public static class InterfaceReadOnlyListFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        SerializeReadOnlyCollection<IReadOnlyList<T?>, T?>(ref writer, ref value);");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        value = ReadList<T?>(ref reader);");
    }
}

public static class InterfaceDictionaryFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;");
        sb.AppendLine("        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("        writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        foreach (var item in value)");
        sb.AppendLine("        {");
        sb.AppendLine("            KeyValuePairParser.Serialize(keyParser, valueParser, ref writer, item!);");
        sb.AppendLine("        }");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        var dict = new Dictionary<TKey, TValue?>(_equalityComparer);");
        sb.AppendLine();
        sb.AppendLine("        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;");
        sb.AppendLine("        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);");
        sb.AppendLine("            dict.Add(k!, v);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = dict;");
    }
}

public static class InterfaceReadOnlyDictionaryFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;");
        sb.AppendLine("        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("        writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        foreach (var item in value)");
        sb.AppendLine("        {");
        sb.AppendLine("            KeyValuePairParser.Serialize(keyParser, valueParser, ref writer, item!);");
        sb.AppendLine("        }");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        var dict = new Dictionary<TKey, TValue?>(_equalityComparer);");
        sb.AppendLine();
        sb.AppendLine("        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;");
        sb.AppendLine("        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);");
        sb.AppendLine("            dict.Add(k!, v);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = dict;");
    }
}

public static class InterfaceLookupFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var parser = LuminPackParseProvider.Cache<IGrouping<TKey, TElement>>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("        writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        foreach (var item in value)");
        sb.AppendLine("        {");
        sb.AppendLine("            var v = item;");
        sb.AppendLine("            parser.Serialize(ref writer, ref v);");
        sb.AppendLine("        }");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        var dict = new Dictionary<TKey, IGrouping<TKey, TElement>>(_equalityComparer);");
        sb.AppendLine();
        sb.AppendLine("        var parser = LuminPackParseProvider.Cache<IGrouping<TKey, TElement>>.Parser!;");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            IGrouping<TKey, TElement>? item = default;");
        sb.AppendLine("            parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("            if (item != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                dict.Add(item.Key, item);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        value = new Lookup<TKey, TElement>(dict);");
    }
}

public static class InterfaceGroupingFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteValue(value.Key);");
        sb.AppendLine("        writer.WriteValue<IEnumerable<TElement>>(value); // write as IEnumerable<TElement>");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (!reader.TryReadObjectHead(ref index))");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var key = reader.ReadValue<TKey>();");
        sb.AppendLine("        var values = reader.ReadArray<TElement>() as IEnumerable<TElement>;");
        sb.AppendLine();
        sb.AppendLine("        if (key is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(key));");
        sb.AppendLine("        if (values is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(values));");
        sb.AppendLine();
        sb.AppendLine("        value = new Grouping<TKey, TElement>(key, values);");
    }
}

public static class InterfaceSetFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var parser = LuminPackParseProvider.Cache<T>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("        writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        foreach (var item in value)");
        sb.AppendLine("        {");
        sb.AppendLine("            var v = item;");
        sb.AppendLine("            parser.Serialize(ref writer, ref v);");
        sb.AppendLine("        }");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        var set = new HashSet<T?>(length, _equalityComparer);");
        sb.AppendLine();
        sb.AppendLine("        var parser = LuminPackParseProvider.Cache<T>.Parser!;");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            T? item = default;");
        sb.AppendLine("            parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("            set.Add(item);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = set;");
    }
}

public static class InterfaceReadOnlySetFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var parser = writer.GetParser<T>();");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("        writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        foreach (var item in value)");
        sb.AppendLine("        {");
        sb.AppendLine("            var v = item;");
        sb.AppendLine("            parser.Serialize(ref writer, ref v);");
        sb.AppendLine("        }");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        var set = new HashSet<T?>(length, _equalityComparer);");
        sb.AppendLine();
        sb.AppendLine("        var parser = reader.GetParser<T>();");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            T? item = default;");
        sb.AppendLine("            parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("            set.Add(item);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = set;");
    }
}