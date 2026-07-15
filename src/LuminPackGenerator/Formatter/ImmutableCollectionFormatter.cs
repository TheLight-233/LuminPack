using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class ImmutableArrayFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value.IsDefault)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                writer.WriteSpan(value.AsSpan());");
        sb.AppendLine("            }");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine($"            var array = reader.ReadArray<{elementType}>();");
        sb.AppendLine();
        sb.AppendLine("            if (array is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (array.Length == 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableArray<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine($"            value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(array);");
        sb.AppendLine("#else");
        sb.AppendLine($"            value = global::System.Collections.Immutable.ImmutableArray.Create<{elementType}>();");
        sb.AppendLine($"            ref var view = ref LuminPackMarshal.As<global::System.Collections.Immutable.ImmutableArray<{elementType}>, ImmutableArrayView<{elementType}>>(ref value);");
        sb.AppendLine("            view.array = array;");
        sb.AppendLine("#endif");
    }
}

public static class ImmutableListFormatter
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
        sb.AppendLine($"            var parser = writer.GetParser<{elementType}>();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref writer, ref v);");
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
        sb.AppendLine("            if (length == 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableList<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = reader.ReadValue<{elementType}>();");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableList.Create(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var parser = LuminPackParseProvider.Cache<{elementType}>.Parser!;");
        sb.AppendLine();
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableList.CreateBuilder<{elementType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("                builder.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}

public static class ImmutableQueueFormatter
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
        sb.AppendLine("            var tempBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var tempWriter = new LuminPackWriter(tempBuffer, writer.OptionState);");
        sb.AppendLine();
        sb.AppendLine("                var count = 0;");
        sb.AppendLine($"                var parser = writer.GetParser<{elementType}>();");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    parser.Serialize(ref tempWriter, ref v);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(tempBuffer);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableQueue<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length is 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = reader.ReadValue<{elementType}>();");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableQueue.Create(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var rentArray = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(length);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                var parser = LuminPackParseProvider.Cache<{elementType}>.Parser!;");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    parser.Deserialize(ref reader, ref rentArray[i]);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (rentArray.Length == length)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableQueue.Create(rentArray);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableQueue.CreateRange((new global::System.ArraySegment<{elementType}>(rentArray, 0, length)).AsEnumerable());");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(rentArray, clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>());");
        sb.AppendLine("            }");
    }
}

public static class ImmutableStackFormatter
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
        sb.AppendLine("            var tempBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var tempWriter = new LuminPackWriter(tempBuffer, writer.OptionState);");
        sb.AppendLine();
        sb.AppendLine("                var count = 0;");
        sb.AppendLine($"                var parser = writer.GetParser<{elementType}>();");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    parser.Serialize(ref tempWriter, ref v);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(tempBuffer);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableStack<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length is 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = reader.ReadValue<{elementType}>();");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableStack.Create(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var rentArray = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(length);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                var parser = LuminPackParseProvider.Cache<{elementType}>.Parser!;");
        sb.AppendLine("                for (int i = length - 1; i >= 0; i--)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    parser.Deserialize(ref reader, ref rentArray[i]);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (rentArray.Length == length)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableStack.Create(rentArray);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableStack.CreateRange((new global::System.ArraySegment<{elementType}>(rentArray, 0, length)).AsEnumerable());");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(rentArray, clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>());");
        sb.AppendLine("            }");
    }
}

public static class ImmutableDictionaryFormatter
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
        sb.AppendLine($"            var keyFormatter = writer.GetParser<{keyType}>();");
        sb.AppendLine($"            var valueFormatter = writer.GetParser<{valueType}>();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine($"                KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableDictionary<{keyType}, {valueType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var keyParser = LuminPackParseProvider.Cache<{keyType}>.Parser!;");
        sb.AppendLine($"            var valueParser = LuminPackParseProvider.Cache<{valueType}>.Parser!;");
        sb.AppendLine();
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<{keyType}, {valueType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);");
        sb.AppendLine("                builder.Add(k!, v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}

public static class ImmutableHashSetFormatter
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
        sb.AppendLine($"            var parser = writer.GetParser<{elementType}>();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref writer, ref v);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableHashSet<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = reader.ReadValue<{elementType}>();");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableHashSet.Create<{elementType}>(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var parser = LuminPackParseProvider.Cache<{elementType}>.Parser!;");
        sb.AppendLine();
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableHashSet.CreateBuilder<{elementType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("                builder.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}

public static class ImmutableSortedDictionaryFormatter
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
        sb.AppendLine($"            var keyFormatter = writer.GetParser<{keyType}>();");
        sb.AppendLine($"            var valueFormatter = writer.GetParser<{valueType}>();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine($"                KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableSortedDictionary<{keyType}, {valueType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var keyParser = LuminPackParseProvider.Cache<{keyType}>.Parser!;");
        sb.AppendLine($"            var valueParser = LuminPackParseProvider.Cache<{valueType}>.Parser!;");
        sb.AppendLine();
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableSortedDictionary.CreateBuilder<{keyType}, {valueType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);");
        sb.AppendLine("                builder.Add(k!, v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}

public static class ImmutableSortedSetFormatter
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
        sb.AppendLine($"            var parser = writer.GetParser<{elementType}>();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref writer, ref v);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableSortedSet<{elementType}>.Empty;");
        sb.AppendLine("                if (keyComparer != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = value.WithComparer(keyComparer);");
        sb.AppendLine("                }");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = reader.ReadValue<{elementType}>();");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableSortedSet.Create(keyComparer, item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var parser = LuminPackParseProvider.Cache<{elementType}>.Parser!;");
        sb.AppendLine();
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableSortedSet.CreateBuilder<{elementType}>(keyComparer);");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("                builder.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}

public static class InterfaceImmutableListFormatter
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
        sb.AppendLine($"            var parser = writer.GetParser<{elementType}>();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref writer, ref v);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableList<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = reader.ReadValue<{elementType}>();");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableList.Create(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var parser = LuminPackParseProvider.Cache<{elementType}>.Parser!;");
        sb.AppendLine();
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableList.CreateBuilder<{elementType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("                builder.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}

public static class InterfaceImmutableQueueFormatter
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
        sb.AppendLine("            var tempBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var tempWriter = new LuminPackWriter(tempBuffer, writer.OptionState);");
        sb.AppendLine();
        sb.AppendLine("                var count = 0;");
        sb.AppendLine($"                var parser = writer.GetParser<{elementType}>();");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    parser.Serialize(ref tempWriter, ref v);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(tempBuffer);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableQueue<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length is 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = reader.ReadValue<{elementType}>();");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableQueue.Create(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var rentArray = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(length);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                var parser = LuminPackParseProvider.Cache<{elementType}>.Parser!;");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    parser.Deserialize(ref reader, ref rentArray[i]);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (rentArray.Length == length)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableQueue.Create(rentArray);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableQueue.CreateRange((new global::System.ArraySegment<{elementType}>(rentArray, 0, length)).AsEnumerable());");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(rentArray, clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>());");
        sb.AppendLine("            }");
    }
}

public static class InterfaceImmutableStackFormatter
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
        sb.AppendLine("            var tempBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var tempWriter = new LuminPackWriter(tempBuffer, writer.OptionState);");
        sb.AppendLine();
        sb.AppendLine("                var count = 0;");
        sb.AppendLine($"                var parser = writer.GetParser<{elementType}>();");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    parser.Serialize(ref tempWriter, ref v);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                tempBuffer.WriteToAndReset(ref writer);");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(tempBuffer);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableStack<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length is 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = reader.ReadValue<{elementType}>();");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableStack.Create(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var rentArray = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(length);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                var parser = LuminPackParseProvider.Cache<{elementType}>.Parser!;");
        sb.AppendLine("                for (int i = length - 1; i >= 0; i--)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    parser.Deserialize(ref reader, ref rentArray[i]);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (rentArray.Length == length)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableStack.Create(rentArray);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    value = global::System.Collections.Immutable.ImmutableStack.CreateRange((new global::System.ArraySegment<{elementType}>(rentArray, 0, length)).AsEnumerable());");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine($"                global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(rentArray, clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>());");
        sb.AppendLine("            }");
    }
}

public static class InterfaceImmutableDictionaryFormatter
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
        sb.AppendLine($"            var keyParser = writer.GetParser<{keyType}>();");
        sb.AppendLine($"            var valueParser = writer.GetParser<{valueType}>();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine($"                KeyValuePairParser.Serialize(keyParser, valueParser, ref writer, item!);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableDictionary<{keyType}, {valueType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var keyParser = LuminPackParseProvider.Cache<{keyType}>.Parser!;");
        sb.AppendLine($"            var valueParser = LuminPackParseProvider.Cache<{valueType}>.Parser!;");
        sb.AppendLine();
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<{keyType}, {valueType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);");
        sb.AppendLine("                builder.Add(k!, v);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}

public static class InterfaceImmutableSetFormatter
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
        sb.AppendLine($"            var parser = writer.GetParser<{elementType}>();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                parser.Serialize(ref writer, ref v);");
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableHashSet<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = reader.ReadValue<{elementType}>();");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableHashSet.Create(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            var parser = LuminPackParseProvider.Cache<{elementType}>.Parser!;");
        sb.AppendLine();
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableHashSet.CreateBuilder<{elementType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                parser.Deserialize(ref reader, ref item);");
        sb.AppendLine("                builder.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}