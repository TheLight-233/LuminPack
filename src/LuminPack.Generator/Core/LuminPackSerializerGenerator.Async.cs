using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void AppendSerializeAsync(StringBuilder sb, string typeName, MetaInfo metaInfo)
    {
        sb.AppendLine("        /// <summary>Asynchronously serializes <paramref name=\"value\"/> and writes the binary payload to a stream.</summary>");
        sb.AppendLine("        /// <param name=\"stream\">The destination stream.</param>");
        sb.AppendLine("        /// <param name=\"value\">The value to serialize.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this operation, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <param name=\"cancellationToken\">A token that can cancel stream writes and flushing.</param>");
        sb.AppendLine("        /// <returns>A task-like value that completes after the stream has been flushed.</returns>");
        sb.AppendLine("        /// <remarks>This convenience API internally rents and returns a buffer; the caller does not manage that buffer.</remarks>");
        sb.AppendLine("        " + _currentAccess + " static async ValueTask SerializeAsync(");
        sb.AppendLine("            Stream stream, " + typeName + " value,");
        sb.AppendLine("            LuminPackSerializerOption? option = null,");
        sb.AppendLine("            CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            var tempWriter = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");

        if (metaInfo.IsNet9_OR_GREATER)
        {
            // For .NET 9+ keep the original code directly (compiler allows it)
            sb.AppendLine("                var writer = new LuminPackWriter(tempWriter);");
            AppendBinaryOperation(sb, read: false);
            sb.AppendLine("                tempWriter.CompleteWrite(writer.CurrentIndex);");
        }
        else
        {
            // For older frameworks, define a static local function that does the synchronous serialization
            sb.AppendLine("                static void WriteValue(LuminBufferWriter tempWriter, in " + typeName + " value)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var writer = new LuminPackWriter(tempWriter);");
            AppendBinaryOperation(sb, read: false, indent: 20);
            sb.AppendLine("                    tempWriter.CompleteWrite(writer.CurrentIndex);");
            sb.AppendLine("                }");
            sb.AppendLine("                WriteValue(tempWriter, in value);");
        }

        sb.AppendLine("                await tempWriter.WriteToAndResetAsync(stream, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(tempWriter);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="metadata">Current language and framework settings.</param>
    private static void AppendDeserializeAsyncValue(StringBuilder sb, string typeName, MetaInfo metadata)
    {
        sb.AppendLine("        /// <summary>Asynchronously reads a stream to completion and returns the deserialized value.</summary>");
        sb.AppendLine("        /// <param name=\"stream\">The source stream.</param>");
        sb.AppendLine("        /// <param name=\"value\">A compatibility parameter that is currently ignored; the result is deserialized independently.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this operation, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <param name=\"cancellationToken\">A token that can cancel stream reads.</param>");
        sb.AppendLine("        /// <returns>A task-like value containing the deserialized value.</returns>");
        sb.AppendLine("        /// <remarks>This overload buffers the complete stream in a temporary MemoryStream before deserialization. The caller owns the input stream.</remarks>");
        sb.AppendLine("        " + _currentAccess + " static async ValueTask<" + typeName + "> DeserializeAsync(");
        sb.AppendLine("            Stream stream, " + typeName + " value, LuminPackSerializerOption? options = null,");
        sb.AppendLine("            CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            using var data = new MemoryStream();");
        sb.AppendLine("            await stream.CopyToAsync(data, cancellationToken).ConfigureAwait(false);");
        if (metadata.IsNet9_OR_GREATER)
        {
            sb.AppendLine("            value = default!;");
            AppendAsyncReadBody(sb, indent: 12);
        }
        else
        {
            sb.AppendLine("            static " + typeName + " ReadValue(MemoryStream data, LuminPackSerializerOption? options)");
            sb.AppendLine("            {");
            sb.AppendLine("                " + typeName + " value = default!;");
            AppendAsyncReadBody(sb, indent: 16);
            sb.AppendLine("            }");
            sb.AppendLine("            return ReadValue(data, options);");
        }
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Emits the synchronous read operation after an asynchronous entry has buffered its input.</summary>
    /// <param name="builder">The concrete asynchronous overload builder.</param>
    /// <param name="indent">The enclosing method or compatibility local-function indentation.</param>
    private static void AppendAsyncReadBody(StringBuilder builder, int indent)
    {
        string padding = new string(' ', indent);
        builder.Append(padding).AppendLine("var state = _threadStaticReaderOptionalState ??= new LuminPackReaderOptionalState();");
        builder.Append(padding).AppendLine("state.Init(options);");
        builder.Append(padding).AppendLine("ReadOnlySpan<byte> buffer = data.GetBuffer().AsSpan(0, checked((int)data.Length));");
        builder.Append(padding).AppendLine("var reader = new LuminPackReader(ref buffer, state);");
        builder.Append(padding).AppendLine("try");
        builder.Append(padding).AppendLine("{");
        AppendBinaryOperation(builder, read: true, indent: indent + 4);
        builder.Append(padding).AppendLine("    return value;");
        builder.Append(padding).AppendLine("}");
        builder.Append(padding).AppendLine("finally { state.Reset(); }");
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeAsync(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Asynchronously reads a stream to completion and deserializes one <typeparamref name=\"T\"/>.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"stream\">The source stream.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this operation, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <param name=\"cancellationToken\">A token that can cancel stream reads.</param>");
        sb.AppendLine("        /// <returns>A task-like value containing the deserialized value.</returns>");
        sb.AppendLine("        " + _currentAccess + " static async ValueTask<T> DeserializeAsync<T>(");
        sb.AppendLine("            Stream stream, LuminPackSerializerOption? options = null,");
        sb.AppendLine("            CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            using var data = new MemoryStream();");
        sb.AppendLine("            await stream.CopyToAsync(data, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("            return Deserialize<T>(data.GetBuffer().AsSpan(0, checked((int)data.Length)), options);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericDeserializeAsyncValue(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Asynchronously reads a stream to completion and returns the deserialized value.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The target value type.</typeparam>");
        sb.AppendLine("        /// <param name=\"stream\">The source stream.</param>");
        sb.AppendLine("        /// <param name=\"value\">A compatibility parameter that is currently ignored; the result is deserialized independently.</param>");
        sb.AppendLine("        /// <param name=\"options\">The configuration for this operation, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <param name=\"cancellationToken\">A token that can cancel stream reads.</param>");
        sb.AppendLine("        /// <returns>A task-like value containing the deserialized value.</returns>");
        sb.AppendLine("        " + _currentAccess + " static ValueTask<T> DeserializeAsync<T>(");
        sb.AppendLine("            Stream stream, T value, LuminPackSerializerOption? options = null,");
        sb.AppendLine("            CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            return DeserializeAsync<T>(stream, options, cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
