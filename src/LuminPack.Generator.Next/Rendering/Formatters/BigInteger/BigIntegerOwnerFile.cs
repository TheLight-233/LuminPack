using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Numerics.BigInteger</c>.</summary>
/// <remarks>The binary record is the signed little-endian two's-complement payload behind its byte count; the JSON form
/// stores the invariant decimal text.</remarks>
internal static class BigIntegerOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations) =>
        SpecialOwnerFile.Render(set, operations, "BigInteger", "global::System.Numerics.BigInteger", false, BinaryWrite,
            (w, c) => BinaryRead(w, c, set), (w, c) => JsonWrite(w, c, set), (w, c) => JsonRead(w, c, set));

    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("#if !UNITY_2021_2_OR_NEWER");
        w.Line("int byteCount = value.GetByteCount();");
        w.Line("var record = writer.Reserve(writer.Position, checked(sizeof(int) + byteCount));");
        w.Line("global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(record), byteCount);");
        w.Line("value.TryWriteBytes(record.Slice(sizeof(int)), out _);");
        w.Line("writer.Advance(sizeof(int) + byteCount);");
        w.Line("writer.CheckBuffer();");
        w.Line("#else");
        w.Line("var payload = value.ToByteArray();");
        w.Line("writer.Write<byte>(payload, payload.Length);");
        w.Line("#endif");
    }

    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context, SharedFormatterSet set)
    {
        w.Line("if (!reader.TryReadCollectionHead(out int length))");
        w.Open();
        w.Line("value = default;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("reader.EnsureCollectionPayload(reader.Position, length);");
        w.Line("var payload = reader.Buffer.Slice(reader.Position, length);");
        w.Line(set.FindFormatter("global::System.Numerics.BigInteger")?.Supports("CanReadBinaryBigInteger") == true ?
            "global::LuminPack.Code.LuminPackMarshal.ReadBigInteger(payload, out value);" :
            "value = new global::System.Numerics.BigInteger(payload);");
        w.Line("reader.Advance(length);");
    }

    private static void JsonWrite(CodeWriter w, SpecialOwnerContext context, SharedFormatterSet set)
    {
        w.Line(set.FindFormatter("global::System.Numerics.BigInteger")?.Supports("CanWriteJsonBigInteger") == true ?
            "writer.WriteBigInteger<" + context.Profile + ">(value);" :
            "writer.Write<" + context.Profile + ">(value.ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
    }

    private static void JsonRead(CodeWriter w, SpecialOwnerContext context, SharedFormatterSet set)
    {
        if (set.FindFormatter("global::System.Numerics.BigInteger")?.Supports("CanReadJsonBigInteger") == true)
        {
            w.Line("value = reader.ReadBigInteger<" + context.Profile + ">();");
            return;
        }
        w.Line("var str = reader.Read<string, " + context.Profile + ">();");
        w.Line("value = global::System.Numerics.BigInteger.Parse(str, global::System.Globalization.CultureInfo.InvariantCulture);");
    }
}
