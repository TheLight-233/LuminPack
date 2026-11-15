using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>List&lt;T&gt;</c> whose element is a packable class.</summary>
/// <remarks>The write counts the enumeration of the list as it expands each element's record body through an isolated state. The
/// read creates a presized list or keeps the existing one, sizes it to the payload and expands each element's record body into
/// the existing element in place on the caller's reader, as the original list read did: a null marker clears the slot and a
/// missing element is constructed. The JSON roots read each element through the element's own formatter owner.</remarks>
internal static class ListPayloadOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(c => c.CountedWrite("value"), Read, "value.Add(item);", listReuse: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The payload-element list owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Handles read for <c>ListPayloadOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("value ??= new " + c.Sequence + "(length);");
        w.Line("global::System.Span<" + c.Scalar + "> span = global::LuminPack.Code.LuminPackMarshal.GetListSpan(value, length);");
        c.ReadBlockInPlace("span");
    }
}
