using System.Text;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendDeserializeSpanRef(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Deserializes a binary span into <paramref name=\"value\"/>.</summary>");
        sb.AppendLine("        /// <param name=\"buffer\">The span containing a complete or leading LuminPack payload.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The number of bytes consumed.</returns>");
        sb.AppendLine("        /// <remarks>This convenience overload may use thread-static reader operation state.</remarks>");
        sb.AppendLine("        " + _currentAccess + " static int Deserialize(");
        sb.AppendLine("            ReadOnlySpan<byte> buffer, ref " + typeName + " value,");
        sb.AppendLine("            LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        sb.AppendLine("            state.Init(options);");
        sb.AppendLine();
        sb.AppendLine("            var reader = new LuminPackReader(ref buffer, state);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        AppendBinaryOperation(sb, read: true);
        sb.AppendLine("                return reader.GetCurrentSpanIndex();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendDeserializeBufferRef(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Deserializes into <paramref name=\"value\"/> from a reusable <see cref=\"LuminBufferWriter\"/>.</summary>");
        sb.AppendLine("        /// <param name=\"bufferWriter\">The buffer containing binary data and the reader operation context.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <returns>The number of bytes consumed from the buffer.</returns>");
        sb.AppendLine("        /// <remarks>");
        sb.AppendLine("        /// Configuration comes from <see cref=\"LuminBufferWriter.Option\"/> and persists until the buffer is returned.");
        sb.AppendLine("        /// <see cref=\"LuminBufferWriterPool.Return\"/> restores defaults and clears both operation states. This method does");
        sb.AppendLine("        /// not use thread-static optional state and requires exclusive ownership of <paramref name=\"bufferWriter\"/>.");
        sb.AppendLine("        /// </remarks>");
        sb.AppendLine("        " + _currentAccess + " static int Deserialize(LuminBufferWriter bufferWriter, ref " + typeName + " value)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = bufferWriter.ReaderState;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var bytes = bufferWriter.GetSpan();");
        sb.AppendLine("                var reader = new LuminPackReader(ref bytes, state);");
        AppendBinaryOperation(sb, read: true);
        sb.AppendLine("                return reader.GetCurrentSpanIndex();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.ResetOperationState();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendDeserializeSequenceRef(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Deserializes a possibly multi-segment sequence into <paramref name=\"value\"/>.</summary>");
        sb.AppendLine("        /// <param name=\"buffer\">The sequence containing a LuminPack payload.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The number of bytes consumed.</returns>");
        sb.AppendLine("        /// <remarks>This convenience overload may use thread-static reader operation state.</remarks>");
        sb.AppendLine("        " + _currentAccess + " static int Deserialize(");
        sb.AppendLine("            ReadOnlySequence<byte> buffer, ref " + typeName + " value,");
        sb.AppendLine("            LuminPackSerializerOption? options = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!RuntimeHelpers.IsReferenceOrContainsReferences<" + typeName + ">())");
        sb.AppendLine("            {");
        AppendUnmanagedSequenceRead(sb, typeName, indent: 16);
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (!buffer.IsSingleSegment)");
        sb.AppendLine("            {");
        sb.AppendLine("                var staging = LuminBufferWriterPool.Rent();");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    staging.LoadSequence(in buffer);");
        sb.AppendLine("                    ReadOnlySpan<byte> contiguous = staging.GetSpan();");
        sb.AppendLine("                    var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        sb.AppendLine("                    state.Init(options);");
        sb.AppendLine("                    var reader = new LuminPackReader(ref contiguous, state);");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        AppendBinaryOperation(sb, read: true, indent: 24);
        sb.AppendLine("                        return reader.GetCurrentSpanIndex();");
        sb.AppendLine("                    }");
        sb.AppendLine("                    finally { state.Reset(); }");
        sb.AppendLine("                }");
        sb.AppendLine("                finally { LuminBufferWriterPool.Return(staging); }");
        sb.AppendLine("            }");
        sb.AppendLine("            {");
        sb.AppendLine("            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        sb.AppendLine("            state.Init(options);");
        sb.AppendLine();
        sb.AppendLine("            var reader = new LuminPackReader(ref buffer, state);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        AppendBinaryOperation(sb, read: true);
        sb.AppendLine("                return reader.GetCurrentSpanIndex();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeSpan(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes a <typeparamref name=\"T\"/> from a binary span using the generated generic dispatch.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"buffer\">The span containing a complete or leading LuminPack payload.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The deserialized value.</returns>");
        sb.AppendLine("        /// <remarks>This convenience overload may use thread-static reader operation state.</remarks>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static T Deserialize<T>(ReadOnlySpan<byte> buffer, LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            T value = default!;");
        sb.AppendLine("            Deserialize<T>(buffer, ref value, options);");
        sb.AppendLine("            return value;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeSpanRef(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes a binary span into <paramref name=\"value\"/> using the generated generic dispatch.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"buffer\">The span containing a complete or leading LuminPack payload.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The number of bytes consumed.</returns>");
        sb.AppendLine("        " + _currentAccess + " static int Deserialize<T>(");
        sb.AppendLine("            ReadOnlySpan<byte> buffer, ref T value,");
        sb.AppendLine("            LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        sb.AppendLine("            state.Init(options);");
        sb.AppendLine("            try { return DeserializeCore(buffer, state, ref value); }");
        sb.AppendLine("            finally { state.Reset(); }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeBuffer(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes a <typeparamref name=\"T\"/> from a reusable <see cref=\"LuminBufferWriter\"/>.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"bufferWriter\">The buffer containing binary data and the reader operation context.</param>");
        sb.AppendLine("        /// <returns>The deserialized value.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static T Deserialize<T>(LuminBufferWriter bufferWriter)");
        sb.AppendLine("        {");
        sb.AppendLine("            T value = default!;");
        sb.AppendLine("            Deserialize<T>(bufferWriter, ref value);");
        sb.AppendLine("            return value;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeBufferRef(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes into <paramref name=\"value\"/> from a reusable <see cref=\"LuminBufferWriter\"/>.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"bufferWriter\">The buffer containing binary data and the reader operation context.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <returns>The number of bytes consumed from the buffer.</returns>");
        sb.AppendLine("        " + _currentAccess + " static int Deserialize<T>(LuminBufferWriter bufferWriter, ref T value)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = bufferWriter.ReaderState;");
        sb.AppendLine("            try { return DeserializeCore(bufferWriter.GetSpan(), state, ref value); }");
        sb.AppendLine("            finally { state.ResetOperationState(); }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeSequence(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes a <typeparamref name=\"T\"/> from a possibly multi-segment sequence.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"buffer\">The sequence containing a LuminPack payload.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The deserialized value.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static T Deserialize<T>(in ReadOnlySequence<byte> buffer, LuminPackSerializerOption? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            T value = default!;");
        sb.AppendLine("            Deserialize(buffer, ref value, options);");
        sb.AppendLine("            return value;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeSequenceRef(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Deserializes a possibly multi-segment sequence into <paramref name=\"value\"/>.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"buffer\">The sequence containing a LuminPack payload.</param>");
        sb.AppendLine("        /// <param name=\"value\">Receives the deserialized value.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this call, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The number of bytes consumed.</returns>");
        sb.AppendLine("        " + _currentAccess + " static int Deserialize<T>(");
        sb.AppendLine("            ReadOnlySequence<byte> buffer, ref T value,");
        sb.AppendLine("            LuminPackSerializerOption? options = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!buffer.IsSingleSegment)");
        sb.AppendLine("            {");
        sb.AppendLine("                var staging = LuminBufferWriterPool.Rent();");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    staging.LoadSequence(in buffer);");
        sb.AppendLine("                    ReadOnlySpan<byte> contiguous = staging.GetSpan();");
        sb.AppendLine("                    return Deserialize(contiguous, ref value, options);");
        sb.AppendLine("                }");
        sb.AppendLine("                finally { LuminBufferWriterPool.Return(staging); }");
        sb.AppendLine("            }");
        sb.AppendLine("            var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        sb.AppendLine("            state.Init(options);");
        sb.AppendLine();
        sb.AppendLine("            var reader = new LuminPackReader(ref buffer, state);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.ReadValue(ref value);");
        sb.AppendLine("                return reader.GetCurrentSpanIndex();");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
