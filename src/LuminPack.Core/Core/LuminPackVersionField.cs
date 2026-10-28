using System.Collections.Generic;
using System.Text;

namespace LuminPack.Core;

/// <summary>Owns the immutable metadata for one generated version-tolerant binary member.</summary>
public sealed class LuminPackVersionField
{
    internal readonly byte[] Name;
    internal readonly byte[][] Aliases;
    internal readonly byte[] Contract;
    internal readonly byte[] Prefix;

    /// <summary>Gets the number of bytes in the cached name and type metadata.</summary>
    public int MetadataLength => Prefix.Length;

    /// <summary>Creates metadata once for a generated member, outside steady-state serialization.</summary>
    /// <param name="name">The stable written name.</param>
    /// <param name="aliases">All additional accepted names.</param>
    /// <param name="type">The declared member type.</param>
    /// <param name="representation">The generator's wire classification and binary options.</param>
    public LuminPackVersionField(string name, string[] aliases, Type type, string representation)
    {
        Name = Encoding.UTF8.GetBytes(name);
        Aliases = new byte[aliases.Length][];
        for (int i = 0; i < aliases.Length; i++) Aliases[i] = Encoding.UTF8.GetBytes(aliases[i]);
        var contract = new StringBuilder(representation);
        if (!IsDirectPrimitive(representation, type)) AppendType(contract, type);
        Contract = Encoding.UTF8.GetBytes(contract.ToString());
        var prefix = new List<byte>(Name.Length + Contract.Length + 10);
        AppendLength(prefix, Name.Length);
        prefix.AddRange(Name);
        AppendLength(prefix, Contract.Length);
        prefix.AddRange(Contract);
        Prefix = prefix.ToArray();
    }

    /// <summary>Encodes a nonnegative metadata length using unsigned base-128 groups.</summary>
    /// <param name="bytes">The cached metadata under construction.</param>
    /// <param name="length">The length to encode.</param>
    private static void AppendLength(List<byte> bytes, int length)
    {
        while (length >= 128) { bytes.Add((byte)(length | 128)); length >>= 7; }
        bytes.Add((byte)length);
    }

    /// <summary>Appends a stable type identity without assembly versions or nullable annotations.</summary>
    /// <param name="builder">The contract being constructed.</param>
    /// <param name="type">The declared type, including closed generic arguments.</param>
    private static void AppendType(StringBuilder builder, Type type)
    {
        string? primitive = PrimitiveIdentity(type);
        if (primitive != null) builder.Append('#').Append(primitive);
        else if (type.IsArray)
        {
            AppendType(builder, type.GetElementType()!);
            builder.Append('[').Append(',', type.GetArrayRank() - 1).Append(']');
        }
        else if (type.IsGenericType)
        {
            builder.Append('@').Append(type.GetGenericTypeDefinition().FullName).Append('<');
            foreach (var argument in type.GetGenericArguments()) { AppendType(builder, argument); builder.Append(';'); }
            builder.Append('>');
        }
        else builder.Append('@').Append(type.FullName);
    }

    /// <summary>Uses exact compact identities for common framework types without hashing user type names.</summary>
    /// <param name="type">The exact runtime type.</param>
    /// <returns>The compact identity, or null for a fully named type.</returns>
    private static string? PrimitiveIdentity(Type type)
    {
        if (type == typeof(bool)) return "b";
        if (type == typeof(byte)) return "u1";
        if (type == typeof(sbyte)) return "i1";
        if (type == typeof(short)) return "i2";
        if (type == typeof(ushort)) return "u2";
        if (type == typeof(int)) return "i4";
        if (type == typeof(uint)) return "u4";
        if (type == typeof(long)) return "i8";
        if (type == typeof(ulong)) return "u8";
        if (type == typeof(float)) return "f4";
        if (type == typeof(double)) return "f8";
        if (type == typeof(decimal)) return "d16";
        if (type == typeof(char)) return "c2";
        if (type == typeof(string)) return "s";
        if (type == typeof(object)) return "o";
        if (type == typeof(IntPtr)) return "ni";
        if (type == typeof(UIntPtr)) return "nu";
        if (type == typeof(Guid)) return "g";
        if (type == typeof(DateTime)) return "dt";
        if (type == typeof(TimeSpan)) return "ts";
        if (type == typeof(DateTimeOffset)) return "dto";
        return null;
    }

    /// <summary>Checks whether the wire classification already proves the exact primitive type.</summary>
    /// <param name="representation">The single representation character containing kind and profile bits.</param>
    /// <param name="type">The exact declared runtime type.</param>
    /// <returns>Whether a separate type identity would be redundant.</returns>
    private static bool IsDirectPrimitive(string representation, Type type)
    {
        if (representation.Length != 1) return false;
        string? expected = (representation[0] & 31) switch
        {
            0 => "i4", 1 => "u4", 2 => "u1", 3 => "i1", 4 => "i2", 5 => "u2", 6 => "i8", 7 => "u8",
            8 => "f4", 9 => "f8", 10 => "d16", 11 => "c2", 12 => "s", 13 => "b", _ => null
        };
        return expected != null && PrimitiveIdentity(type) == expected;
    }

    /// <summary>Combines a model's immutable name and type table for one contiguous comparison or copy.</summary>
    /// <param name="fields">The generated members in write order.</param>
    /// <returns>The complete metadata table, excluding dynamic payload lengths.</returns>
    public static byte[] CreateMetadata(LuminPackVersionField[] fields)
    {
        int length = 0;
        foreach (var field in fields) length = checked(length + field.Prefix.Length);
        var result = new byte[length];
        int offset = 0;
        foreach (var field in fields)
        {
            field.Prefix.AsSpan().CopyTo(result.AsSpan(offset));
            offset += field.Prefix.Length;
        }
        return result;
    }

    /// <summary>Caches a complete default-profile prefix for closed generic schemas.</summary>
    /// <param name="metadata">The complete name and type table.</param>
    /// <param name="count">The member count.</param>
    /// <returns>The header followed by its exact metadata bytes.</returns>
    public static byte[] CreateDefaultPrefix(byte[] metadata, byte count)
    {
        var prefix = new byte[checked(metadata.Length + 4)];
        prefix[0] = 251;
        prefix[1] = 1;
        prefix[3] = count;
        metadata.AsSpan().CopyTo(prefix.AsSpan(4));
        return prefix;
    }

    /// <summary>Finds a member using full UTF-8 names, checking the expected position first.</summary>
    /// <param name="fields">The generated receiver schema.</param>
    /// <param name="name">The name from the input table.</param>
    /// <param name="expected">The common same-schema position.</param>
    /// <returns>The local member index, or minus one for an unknown member.</returns>
    internal static int Find(LuminPackVersionField[] fields, ReadOnlySpan<byte> name, int expected)
    {
        if ((uint)expected < (uint)fields.Length && fields[expected].Matches(name)) return expected;
        for (int i = 0; i < fields.Length; i++)
            if (i != expected && fields[i].Matches(name)) return i;
        return -1;
    }

    /// <summary>Compares a complete name against the stable name and accepted aliases.</summary>
    /// <param name="name">The input name.</param>
    /// <returns>Whether the name identifies this member.</returns>
    private bool Matches(ReadOnlySpan<byte> name)
    {
        if (name.SequenceEqual(Name)) return true;
        foreach (var alias in Aliases) if (name.SequenceEqual(alias)) return true;
        return false;
    }
}
