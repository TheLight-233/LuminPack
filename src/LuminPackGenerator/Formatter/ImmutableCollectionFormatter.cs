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
        sb.AppendLine($"            ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.Immutable.ImmutableArray<{elementType}>, global::LuminPack.Code.ImmutableArrayView<{elementType}>>(ref value);");
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
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableList.CreateBuilder<{elementType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                reader.ReadValue(ref item);");
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
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    tempWriter.WriteValue(v);");
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
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    reader.ReadValue(ref rentArray[i]);");
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
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    tempWriter.WriteValue(v);");
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
        sb.AppendLine("                for (int i = length - 1; i >= 0; i--)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    reader.ReadValue(ref rentArray[i]);");
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
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var key = item.Key;");
        sb.AppendLine("                writer.WriteValue(key);");
        sb.AppendLine("                var itemValue = item.Value;");
        sb.AppendLine("                writer.WriteValue(itemValue);");
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
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<{keyType}, {valueType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {keyType} k = default!;");
        sb.AppendLine($"                {valueType} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
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
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableHashSet.CreateBuilder<{elementType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                reader.ReadValue(ref item);");
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
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var key = item.Key;");
        sb.AppendLine("                writer.WriteValue(key);");
        sb.AppendLine("                var itemValue = item.Value;");
        sb.AppendLine("                writer.WriteValue(itemValue);");
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
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableSortedDictionary.CreateBuilder<{keyType}, {valueType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {keyType} k = default!;");
        sb.AppendLine($"                {valueType} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
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
        sb.AppendLine($"            global::System.Collections.Generic.IComparer<{elementType}>? keyComparer = null;");
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
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableSortedSet.CreateBuilder<{elementType}>(keyComparer);");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                reader.ReadValue(ref item);");
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
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableList.CreateBuilder<{elementType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                reader.ReadValue(ref item);");
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
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    tempWriter.WriteValue(v);");
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
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    reader.ReadValue(ref rentArray[i]);");
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
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    count++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    tempWriter.WriteValue(v);");
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
        sb.AppendLine("                for (int i = length - 1; i >= 0; i--)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    reader.ReadValue(ref rentArray[i]);");
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
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var key = item.Key;");
        sb.AppendLine("                writer.WriteValue(key);");
        sb.AppendLine("                var itemValue = item.Value;");
        sb.AppendLine("                writer.WriteValue(itemValue);");
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
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<{keyType}, {valueType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {keyType} k = default!;");
        sb.AppendLine($"                {valueType} v = default!;");
        sb.AppendLine("                reader.ReadValue(ref k);");
        sb.AppendLine("                reader.ReadValue(ref v);");
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
        sb.AppendLine($"            var builder = global::System.Collections.Immutable.ImmutableHashSet.CreateBuilder<{elementType}>();");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                builder.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
    }
}

/// <summary>
/// JSON counterparts of the immutable collection parsers.  The generated code
/// keeps their token loops and pooled-buffer construction, while every element
/// is handled by a generated extension without runtime instances.
/// </summary>
internal static class ImmutableCollectionFormatterHelper
{
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseTypeName(fieldData.TypeName);
        if (IsDictionary(baseType))
        {
            AppendDictionarySerialize(sb);
            return;
        }

        string elementType = GetFirstGeneric(fieldData.TypeName);
        if (baseType == "global::System.Collections.Immutable.ImmutableArray")
        {
            sb.AppendLine("            if (value.IsDefault)");
        }
        else
        {
            sb.AppendLine("            if (value == null)");
        }
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayStart();");
        if (baseType == "global::System.Collections.Immutable.ImmutableArray")
        {
            sb.AppendLine("            if (value.Length == 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteArrayEnd();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        else if (baseType is not ("global::System.Collections.Immutable.ImmutableQueue" or
                     "global::System.Collections.Immutable.IImmutableQueue" or
                     "global::System.Collections.Immutable.ImmutableStack" or
                     "global::System.Collections.Immutable.IImmutableStack"))
        {
            sb.AppendLine("            if (value.Count == 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteArrayEnd();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            bool isFirst = true;");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine("                else isFirst = false;");
        sb.AppendLine("                writer.SetFirstElement(true);");
        sb.AppendLine("                var temp = item;");
        sb.AppendLine("                global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in temp);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseTypeName(fieldData.TypeName);
        if (IsDictionary(baseType))
        {
            AppendDictionaryDeserialize(fieldData, sb, baseType);
            return;
        }

        AppendSequenceDeserialize(fieldData, sb, baseType);
    }

    private static void AppendSequenceDeserialize(LuminLocalFieldData fieldData, StringBuilder sb, string baseType)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        bool immutableArray = baseType == "global::System.Collections.Immutable.ImmutableArray";
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine(immutableArray ? "                value = default;" : "                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            const int initialCapacity = 16;");
        sb.AppendLine("            var buffer = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(initialCapacity);");
        sb.AppendLine("            var count = 0;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                while (reader.Read())");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                        continue;");
        sb.AppendLine("                    if (count >= buffer.Length)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var newBuffer = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(buffer.Length * 2);");
        sb.AppendLine("                        global::System.Array.Copy(buffer, newBuffer, count);");
        sb.AppendLine("                        global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">());");
        sb.AppendLine("                        buffer = newBuffer;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    " + elementType + " item = default!;");
        sb.AppendLine("                    global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref item);");
        sb.AppendLine("                    buffer[count++] = item;");
        sb.AppendLine("                }");
        AppendSequenceConstruction(sb, elementType, baseType);
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">());");
        sb.AppendLine("            }");
    }

    private static void AppendSequenceConstruction(StringBuilder sb, string elementType, string baseType)
    {
        if (baseType == "global::System.Collections.Immutable.ImmutableArray")
        {
            sb.AppendLine("                if (count == 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    value = global::System.Collections.Immutable.ImmutableArray<" + elementType + ">.Empty;");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("                var finalArray = new " + elementType + "[count];");
            sb.AppendLine("                global::System.Array.Copy(buffer, finalArray, count);");
            sb.AppendLine("#if NET8_0_OR_GREATER");
            sb.AppendLine("                value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(finalArray);");
            sb.AppendLine("#else");
            sb.AppendLine("                value = global::System.Collections.Immutable.ImmutableArray.Create<" + elementType + ">();");
            sb.AppendLine("                ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.Immutable.ImmutableArray<" + elementType + ">, global::LuminPack.Code.ImmutableArrayView<" + elementType + ">>(ref value);");
            sb.AppendLine("                view.array = finalArray;");
            sb.AppendLine("#endif");
            return;
        }

        string target = baseType switch
        {
            "global::System.Collections.Immutable.ImmutableList" or "global::System.Collections.Immutable.IImmutableList" => "ImmutableList",
            "global::System.Collections.Immutable.ImmutableHashSet" or "global::System.Collections.Immutable.IImmutableSet" => "ImmutableHashSet",
            "global::System.Collections.Immutable.ImmutableSortedSet" => "ImmutableSortedSet",
            "global::System.Collections.Immutable.ImmutableQueue" or "global::System.Collections.Immutable.IImmutableQueue" => "ImmutableQueue",
            "global::System.Collections.Immutable.ImmutableStack" or "global::System.Collections.Immutable.IImmutableStack" => "ImmutableStack",
            _ => throw new global::System.InvalidOperationException("Unsupported immutable collection: " + baseType)
        };

        sb.AppendLine("                if (count == 0)");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = global::System.Collections.Immutable." + target + "<" + elementType + ">.Empty;");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        if (target == "ImmutableStack")
        {
            sb.AppendLine("                global::System.Array.Reverse(buffer, 0, count);");
        }
        if (target is "ImmutableQueue" or "ImmutableStack")
        {
            sb.AppendLine("                value = global::System.Collections.Immutable." + target + ".CreateRange((new global::System.ArraySegment<" + elementType + ">(buffer, 0, count)).AsEnumerable());");
            return;
        }
        sb.AppendLine("                var builder = global::System.Collections.Immutable." + target + ".CreateBuilder<" + elementType + ">();");
        sb.AppendLine("                for (int i = 0; i < count; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    builder.Add(buffer[i]);");
        sb.AppendLine("                }");
        sb.AppendLine("                value = builder.ToImmutable();");
    }

    private static void AppendDictionarySerialize(StringBuilder sb)
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
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArrayStart();");
        sb.AppendLine("                var key = item.Key;");
        sb.AppendLine("                global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in key);");
        sb.AppendLine("                var itemValue = item.Value;");
        sb.AppendLine("                global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in itemValue);");
        sb.AppendLine("                writer.WriteArrayEnd();");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    private static void AppendDictionaryDeserialize(LuminLocalFieldData fieldData, StringBuilder sb, string baseType)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        string target = baseType == "global::System.Collections.Immutable.ImmutableSortedDictionary"
            ? "ImmutableSortedDictionary"
            : "ImmutableDictionary";
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            var builder = global::System.Collections.Immutable." + target + ".CreateBuilder<" + keyType + ", " + valueType + ">();");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayStart)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        sb.AppendLine("                if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref key);");
        sb.AppendLine("                    if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref itemValue);");
        sb.AppendLine("                }");
        sb.AppendLine("                while (reader.Read())");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine("                }");
        sb.AppendLine("                builder.Add(key!, itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = builder.ToImmutable();");
    }

    private static bool IsDictionary(string baseType) => baseType is
        "global::System.Collections.Immutable.ImmutableDictionary" or
        "global::System.Collections.Immutable.ImmutableSortedDictionary" or
        "global::System.Collections.Immutable.IImmutableDictionary";

    private static string GetBaseTypeName(string typeName)
    {
        int separator = typeName.IndexOf('<');
        return separator < 0 ? typeName : typeName.Substring(0, separator);
    }
}
