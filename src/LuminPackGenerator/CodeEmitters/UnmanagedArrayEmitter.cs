using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides unmanaged array emitter functionality.</summary>
public static class UnmanagedArrayEmitter
{
    /// <summary>Appends binary writing statements for unmanaged array values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var offset = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteUnmanagedArray(ref offset, value, out var length);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(length);");
        sb.AppendLine();
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for unmanaged array values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var offset = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadUnmanagedArray(ref offset, ref value, out var length);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(length);");
    }

    // ── Compress variants ────────────────────────────────────────────────────

    /// <summary>Appends binary writing statements for unmanaged array values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCodeWithCompress(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var offset = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            writer.DangerousWriteUnmanagedArrayWithCompress(ref offset, value, out var length);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(length);");
        sb.AppendLine();
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for unmanaged array values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCodeWithCompress(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var offset = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            reader.DangerousReadUnmanagedArrayWithCompress(ref offset, ref value, out var length);");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(length);");
    }
}
