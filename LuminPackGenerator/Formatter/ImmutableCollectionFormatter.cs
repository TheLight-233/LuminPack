using System.Text;

namespace LuminPack.SourceGenerator.Formatter;

public static class ImmutableArrayFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        if (value.IsDefault)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteSpan(value.AsSpan());");
        sb.AppendLine("        }");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        var array = reader.ReadArray<T?>();");
        sb.AppendLine();
        sb.AppendLine("        if (array is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = default;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (array.Length == 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableArray<T?>.Empty;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("        value = ImmutableCollectionsMarshal.AsImmutableArray(array);");
        sb.AppendLine("#else");
        sb.AppendLine("        value = ImmutableArray.Create<T?>();");
        sb.AppendLine("        ref var view = ref LuminPackMarshal.As<ImmutableArray<T?>, ImmutableArrayView<T?>>(ref value);");
        sb.AppendLine("        view.array = array;");
        sb.AppendLine("#endif");
    }
}

public static class ImmutableListFormatter
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
        sb.AppendLine("        var parser = writer.GetParser<T?>();");
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
        sb.AppendLine("        if (length == 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableList<T?>.Empty;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (length == 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            var item = reader.ReadValue<T>();");
        sb.AppendLine("            value = ImmutableList.Create(item);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        var builder = ImmutableList.CreateBuilder<T?>();");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            T? item = default;");
        sb.AppendLine("            parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("            builder.Add(item);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = builder.ToImmutable();");
    }
}

public static class ImmutableQueueFormatter
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
        sb.AppendLine("        // ImmutableQueue<T> has no Count, so use similar serialization of IEnumerable<T>");
        sb.AppendLine();
        sb.AppendLine("        var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var tempWriter = new LuminPackWriter(writer.OptionState);");
        sb.AppendLine();
        sb.AppendLine("            tempWriter.SetWriteBuffer(tempBuffer);");
        sb.AppendLine();
        sb.AppendLine("            var count = 0;");
        sb.AppendLine("            var parser = writer.GetParser<T?>();");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                count++;");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref tempWriter, ref v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            tempWriter.Flush();");
        sb.AppendLine();
        sb.AppendLine("            // write to parameter writer.");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine("            tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableQueue<T?>.Empty;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (length is 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            var item = reader.ReadValue<T>();");
        sb.AppendLine("            value = ImmutableQueue.Create(item);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // ImmutableQueue<T> has no builder");
        sb.AppendLine();
        sb.AppendLine("        var rentArray = ArrayPool<T?>.Shared.Rent(length);");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                parser.Deserialize(ref reader, ref rentArray[i]);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (rentArray.Length == length)");
        sb.AppendLine("            {");
        sb.AppendLine("                // we can use T[] ctor");
        sb.AppendLine("                value = ImmutableQueue.Create(rentArray);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                // IEnumerable<T> method");
        sb.AppendLine("                value = ImmutableQueue.CreateRange((new ArraySegment<T?>(rentArray, 0, length)).AsEnumerable());");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            ArrayPool<T?>.Shared.Return(rentArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());");
        sb.AppendLine("        }");
    }
}

public static class ImmutableStackFormatter
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
        sb.AppendLine("        var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var tempWriter = new LuminPackWriter(writer.OptionState);");
        sb.AppendLine();
        sb.AppendLine("            tempWriter.SetWriteBuffer(tempBuffer);");
        sb.AppendLine();
        sb.AppendLine("            var count = 0;");
        sb.AppendLine("            var parser = writer.GetParser<T?>();");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                count++;");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref tempWriter, ref v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            tempWriter.Flush();");
        sb.AppendLine();
        sb.AppendLine("            // write to parameter writer.");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine("            tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableStack<T?>.Empty;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (length is 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            var item = reader.ReadValue<T>();");
        sb.AppendLine("            value = ImmutableStack.Create(item);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var rentArray = ArrayPool<T?>.Shared.Rent(length);");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                parser.Deserialize(ref reader, ref rentArray[i]);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (rentArray.Length == length)");
        sb.AppendLine("            {");
        sb.AppendLine("                // we can use T[] ctor");
        sb.AppendLine("                value = ImmutableStack.Create(rentArray);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                // IEnumerable<T> method");
        sb.AppendLine("                value = ImmutableStack.CreateRange((new ArraySegment<T?>(rentArray, 0, length)).AsEnumerable());");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            ArrayPool<T?>.Shared.Return(rentArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());");
        sb.AppendLine("        }");
    }
}

public static class ImmutableDictionaryFormatter
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
        sb.AppendLine("        var keyFormatter = writer.GetParser<TKey>();");
        sb.AppendLine("        var valueFormatter = writer.GetParser<TValue>();");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("        writer.Advance(4);");
        sb.AppendLine("        foreach (var item in value)");
        sb.AppendLine("        {");
        sb.AppendLine("            KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableDictionary<TKey, TValue?>.Empty;");
        sb.AppendLine("            if (keyEqualityComparer != null || valueEqualityComparer != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = value.WithComparers(keyEqualityComparer, valueEqualityComparer);");
        sb.AppendLine("            }");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;");
        sb.AppendLine("        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        var builder = ImmutableDictionary.CreateBuilder(keyEqualityComparer, valueEqualityComparer);");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);");
        sb.AppendLine("            builder.Add(k!, v);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = builder.ToImmutable();");
    }
}

public static class ImmutableHashSetFormatter
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
        sb.AppendLine("        var parser = writer.GetParser<T?>();");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableHashSet<T?>.Empty;");
        sb.AppendLine("            if (equalityComparer != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = value.WithComparer(equalityComparer);");
        sb.AppendLine("            }");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (length == 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            var item = reader.ReadValue<T>();");
        sb.AppendLine("            value = ImmutableHashSet.Create(equalityComparer, item);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        var builder = ImmutableHashSet.CreateBuilder(equalityComparer);");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            T? item = default;");
        sb.AppendLine("            parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("            builder.Add(item);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = builder.ToImmutable();");
    }
}

public static class ImmutableSortedDictionaryFormatter
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
        sb.AppendLine("        var keyFormatter = writer.GetParser<TKey>();");
        sb.AppendLine("        var valueFormatter = writer.GetParser<TValue>();");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("        writer.Advance(4);");
        sb.AppendLine("        foreach (var item in value)");
        sb.AppendLine("        {");
        sb.AppendLine("            KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableSortedDictionary<TKey, TValue?>.Empty;");
        sb.AppendLine("            if (keyComparer != null || valueEqualityComparer != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = value.WithComparers(keyComparer, valueEqualityComparer);");
        sb.AppendLine("            }");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;");
        sb.AppendLine("        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        var builder = ImmutableSortedDictionary.CreateBuilder(keyComparer, valueEqualityComparer);");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);");
        sb.AppendLine("            builder.Add(k!, v);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = builder.ToImmutable();");
    }
}

public static class ImmutableSortedSetFormatter
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
        sb.AppendLine("        var parser = writer.GetParser<T?>();");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableSortedSet<T?>.Empty;");
        sb.AppendLine("            if (keyComparer != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = value.WithComparer(keyComparer);");
        sb.AppendLine("            }");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (length == 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            var item = reader.ReadValue<T>();");
        sb.AppendLine("            value = ImmutableSortedSet.Create(keyComparer, item);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        var builder = ImmutableSortedSet.CreateBuilder(keyComparer);");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            T? item = default;");
        sb.AppendLine("            parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("            builder.Add(item);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = builder.ToImmutable();");
    }
}

public static class InterfaceImmutableListFormatter
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
        sb.AppendLine("        var parser = writer.GetParser<T?>();");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableList<T?>.Empty;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (length == 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            var item = reader.ReadValue<T>();");
        sb.AppendLine("            value = ImmutableList.Create(item);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        var builder = ImmutableList.CreateBuilder<T?>();");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            T? item = default;");
        sb.AppendLine("            parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("            builder.Add(item);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = builder.ToImmutable();");
    }
}

public static class InterfaceImmutableQueueFormatter
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
        sb.AppendLine("        // ImmutableQueue<T> has no Count, so use similar serialization of IEnumerable<T>");
        sb.AppendLine();
        sb.AppendLine("        var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var tempWriter = new LuminPackWriter(writer.OptionState);");
        sb.AppendLine();
        sb.AppendLine("            tempWriter.SetWriteBuffer(tempBuffer);");
        sb.AppendLine();
        sb.AppendLine("            var count = 0;");
        sb.AppendLine("            var parser = writer.GetParser<T?>();");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                count++;");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref tempWriter, ref v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            tempWriter.Flush();");
        sb.AppendLine();
        sb.AppendLine("            // write to parameter writer.");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine("            tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableQueue<T?>.Empty;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (length is 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            var item = reader.ReadValue<T>();");
        sb.AppendLine("            value = ImmutableQueue.Create(item);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // ImmutableQueue<T> has no builder");
        sb.AppendLine();
        sb.AppendLine("        var rentArray = ArrayPool<T?>.Shared.Rent(length);");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                parser.Deserialize(ref reader, ref rentArray[i]);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (rentArray.Length == length)");
        sb.AppendLine("            {");
        sb.AppendLine("                // we can use T[] ctor");
        sb.AppendLine("                value = ImmutableQueue.Create(rentArray);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                // IEnumerable<T> method");
        sb.AppendLine("                value = ImmutableQueue.CreateRange((new ArraySegment<T?>(rentArray, 0, length)).AsEnumerable());");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            ArrayPool<T?>.Shared.Return(rentArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());");
        sb.AppendLine("        }");
    }
}

public static class InterfaceImmutableStackFormatter
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
        sb.AppendLine("        var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var tempWriter = new LuminPackWriter(writer.OptionState);");
        sb.AppendLine();
        sb.AppendLine("            tempWriter.SetWriteBuffer(tempBuffer);");
        sb.AppendLine();
        sb.AppendLine("            var count = 0;");
        sb.AppendLine("            var parser = writer.GetParser<T?>();");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                count++;");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref tempWriter, ref v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            tempWriter.Flush();");
        sb.AppendLine();
        sb.AppendLine("            // write to parameter writer.");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine("            tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = ImmutableStack<T?>.Empty;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (length is 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            var item = reader.ReadValue<T>();");
        sb.AppendLine("            value = ImmutableStack.Create(item);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var rentArray = ArrayPool<T?>.Shared.Rent(length);");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                parser.Deserialize(ref reader, ref rentArray[i]);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (rentArray.Length == length)");
        sb.AppendLine("            {");
        sb.AppendLine("                // we can use T[] ctor");
        sb.AppendLine("                value = ImmutableStack.Create(rentArray);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                // IEnumerable<T> method");
        sb.AppendLine("                value = ImmutableStack.CreateRange((new ArraySegment<T?>(rentArray, 0, length)).AsEnumerable());");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            ArrayPool<T?>.Shared.Return(rentArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());");
        sb.AppendLine("        }");
    }
}

public static class InterfaceImmutableDictionaryFormatter
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
        sb.AppendLine("        var keyParser = writer.GetParser<TKey>();");
        sb.AppendLine("        var valueParser = writer.GetParser<TValue>();");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (keyEqualityComparer != null || valueEqualityComparer != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = ImmutableDictionary<TKey, TValue?>.Empty.WithComparers(keyEqualityComparer, valueEqualityComparer);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value = ImmutableDictionary<TKey, TValue?>.Empty;");
        sb.AppendLine("            }");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;");
        sb.AppendLine("        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        var builder = ImmutableDictionary.CreateBuilder(keyEqualityComparer, valueEqualityComparer);");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);");
        sb.AppendLine("            builder.Add(k!, v);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = builder.ToImmutable();");
    }
}

public static class InterfaceImmutableSetFormatter
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
        sb.AppendLine("        var parser = writer.GetParser<T?>();");
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
        sb.AppendLine("        if (length is 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (equalityComparer != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = ImmutableHashSet<T?>.Empty.WithComparer(equalityComparer);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value = ImmutableHashSet<T?>.Empty;");
        sb.AppendLine("            }");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (length == 1)");
        sb.AppendLine("        {");
        sb.AppendLine("            var item = reader.ReadValue<T>();");
        sb.AppendLine("            value = ImmutableHashSet.Create(equalityComparer, item);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var parser = LuminPackParseProvider.Cache<T?>.Parser!;");
        sb.AppendLine();
        sb.AppendLine("        var builder = ImmutableHashSet.CreateBuilder(equalityComparer);");
        sb.AppendLine("        for (int i = 0; i < length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            T? item = default;");
        sb.AppendLine("            parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("            builder.Add(item);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = builder.ToImmutable();");
    }
}