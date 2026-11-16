using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Binary;
using LuminPack.SourceGenerator.Next.Rendering.Json;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Publishes each requested formatter owner once through its registered renderer.</summary>
internal static class SharedFormatterPublisher
{
    /// <summary>Names the binary source of a closed payload owner.</summary>
    /// <param name="name">The readable owner name.</param>
    /// <returns>The shared binary source hint.</returns>
    internal static string PayloadBinaryHint(string name) => "LuminPack.SharedFormatters/Payload_" + name + ".g.cs";

    /// <summary>Publishes every shared owner file of the set.</summary>
    /// <param name="production">The source output.</param>
    /// <param name="set">The collected owner requirements.</param>
    /// <param name="operations">The operations the project calls.</param>
    internal static void Publish(SourceProductionContext production, SharedFormatterSet set, GeneratedOperations operations)
    {
        foreach (var source in Render(set, operations))
            production.AddSource(source.Key, source.Value);
    }

    /// <summary>Renders every shared owner file of the set as hint name and source pairs.</summary>
    /// <param name="set">The collected owner requirements.</param>
    /// <param name="operations">The operations the project calls.</param>
    /// <returns>The owner files in publication order.</returns>
    /// <remarks>Bounded untrusted roots copy owner read bodies from the same text that is published.</remarks>
    internal static IEnumerable<KeyValuePair<string, string>> Render(SharedFormatterSet set, GeneratedOperations operations) =>
        Render(set, operations == GeneratedOperations.None ? null : new OwnerOperations(operations));

    /// <summary>Renders every shared owner file with the operations of its owner type.</summary>
    /// <param name="set">The collected owner requirements.</param>
    /// <param name="operations">The operations of each owner type key; an owner without operations publishes nothing.</param>
    /// <returns>The owner files in publication order.</returns>
    internal static IEnumerable<KeyValuePair<string, string>> Render(SharedFormatterSet set, OwnerOperations? operations)
    {
        if (set.IsEmpty || operations == null)
            yield break;
        // Two requirements that differ only in annotations name the same owner class; it is published once.
        var hints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in RenderAll(set, operations))
            if (hints.Add(source.Key))
                yield return source;
    }

    /// <summary>Renders all.</summary>
    /// <param name="set">The shared formatter owners and publication options.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    /// <returns>The all.</returns>
    private static IEnumerable<KeyValuePair<string, string>> RenderAll(SharedFormatterSet set, OwnerOperations operations)
    {
        var general = new HashSet<string>(StringComparer.Ordinal);
        foreach (FormatterModel model in set.GeneralOwners ?? Array.Empty<FormatterModel>())
            if (model.Definition != FormatterDefinitions.PayloadReference)
                general.Add(GeneratedNames.PayloadKey(model.Type));
        GeneratedOperations selected;
        foreach (string owner in set.Owners)
        {
            string key = GeneratedNames.PayloadKey(owner);
            if (!general.Contains(key) && (selected = operations.For(key)) != GeneratedOperations.None)
                yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/" + ReadableTypeName.Of(owner) + ".g.cs",
                FormatterRegistry.RenderStandalone(owner, set, selected));
        }
        foreach (string scalar in set.Scalars)
        {
            string type = scalar.Split('=')[0], key = GeneratedNames.PayloadKey(type);
            if (!general.Contains(key) && (selected = operations.For(key)) != GeneratedOperations.None)
                yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Scalar_" + ReadableTypeName.Of(type) + ".g.cs",
                ScalarOwnerFile.Render(set, scalar, selected));
        }

        var shared = new GenericFormatterReuse.Publication(set);
        foreach (DictionaryOwnerFact fact in set.DictionaryOwners ?? Array.Empty<DictionaryOwnerFact>())
        {
            string key = GeneratedNames.PayloadKey(fact.DeclaredType);
            if (general.Contains(key) || (selected = operations.For(key)) == GeneratedOperations.None)
                continue;
            var render = fact.Element != null || fact.ForwardedValue ? fact.Family.Payload ?? fact.Family.Entries : fact.Family.Entries;
            DictionaryOwnerFact closed = fact;
            if (shared.TryDictionary(fact.DeclaredType, selected, chosen => render(set, closed, chosen), fact.Family, fact))
                continue;
            yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Dictionary_" +
                ReadableTypeName.Of(key) + ".g.cs", render(set, fact, selected));
        }

        // The immutable candidate cache is declared by the post-initialization facade, as in the original serializer source.
        foreach (SequenceOwnerFact fact in set.SequenceOwners)
        {
            string key = GeneratedNames.PayloadKey(fact.DeclaredType);
            if (general.Contains(key) || (selected = operations.For(key)) == GeneratedOperations.None)
                continue;
            RenderSequenceOwner? render = fact.ForwardedType != null ? fact.Family.Forwarded :
                fact.Element != null ? fact.Family.Payload : fact.Scalar == "string" ? fact.Family.Text : fact.Family.Scalar;
            render ??= LuminPackExceptionHelper.ThrowInvalidOperation<RenderSequenceOwner>("A formatter requirement has no registered element renderer.");
            SequenceOwnerFact sequence = fact;
            RenderSequenceOwner chosenRender = render;
            if (shared.TrySequence(fact.DeclaredType, selected, chosen => chosenRender(set, sequence, chosen), fact))
                continue;
            yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Sequence_" +
                ReadableTypeName.Of(key) + ".g.cs", render(set, fact, selected));
        }
        bool compoundStringReaderNeeded = false;
        foreach (FormatterModel model in set.GeneralOwners ?? Array.Empty<FormatterModel>())
        {
            if ((selected = operations.For(GeneratedNames.PayloadKey(model.Type))) == GeneratedOperations.None)
                continue;
            if (!compoundStringReaderNeeded && (selected & GeneratedOperations.BinaryRead) != 0 &&
                ClosedGenericOwnerFile.ReusesStringComponents(model.Definition) && Array.Exists(model.Arguments, argument => argument.Type == "string"))
                compoundStringReaderNeeded = true;
            if (model.Definition == FormatterDefinitions.PayloadReference)
            {
                if (model.Payload != null && model.PublishPayload)
                {
                    var renderContext = new RenderContext(set.Options, AliasTable.For(set.AvailableAliases), selected);
                    string name = ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type));
                    string binary = BinaryRenderer.Instance.Render(model.Payload, renderContext);
                    string json = JsonRenderer.Instance.Render(model.Payload, renderContext);
                    if (binary.Length != 0)
                        yield return new KeyValuePair<string, string>(PayloadBinaryHint(name), binary);
                    if (json.Length != 0)
                        yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Payload_" + name + ".Json.g.cs", json);
                }
                continue;
            }
            FormatterModel closedModel = model;
            if (FormatterRegistry.FindDictionary(model.Type) is null &&
                shared.TrySequence(model.Type, selected, chosen => DefaultFormatterOwnerFile.Render(set, closedModel, chosen)))
                continue;
            if (FormatterRegistry.FindDictionary(model.Type) is { } dictionary &&
                shared.TryDictionary(model.Type, selected, chosen => DefaultFormatterOwnerFile.Render(set, closedModel, chosen), dictionary))
                continue;
            yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Default_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type)) + ".g.cs",
                DefaultFormatterOwnerFile.Render(set, model, selected));
        }
        if (compoundStringReaderNeeded)
            yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/CompoundStringRead.g.cs", ClosedGenericOwnerFile.RenderStringReader(set));
        foreach (var source in shared.Render())
            yield return source;
    }
}

/// <summary>The operations each shared owner type keeps, keyed by <see cref="GeneratedNames.PayloadKey"/>.</summary>
internal sealed class OwnerOperations : IEquatable<OwnerOperations>
{
    private readonly GeneratedOperations _all;
    private readonly Dictionary<string, GeneratedOperations>? _owners;

    /// <summary>Selects the same operations for every owner.</summary>
    /// <param name="all">The operations of every owner.</param>
    internal OwnerOperations(GeneratedOperations all) => _all = all;

    /// <summary>Selects operations per owner on top of the operations every owner keeps.</summary>
    /// <param name="all">The operations every owner keeps.</param>
    /// <param name="owners">The further operations of each owner type key.</param>
    internal OwnerOperations(GeneratedOperations all, Dictionary<string, GeneratedOperations> owners)
    {
        _all = all;
        _owners = owners;
    }

    /// <summary>Selects the operations of one owner.</summary>
    /// <param name="key">The owner type key.</param>
    /// <returns>The owner's operations.</returns>
    internal GeneratedOperations For(string key) =>
        _owners != null && _owners.TryGetValue(key, out GeneratedOperations own) ? _all | own : _all;

    /// <inheritdoc/>
    public bool Equals(OwnerOperations? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other == null || _all != other._all || (_owners == null) != (other._owners == null))
            return false;
        if (_owners == null)
            return true;
        if (_owners.Count != other._owners!.Count)
            return false;
        foreach (KeyValuePair<string, GeneratedOperations> pair in _owners)
            if (!other._owners.TryGetValue(pair.Key, out GeneratedOperations value) || value != pair.Value)
                return false;
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as OwnerOperations);

    /// <inheritdoc/>
    public override int GetHashCode() => (int)_all ^ (_owners?.Count ?? 0);
}
