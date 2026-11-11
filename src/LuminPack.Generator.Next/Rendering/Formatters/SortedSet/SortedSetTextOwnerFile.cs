using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>SortedSet&lt;string&gt;</c>.</summary>
/// <remarks>The ordered tree kernel stores raw unmanaged elements only, so the write counts the in-order enumeration as it writes
/// each string; the read creates the set or clears the reused one and adds each string read into a pooled block.</remarks>
internal static class SortedSetTextOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "value.Add(item);");

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Write(EnumerableOwnerContext c) => c.CountedWrite("value");

    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        c.PooledRead(() =>
        {
            w.Line("for (int index = 0; index < length; index++)");
            w.Indented(() => w.Line("value.Add(pooled[index]);"));
        });
    }
}
