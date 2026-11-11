using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>FrozenSet&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The write stores the item array as one block. The read returns the empty instance for an empty payload, otherwise
/// reads one pooled block into a presized hash set and freezes it. The JSON read stages elements in a hash set that is frozen
/// after the last element.</remarks>
internal static class FrozenSetOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "staged.Add(item);",
        BeforeJson, AfterJson);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Write(EnumerableOwnerContext c)
    {
        if (c.Payload != null || c.Forwarded)
        {
            c.CountedWrite("value");
            return;
        }
        var lines = new List<string>();
        InterfaceTraversal.WriteFrozen(lines, c.Scalar, "value");
        foreach (string line in lines)
            c.W.Line(line);
    }

    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = " + c.Sequence + ".Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        bool rented = c.Supports("RentFrozenSet");
        w.Line(rented ? "var hashSet = global::LuminPack.Utility.LuminFrozenConstruction.RentSet<" + c.Scalar + ">(length);" :
            "var hashSet = new global::System.Collections.Generic.HashSet<" + c.Scalar + ">(length);");
        if (rented) { w.Line("try"); w.Open(); }
        c.PooledRead(() =>
        {
            w.Blank();
            w.Line("for (int index = 0; index < length; index++)");
            w.Indented(() => w.Line("hashSet.Add(pooled[index]);"));
            w.Line("value = global::System.Collections.Frozen.FrozenSet.ToFrozenSet(hashSet);");
        });
        if (rented)
        {
            w.Close();
            w.Line("finally");
            w.Open();
            w.Line("global::LuminPack.Utility.LuminFrozenConstruction.ReturnSet(hashSet);");
            w.Close();
        }
    }

    /// <summary>Rents the JSON set builder when the ownership API is available.</summary>
    private static void BeforeJson(EnumerableOwnerContext c)
    {
        c.W.Line(c.Supports("RentFrozenSet") ?
            "var staged = global::LuminPack.Utility.LuminFrozenConstruction.RentSet<" + c.Scalar + ">(0);" :
            "var staged = new global::System.Collections.Generic.HashSet<" + c.Scalar + ">();");
        if (c.Supports("RentFrozenSet")) { c.W.Line("try"); c.W.Open(); }
    }

    /// <summary>Freezes the elements and returns a leased builder on every exit.</summary>
    private static void AfterJson(EnumerableOwnerContext c)
    {
        c.W.Line("value = global::System.Collections.Frozen.FrozenSet.ToFrozenSet(staged);");
        if (!c.Supports("RentFrozenSet")) return;
        c.W.Close();
        c.W.Line("finally");
        c.W.Open();
        c.W.Line("global::LuminPack.Utility.LuminFrozenConstruction.ReturnSet(staged);");
        c.W.Close();
    }
}
