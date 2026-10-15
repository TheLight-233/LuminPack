using System.Text;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Appends JSON serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendSerializeJson(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Serializes <paramref name=\"value\"/> to a newly allocated JSON string.</summary>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this call, or <see langword=\"null\"/> for LuminPack defaults.</param>");
        sb.AppendLine("        /// <returns>A new string containing the complete JSON document.</returns>");
        sb.AppendLine("        /// <remarks>This convenience overload allocates the returned string and may use thread-static operation state.</remarks>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static string SerializeJson(" + typeName + " value, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var writerBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine();
        sb.AppendLine("            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();");
        sb.AppendLine();
        sb.AppendLine("            state.Init(option);");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var writer = new LuminPackJsonWriter(writerBuffer, state);");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteValue(in value);");
        sb.AppendLine();
        sb.AppendLine("                return writer.Option.StringEncoding is LuminPackStringEncoding.UTF8");
        sb.AppendLine("                    ? Encoding.UTF8.GetString(writer.GetSpan())");
        sb.AppendLine("                    : Encoding.Unicode.GetString(writer.GetSpan());");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("                LuminBufferWriterPool.Return(writerBuffer);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendSerializeJsonBuffer(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Serializes <paramref name=\"value\"/> as JSON into a reusable <see cref=\"LuminBufferWriter\"/>.</summary>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"writerBuffer\">The exclusive buffer and operation context that receives the JSON bytes.</param>");
        sb.AppendLine("        /// <remarks>");
        sb.AppendLine("        /// <para>The JSON encoding and other settings come from <see cref=\"LuminBufferWriter.Option\"/>; this overload");
        sb.AppendLine("        /// intentionally has no separate option parameter.</para>");
        sb.AppendLine("        /// <para>The option remains configured throughout the current Rent-to-Return lifetime and is restored to defaults");
        sb.AppendLine("        /// by <see cref=\"LuminBufferWriterPool.Return\"/>. Writer and reader temporary state are cleared as part of Return.</para>");
        sb.AppendLine("        /// <para>This high-performance path does not access serializer thread-static optional state. Do not use the same");
        sb.AppendLine("        /// buffer concurrently or reenter this method with that buffer.</para>");
        sb.AppendLine("        /// </remarks>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static void SerializeJson(" + typeName + " value, LuminBufferWriter writerBuffer)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = writerBuffer.WriterState;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var writer = new LuminPackJsonWriter(writerBuffer, state);");
        sb.AppendLine();
        sb.AppendLine("                writer.WriteValue(in value);");
        sb.AppendLine();
        sb.AppendLine("                writerBuffer.CompleteWrite(writer.CurrentIndex);");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (global::System.Exception exception)");
        sb.AppendLine("            {");
        sb.AppendLine("                writerBuffer.ResetCore();");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.Rethrow(exception);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.ResetOperationState();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendDeserializeJsonStringRef(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Deserializes JSON from <paramref name=\"buffer\"/> into <paramref name=\"value\"/>.</summary>");
        sb.AppendLine("        /// <param name=\"buffer\">The JSON document.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The number of consumed bytes in the selected encoding, after any required text transcoding.</returns>");
        sb.AppendLine("        /// <remarks>This overload performs any required text transcoding and may use pooled temporary storage.</remarks>");
        sb.AppendLine("        " + _currentAccess + " static int DeserializeJson(string buffer, ref " + typeName + " value, LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (buffer is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(buffer));");
        sb.AppendLine("            if (options == null || options.StringEncoding is LuminPackStringEncoding.UTF8)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (buffer.Length > 1024)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var byteCount = Encoding.UTF8.GetByteCount(buffer);");
        sb.AppendLine("                    byte[] byteBuffer = ArrayPool<byte>.Shared.Rent(byteCount);");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        sb.AppendLine("                        Encoding.UTF8.GetBytes(buffer, 0, buffer.Length, byteBuffer, 0);");
        sb.AppendLine("                        return DeserializeJson(byteBuffer.AsSpan(0, byteCount), ref value, options);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    finally");
        sb.AppendLine("                    {");
        sb.AppendLine("                        ArrayPool<byte>.Shared.Return(byteBuffer);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    Span<byte> span = stackalloc byte[buffer.Length * 3];");
        sb.AppendLine("                    var written = Encoding.UTF8.GetBytes(buffer, span);");
        sb.AppendLine("                    return DeserializeJson(span[..written], ref value, options);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                return DeserializeJson(buffer.AsSpan(), ref value, options);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendDeserializeJsonSpanByteRef(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Deserializes JSON from <paramref name=\"buffer\"/> into <paramref name=\"value\"/>.</summary>");
        sb.AppendLine("        /// <param name=\"buffer\">The JSON byte span.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The number of consumed bytes.</returns>");
        sb.AppendLine("        " + _currentAccess + " static int DeserializeJson(ReadOnlySpan<byte> buffer, ref " + typeName + " value, LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        sb.AppendLine("            state.Init(options);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var reader = new LuminPackJsonReader(ref buffer, state);");
        sb.AppendLine("                if (!reader.Read()) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"JSON input does not contain a value\");");
        sb.AppendLine("                reader.ReadValue(ref value);");
        sb.AppendLine("                reader.EnsureEndOfDocument();");
        sb.AppendLine("                return reader.CurrentIndex;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendDeserializeJsonSpanCharRef(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Deserializes JSON from <paramref name=\"buffer\"/> into <paramref name=\"value\"/>.</summary>");
        sb.AppendLine("        /// <param name=\"buffer\">The JSON character span.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The number of consumed bytes in the selected encoding, after any required text transcoding.</returns>");
        sb.AppendLine("        /// <remarks>This overload may use thread-static reader operation state.</remarks>");
        sb.AppendLine("        " + _currentAccess + " static int DeserializeJson(ReadOnlySpan<char> buffer, ref " + typeName + " value, LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (options?.StringEncoding is not LuminPackStringEncoding.UTF16)");
        sb.AppendLine("            {");
        sb.AppendLine("                int byteCount = Encoding.UTF8.GetByteCount(buffer);");
        sb.AppendLine("                byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    int written = Encoding.UTF8.GetBytes(buffer, rented);");
        sb.AppendLine("                    return DeserializeJson(rented.AsSpan(0, written), ref value, options);");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine("                    ArrayPool<byte>.Shared.Return(rented);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        sb.AppendLine("            state.Init(options);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var span = MemoryMarshal.Cast<char, byte>(buffer);");
        sb.AppendLine("                var reader = new LuminPackJsonReader(ref span, state);");
        sb.AppendLine("                if (!reader.Read()) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"JSON input does not contain a value\");");
        sb.AppendLine("                reader.ReadValue(ref value);");
        sb.AppendLine("                reader.EnsureEndOfDocument();");
        sb.AppendLine("                return reader.CurrentIndex;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendDeserializeJsonBufferRef(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Deserializes JSON from a reusable <see cref=\"LuminBufferWriter\"/> into <paramref name=\"value\"/>.</summary>");
        sb.AppendLine("        /// <param name=\"bufferWriter\">The buffer containing JSON bytes and the reader operation context.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <returns>The number of consumed bytes.</returns>");
        sb.AppendLine("        /// <remarks>");
        sb.AppendLine("        /// <para>JSON encoding and all other settings come from <see cref=\"LuminBufferWriter.Option\"/>. The option remains");
        sb.AppendLine("        /// configured for the current Rent-to-Return lifetime and is reset by <see cref=\"LuminBufferWriterPool.Return\"/>.</para>");
        sb.AppendLine("        /// <para>This high-performance overload uses the buffer-owned reader state and never accesses serializer");
        sb.AppendLine("        /// thread-static optional state. The buffer must be exclusively owned and cannot be used for same-instance reentry.</para>");
        sb.AppendLine("        /// </remarks>");
        sb.AppendLine("        " + _currentAccess + " static int DeserializeJson(LuminBufferWriter bufferWriter, ref " + typeName + " value)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = bufferWriter.ReaderState;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var bytes = bufferWriter.GetSpan();");
        sb.AppendLine("                var reader = new LuminPackJsonReader(ref bytes, state);");
        sb.AppendLine("                if (!reader.Read()) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"JSON input does not contain a value\");");
        sb.AppendLine("                reader.ReadValue(ref value);");
        sb.AppendLine("                reader.EnsureEndOfDocument();");
        sb.AppendLine("                return reader.CurrentIndex;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.ResetOperationState();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a generic <c>SerializeJson&lt;T&gt;(T, IBufferWriter&lt;byte&gt;)</c> overload writing the JSON
    /// bytes (in the configured encoding) into an arbitrary <c>IBufferWriter&lt;byte&gt;</c>.
    /// </summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericSerializeJsonBufferWriter(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Serializes <typeparamref name=\"T\"/> as JSON bytes into an arbitrary <see cref=\"IBufferWriter{T}\"/> using the generated generic dispatch.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The value type to serialize.</typeparam>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"bufferWriter\">The destination writer that receives the JSON bytes.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this call, or <see langword=\"null\"/> for LuminPack defaults.</param>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static void SerializeJson<T>(T value, global::System.Buffers.IBufferWriter<byte> bufferWriter, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (bufferWriter is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(bufferWriter));");
        sb.AppendLine("            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();");
        sb.AppendLine("            state.Init(option);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var writer = new LuminPackJsonWriter(bufferWriter, state, 256);");
        sb.AppendLine("                writer.WriteValue(in value);");
        sb.AppendLine("                writer.FlushToBufferWriter();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericSerializeJson(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Serializes a <typeparamref name=\"T\"/> to a newly allocated JSON string using the generated generic dispatch.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The value type to serialize.</typeparam>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this call, or <see langword=\"null\"/> for LuminPack defaults.</param>");
        sb.AppendLine("        /// <returns>A new string containing the complete JSON document.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static string SerializeJson<T>(T value, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var writerBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();");
        sb.AppendLine("            state.Init(option);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var writer = new LuminPackJsonWriter(writerBuffer, state);");
        sb.AppendLine("                writer.WriteValue(in value);");
        sb.AppendLine("                return writer.Option.StringEncoding is LuminPackStringEncoding.UTF8");
        sb.AppendLine("                    ? Encoding.UTF8.GetString(writer.GetSpan())");
        sb.AppendLine("                    : Encoding.Unicode.GetString(writer.GetSpan());");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("                LuminBufferWriterPool.Return(writerBuffer);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeJsonString(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes a <typeparamref name=\"T\"/> from a JSON string.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"buffer\">The JSON document.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The deserialized value.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static T DeserializeJson<T>(string buffer, LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (buffer is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(buffer));");
        sb.AppendLine("            return DeserializeJson<T>(buffer.AsSpan(), options);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeJsonSpanByte(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes a <typeparamref name=\"T\"/> from a JSON byte span.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"buffer\">The JSON byte span.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The deserialized value.</returns>");
        sb.AppendLine("        " + _currentAccess + " static T DeserializeJson<T>(ReadOnlySpan<byte> buffer, LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            T value = default!;");
        sb.AppendLine("            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        sb.AppendLine("            state.Init(options);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var reader = new LuminPackJsonReader(ref buffer, state);");
        sb.AppendLine("                if (!reader.Read()) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"JSON input does not contain a value\");");
        sb.AppendLine("                reader.ReadValue(ref value);");
        sb.AppendLine("                reader.EnsureEndOfDocument();");
        sb.AppendLine("                return value;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeJsonSpanChar(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes a <typeparamref name=\"T\"/> from a UTF-16 JSON character span.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"buffer\">The JSON character span.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The deserialized value.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static T DeserializeJson<T>(ReadOnlySpan<char> buffer, LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (options?.StringEncoding is not LuminPackStringEncoding.UTF16)");
        sb.AppendLine("            {");
        sb.AppendLine("                int byteCount = Encoding.UTF8.GetByteCount(buffer);");
        sb.AppendLine("                byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    int written = Encoding.UTF8.GetBytes(buffer, rented);");
        sb.AppendLine("                    return DeserializeJson<T>(rented.AsSpan(0, written), options);");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine("                    ArrayPool<byte>.Shared.Return(rented);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            T value = default!;");
        sb.AppendLine("            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        sb.AppendLine("            state.Init(options);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var bytes = MemoryMarshal.Cast<char, byte>(buffer);");
        sb.AppendLine("                var reader = new LuminPackJsonReader(ref bytes, state);");
        sb.AppendLine("                if (!reader.Read()) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"JSON input does not contain a value\");");
        sb.AppendLine("                reader.ReadValue(ref value);");
        sb.AppendLine("                reader.EnsureEndOfDocument();");
        sb.AppendLine("                return value;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeJsonBuffer(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes a <typeparamref name=\"T\"/> from JSON bytes in a reusable <see cref=\"LuminBufferWriter\"/>.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"bufferWriter\">The buffer containing JSON bytes and the reader operation context.</param>");
        sb.AppendLine("        /// <returns>The deserialized value.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static T DeserializeJson<T>(LuminBufferWriter bufferWriter)");
        sb.AppendLine("        {");
        sb.AppendLine("            T value = default!;");
        sb.AppendLine("            var state = bufferWriter.ReaderState;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var bytes = bufferWriter.GetSpan();");
        sb.AppendLine("                var reader = new LuminPackJsonReader(ref bytes, state);");
        sb.AppendLine("                if (!reader.Read()) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"JSON input does not contain a value\");");
        sb.AppendLine("                reader.ReadValue(ref value);");
        sb.AppendLine("                reader.EnsureEndOfDocument();");
        sb.AppendLine("                return value;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.ResetOperationState();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
