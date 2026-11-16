using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ConcurrentBag&lt;string&gt;</c>.</summary>
/// <remarks>The write counts the enumeration of the bag as it writes each string; the read creates the bag or clears the reused one
/// and adds each string read into a pooled block.</remarks>
internal static class ConcurrentBagTextOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "value.Add(item);", jsonCount: false,
        readInlining: "NoInlining", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Handles write for <c>ConcurrentBagTextOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Write(EnumerableOwnerContext c)
    {
        if (c.Forwarded)
            c.BagSnapshotWrite("value");
        else
            c.CountedWrite("value");
    }

    /// <summary>Handles read for <c>ConcurrentBagTextOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        bool presize = c.Supports("BagFreshSizing");
        if (presize)
            w.Line(c.Sequence + "? fresh = null;");
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = " + (presize ? "fresh = " : "") + "new " + c.Sequence + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        c.PooledRead(() =>
        {
            w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(pooled);");
            ConcurrentBagOwnerFile.AddStagedItems(w, c.Memory, presize);
        });
    }
}
