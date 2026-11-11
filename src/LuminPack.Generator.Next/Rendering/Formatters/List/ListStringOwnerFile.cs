using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the string-span operations for the string list.</summary>
internal static class ListStringOwnerFile
{
    /// <summary>Renders the selected binary and JSON string list operations.</summary>
    /// <param name="set">The shared owner publication requirements.</param>
    /// <param name="fact">The closed list type.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete generated source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        ListForwardingOwnerFile.Render(set, SequenceOwnerFact.ForForwarding(fact.Family, fact.DeclaredType, "string"), operations);
}
