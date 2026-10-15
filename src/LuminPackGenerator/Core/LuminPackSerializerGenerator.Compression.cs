using System.Text;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Builds the compress decompress source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendCompressDecompress(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Compresses the input payload.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"source\">The input data.</param>");
        sb.AppendLine("        /// <param name=\"destination\">The destination receiving the output.</param>");
        sb.AppendLine("        /// <returns>The compressed length.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static int Compress(LuminBufferWriter source, LuminBufferWriter destination)");
        sb.AppendLine("        {");
        sb.AppendLine("            return LuminCompressor.Compress(source, destination);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Compresses the input payload.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"source\">The input data.</param>");
        sb.AppendLine("        /// <returns>A newly allocated byte array containing the compressed payload.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static byte[] Compress(ReadOnlySpan<byte> source)");
        sb.AppendLine("        {");
        sb.AppendLine("            return LuminCompressor.Compress(source);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Compresses the input payload.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"source\">The input data.</param>");
        sb.AppendLine("        /// <param name=\"destination\">The destination receiving the output.</param>");
        sb.AppendLine("        /// <returns>The compressed length.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static int Compress(ReadOnlySpan<byte> source, Span<byte> destination)");
        sb.AppendLine("        {");
        sb.AppendLine("            return LuminCompressor.Compress(source, destination);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Decompresses the input payload.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"source\">The input data.</param>");
        sb.AppendLine("        /// <param name=\"destination\">The destination receiving the output.</param>");
        sb.AppendLine("        /// <returns>The decompressed length.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)");
        sb.AppendLine("        {");
        sb.AppendLine("            return LuminCompressor.Decompress(source, destination);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Decompresses the input payload.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"source\">The input data.</param>");
        sb.AppendLine("        /// <param name=\"destination\">The destination receiving the output.</param>");
        sb.AppendLine("        /// <returns>The decompressed length.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static int Decompress(LuminBufferWriter source, LuminBufferWriter destination)");
        sb.AppendLine("        {");
        sb.AppendLine("            return LuminCompressor.Decompress(source, destination);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Decompresses the input payload.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"source\">The input data.</param>");
        sb.AppendLine("        /// <returns>A newly allocated byte array containing the decompressed payload.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static byte[] Decompress(ReadOnlySpan<byte> source)");
        sb.AppendLine("        {");
        sb.AppendLine("            return LuminCompressor.Decompress(source);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
