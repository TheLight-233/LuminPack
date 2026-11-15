using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed one-dimensional array of strings or packable classes.</summary>
/// <remarks>The write counts the enumeration of the array as it writes each string or expands each element's record body. The
/// read returns the shared empty array for an empty payload, reuses a destination of the stored length and otherwise allocates an
/// uninitialized array, then fills it element by element. The JSON read stages the elements in a list and publishes its array.</remarks>
internal static class ArrayTextOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "staged.Add(item);",
        c => c.W.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">();"),
        c => c.W.Line("value = staged.ToArray();"), jsonCount: false, compressEntries: true, arrayReuse: true, outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed array owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Handles write for <c>ArrayTextOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Write(EnumerableOwnerContext c)
    {
        if (!c.Text || !c.Supports("WriteArrayStrings"))
        {
            c.CountedWrite("value");
            return;
        }
        var w = c.W;
        w.Line("int length = value.Length;");
        w.Line("operationState.EnsureAdditionalCapacity(sizeof(int));");
        w.Line("operationState.WriteCollectionHeader(length);");
        w.Line("if (length != 0)");
        w.Open();
        w.Line("ref string first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(value);");
        w.Line("operationState.WriteStrings<TLuminProfile>(" + c.Marshal + ".CreateReadOnlySpan(ref first, length));");
        w.Close();
        w.Line("operationState.CheckBuffer();");
    }

    /// <summary>Handles read for <c>ArrayTextOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = global::System.Array.Empty<" + c.Scalar + ">();");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("if (value is null || value.Length != length)");
        w.Indented(() => w.Line("value = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<" + c.Scalar + ">(length);"));
        // A packable element is read into the existing element in place, as the original array read did.
        if (c.Payload != null)
        {
            c.ReadBlockInPlace("span", array: "value");
            return;
        }
        w.Line("var span = new global::System.Span<" + c.Scalar + ">(value);");
        c.ReadBlock("span");
    }
}
