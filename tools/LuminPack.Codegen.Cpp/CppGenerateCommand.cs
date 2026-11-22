/// <summary>Parses the standalone C++ generation command.</summary>
internal static class CppGenerateCommand
{
    /// <summary>Generates a header when the arguments describe one complete root request.</summary>
    /// <param name="arguments">Generation flags followed by the manifest, root identity, and output path.</param>
    /// <returns>Whether a valid invocation was handled.</returns>
    internal static bool TryRun(ReadOnlySpan<string> arguments)
    {
        bool useRtti = false;
        int? binaryProfile = null;
        int index = 0;
        while (index < arguments.Length && arguments[index].StartsWith("--", StringComparison.Ordinal))
        {
            switch (arguments[index++])
            {
                case "--rtti" when !useRtti:
                    useRtti = true;
                    break;
                case "--binary-profile" when binaryProfile == null && index < arguments.Length:
                    binaryProfile = CppEmitter.ParseBinaryProfile(arguments[index++]);
                    break;
                default:
                    return false;
            }
        }

        if (arguments.Length - index != 3)
            return false;
        CppEmitter.Generate(arguments[index], arguments[index + 1], arguments[index + 2], useRtti, binaryProfile);
        return true;
    }
}
