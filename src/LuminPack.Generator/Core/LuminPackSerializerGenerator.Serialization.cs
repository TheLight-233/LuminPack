using System.Text;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="nullableElementType">The raw scalar underlying a proven direct Nullable record, or null for other records.</param>
    /// <param name="timeZoneSnapshot">Whether the managed-input target can use a complete immutable snapshot without a buffer owner.</param>
    /// <param name="exactInterfaceWrite">The proven exact implementation for allocating writes, or null for the original entry.</param>
    /// <param name="smallHashSetWrite">Whether the referenced runtime supports grouped stores for this small raw element.</param>
    /// <param name="fixedEnum">Whether the existing enum operation writes exactly its native width.</param>
    /// <param name="rawSequenceWriteElement">The proven raw sequence element for direct allocating writes only.</param>
    private static void AppendSerialize(StringBuilder sb, string typeName, string nullableElementType, bool timeZoneSnapshot, string exactInterfaceWrite, bool smallHashSetWrite, bool fixedEnum, string rawSequenceWriteElement)
    {
        sb.AppendLine("        /// <summary>Serializes <paramref name=\"value\"/> to a newly allocated LuminPack binary payload.</summary>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this call, or <see langword=\"null\"/> for LuminPack defaults.</param>");
        sb.AppendLine("        /// <returns>A new byte array containing the complete serialized payload.</returns>");
        sb.AppendLine("        /// <remarks>");
        if (exactInterfaceWrite != null)
            sb.AppendLine("        /// The exact built-in implementation writes into the returned array; other implementations retain pooled output.");
        else if (_currentRawScalar || _currentDirectRecord)
            sb.AppendLine("        /// This record writes directly into the returned array without pooled operation state.");
        else
        {
            sb.AppendLine("        /// This convenience overload allocates the returned array and may use thread-static operation state.");
            sb.AppendLine("        /// Use the overload accepting a reusable <see cref=\"LuminBufferWriter\"/> to avoid the");
            sb.AppendLine("        /// serializer's thread-static optional-state path.");
        }
        sb.AppendLine("        /// </remarks>");
        // Pooled output writes a whole graph; inlining that body into the caller would spend the caller's inline
        // budget and leave the writer's cursor in memory. Direct records stay inlined.
        if (exactInterfaceWrite != null || _currentRawScalar || _currentDirectRecord || timeZoneSnapshot || rawSequenceWriteElement != null)
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static byte[] Serialize(in " + typeName + " value, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        if (exactInterfaceWrite != null)
        {
            AppendExactScalarInterfaceSerialize(sb, typeName, exactInterfaceWrite, smallHashSetWrite);
            return;
        }
        if (_currentRawScalar)
        {
            sb.AppendLine("            var buffer = AllocateUninitializedArray<byte>(Unsafe.SizeOf<" + typeName + ">());");
            sb.AppendLine("            LuminPackWriter.Write(ref MemoryMarshal.GetArrayDataReference(buffer), in value);");
            sb.AppendLine("            return buffer;");
            sb.AppendLine("        }");
            sb.AppendLine();
            return;
        }
        if (timeZoneSnapshot)
        {
            sb.AppendLine("            if (default(global::LuminPack.Option.LuminPackDefaultOptions).StringEncoding == LuminPackStringEncoding.UTF16 &&");
            sb.AppendLine("                default(global::LuminPack.Option.LuminPackDefaultOptions).StringRecording == LuminPackStringRecording.Length)");
            sb.AppendLine("            {");
            sb.AppendLine("                string? snapshot = value is null ? null : global::LuminPack.Code.LuminPackTimeZoneInfoCache.GetText(value);");
            // The exact width: Latin-1 snapshots use the compact one-byte-per-character record.
            sb.AppendLine("                int snapshotLength = global::LuminPack.Code.LuminPackTimeZoneInfoCache.Utf16SnapshotRecordLength(snapshot);");
            sb.AppendLine("                var snapshotBuffer = AllocateUninitializedArray<byte>(snapshotLength);");
            sb.AppendLine("                var snapshotSpan = snapshotBuffer.AsSpan();");
            sb.AppendLine("                var writer = new LuminPackWriter(ref snapshotSpan);");
            sb.AppendLine("                writer.WriteTimeZoneText(snapshot, capacityReserved: true);");
            sb.AppendLine("                return snapshotBuffer;");
            sb.AppendLine("            }");
        }
        if (_currentDirectRecord && typeName == "global::System.Numerics.BigInteger")
        {
            // The count-prefixed two's-complement record of RootWrite, with its byte count computed once.
            sb.AppendLine("            int byteCount = value.GetByteCount();");
            sb.AppendLine("            var buffer = AllocateUninitializedArray<byte>(checked(sizeof(int) + byteCount));");
            sb.AppendLine("            LuminPackWriter.Write(ref MemoryMarshal.GetArrayDataReference(buffer), byteCount);");
            sb.AppendLine("            value.TryWriteBytes(buffer.AsSpan(sizeof(int)), out _);");
            sb.AppendLine("            return buffer;");
            sb.AppendLine("        }");
            sb.AppendLine();
            return;
        }
        bool directWrite = _currentDirectRecord || rawSequenceWriteElement != null;
        if (directWrite)
        {
            if (rawSequenceWriteElement != null)
            {
                sb.AppendLine("            var directValue = value;");
                sb.AppendLine("            int directLength = checked(sizeof(int) + checked((int)directValue.Length) * Unsafe.SizeOf<" + rawSequenceWriteElement + ">());");
            }
            else if (fixedEnum)
                sb.AppendLine("            int directLength = Unsafe.SizeOf<" + typeName + ">();");
            else if (nullableElementType != null)
                sb.AppendLine("            int directLength = value.HasValue ? 1 + Unsafe.SizeOf<" + nullableElementType + ">() : 1;");
            else
            {
                sb.AppendLine("            int directLength = 0;");
                sb.AppendLine("            var directEvaluator = new LuminPackEvaluator(ref directLength);");
                sb.AppendLine("            var directValue = value;");
                sb.AppendLine("            global::LuminPack.LuminPackSerializer.Sizeof(ref directEvaluator, ref directValue);");
            }
            // A fixed record is a few primitives wide; a plain array lets the JIT drop the large-array branch and null check.
            sb.AppendLine(_currentFixedRecord || nullableElementType != null
                ? "            var directBuffer = new byte[directLength];"
                : "            var directBuffer = AllocateUninitializedArray<byte>(directLength);");
            sb.AppendLine("            var directSpan = directBuffer.AsSpan();");
            sb.AppendLine("            var writer = new LuminPackWriter(ref directSpan);");
            if (rawSequenceWriteElement != null)
                sb.AppendLine("            global::LuminPack.LuminPackSerializer.Serialize(ref writer, in directValue);");
            else
                AppendBinaryOperation(sb, read: false, indent: 12);
            if (_currentFixedRecord || nullableElementType != null)
            {
                // The constant evaluated width is exact, so no pooled fallback is emitted.
                sb.AppendLine("            return directBuffer;");
                sb.AppendLine("        }");
                sb.AppendLine();
                return;
            }
            sb.AppendLine("            if (writer.Position == directLength) return directBuffer;");
        }
        // The rented buffer's own writer state is exclusively owned by this operation and reset by Return.
        sb.AppendLine("            var writerBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var " + (directWrite || timeZoneSnapshot ? "fallbackWriter" : "writer") + " = new LuminPackWriter(writerBuffer);");
        sb.AppendLine();
        if (directWrite || timeZoneSnapshot)
            sb.AppendLine("                global::LuminPack.LuminPackSerializer.Serialize(ref fallbackWriter, in " +
                (rawSequenceWriteElement != null ? "directValue" : "value") + ");");
        else
            AppendBinaryOperation(sb, read: false);
        sb.AppendLine();
        string writerName = directWrite || timeZoneSnapshot ? "fallbackWriter" : "writer";
        sb.AppendLine("                var buffer = AllocateUninitializedArray<byte>(" + writerName + ".CurrentIndex);");
        sb.AppendLine("                " + writerName + ".GetSpan().CopyTo(buffer.AsSpan());");
        sb.AppendLine("                return buffer;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(writerBuffer);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendSerializeBuffer(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Serializes <paramref name=\"value\"/> into a reusable <see cref=\"LuminBufferWriter\"/>.</summary>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"writerBuffer\">The exclusive buffer and operation context that receives the payload.</param>");
        sb.AppendLine("        /// <remarks>");
        sb.AppendLine("        /// <para>This high-performance overload obtains its configuration from declaration-time options.</para>");
        sb.AppendLine("        /// <para>Appends at the current position and retains reference tracking across calls. Call");
        sb.AppendLine("        /// <see cref=\"LuminBufferWriter.ResetForReuse\"/> before an independent session or after a failed write.</para>");
        sb.AppendLine("        /// <para>The method uses the writer state owned by <paramref name=\"writerBuffer\"/> and does not access the");
        sb.AppendLine("        /// serializer's thread-static optional state. The caller must exclusively own the buffer until it is returned.</para>");
        sb.AppendLine("        /// </remarks>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static void Serialize(in " + typeName + " value, LuminBufferWriter writerBuffer)");
        sb.AppendLine("        {");
        sb.AppendLine("            var writer = new LuminPackWriter(writerBuffer);");
        AppendBinaryOperation(sb, read: false);
        sb.AppendLine("            writerBuffer.CompleteWrite(writer.CurrentIndex);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="managedInput">Whether a raw scalar may be written through a state bound directly to the result array.</param>
    private static void AppendGenericSerialize(StringBuilder sb, bool managedInput)
    {
        sb.AppendLine("        /// <summary>Serializes a <typeparamref name=\"T\"/> to a newly allocated LuminPack binary payload using the generated generic dispatch.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The value type to serialize.</typeparam>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this call, or <see langword=\"null\"/> for LuminPack defaults.</param>");
        sb.AppendLine("        /// <returns>A new byte array containing the complete serialized payload.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static byte[] Serialize<T>(in T value, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var writerBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                SerializeCore(writerBuffer, in value);");
        sb.AppendLine("                var source = writerBuffer.GetSpan();");
        sb.AppendLine("                var buffer = AllocateUninitializedArray<byte>(source.Length);");
        sb.AppendLine("                source.CopyTo(buffer.AsSpan());");
        sb.AppendLine("                return buffer;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(writerBuffer);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a concrete or fallback <c>Serialize</c> buffer-writer overload. Serialization
    /// runs through the existing LuminBufferWriter fast path (native buffer, concrete method calls only)
    /// and the published payload is copied into the caller's <c>IBufferWriter&lt;byte&gt;</c> (e.g. a
    /// <c>System.IO.Pipelines.PipeWriter</c> or <c>ArrayBufferWriter&lt;byte&gt;</c>).  The internal
    /// generated call path is untouched, so no interface virtual dispatch enters the hot path.
    /// </summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The concrete payload, or null for the generic fallback.</param>
    private static void AppendSerializeBufferWriter(StringBuilder sb, string typeName = null)
    {
        sb.AppendLine("        /// <summary>Serializes a value into an arbitrary byte buffer writer.</summary>");
        if (typeName == null) sb.AppendLine("        /// <typeparam name=\"T\">The value type to serialize.</typeparam>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"bufferWriter\">The destination writer, e.g. <c>System.IO.Pipelines.PipeWriter</c> or <c>ArrayBufferWriter&lt;byte&gt;</c>.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this call, or <see langword=\"null\"/> for LuminPack defaults.</param>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static void Serialize" + (typeName == null ? "<T>" : "") + "(in " + (typeName ?? "T") + " value, global::System.Buffers.IBufferWriter<byte> bufferWriter, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (bufferWriter is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(bufferWriter));");
        sb.AppendLine("            var writerBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        if (typeName == null)
        {
            sb.AppendLine("                var writer = new LuminPackWriter(writerBuffer);");
            sb.AppendLine("                writer.WriteValue(in value);");
            sb.AppendLine("                writerBuffer.CompleteWrite(writer.CurrentIndex);");
        }
        else
        {
            sb.AppendLine("                var writer = new LuminPackWriter(writerBuffer);");
            AppendBinaryOperation(sb, read: false);
            sb.AppendLine("                writerBuffer.CompleteWrite(writer.CurrentIndex);");
        }
        sb.AppendLine("                var source = writerBuffer.GetSpan();");
        sb.AppendLine("                var destination = bufferWriter.GetSpan(source.Length);");
        sb.AppendLine("                source.CopyTo(destination);");
        sb.AppendLine("                bufferWriter.Advance(source.Length);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(writerBuffer);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
