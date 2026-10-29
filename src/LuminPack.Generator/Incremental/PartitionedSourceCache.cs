using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LuminPack.SourceGenerator;

/// <summary>Reuses complete generated files without assembling an aggregate source string.</summary>
internal sealed class PartitionedSourceCache
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>Releases files that no longer belong to the completed output batch.</summary>
    /// <param name="names">Complete filenames still needed by the current batch.</param>
    internal void Retain(IEnumerable<string> names)
    {
        var retained = new HashSet<string>(names, StringComparer.Ordinal);
        lock (_entries)
        {
            foreach (var key in _entries.Where(pair => !retained.Contains(pair.Value.Source.Name)).Select(pair => pair.Key).ToArray())
                _entries.Remove(key);
        }
    }

    /// <summary>Wraps a changed body or returns the exact previous complete file value.</summary>
    /// <param name="prefix">The generated file family.</param>
    /// <param name="identity">The complete canonical identity within that family.</param>
    /// <param name="header">The file's imports and declaration wrapper.</param>
    /// <param name="body">The independently cached declaration body.</param>
    /// <param name="footer">The closing wrapper.</param>
    /// <param name="build">An optional complete-file renderer used only after its raw inputs change.</param>
    /// <param name="settings">Additional immutable rendering settings.</param>
    /// <returns>A stable filename and complete source value.</returns>
    internal FrozenGeneratedSource Wrap(string prefix, string identity, string header, string body, string footer, Func<string> build = null, object settings = null)
    {
        string key = prefix + ":" + identity;
        lock (_entries)
        {
            if (_entries.TryGetValue(key, out var previous) && previous.Header == header && previous.Body == body && previous.Footer == footer && Equals(previous.Settings, settings))
                return previous.Source;
            string name = previous?.Source.Name ?? CreateHintName(prefix, identity);
            var source = new FrozenGeneratedSource(name, build == null ? header + body + footer : build());
            _entries[key] = new Entry(header, body, footer, source, settings);
            System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.PartitionedFileBuilds);
            return source;
        }
    }

    /// <summary>Derives a filesystem-safe name from the complete identity instead of lossy punctuation replacement.</summary>
    /// <param name="prefix">The generated file family.</param>
    /// <param name="identity">The canonical type or declaration identity.</param>
    /// <returns>A deterministic name with a complete SHA-256 identity suffix.</returns>
    internal static string CreateHintName(string prefix, string identity)
    {
        using var hash = SHA256.Create();
        var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(identity));
        var name = new StringBuilder(prefix).Append('.');
        foreach (byte value in bytes) name.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return name.Append(".g.cs").ToString();
    }

    /// <summary>Retains source text components and their completed output without semantic symbols.</summary>
    private sealed class Entry
    {
        internal readonly string Header;
        internal readonly string Body;
        internal readonly string Footer;
        internal readonly FrozenGeneratedSource Source;
        internal readonly object Settings;

        /// <summary>Captures a completed file wrapper for later value comparison.</summary>
        /// <param name="header">Imports and enclosing declarations.</param>
        /// <param name="body">The cached file contribution.</param>
        /// <param name="footer">Closing declarations.</param>
        /// <param name="source">The completed generated file.</param>
        /// <param name="settings">Additional immutable rendering settings.</param>
        internal Entry(string header, string body, string footer, FrozenGeneratedSource source, object settings)
        { Header = header; Body = body; Footer = footer; Source = source; Settings = settings; }
    }
}
