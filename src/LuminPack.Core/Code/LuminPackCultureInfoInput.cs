using System.ComponentModel;
using System.Globalization;

namespace LuminPack.Code;

/// <summary>Resolves culture names from untrusted input against the platform's finite culture list.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminPackCultureInfoInput
{
    /// <summary>Checks the name before allowing the framework to cache the culture.</summary>
    /// <param name="name">The decoded culture name.</param>
    /// <returns>The current read-only framework instance.</returns>
    public static CultureInfo ResolveCulture(string name)
    {
#if NET10_0_OR_GREATER
        return LuminPackCultureInfoCache.ResolveCheckedCulture(name);
#else
        return CultureInfo.GetCultureInfo(AllowedName(name));
#endif
    }

    /// <summary>Rejects names outside the finite set supported by the current platform.</summary>
    /// <param name="name">The decoded culture name.</param>
    /// <returns>The allowed spelling the framework may cache.</returns>
    internal static string AllowedName(string name)
    {
        if (name.Length == 0)
            return string.Empty;
        if (PlatformNames.Allowed.TryGetValue(name, out string? allowed))
            return allowed;
        LuminPackExceptionHelper.ThrowInvalidDataException("The culture name is not supported for untrusted input.");
        return null!;
    }

    private static class PlatformNames
    {
        internal static readonly Dictionary<string, string> Allowed = GetNames();

        /// <summary>Captures platform names and supported compatibility aliases independently of input.</summary>
        /// <returns>The fixed set of allowed culture names.</returns>
        private static Dictionary<string, string> GetNames()
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
                names[culture.Name] = culture.Name;
            AddAlias("zh-CN", "zh-Hans");
            AddAlias("zh-CHS", "zh-Hans");
            AddAlias("zh-TW", "zh-Hant");
            AddAlias("zh-CHT", "zh-Hant");
            return names;

            void AddAlias(string name, string culture)
            {
                if (names.ContainsKey(name) || !names.ContainsKey(culture))
                    return;
                try
                {
                    _ = CultureInfo.GetCultureInfo(name);
                    names.Add(name, name);
                }
                catch (CultureNotFoundException)
                {
                    // An alias available on one globalization backend can be absent on another.
                }
            }
        }
    }
}
