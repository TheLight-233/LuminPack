using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>HashSet&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy hash-set operations. The binary write copies the live entries through the shared raw
/// kernel. The binary read creates a presized set and rebuilds its buckets in entry order. Fixed callback-free elements
/// can build the buckets while loading the entries. An empty payload reuses and clears a non-null destination.</remarks>
internal static class HashSetOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "value.Add(item);",
        readInlining: "NoInlining", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec, nonEmptyRawHeaderBytes: sizeof(int), directRawWrite: WriteDirect);

    /// <summary>Writes a nonempty or empty raw set to its fixed destination without copying writer state.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void WriteDirect(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (writer.Owner is null && input is not null)");
        w.Open();
        WriteRaw(c, "writer", "input");
        w.Line("return;");
        w.Close();
    }

    /// <summary>Handles write for <c>HashSetOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Write(EnumerableOwnerContext c)
    {
        // The raw kernel needs unmanaged elements; strings are counted while the set is enumerated.
        if (c.Counted)
        {
            c.CountedWrite("value");
            return;
        }
        WriteRaw(c, "operationState", "value");
    }

    /// <summary>Writes raw.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    /// <param name="state">The current operation state.</param>
    /// <param name="value">The value to process.</param>
    private static void WriteRaw(EnumerableOwnerContext c, string state, string value) =>
        c.W.Line("global::LuminPack.Code.LuminPackMarshal." + (c.Supports("SmallHashSetWrite") ? "WriteSmallHashSetRaw" : "WriteHashSetRaw") +
            "(ref " + state + ", " + value + ");");

    /// <summary>Handles read for <c>HashSetOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value ??= new " + c.Sequence + "(0);");
        w.Line("value.Clear();");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line(c.HashSetRefill
            ? "value = global::LuminPack.Code.LuminPackCollectionAccess.PrepareHashSetForRefill(value, length);"
            : "value = new " + c.Sequence + "(length);");
        w.Blank();
        if (!c.Counted && c.Supports("ReadRawHashSet"))
        {
            w.Line("global::LuminPack.Code.LuminPackMarshal.ReadRawHashSet(ref operationState, value, length);");
            return;
        }
        w.Line("var setView = global::LuminPack.Code.LuminPackMarshal.GetHashSetView(value);");
        w.Line("ref var entryRef = ref global::LuminPack.Code.LuminPackMarshal.GetArrayReference(setView._entries);");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("ref var entry = ref " + c.Memory + ".Add(ref entryRef, (nint)(uint)index);");
        c.ReadElement("entry.Value");
        w.Close();
        w.Line("while (++index < length);");
        w.Blank();
        w.Line("global::LuminPack.Code.LuminPackMarshal.RebuildHashSetBuckets(setView, length);");
    }
}
