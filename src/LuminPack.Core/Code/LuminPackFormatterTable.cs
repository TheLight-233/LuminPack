using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LuminPack.Code;

/// <summary>Retains registered delegates for their payload type's lifetime without rooting collectible payload assemblies.</summary>
/// <remarks>Method-table indexes hold weak references. The conditional table owns each complete entry while its payload type is alive.</remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class LuminPackFormatterTable
{
    private readonly ConditionalWeakTable<Type, Entry> _owners = new();
    private readonly ConcurrentDictionary<nint, WeakReference<Entry>> _entries = new();

    /// <summary>Publishes the generated operations available for a payload in one consuming assembly.</summary>
    /// <typeparam name="T">The exact closed payload type.</typeparam>
    /// <param name="write">The binary writer, when emitted.</param>
    /// <param name="read">The binary reader, when emitted.</param>
    /// <param name="writeJson">The JSON writer, when emitted.</param>
    /// <param name="readJson">The JSON reader, when emitted.</param>
    internal void RegisterGenerated<T>(LuminPackWriteDelegate<T>? write, LuminPackReadDelegate<T>? read,
        LuminPackJsonWriteDelegate<T>? writeJson, LuminPackJsonReadDelegate<T>? readJson)
    {
        TryAdd(typeof(T), new LuminPackFormatter
        {
            WriteValue = write,
            ReadValue = read,
            WriteValueJson = writeJson,
            ReadValueJson = readJson
        });
    }

    /// <summary>Publishes an explicit entry and then invalidates cached lookups.</summary>
    /// <param name="type">The registration's lifetime owner.</param>
    /// <param name="formatter">The validated formatter entry.</param>
    /// <param name="duplicateMessage">The diagnostic for an entry already in this table.</param>
    internal void PublishExplicit(Type type, LuminPackFormatter formatter, string duplicateMessage)
    {
        if (!TryAdd(type, formatter))
            LuminPackExceptionHelper.ThrowArgumentException(duplicateMessage);
        Interlocked.Increment(ref LuminPackCompiledFormatterTables.Revision);
    }

    /// <summary>Publishes one complete registration for a payload type.</summary>
    /// <param name="type">The registration's lifetime owner.</param>
    /// <param name="formatter">The complete immutable registration value.</param>
    /// <returns>False when this type already has a live registration.</returns>
    public bool TryAdd(Type type, LuminPackFormatter formatter)
    {
        lock (_owners)
        {
            if (_owners.TryGetValue(type, out _))
                return false;
            var entry = new Entry(formatter);
            _owners.Add(type, entry);
            _entries[LuminPackMarshal.GetMethodTable(type)] = new WeakReference<Entry>(entry);
            return true;
        }
    }

    /// <summary>Finds a live registration by its payload method table.</summary>
    /// <param name="key">The payload method table.</param>
    /// <param name="formatter">Receives the complete registration.</param>
    /// <returns>Whether the payload still owns a registration.</returns>
    public bool TryGetValue(nint key, out LuminPackFormatter formatter)
    {
        if (_entries.TryGetValue(key, out var weak) && weak.TryGetTarget(out var entry))
        {
            formatter = entry.Formatter;
            return true;
        }
        formatter = default;
        return false;
    }

    /// <summary>Finds a live Union case by wire tag.</summary>
    /// <param name="tag">The requested case tag.</param>
    /// <param name="formatter">Receives the complete case registration.</param>
    /// <returns>Whether a live case owns this tag.</returns>
    public bool TryGetTag(int tag, out LuminPackFormatter formatter)
    {
        foreach (var pair in _entries)
            if (pair.Value.TryGetTarget(out var entry) && entry.Formatter.Tag == tag)
            {
                formatter = entry.Formatter;
                return true;
            }
        formatter = default;
        return false;
    }

    /// <summary>Stores the complete value behind an ephemeron and weak indexes.</summary>
    private sealed class Entry
    {
        internal readonly LuminPackFormatter Formatter;

        /// <summary>Freezes the registration value before publication.</summary>
        /// <param name="formatter">The complete registration.</param>
        internal Entry(LuminPackFormatter formatter) => Formatter = formatter;
    }
}
