using System.Text;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Builds the private helpers source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendPrivateHelpers(StringBuilder sb)
    {
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        private static int DeserializeUnmanagedSequence<T>(ReadOnlySequence<byte> buffer, ref T value)");
        sb.AppendLine("        {");
        sb.AppendLine("            int size = Unsafe.SizeOf<T>();");
        sb.AppendLine("            if (buffer.Length < size)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidRange(size, (int)buffer.Length);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (buffer.IsSingleSegment)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(buffer.FirstSpan));");
        sb.AppendLine("                return size;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (size <= 512)");
        sb.AppendLine("            {");
        sb.AppendLine("                Span<byte> tempSpan = stackalloc byte[size];");
        sb.AppendLine("                buffer.Slice(0, size).CopyTo(tempSpan);");
        sb.AppendLine("                value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(tempSpan));");
        sb.AppendLine("                return size;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var rentedBuffer = ArrayPool<byte>.Shared.Rent(size);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var tempSpan = rentedBuffer.AsSpan(0, size);");
        sb.AppendLine("                buffer.Slice(0, size).CopyTo(tempSpan);");
        sb.AppendLine("                value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(tempSpan));");
        sb.AppendLine("                return size;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                ArrayPool<byte>.Shared.Return(rentedBuffer);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
