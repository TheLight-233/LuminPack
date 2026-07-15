using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class ListFormatter
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
        sb.AppendLine("                writer.WriteValue(item!);");
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
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        if (KnownValueTypes.Contains(elementType))
        {
            sb.AppendLine("            reader.DangerousReadUnmanagedSpan(ref index, ref span, out var spanOffset);");
            sb.AppendLine("            reader.Advance(spanOffset);");
            return;
        }

        if (!elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
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
            sb.AppendLine("                reader.ReadSpan(ref index, length, ref span);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        
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

    // ── Compress variants ────────────────────────────────────────────────────

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
            : "writer.WriteValue(item!);";
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
            // index still points at [count:4] — DangerousRead reads count internally, spanOffset = 8+compressedLen
            sb.AppendLine("            reader.DangerousReadUnmanagedSpanWithCompress(ref index, ref span, out var spanOffset);");
            sb.AppendLine("            LuminPackMarshal.SetListSize(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
            sb.AppendLine("            reader.Advance(spanOffset);");
            return;
        }

        if (!elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            // index still at [count:4] — correct starting position for DangerousRead
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
}

public static class DictionaryFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            var index = writer.CurrentIndex;");
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
        sb.AppendLine("            nuint dictIndex = 0;");
        sb.AppendLine("            var dictView = LuminPackMarshal.GetDictionaryView(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        sb.AppendLine("            ref var arrayRef = ref LuminPackMarshal.GetArrayReference(dictView._entries);");
        sb.AppendLine();
        sb.AppendLine("            while (dictIndex < (uint) dictView._count)");
        sb.AppendLine("            {");
        sb.AppendLine($"                ref var local = ref Unsafe.Add(ref arrayRef, dictIndex++);");
        sb.AppendLine("                if (local.Next >= -1)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteValue(local.Key!);");
        sb.AppendLine("                    writer.WriteValue(local.Value!);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);

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
        sb.AppendLine($"                value ??= new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(0);");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            value = new global::System.Collections.Generic.Dictionary<{keyType}, {valueType}>(length);");
        sb.AppendLine();
        sb.AppendLine("            var _dictView = LuminPackMarshal.GetDictionaryView(value);");
        sb.AppendLine("            ref var _entryRef = ref LuminPackMarshal.GetArrayReference(_dictView._entries);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var _entry = ref Unsafe.Add(ref _entryRef, (nint)(uint)i);");
        sb.AppendLine("                reader.ReadValue(ref _entry.Key!);");
        sb.AppendLine("                reader.ReadValue(ref _entry.Value!);");
        sb.AppendLine("            }");
        
        sb.AppendLine();
        sb.AppendLine($"            LuminPackMarshal.RebuildDictionaryBuckets(_dictView, length);");
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
        sb.AppendLine("            var span = LuminPackMarshal.GetStackSpan(Unsafe.AsRef(in value));");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteSpan(ref index, span);");
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
        sb.AppendLine("            var span = LuminPackMarshal.GetStackSpan(value, length);");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadSpan(ref index, length, ref span);");
    }
}

public static class QueueFormatter
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
        sb.AppendLine("            var span = LuminPackMarshal.GetQueueSpan(Unsafe.AsRef(in value), value.Count);");
        sb.AppendLine();
        sb.AppendLine("            LuminPackMarshal.GetQueueSize(Unsafe.AsRef(in value), out var head, out var tail, out var size);");
        sb.AppendLine();
        sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{GetFirstGeneric(fieldData.TypeName)}>())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (span.IsEmpty)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteCollectionHeader(ref index, 0);");
        sb.AppendLine();
        sb.AppendLine("                    writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteUnmanaged(head);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteUnmanaged(tail);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteUnmanaged(size);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine($"                var srcLength = Unsafe.SizeOf<{GetFirstGeneric(fieldData.TypeName)}>() * span.Length;");
        sb.AppendLine();
        sb.AppendLine("                ref var dest = ref writer.GetSpanReference(index);");
        sb.AppendLine($"                ref var src = ref Unsafe.As<{GetFirstGeneric(fieldData.TypeName)}, byte>(ref span.GetPinnableReference());");
        sb.AppendLine();
        sb.AppendLine("                Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)srcLength);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(srcLength);");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (span.IsEmpty)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteUnmanaged(head);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteUnmanaged(tail);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteUnmanaged(size);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in span)");
        sb.AppendLine("            {");
        sb.AppendLine("                var v = item;");
        sb.AppendLine("                writer.WriteValue(v);");
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
        sb.AppendLine("            var span = LuminPackMarshal.GetQueueSpan(value, length);");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanaged(out int head);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanaged(out int tail);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanaged(out int size);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            LuminPackMarshal.SetQueueSize(ref value, head, tail, size);");
        sb.AppendLine();
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (span.Length != length)");
        sb.AppendLine("            {");
        sb.AppendLine($"                span = LuminPackMarshal.AllocateUninitializedArray<{GetFirstGeneric(fieldData.TypeName)}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{GetFirstGeneric(fieldData.TypeName)}>())");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());");
        sb.AppendLine();
        sb.AppendLine($"                var srcLength = length * Unsafe.SizeOf<{GetFirstGeneric(fieldData.TypeName)}>();");
        sb.AppendLine();
        sb.AppendLine("                Unsafe.CopyBlockUnaligned(ref dest, ref reader.GetSpanReference(index), (uint)srcLength);");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(srcLength);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.ReadValue(ref span[i]);");
        sb.AppendLine("            }");
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
        sb.AppendLine("                writer.WriteValue(item.Key);");
        sb.AppendLine("                writer.WriteValue(item.Value);");
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
        sb.AppendLine("                writer.WriteValue(v);");
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
        sb.AppendLine("                writer.WriteValue(v);");
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
        sb.AppendLine("                writer.WriteValue(v);");
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
        sb.AppendLine("                writer.WriteValue(v);");
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
        sb.AppendLine("                    writer.WriteValue(v);");
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
        sb.AppendLine("            var list = ListParser.GetUnderlyingIList(value, ref depth);");
        sb.AppendLine();
        sb.AppendLine("            if (list != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ListParser.SerializePackable(ref writer, list);");
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
        sb.AppendLine("                    writer.WriteValue(temp);");
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
        sb.AppendLine("            var list = ListParser.GetUnderlyingIList(value, ref depth);");
        sb.AppendLine();
        sb.AppendLine("            if (list != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ListParser.SerializePackable(ref writer, list);");
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
        sb.AppendLine("                    writer.WriteValue(temp);");
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
        sb.AppendLine("            list.items = new global::System.Collections.Generic.List<{elementType}>(length);");
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
        sb.AppendLine("            var list = ListParser.GetUnderlyingIList(value, ref depth);");
        sb.AppendLine();
        sb.AppendLine("            if (list != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ListParser.SerializePackable(ref writer, list);");
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
        sb.AppendLine("                    writer.WriteValue(temp);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine("            var array = reader.ReadArray<{elementType}>();");
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
        sb.AppendLine("            var list = ListParser.GetUnderlyingIList(value, ref depth);");
        sb.AppendLine();
        sb.AppendLine("            if (list != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ListParser.SerializePackable(ref writer, list);");
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
        sb.AppendLine("                    writer.WriteValue(temp);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("            }");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        
        sb.AppendLine("            var array = reader.ReadArray<{elementType}>();");
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
}