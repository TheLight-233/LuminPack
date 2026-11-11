using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Binary;
using LuminPack.SourceGenerator.Next.Rendering.Json;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Publishes each requested formatter owner once through its registered renderer.</summary>
internal static class SharedFormatterPublisher
{
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
    internal static IEnumerable<KeyValuePair<string, string>> Render(SharedFormatterSet set, GeneratedOperations operations)
    {
        // Two requirements that differ only in annotations name the same owner class; it is published once.
        var hints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in RenderAll(set, operations))
            if (hints.Add(source.Key))
                yield return source;
    }

    private static IEnumerable<KeyValuePair<string, string>> RenderAll(SharedFormatterSet set, GeneratedOperations operations)
    {
        if (set.IsEmpty || operations == GeneratedOperations.None)
            yield break;
        var general = new HashSet<string>(StringComparer.Ordinal);
        foreach (FormatterModel model in set.GeneralOwners ?? Array.Empty<FormatterModel>())
            if (model.Definition != FormatterDefinitions.PayloadReference)
                general.Add(GeneratedNames.PayloadKey(model.Type));
        foreach (string owner in set.Owners)
            if (!general.Contains(GeneratedNames.PayloadKey(owner)))
                yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/" + ReadableTypeName.Of(owner) + ".g.cs",
                FormatterRegistry.RenderStandalone(owner, set, operations));
        foreach (string scalar in set.Scalars)
            if (!general.Contains(GeneratedNames.PayloadKey(scalar.Split('=')[0])))
                yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Scalar_" + ReadableTypeName.Of(scalar.Split('=')[0]) + ".g.cs",
                ScalarOwnerFile.Render(set, scalar, operations));

        foreach (DictionaryOwnerFact fact in set.DictionaryOwners ?? Array.Empty<DictionaryOwnerFact>())
        {
            if (general.Contains(GeneratedNames.PayloadKey(fact.DeclaredType)))
                continue;
            var render = fact.Element != null || fact.ForwardedValue ? fact.Family.Payload ?? fact.Family.Entries : fact.Family.Entries;
            yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Dictionary_" +
                ReadableTypeName.Of(GeneratedNames.PayloadKey(fact.DeclaredType)) + ".g.cs", render(set, fact, operations));
        }

        // The immutable candidate cache is declared by the post-initialization facade, as in the original serializer source.
        foreach (SequenceOwnerFact fact in set.SequenceOwners)
        {
            if (general.Contains(GeneratedNames.PayloadKey(fact.DeclaredType)))
                continue;
            RenderSequenceOwner? render = fact.ForwardedType != null ? fact.Family.Forwarded :
                fact.Element != null ? fact.Family.Payload : fact.Scalar == "string" ? fact.Family.Text : fact.Family.Scalar;
            if (render == null)
                throw new InvalidOperationException("A formatter requirement has no registered element renderer.");
            yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Sequence_" +
                ReadableTypeName.Of(GeneratedNames.PayloadKey(fact.DeclaredType)) + ".g.cs", render(set, fact, operations));
        }
        foreach (FormatterModel model in set.GeneralOwners ?? Array.Empty<FormatterModel>())
        {
            if (model.Definition == FormatterDefinitions.PayloadReference)
            {
                if (model.Payload != null && model.PublishPayload)
                {
                    var renderContext = new RenderContext(set.Options, AliasTable.For(set.AvailableAliases), operations);
                    string name = ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type));
                    string binary = BinaryRenderer.Instance.Render(model.Payload, renderContext);
                    string json = JsonRenderer.Instance.Render(model.Payload, renderContext);
                    if (binary.Length != 0) yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Payload_" + name + ".g.cs", binary);
                    if (json.Length != 0) yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Payload_" + name + ".Json.g.cs", json);
                }
                continue;
            }
            yield return new KeyValuePair<string, string>("LuminPack.SharedFormatters/Default_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type)) + ".g.cs",
                DefaultFormatterOwnerFile.Render(set, model, operations));
        }
    }
}
