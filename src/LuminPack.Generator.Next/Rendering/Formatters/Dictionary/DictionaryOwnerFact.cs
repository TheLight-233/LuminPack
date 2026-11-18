using System;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Model;

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
    /// <param name="forwardsToDictionary">Whether to enable forwards to dictionary for this operation.</param>
    /// <param name="payload">The payload supplied to <c>DictionaryFormatter</c>.</param>
    /// <param name="jsonReuseStore">The JSON reuse store supplied to <c>DictionaryFormatter</c>.</param>
    /// <param name="nativeEntries">Whether to enable native entries for this operation.</param>
    /// <param name="general">The general supplied to <c>DictionaryFormatter</c>.</param>
    /// <param name="isMap">Whether entries represent unique dictionary keys.</param>
    internal DictionaryFormatter(RenderDictionaryOwner entries, bool forwardsToDictionary = false, RenderDictionaryOwner? payload = null,
        string? jsonReuseStore = null, bool nativeEntries = false, RenderDictionaryOwner? general = null, bool isMap = true)
    {
        General = general;
        NativeEntries = nativeEntries || forwardsToDictionary;
        JsonReuseStore = jsonReuseStore;
        Entries = entries;
        IsMap = isMap;
        Payload = payload;
        ForwardsToDictionary = forwardsToDictionary;
    }

    /// <summary>Whether a string key can represent a JSON property in this collection.</summary>
    internal bool IsMap { get; }

    /// <summary>Whether a fully raw pair follows the concrete Dictionary contiguous entry contract.</summary>
    internal bool NativeEntries { get; }

    /// <summary>The insertion statement after an existing mutable JSON target has been staged.</summary>
    internal string? JsonReuseStore { get; }

    /// <summary>The optional family-specific general contract renderer.</summary>
    internal RenderDictionaryOwner? General { get; }

    /// <summary>Selects retained entry specializations or the default element-owner traversal.</summary>
    /// <param name="set">The shared formatter owners and publication options.</param>
    /// <param name="fact">The fact supplied to <c>RenderDefault</c>.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    /// <returns>The generated C# source.</returns>
    internal string RenderDefault(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations)
    {
        if (General != null)
            return General(set, fact, operations);
        FormatterModel key = fact.Contract!.Arguments[0], value = fact.Contract.Arguments[1];
        // A packable payload is a native entry only when its own formatter is a plain storage copy.
        static bool Native(FormatterModel side) => side.Raw && (side.Payload == null || side.RawStorage);
        bool direct = ((Native(key) || key.Type == "string") && (Native(value) || value.Type == "string") ||
            NativeEntries && key.RawStorage && value.RawStorage) && key.Type != "bool" && value.Type != "bool";
        return direct ? Entries(set, new DictionaryOwnerFact(this, fact.DeclaredType, key.Type, value.Type), operations) :
            (Payload ?? Entries)(set, fact, operations);
    }

    /// <summary>The renderer for a packable class value, when it differs from the raw and string renderer.</summary>
    internal RenderDictionaryOwner? Payload { get; }

    /// <summary>Whether the owner reads through the concrete <c>Dictionary&lt;TKey, TValue&gt;</c> owner of the same entries.</summary>
    internal bool ForwardsToDictionary { get; }
}

/// <summary>One closed dictionary owner the compilation must publish.</summary>
/// <remarks>The key and value are each a raw scalar keyword or <c>string</c>. These facts contain no compiler objects.</remarks>
internal readonly struct DictionaryOwnerFact : IEquatable<DictionaryOwnerFact>
{
    /// <summary>The default argument contracts of a closed dictionary.</summary>
    internal readonly FormatterModel? Contract;

    /// <summary>The declared dictionary family, which selects the owner template.</summary>
    internal readonly DictionaryFormatter Family;

    /// <summary>The declared dictionary spelling.</summary>
    internal readonly string DeclaredType;

    /// <summary>The key keyword, or <c>string</c>.</summary>
    internal readonly string Key;

    /// <summary>The value keyword, <c>string</c>, or the global name of a packable class value.</summary>
    internal readonly string Value;

    /// <summary>The packable value's render model, which the owner expands in place, or null.</summary>
    internal readonly PayloadRenderModel? Element;

    /// <summary>Whether the value calls another collection owner.</summary>
    internal readonly bool ForwardedValue;

    /// <summary>Captures a dictionary of raw scalar, string or forwarded entries.</summary>
    /// <param name="family">The declared dictionary family.</param>
    /// <param name="declaredType">The declared dictionary spelling.</param>
    /// <param name="key">The key keyword or <c>string</c>.</param>
    /// <param name="value">The value keyword or <c>string</c>.</param>
    /// <param name="element">The element used by this operation.</param>
    /// <param name="forwardedValue">The forwarded value used by this operation.</param>
    /// <param name="contract">The normalized closed formatter contract.</param>
    internal DictionaryOwnerFact(DictionaryFormatter family, string declaredType, string key, string value, PayloadRenderModel? element = null,
        bool forwardedValue = false, FormatterModel? contract = null)
    {
        Contract = contract;
        ForwardedValue = forwardedValue;
        Element = element;
        Family = family;
        DeclaredType = declaredType;
        Key = key;
        Value = value;
    }

    /// <inheritdoc/>
    public bool Equals(DictionaryOwnerFact other) => Family == other.Family && DeclaredType == other.DeclaredType &&
        Key == other.Key && Value == other.Value && Equals(Element, other.Element) && ForwardedValue == other.ForwardedValue && Equals(Contract, other.Contract);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DictionaryOwnerFact other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (DeclaredType == null ? 0 : DeclaredType.GetHashCode()) ^
        (Key == null ? 0 : Key.GetHashCode() * 31) ^ (Value == null ? 0 : Value.GetHashCode() * 17) ^ (Element == null ? 0 : Element.GetHashCode()) ^
        (Family == null ? 0 : Family.GetHashCode());
}
