#if NET10_0_OR_GREATER
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LuminPack.Code;

/// <summary>Reuses one canonical read-only culture per thread while the framework's name cache is current.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminPackCultureInfoCache
{
    [ThreadStatic] private static string? _name;
    [ThreadStatic] private static CultureInfo? _culture;
    [ThreadStatic] private static WeakReference<Dictionary<string, CultureInfo>>? _owner;
    private static readonly bool Available = Probe();

    /// <summary>Gets a bounded name candidate for the existing checked string reader.</summary>
    public static string? Name => _name;

    /// <summary>Resolves a decoded name through the framework or its verified canonical cache entry.</summary>
    /// <param name="name">The decoded culture name.</param>
    /// <returns>The current read-only framework instance.</returns>
    public static CultureInfo ResolveCulture(string name)
    {
        if (!Available)
            return CultureInfo.GetCultureInfo(name);
        var table = Volatile.Read(ref NameCache(null));
        bool repeated = string.Equals(name, _name, StringComparison.Ordinal);
        if (repeated && _culture is { } cached && table is not null &&
            _owner!.TryGetTarget(out var owner) && ReferenceEquals(table, owner))
            return cached;

        var resolved = CultureInfo.GetCultureInfo(name);
        _culture = null;
        if (!repeated)
        {
            _name = name.Length <= 85 && name == resolved.Name ? resolved.Name : null;
            return resolved;
        }
        if (table is null || !ReferenceEquals(table, Volatile.Read(ref NameCache(null))))
            return resolved;
        foreach (char c in name)
            if (c > 127)
                return resolved;
        string key = name.ToLowerInvariant();
        // Concurrent first lookups can create competing instances. Retain only the instance actually stored by the BCL.
        lock (table)
        {
            if (!table.TryGetValue(key, out var canonical) || !ReferenceEquals(resolved, canonical))
                return resolved;
            if (_owner is null)
                _owner = new(table);
            else
                _owner.SetTarget(table);
            _culture = canonical;
        }
        return resolved;
    }

    /// <summary>Checks the binding and canonical entry on the inspected CoreCLR implementations.</summary>
    /// <returns>Whether the BCL exposes the add-only table replaced by ClearCachedData.</returns>
    private static bool Probe()
    {
        if (Environment.Version.Major is not (10 or 11) || !RuntimeFeature.IsDynamicCodeCompiled)
            return false;
        try
        {
            var invariant = CultureInfo.GetCultureInfo(string.Empty);
            var table = Volatile.Read(ref NameCache(null));
            if (table is null)
                return false;
            lock (table)
                return table.TryGetValue(string.Empty, out var value) && ReferenceEquals(value, invariant);
        }
        catch
        {
            return false; }
    }

    /// <summary>Accesses the framework name cache that is replaced when globalization caches are cleared.</summary>
    /// <param name="owner">Identifies the declaring framework type.</param>
    /// <returns>The current cache slot.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_cachedCulturesByName")]
    private static extern ref Dictionary<string, CultureInfo>? NameCache(CultureInfo? owner);
}
#endif
