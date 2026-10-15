using System.Text;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>
/// Emits the enum branch used by unmanaged serialization code.
/// The JSON representation deliberately remains the integer token format.
/// </summary>
public static class EnumEmitter
{
    /// <summary>Appends binary writing statements for enum values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            global::System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref writer.GetCurrentSpanReference(), value);");
        sb.AppendLine("            writer.Advance(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + data.TypeName + ">());");
    }

    /// <summary>Appends binary reading statements for enum values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            reader.EnsureReadable(reader.GetCurrentSpanOffset(), global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + data.TypeName + ">());");
        sb.AppendLine("            value = global::System.Runtime.CompilerServices.Unsafe.ReadUnaligned<" + data.TypeName + ">(ref reader.GetCurrentSpanReference());");
        sb.AppendLine("            reader.Advance(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + data.TypeName + ">());");
    }

    /// <summary>Appends statements that calculate the binary payload size.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateCalculateOffsetCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            evaluator += global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + data.TypeName + ">();");
    }

    /// <summary>Appends JSON writing statements for enum values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        var names = GetJsonEnumTypeNames(data.TypeSymbol);
        sb.AppendLine("            writer." + names.WriteMethod + "((" + names.Keyword + ")value);");
    }

    /// <summary>Appends JSON reading statements for enum values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        var names = GetJsonEnumTypeNames(data.TypeSymbol);
        sb.AppendLine("            value = (" + data.TypeName + ")reader." + names.ReadMethod + "();");
    }

    /// <summary>
    /// Resolves the underlying-type keyword and the matching JSON read/write method names
    /// for an enum. The JSON representation writes the raw underlying value, so every
    /// declared width must be preserved: narrowing to <c>int</c> silently truncated
    /// 64-bit-backed enums, and reinterpreting a narrow enum as <c>int</c> read past it.
    /// A symbol that is not a resolvable enum keeps the historical <c>int</c> behavior.
    /// </summary>
    /// <param name="typeSymbol">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    internal static (string Keyword, string WriteMethod, string ReadMethod) GetJsonEnumTypeNames(ITypeSymbol? typeSymbol)
    {
        var underlying = typeSymbol is INamedTypeSymbol { TypeKind: TypeKind.Enum } named
            ? named.EnumUnderlyingType?.SpecialType
            : null;

        return underlying switch
        {
            SpecialType.System_SByte => ("sbyte", "WriteSByte", "ReadSByte"),
            SpecialType.System_Byte => ("byte", "WriteByte", "ReadByte"),
            SpecialType.System_Int16 => ("short", "WriteShort", "ReadShort"),
            SpecialType.System_UInt16 => ("ushort", "WriteUShort", "ReadUShort"),
            SpecialType.System_UInt32 => ("uint", "WriteUInt", "ReadUInt"),
            SpecialType.System_Int64 => ("long", "WriteLong", "ReadLong"),
            SpecialType.System_UInt64 => ("ulong", "WriteULong", "ReadULong"),
            _ => ("int", "WriteInt", "ReadInt")
        };
    }
}
