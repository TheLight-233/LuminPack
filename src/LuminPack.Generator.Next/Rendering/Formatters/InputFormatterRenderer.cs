using System;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders existing inline-element owners with the checked root's immutable element policy.</summary>
internal static class InputFormatterRenderer
{
    /// <summary>Reuses the original family renderer when a shared owner expands payload elements in place.</summary>
    /// <param name="set">The original shared owner catalog.</param>
    /// <param name="target">The requested readable owner name.</param>
    /// <param name="redirect">The requesting root's frozen input policy.</param>
    /// <returns>The read-only owner source, or null when this owner uses its published read body.</returns>
    internal static string? Render(SharedFormatterSet set, string target, InputRedirect redirect)
    {
        // General contracts take precedence over family specializations in the original publisher. Preserve that choice.
        foreach (FormatterModel owner in set.GeneralOwners ?? Array.Empty<FormatterModel>())
            if (owner.Definition != FormatterDefinitions.PayloadReference && Matches(owner.Type, target))
            {
                if (set.Options.Net8 && owner.Definition == "System.Collections.Generic.List" &&
                    CheckedUnion(owner.Arguments[0]) is { } listItem)
                    return ContiguousOwnerFile.Render(set, owner, GeneratedOperations.BinaryRead, ContiguousShape.List, listItem);
                if (owner.Definition == "System.Collections.Generic.Dictionary" && CheckedUnion(owner.Arguments[1]) is { } value &&
                    FormatterRegistry.FindDictionary(owner.Type) is { } family)
                {
                    var fact = new DictionaryOwnerFact(family, owner.Type, owner.Arguments[0].Type,
                        owner.Arguments[1].Type, contract: owner);
                    return PayloadDictionaryOwnerFile.Render(set, fact, GeneratedOperations.BinaryRead, value);
                }
                return null;
            }
        foreach (SequenceOwnerFact fact in set.SequenceOwners ?? Array.Empty<SequenceOwnerFact>())
            if (fact.Element != null && fact.Family.Payload != null && Matches(fact.DeclaredType, target))
                return fact.Family.Payload(set, fact.WithInput(redirect), GeneratedOperations.BinaryRead);
        foreach (DictionaryOwnerFact fact in set.DictionaryOwners ?? Array.Empty<DictionaryOwnerFact>())
            if (fact.Element != null && Matches(fact.DeclaredType, target))
            {
                var element = fact.Element.WithInput(redirect.Limits, fact.Element.Recursive, redirect);
                var checkedFact = new DictionaryOwnerFact(fact.Family, fact.DeclaredType, fact.Key, fact.Value, element,
                    fact.ForwardedValue, fact.Contract);
                return (fact.Family.Payload ?? fact.Family.Entries)(set, checkedFact, GeneratedOperations.BinaryRead);
            }
        return null;

        PayloadRenderModel? CheckedUnion(FormatterModel argument) =>
            argument.Payload is UnionModel { Referenced: false, Rejection: null } union && !union.Identity.IsGeneric &&
            RenderModelFactory.Create(union, set.Options, checkedUnionCopy: true) is { } model
                ? model.WithInput(redirect.Limits, recursive: false, redirect) : null;
    }

    /// <summary>Matches a catalog type using the publisher's exact readable owner identity.</summary>
    /// <param name="type">The original declared type spelling.</param>
    /// <param name="target">The requested owner identity.</param>
    /// <returns>Whether this fact publishes the requested owner.</returns>
    private static bool Matches(string type, string target) => ReadableTypeName.Of(GeneratedNames.PayloadKey(type)) == target;
}
