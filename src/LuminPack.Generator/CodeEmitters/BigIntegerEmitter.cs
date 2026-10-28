using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides big integer emitter functionality.</summary>
public static class BigIntegerEmitter
{
    /// <summary>Evaluates the signed little-endian byte record without serializing it.</summary>
    /// <param name="fieldData">The BigInteger metadata.</param>
    /// <param name="sb">The emitted statements.</param>
    public static void GenerateCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("#if !UNITY_2021_2_OR_NEWER");
        sb.AppendLine("            evaluator += checked(4 + value.GetByteCount());");
        sb.AppendLine("#else");
        sb.AppendLine("            evaluator += checked(4 + value.ToByteArray().Length);");
        sb.AppendLine("#endif");
    }

    /// <summary>Appends binary writing statements for big integer values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        // The exact two's-complement width is known up front, so the record is reserved once and the bytes are written
        // straight into it: the same count header and payload as the former staged copy.
        sb.AppendLine("#if !UNITY_2021_2_OR_NEWER");
        sb.AppendLine("            int byteCount = value.GetByteCount();");
        sb.AppendLine("            var record = writer.Reserve(writer.Position, checked(sizeof(int) + byteCount));");
        sb.AppendLine("            global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(record), byteCount);");
        sb.AppendLine("            value.TryWriteBytes(record.Slice(sizeof(int)), out _);");
        sb.AppendLine("            writer.Advance(sizeof(int) + byteCount);");
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine("#else");
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            var byteArray = value.ToByteArray();");
        sb.AppendLine("            writer.WriteUnmanagedArray(ref index, byteArray, out var offset);");
        sb.AppendLine("            writer.Advance(offset);");
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine("#endif");
    }

    /// <summary>Appends binary reading statements for big integer values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            ");
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                ");
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine("                ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.EnsureCollectionPayload(reader.Position, length);");
        sb.AppendLine("            ref var src = ref reader.GetSpanReference(index);");
        sb.AppendLine("            value = new global::System.Numerics.BigInteger(global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref src, length));");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.Advance(length);");
    }

    /// <summary>Appends JSON writing statements for big integer values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            writer.WriteString(value.ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
    }

    /// <summary>Appends JSON reading statements for big integer values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            var str = reader.ReadString();");
        sb.AppendLine("            value = global::System.Numerics.BigInteger.Parse(str, global::System.Globalization.CultureInfo.InvariantCulture);");
    }
}
