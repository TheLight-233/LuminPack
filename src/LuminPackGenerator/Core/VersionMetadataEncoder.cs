using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Encodes closed schema constants so modern readers compare static data with a known length.</summary>
internal static class VersionMetadataEncoder
{
    /// <summary>Builds an exact constant length table for architecture-independent primitive field storage.</summary>
    /// <param name="data">The complete member contract.</param>
    /// <returns>The fixed VarInt table, or null when a field width requires runtime layout information.</returns>
    internal static byte[] FixedLengths(LuminDataInfo data)
    {
        if (!LuminPackCodeGenerator.FindAllUnmanagedType(data.fields)) return null;
        var lengths = new List<byte>();
        foreach (var field in data.fields)
        {
            if (TypeIdentity(field.TypeSymbol) == null) return null;
            var type = field.TypeSymbol;
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration) type = enumeration.EnumUnderlyingType;
            int size = type.SpecialType switch
            {
                SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => 1,
                SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 => 2,
                SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
                SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => 8,
                SpecialType.System_Decimal => 16,
                _ => 0
            };
            if (size == 0) return null;
            lengths.Add((byte)size);
        }
        return lengths.ToArray();
    }

    /// <summary>Builds exact runtime-compatible metadata when every member type is closed.</summary>
    /// <param name="data">The member contract.</param>
    /// <returns>The constant table, or null when a generic argument is determined at runtime.</returns>
    internal static byte[] Encode(LuminDataInfo data)
    {
        var bytes = new List<byte>();
        foreach (var field in data.fields)
        {
            string type = TypeIdentity(field.TypeSymbol);
            if (type == null) return null;
            Append(bytes, field.SerializedName);
            Append(bytes, Representation(field) + (IsDirectPrimitive(field) ? string.Empty : type));
        }
        return bytes.ToArray();
    }

    /// <summary>Encodes declaration-time wire classification, compression and string profile.</summary>
    /// <param name="field">The member contract.</param>
    /// <returns>The compact representation prefix.</returns>
    internal static string Representation(LuminDataField field)
    {
        var options = field.Options ?? LuminPack.SourceGenerator.CompiledOptions.Default;
        return new string((char)((int)field.Type | (field.IsCompress ? 32 : 0) | (options.Encoding << 6) | (options.Recording << 7)), 1);
    }

    /// <summary>Checks when the classification byte already identifies the exact primitive type.</summary>
    /// <param name="field">The canonical declaration-time field classification.</param>
    /// <returns>Whether an additional type identity would repeat the same information.</returns>
    private static bool IsDirectPrimitive(LuminDataField field) => (int)field.Type == (field.TypeSymbol.SpecialType switch
    {
        SpecialType.System_Int32 => 0, SpecialType.System_UInt32 => 1, SpecialType.System_Byte => 2,
        SpecialType.System_SByte => 3, SpecialType.System_Int16 => 4, SpecialType.System_UInt16 => 5,
        SpecialType.System_Int64 => 6, SpecialType.System_UInt64 => 7, SpecialType.System_Single => 8,
        SpecialType.System_Double => 9, SpecialType.System_Decimal => 10, SpecialType.System_Char => 11,
        SpecialType.System_String => 12, SpecialType.System_Boolean => 13, _ => -1
    });

    /// <summary>Appends a UTF-8 metadata string with its unsigned base-128 length.</summary>
    /// <param name="target">The table under construction.</param>
    /// <param name="value">The exact metadata value.</param>
    private static void Append(List<byte> target, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        int length = bytes.Length;
        while (length >= 128) { target.Add((byte)(length | 128)); length >>= 7; }
        target.Add((byte)length);
        target.AddRange(bytes);
    }

    /// <summary>Normalizes a symbol using the same primitive codes and assembly-independent names as the runtime.</summary>
    /// <param name="type">The declared member or generic argument.</param>
    /// <returns>The exact identity, or null for an open type parameter.</returns>
    private static string TypeIdentity(ITypeSymbol type)
    {
        string primitive = type.SpecialType switch
        {
            SpecialType.System_Boolean => "b", SpecialType.System_Byte => "u1", SpecialType.System_SByte => "i1",
            SpecialType.System_Int16 => "i2", SpecialType.System_UInt16 => "u2", SpecialType.System_Int32 => "i4",
            SpecialType.System_UInt32 => "u4", SpecialType.System_Int64 => "i8", SpecialType.System_UInt64 => "u8",
            SpecialType.System_Single => "f4", SpecialType.System_Double => "f8", SpecialType.System_Decimal => "d16",
            SpecialType.System_Char => "c2", SpecialType.System_String => "s", SpecialType.System_Object => "o",
            SpecialType.System_IntPtr => "ni", SpecialType.System_UIntPtr => "nu", SpecialType.System_DateTime => "dt",
            _ => null
        };
        if (primitive != null) return "#" + primitive;
        if (type is IArrayTypeSymbol array)
        {
            string element = TypeIdentity(array.ElementType);
            return element == null ? null : element + "[" + new string(',', array.Rank - 1) + "]";
        }
        if (type is not INamedTypeSymbol named) return null;
        if (named.ContainingAssembly.Name is "System.Private.CoreLib" or "mscorlib" or "System.Runtime" or "netstandard")
        {
            string known = named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) switch
            {
                "global::System.Guid" => "g", "global::System.TimeSpan" => "ts", "global::System.DateTimeOffset" => "dto", _ => null
            };
            if (known != null) return "#" + known;
        }
        var levels = new Stack<INamedTypeSymbol>();
        for (var current = named; current != null; current = current.ContainingType) levels.Push(current);
        var namespaces = new Stack<string>();
        for (var current = named.ContainingNamespace; current != null && !current.IsGlobalNamespace; current = current.ContainingNamespace)
            namespaces.Push(current.MetadataName);
        string ns = namespaces.Count == 0 ? string.Empty : string.Join(".", namespaces) + ".";
        string name = "@" + ns + string.Join("+", levels.Select(level => level.MetadataName));
        var arguments = levels.SelectMany(level => level.TypeArguments).Select(TypeIdentity).ToArray();
        if (arguments.Any(argument => argument == null)) return null;
        return arguments.Length == 0 ? name : name + "<" + string.Join(";", arguments) + ";>";
    }
}
