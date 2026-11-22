/// <summary>Shares the compiled Binary profile ordinals across language targets.</summary>
internal static class BinaryProfiles
{
    internal static readonly string[] Names =
        ["utf8_length", "utf8_token", "utf16_length", "utf16_token", "utf8_varint", "utf16_varint"];

    /// <summary>Resolves the CLI spelling of a built-in Binary profile.</summary>
    /// <param name="name">The requested profile name.</param>
    /// <returns>The profile index used by generated codecs.</returns>
    internal static int Parse(string name)
    {
        int profile = Array.IndexOf(Names, name.Replace('-', '_'));
        if (profile < 0)
            throw new ArgumentException("Unknown Binary profile " + name + ". Choose " +
                string.Join(", ", Names.Select(static value => value.Replace('_', '-'))) + ".");
        return profile;
    }
}
