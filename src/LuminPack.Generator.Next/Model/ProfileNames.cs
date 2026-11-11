using System.Globalization;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>Spells the existing stateless profile composition directly from normalized constants.</summary>
internal static class ProfileNames
{
    /// <summary>Gets the standard-format component identity.</summary>
    /// <param name="profile">The normalized profile.</param>
    /// <returns>The generated format component.</returns>
    internal static string Format(StringProfile profile) => "LuminPackFormat_" + ((int)profile.Format).ToString(CultureInfo.InvariantCulture) +
        "_" + profile.Precision.ToString(CultureInfo.InvariantCulture);

    /// <summary>Gets the JSON depth component identity.</summary>
    /// <param name="profile">The normalized profile.</param>
    /// <returns>The generated depth component.</returns>
    internal static string Depth(StringProfile profile) => "LuminPackDepth_" + (profile.Depth < 0
        ? "N" + (-(long)profile.Depth).ToString(CultureInfo.InvariantCulture) : profile.Depth.ToString(CultureInfo.InvariantCulture));

    /// <summary>Gets the canonical string-profile type.</summary>
    /// <param name="profile">The normalized profile.</param>
    /// <returns>The fully qualified existing string profile.</returns>
    internal static string Strings(StringProfile profile) => "global::LuminPack.Option." +
        (profile.Encoding == StringEncoding.Utf8 ? "LuminPackUtf8" : "LuminPackUtf16") +
        (profile.Recording == StringRecording.Token ? "TokenOptions" : profile.Recording == StringRecording.VarInt ? "VarIntOptions" : "LengthOptions");

    /// <summary>Composes string, format, JSON depth and syntax options using the existing runtime value types.</summary>
    /// <param name="profile">The normalized declaration constants.</param>
    /// <returns>The complete stateless profile type.</returns>
    internal static string Type(StringProfile profile)
    {
        bool format = profile.Format != 'G' || profile.Precision != byte.MaxValue;
        if (!format && profile.Depth == 1024 && !profile.Strict)
            return Strings(profile);
        return "global::LuminPack.Option.LuminPackOptions<" + Strings(profile) + ", " +
            (format ? "global::LuminPack.Generated." + Format(profile) : "global::LuminPack.Option.LuminPackGeneralFormat") + ", " +
            (profile.Depth != 1024 ? "global::LuminPack.Generated." + Depth(profile) : "global::LuminPack.Option.LuminPackDefaultDepth") + ", " +
            (profile.Strict ? "global::LuminPack.Option.LuminPackStrictJson" : "global::LuminPack.Option.LuminPackTrustedJson") + ">";
    }
}
