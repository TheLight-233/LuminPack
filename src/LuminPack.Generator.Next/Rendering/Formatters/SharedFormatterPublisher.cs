using System;
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
        foreach (string owner in set.Owners)
            production.AddSource("LuminPack.SharedFormatters/" + ReadableTypeName.Of(owner) + ".g.cs",
                FormatterRegistry.RenderStandalone(owner, set, operations));
        foreach (string scalar in set.Scalars)
            production.AddSource("LuminPack.SharedFormatters/Scalar_" + ReadableTypeName.Of(scalar) + ".g.cs",
                ScalarOwnerFile.Render(set, scalar, operations));

        foreach (DictionaryOwnerFact fact in set.DictionaryOwners ?? Array.Empty<DictionaryOwnerFact>())
            production.AddSource("LuminPack.SharedFormatters/Dictionary_" +
                ReadableTypeName.Of(GeneratedNames.PayloadKey(fact.DeclaredType)) + ".g.cs", fact.Family.Entries(set, fact, operations));

        bool candidateCache = false;
        foreach (SequenceOwnerFact fact in set.SequenceOwners)
        {
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
    }
}
