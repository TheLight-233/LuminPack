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

    private static void Write(EnumerableOwnerContext c)
    {
        if (!c.Counted)
            c.W.Line("global::LuminPack.Utility.LuminTreeStorage.WriteRaw(ref operationState, value);");
        else if (c.Forwarded)
            c.TreeSnapshotWrite("value");
        else
            c.CountedWrite("value");
    }

    internal static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        const string tree = "global::LuminPack.Utility.LuminTreeStorage";
        w.Line("if (value is null)");
        w.Open();
        c.PooledRead(() => w.Line(c.Supports("BuildSortedSet") ?
            "value = " + tree + ".CreateSortedSet(pooled, length);" :
            "value = new " + c.Sequence + "(new global::System.ArraySegment<" + c.Scalar + ">(pooled, 0, length));"));
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        if (c.Supports("RebuildSortedSet"))
        {
            w.Line("if (" + tree + ".CanRebuildSortedSet(value))");
            w.Open();
            w.Line("value.Clear();");
            c.PooledRead(() =>
            {
                w.Line("if (!" + tree + ".TryRebuildSortedSet(value, pooled, length))");
                AddPrefix("length");
            }, c.ContiguousLayout ? null : () => AddPrefix("completed"));
            w.Line("goto operationComplete_RootRead;");
            w.Close();
        }
        w.Line("value.Clear();");
        if (c.ContiguousLayout)
        {
            c.PooledRead(() => AddPrefix("length"));
            return;
        }
        w.Line("for (int index = 0; index < length; index++)");
        w.Open();
        w.Line(c.Scalar + " item = default!;");
        c.ReadElement("item");
        w.Line("value.Add(item);");
        w.Close();

        void AddPrefix(string count)
        {
            w.Line("for (int prefix = 0; prefix < " + count + "; prefix++)");
            w.Indented(() => w.Line("value.Add(pooled[prefix]);"));
        }
    }
}
