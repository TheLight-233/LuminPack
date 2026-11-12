using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering;

internal static partial class SerializerDocumentation
{
    /// <summary>Retains documented text and only the normalized contracts each final file references.</summary>
    internal sealed class Cache
    {
        private Dictionary<string, Entry> _entries;

        /// <summary>Shares immutable entries with the preceding driver snapshot.</summary>
        /// <param name="prior">The preceding publication cache.</param>
        internal Cache(Cache? prior) => _entries = prior?._entries ?? new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>Documents changed files and drops removed files without retaining obsolete payload graphs.</summary>
        /// <param name="sources">The final compact source files.</param>
        /// <param name="catalog">The current declaration facts.</param>
        /// <returns>The same operations with caller-facing documentation.</returns>
        internal GeneratedSource[] Apply(GeneratedSource[] sources, IReadOnlyDictionary<string, PayloadDocumentation> catalog)
        {
            var current = new Dictionary<string, Entry>(sources.Length, StringComparer.Ordinal);
            var output = new GeneratedSource[sources.Length];
            for (int index = 0; index < sources.Length; index++)
            {
                GeneratedSource source = sources[index];
                string key = ((int)source.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + source.Hint;
                if (!_entries.TryGetValue(key, out Entry? entry) || entry.Raw != source.Text || !entry.Matches(catalog))
                {
                    string documented = Complete(source.Text, catalog, typeSummary: false, out var dependencies);
                    entry = new Entry(source.Text, new GeneratedSource(source.Hint, documented, source.Kind), dependencies);
                }
                current[key] = entry;
                output[index] = entry.Source;
            }
            _entries = current;
            return output;
        }
    }

    /// <summary>One final source and its exact value-model documentation dependencies.</summary>
    private sealed class Entry
    {
        internal readonly string Raw;
        internal readonly GeneratedSource Source;
        private readonly KeyValuePair<string, PayloadDocumentation?>[] _dependencies;

        /// <summary>Retains compiler-free source and normalized documentation dependencies.</summary>
        /// <param name="raw">The unchanged compact source.</param>
        /// <param name="source">The documented source.</param>
        /// <param name="dependencies">Only payload declarations used by this file.</param>
        internal Entry(string raw, GeneratedSource source, KeyValuePair<string, PayloadDocumentation?>[] dependencies)
        {
            Raw = raw;
            Source = source;
            _dependencies = dependencies;
        }

        /// <summary>Invalidates documentation even when a changed CLR layout leaves executable source unchanged.</summary>
        /// <param name="catalog">The current declaration facts.</param>
        /// <returns>Whether every retained contract is still current.</returns>
        internal bool Matches(IReadOnlyDictionary<string, PayloadDocumentation> catalog)
        {
            foreach (var item in _dependencies)
            {
                bool found = catalog.TryGetValue(item.Key, out PayloadDocumentation? value);
                if (item.Value == null ? found : !found || !item.Value.Equals(value))
                    return false;
            }
            return true;
        }
    }
}
