using System.Globalization;
using System.Text;
using System.Text.Json;

internal sealed partial class CppEmitter
{
    /// <summary>Preserves the current descriptor and adds the producer's exact revision-1 compatibility contract.</summary>
    /// <param name="field">The compiler-projected Version field.</param>
    /// <param name="property">The current serialized member name.</param>
    /// <param name="pointer">The C++ pointer to member storage.</param>
    /// <param name="binary">The current Binary field codec.</param>
    /// <param name="descriptor">The producer's packed revision-4 descriptor.</param>
    /// <returns>The concrete Version field binding.</returns>
    private static string BindVersionMember(JsonElement field, string property, string pointer, string binary, ulong descriptor)
    {
        var aliases = field.GetProperty("nameKeys").EnumerateArray().Skip(1)
            .Select(static item => item.GetUInt32().ToString(CultureInfo.InvariantCulture) + "u");
        string member = Apply("luminpack::version_member", new[] { pointer, binary, Hex(descriptor) }.Concat(aliases));
        if (!field.TryGetProperty("legacyVersionContract", out JsonElement legacy) || legacy.ValueKind != JsonValueKind.String)
            return member;

        var names = field.GetProperty("oldNames").EnumerateArray().Select(static item => item.GetString()!);
        return Apply("luminpack::legacy_version_member", new[] { member, VersionByteLiteral(legacy.GetString()!), VersionByteLiteral(property) }
            .Concat(names.Select(VersionByteLiteral)));
    }

    /// <summary>Preserves all bytes of a managed UTF-8 name or type contract, including an initial zero.</summary>
    /// <param name="value">The producer's exact Version metadata string.</param>
    /// <returns>An ordinary C++ literal with explicitly encoded byte values.</returns>
    private static string VersionByteLiteral(string value) => "\"" +
        string.Concat(Encoding.UTF8.GetBytes(value).Select(static item => "\\x" + item.ToString("x2", CultureInfo.InvariantCulture))) + "\"";
}
