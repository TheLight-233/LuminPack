using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>PriorityQueue&lt;TElement, TPriority&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The wire is the node count followed by each element and its priority in the heap's own node order, which the
/// unordered enumeration yields. The read creates a presized queue or clears the reused one and enqueues the nodes in the order
/// stored, which rebuilds the heap array that was written. The JSON form is an array of element and priority arrays.</remarks>
internal static class PriorityQueueOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(c => c.CountedWrite(), Read, "value.Enqueue(key!, itemValue);",
        source: "value.UnorderedItems", keyMember: "Element", valueMember: "Priority");

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed priority queue owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Dictionary + "(length);"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("for (int index = 0; index < length; index++)");
        w.Open();
        w.Line(c.Key + " element = default !;");
        w.Line(c.Value + " priority = default !;");
        c.ReadMember(c.Key, "element");
        c.ReadMember(c.Value, "priority");
        w.Line("value.Enqueue(element!, priority);");
        w.Close();
    }
}
