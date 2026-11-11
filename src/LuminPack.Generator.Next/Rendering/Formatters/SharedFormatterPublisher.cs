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
    /// <summary>Publishes selected operations and their shared support declarations.</summary>
    /// <param name="production">The source production context.</param>
    /// <param name="set">The sorted, value-equal owner requirements.</param>
    /// <param name="operations">The operations to publish.</param>
    internal static void Publish(SourceProductionContext production, SharedFormatterSet set, GeneratedOperations operations)
    {
        if (set.IsEmpty || operations == GeneratedOperations.None)
            return;
        var general = new HashSet<string>(StringComparer.Ordinal);
        foreach (FormatterModel model in set.GeneralOwners ?? Array.Empty<FormatterModel>())
            if (model.Definition != FormatterDefinitions.PayloadReference)
                general.Add(GeneratedNames.PayloadKey(model.Type));
        foreach (string owner in set.Owners)
            if (!general.Contains(GeneratedNames.PayloadKey(owner)))
                production.AddSource("LuminPack.SharedFormatters/" + ReadableTypeName.Of(owner) + ".g.cs",
                FormatterRegistry.RenderStandalone(owner, set, operations));
        foreach (string scalar in set.Scalars)
            if (!general.Contains(GeneratedNames.PayloadKey(scalar.Split('=')[0])))
                production.AddSource("LuminPack.SharedFormatters/Scalar_" + ReadableTypeName.Of(scalar.Split('=')[0]) + ".g.cs",
                ScalarOwnerFile.Render(set, scalar, operations));

        foreach (DictionaryOwnerFact fact in set.DictionaryOwners ?? Array.Empty<DictionaryOwnerFact>())
        {
            if (general.Contains(GeneratedNames.PayloadKey(fact.DeclaredType)))
                continue;
            var render = fact.Element != null || fact.ForwardedValue ? fact.Family.Payload ?? fact.Family.Entries : fact.Family.Entries;
            production.AddSource("LuminPack.SharedFormatters/Dictionary_" +
                ReadableTypeName.Of(GeneratedNames.PayloadKey(fact.DeclaredType)) + ".g.cs", render(set, fact, operations));
        }

        bool candidateCache = false;
        foreach (SequenceOwnerFact fact in set.SequenceOwners)
        {
            if (general.Contains(GeneratedNames.PayloadKey(fact.DeclaredType)))
                continue;
            if (!candidateCache && fact.Family.UsesCandidateCache && (operations & GeneratedOperations.BinaryRead) != 0)
            {
                production.AddSource("LuminPack.SharedFormatters/ImmutableCandidateCache.g.cs",
                    ImmutableHashSetOwnerFile.RenderCache(set));
                candidateCache = true;
            }
            RenderSequenceOwner? render = fact.ForwardedType != null ? fact.Family.Forwarded :
                fact.Element != null ? fact.Family.Payload : fact.Scalar == "string" ? fact.Family.Text : fact.Family.Scalar;
            if (render == null)
                throw new InvalidOperationException("A formatter requirement has no registered element renderer.");
            production.AddSource("LuminPack.SharedFormatters/Sequence_" +
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
                    if (binary.Length != 0) production.AddSource("LuminPack.SharedFormatters/Payload_" + name + ".g.cs", binary);
                    if (json.Length != 0) production.AddSource("LuminPack.SharedFormatters/Payload_" + name + ".Json.g.cs", json);
                }
                continue;
            }
            if (!candidateCache && FormatterRegistry.FindSequence(model.Definition)?.UsesCandidateCache == true &&
                (operations & GeneratedOperations.BinaryRead) != 0)
            {
                production.AddSource("LuminPack.SharedFormatters/ImmutableCandidateCache.g.cs", ImmutableHashSetOwnerFile.RenderCache(set));
                candidateCache = true;
            }
            production.AddSource("LuminPack.SharedFormatters/Default_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type)) + ".g.cs",
                DefaultFormatterOwnerFile.Render(set, model, operations));
        }
    }
}
