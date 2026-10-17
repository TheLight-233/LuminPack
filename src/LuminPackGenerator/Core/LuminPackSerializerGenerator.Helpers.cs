using System.Text;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Emits the unmanaged sequence algorithm directly into the selected concrete entry body.</summary>
    /// <param name="builder">The entry source builder.</param>
    /// <param name="typeName">The concrete type or generic type parameter.</param>
    /// <param name="indent">Indentation of the operation body.</param>
    private static void AppendUnmanagedSequenceRead(StringBuilder builder, string typeName, int indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("            int size = Unsafe.SizeOf<" + typeName + ">();");
        sb.AppendLine("            if (buffer.Length < size)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidRange(size, (int)buffer.Length);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (buffer.IsSingleSegment)");
        sb.AppendLine("            {");
        AppendUnmanagedSequenceValueRead(sb, typeName, "buffer.FirstSpan");
        sb.AppendLine("                return size;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (size <= 512)");
        sb.AppendLine("            {");
        sb.AppendLine("                Span<byte> tempSpan = stackalloc byte[size];");
        sb.AppendLine("                buffer.Slice(0, size).CopyTo(tempSpan);");
        AppendUnmanagedSequenceValueRead(sb, typeName, "tempSpan");
        sb.AppendLine("                return size;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var rentedBuffer = ArrayPool<byte>.Shared.Rent(size);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var tempSpan = rentedBuffer.AsSpan(0, size);");
        sb.AppendLine("                buffer.Slice(0, size).CopyTo(tempSpan);");
        AppendUnmanagedSequenceValueRead(sb, typeName, "tempSpan");
        sb.AppendLine("                return size;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                ArrayPool<byte>.Shared.Return(rentedBuffer);");
        sb.AppendLine("            }");
        string padding = new string(' ', indent);
        foreach (string line in sb.ToString().Split(new[] { System.Environment.NewLine }, System.StringSplitOptions.None))
            if (line.Length != 0) builder.Append(padding).AppendLine(line.Substring(12));
    }

    /// <summary>Preserves the architecture-specific raw value load after sequence staging.</summary>
    /// <param name="builder">The operation source builder.</param>
    /// <param name="typeName">The complete unmanaged type or guarded generic parameter.</param>
    /// <param name="span">The already staged input span.</param>
    private static void AppendUnmanagedSequenceValueRead(StringBuilder builder, string typeName, string span)
    {
        builder.AppendLine("                if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8)");
        builder.AppendLine("                {");
        builder.AppendLine("                    value = Unsafe.ReadUnaligned<" + typeName + ">(ref MemoryMarshal.GetReference(" + span + "));");
        builder.AppendLine("                }");
        builder.AppendLine("                else");
        builder.AppendLine("                {");
        builder.AppendLine("                    " + typeName + " __luminSequenceValue = default!;");
        builder.AppendLine("                    Unsafe.CopyBlockUnaligned(ref Unsafe.As<" + typeName + ", byte>(ref __luminSequenceValue), ref MemoryMarshal.GetReference(" + span + "), (uint)size);");
        builder.AppendLine("                    value = __luminSequenceValue;");
        builder.AppendLine("                }");
    }
}
