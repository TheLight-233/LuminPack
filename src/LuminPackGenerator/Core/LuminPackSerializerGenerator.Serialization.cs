using System.Text;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendSerialize(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Serializes <paramref name=\"value\"/> to a newly allocated LuminPack binary payload.</summary>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this call, or <see langword=\"null\"/> for LuminPack defaults.</param>");
        sb.AppendLine("        /// <returns>A new byte array containing the complete serialized payload.</returns>");
        sb.AppendLine("        /// <remarks>");
        sb.AppendLine("        /// This convenience overload allocates the returned array and may use thread-static operation state.");
        sb.AppendLine("        /// Use the overload accepting a reusable <see cref=\"LuminBufferWriter\"/> to avoid the");
        sb.AppendLine("        /// serializer's thread-static optional-state path.");
        sb.AppendLine("        /// </remarks>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static byte[] Serialize(in " + typeName + " value, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var writerBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine();
        sb.AppendLine("            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();");
        sb.AppendLine("            state.Init(option);");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var writer = new LuminPackWriter(writerBuffer, state);");
        sb.AppendLine();
        AppendBinaryOperation(sb, read: false);
        sb.AppendLine();
        sb.AppendLine("                var buffer = AllocateUninitializedArray<byte>(writer.CurrentIndex);");
        sb.AppendLine("                writer.GetSpan().CopyTo(buffer.AsSpan());");
        sb.AppendLine("                return buffer;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(writerBuffer);");
        sb.AppendLine("                state.Reset();");
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
        sb.AppendLine("        /// <para>This high-performance overload obtains its configuration from <see cref=\"LuminBufferWriter.Option\"/>.</para>");
        sb.AppendLine("        /// <para>Configure <paramref name=\"writerBuffer\"/> before this call. Its option remains unchanged for the current");
        sb.AppendLine("        /// Rent-to-Return lifetime and is restored to defaults by <see cref=\"LuminBufferWriterPool.Return\"/>.</para>");
        sb.AppendLine("        /// <para>The method uses the writer state owned by <paramref name=\"writerBuffer\"/> and does not access the");
        sb.AppendLine("        /// serializer's thread-static optional state. The caller must exclusively own the buffer until it is returned.</para>");
        sb.AppendLine("        /// </remarks>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static void Serialize(in " + typeName + " value, LuminBufferWriter writerBuffer)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = writerBuffer.WriterState;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var writer = new LuminPackWriter(writerBuffer, state);");
        sb.AppendLine();
        AppendBinaryOperation(sb, read: false);
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

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericSerialize(StringBuilder sb)
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
        sb.AppendLine("            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();");
        sb.AppendLine("            state.Init(option);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                SerializeCore(writerBuffer, state, in value);");
        sb.AppendLine("                var source = writerBuffer.GetSpan();");
        sb.AppendLine("                var buffer = AllocateUninitializedArray<byte>(source.Length);");
        sb.AppendLine("                source.CopyTo(buffer.AsSpan());");
        sb.AppendLine("                return buffer;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(writerBuffer);");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a generic <c>Serialize&lt;T&gt;(in T, IBufferWriter&lt;byte&gt;)</c> overload.  Serialization
    /// runs through the existing LuminBufferWriter fast path (native buffer, concrete method calls only)
    /// and the published payload is copied into the caller's <c>IBufferWriter&lt;byte&gt;</c> (e.g. a
    /// <c>System.IO.Pipelines.PipeWriter</c> or <c>ArrayBufferWriter&lt;byte&gt;</c>).  The internal
    /// generated call path is untouched, so no interface virtual dispatch enters the hot path.
    /// </summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericSerializeBufferWriter(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Serializes a <typeparamref name=\"T\"/> into an arbitrary <see cref=\"IBufferWriter{T}\"/> using the generated generic dispatch.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The value type to serialize.</typeparam>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"bufferWriter\">The destination writer, e.g. <c>System.IO.Pipelines.PipeWriter</c> or <c>ArrayBufferWriter&lt;byte&gt;</c>.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this call, or <see langword=\"null\"/> for LuminPack defaults.</param>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static void Serialize<T>(in T value, global::System.Buffers.IBufferWriter<byte> bufferWriter, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (bufferWriter is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(bufferWriter));");
        sb.AppendLine("            var writerBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            var state = _threadStaticWriterOptionalState ??= new LuminPackWriterOptionalState();");
        sb.AppendLine("            state.Init(option);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                SerializeCore(writerBuffer, state, in value);");
        sb.AppendLine("                var source = writerBuffer.GetSpan();");
        sb.AppendLine("                var destination = bufferWriter.GetSpan(source.Length);");
        sb.AppendLine("                source.CopyTo(destination);");
        sb.AppendLine("                bufferWriter.Advance(source.Length);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(writerBuffer);");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
