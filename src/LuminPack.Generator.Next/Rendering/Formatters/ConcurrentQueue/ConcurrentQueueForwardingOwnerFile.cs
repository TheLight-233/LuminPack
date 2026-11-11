using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders a queue of nested collections through their generated element owners.</summary>
internal static class ConcurrentQueueForwardingOwnerFile
{
    /// <summary>Renders the binary and JSON queue operations for one closed type.</summary>
    /// <param name="set">The shared formatter publication requirements.</param>
    /// <param name="fact">The queue type and the type of its forwarded element.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The generated source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        ConcurrentQueueStringOwnerFile.Render(set, fact.DeclaredType, fact.ForwardedType!, stringElement: false, operations);
}
