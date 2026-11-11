using System;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits a closed dictionary owner for the requested operations.</summary>
/// <param name="set">The set used by this generator operation.</param>
/// <param name="fact">The fact used by this generator operation.</param>
/// <param name="operations">The operations used by this generator operation.</param>
/// <returns>The generated owner source.</returns>
internal delegate string RenderDictionaryOwner(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations);

/// <summary>Describes one open dictionary definition and its supported entry forms.</summary>
internal sealed class DictionaryFormatter
{
    /// <summary>The renderer for a key and value that are each a raw scalar or a string.</summary>
    internal RenderDictionaryOwner Entries { get; }

    /// <summary>Registers a dictionary definition.</summary>
    /// <param name="entries">The owner renderer for raw scalar and string entries.</param>
    internal DictionaryFormatter(RenderDictionaryOwner entries, bool forwardsToDictionary = false)
    {
        Entries = entries;
        ForwardsToDictionary = forwardsToDictionary;
    }

    /// <summary>Whether the owner reads through the concrete <c>Dictionary&lt;TKey, TValue&gt;</c> owner of the same entries.</summary>
    internal bool ForwardsToDictionary { get; }
}

/// <summary>One closed dictionary owner the compilation must publish.</summary>
/// <remarks>The key and value are each a raw scalar keyword or <c>string</c>. These facts contain no compiler objects.</remarks>
internal readonly struct DictionaryOwnerFact : IEquatable<DictionaryOwnerFact>
{
    /// <summary>The declared dictionary family, which selects the owner template.</summary>
    internal readonly DictionaryFormatter Family;

    /// <summary>The declared dictionary spelling.</summary>
    internal readonly string DeclaredType;

    /// <summary>The key keyword, or <c>string</c>.</summary>
    internal readonly string Key;

    /// <summary>The value keyword, or <c>string</c>.</summary>
    internal readonly string Value;

    /// <summary>Captures a dictionary of raw scalar or string entries.</summary>
    /// <param name="family">The declared dictionary family.</param>
    /// <param name="declaredType">The declared dictionary spelling.</param>
    /// <param name="key">The key keyword or <c>string</c>.</param>
    /// <param name="value">The value keyword or <c>string</c>.</param>
    internal DictionaryOwnerFact(DictionaryFormatter family, string declaredType, string key, string value)
    {
        Family = family;
        DeclaredType = declaredType;
        Key = key;
        Value = value;
    }

    /// <inheritdoc/>
    public bool Equals(DictionaryOwnerFact other) => Family == other.Family && DeclaredType == other.DeclaredType &&
        Key == other.Key && Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DictionaryOwnerFact other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (DeclaredType == null ? 0 : DeclaredType.GetHashCode()) ^
        (Key == null ? 0 : Key.GetHashCode() * 31) ^ (Value == null ? 0 : Value.GetHashCode() * 17) ^
        (Family == null ? 0 : Family.GetHashCode());
}
