using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owners of the closed sequence interfaces of raw scalars.</summary>
/// <remarks>The write tests the exact runtime type of the common implementations first and counts the enumeration of every other
/// one. The enumerable, collection and read-only collection interfaces read into an array, the list interfaces into a fresh list
/// filled in place, and the set interfaces into a presized hash set whose entries are read directly before its buckets are
/// rebuilt. The JSON read stages elements in a list or hash set that the interface then holds.</remarks>
internal static class InterfaceOwnerFile
{
    private static readonly EnumerableOwnerSpec ArraySpec = new EnumerableOwnerSpec(c => WriteLines(c, false), ReadArray, "staged.Add(item);",
        BeginJsonList,
        c => c.W.Line("value = staged;"), jsonCount: false, interfaceListReuse: true, readInlining: "NoInlining", outlineJson: true);

    private static readonly EnumerableOwnerSpec CountedArraySpec = new EnumerableOwnerSpec(c => WriteLines(c, false), ReadArray, "staged.Add(item);",
        BeginJsonList,
        c => c.W.Line("value = staged;"), interfaceListReuse: true, readInlining: "NoInlining", outlineJson: true);

    private static readonly EnumerableOwnerSpec ListSpec = new EnumerableOwnerSpec(c => WriteLines(c, false), ReadList, "staged.Add(item);",
        BeginJsonList,
        c => c.W.Line("value = staged;"), interfaceListReuse: true, readInlining: "NoInlining", outlineJson: true);

    private static readonly EnumerableOwnerSpec SetSpec = new EnumerableOwnerSpec(c => WriteLines(c, true), ReadSet, "staged.Add(item);",
        c => c.W.Line("var staged = new global::System.Collections.Generic.HashSet<" + c.Scalar + ">();"),
        c => c.W.Line("value = staged;"), readInlining: "NoInlining", outlineJson: true, interfaceSetTraversal: true);

    /// <summary>Renders an <c>IEnumerable&lt;T&gt;</c> owner, whose count is unknown.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderEnumerable(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, ArraySpec);

    /// <summary>Renders a collection or read-only collection owner, which reads into an array.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderCollection(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, CountedArraySpec);

    /// <summary>Renders a list or read-only list owner, which reads into a list.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderList(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, ListSpec);

    /// <summary>Renders a set or read-only set owner, which reads into a hash set.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderSet(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, SetSpec);

    /// <summary>Presizes fresh JSON lists with the same structural hint as the concrete List reader.</summary>
    /// <param name="c">The sequence owner's read context.</param>
    private static void BeginJsonList(EnumerableOwnerContext c)
    {
        string capacity = c.Supports("InterfaceJsonCapacity") ? "reader.GetArrayCapacityHint<TLuminProfile>(10)" : string.Empty;
        c.W.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">(" + capacity + ");");
    }

    /// <summary>Separates common concrete writes from the interface enumeration.</summary>
    /// <param name="c">The sequence owner's write context.</param>
    /// <param name="setInterface">Whether the interface can hold a framework set.</param>
    private static void WriteLines(EnumerableOwnerContext c, bool setInterface)
    {
        var w = c.W;
        string writer = w.A("LuminPackWriter");
        string scalar = c.Scalar;
        string arrayType = ContiguousOwnerFile.ArrayType(scalar);
        string type = "global::System.Collections.Generic." + (setInterface ? "HashSet" : "List") + "<" + scalar + ">";
        bool outlineFallback = c.Counted || setInterface;
        w.Line("if (value.GetType() == typeof(" + type + "))");
        w.Open();
        if (c.Counted)
            w.Line("WriteKnown(ref operationState, " + c.Memory + ".As<" + type + ">(value));");
        else if (setInterface)
            w.Line("global::LuminPack.Code.LuminPackMarshal.WriteHashSetRaw(ref operationState, " + c.Memory + ".As<" + type + ">(value));");
        else
        {
            w.Line("var items = global::LuminPack.Code.LuminPackMarshal.GetListSpan(" + c.Memory + ".As<" + type + ">(value));");
            w.Line("operationState.Write<" + scalar + ">(items, items.Length);");
            w.Line("operationState.CheckBuffer();");
        }
        w.Close();
        if (!setInterface)
        {
            w.Line("else if (value.GetType() == typeof(" + arrayType + "))");
            w.Open();
            string array = c.Memory + ".As<" + arrayType + ">(value)";
            if (c.Counted)
                w.Line("WriteArray(ref operationState, " + array + ");");
            else
            {
                w.Line("global::System.ReadOnlySpan<" + scalar + "> items = " + array + ";");
                w.Line("operationState.Write<" + scalar + ">(items, items.Length);");
                w.Line("operationState.CheckBuffer();");
            }
            w.Close();
        }
        w.Line("else");
        if (outlineFallback)
            w.Indented(() => w.Line("WriteInterface(ref operationState, value);"));
        else
        {
            w.Open();
            c.CountedWrite("value");
            w.Close();
        }
        w.Blank();

        if (c.Counted)
        {
            WriteCounted("WriteKnown", type);
            if (!setInterface)
            {
                if (c.Text)
                {
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("static void WriteArray(scoped ref " + writer + " operationState, string[] items)");
                    w.Open();
                    w.Line("int countPosition = operationState.BeginCollection();");
                    w.Line("operationState.WriteStrings<TLuminProfile>(items);");
                    w.Line("operationState.EndCollection(countPosition, items.Length);");
                    w.Line("operationState.CheckBuffer();");
                    w.Close();
                    w.Blank();
                }
                else
                    WriteCounted("WriteArray", arrayType);
            }
        }
        if (outlineFallback)
            WriteCounted("WriteInterface", c.Sequence);
        if (c.Text)
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static void WriteStringItem(scoped ref " + writer + " operationState, string item)");
            w.Indented(() => w.Line("=> operationState.Write<TLuminProfile>(item);"));
        }

        void WriteCounted(string method, string input)
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static void " + method + "(scoped ref " + writer + " operationState, " + input + " items)");
            w.Open();
            c.CountedWrite("items", outlineText: c.Text);
            w.Close();
            w.Blank();
        }
    }

    private static void ReadArray(EnumerableOwnerContext c)
    {
        var w = c.W;
        c.ReadInterfaceTargets();
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = global::System.Array.Empty<" + c.Scalar + ">();");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("var array = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<" + c.Scalar + ">(length);");
        w.Line("global::System.Span<" + c.Scalar + "> span = array.AsSpan();");
        Block(c);
        w.Line("value = array;");
    }

    private static void ReadList(EnumerableOwnerContext c)
    {
        var w = c.W;
        c.ReadInterfaceTargets();
        w.Line("var list = new global::System.Collections.Generic.List<" + c.Scalar + ">(length);");
        w.Line("if (length != 0)");
        w.Open();
        w.Line("global::System.Span<" + c.Scalar + "> span = global::LuminPack.Code.LuminPackMarshal.GetListSpan(list, length);");
        Block(c);
        w.Close();
        w.Line("value = list;");
    }

    private static void ReadSet(EnumerableOwnerContext c)
    {
        var w = c.W;
        string set = "global::System.Collections.Generic.HashSet<" + c.Scalar + ">";
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = new " + set + "(0);");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("var hashSet = new " + set + "(length);");
        w.Line("var setView = global::LuminPack.Code.LuminPackMarshal.GetHashSetView(hashSet);");
        w.Line("ref var entryRef = ref global::LuminPack.Code.LuminPackMarshal.GetArrayReference(setView._entries);");
        w.Line("for (int index = 0; index < length; index++)");
        w.Open();
        w.Line("ref var entry = ref " + c.Memory + ".Add(ref entryRef, (nint)(uint)index);");
        c.ReadElement("entry.Value");
        w.Close();
        w.Line("global::LuminPack.Code.LuminPackMarshal.RebuildHashSetBuckets(setView, length);");
        w.Line("value = hashSet;");
    }

    private static void Block(EnumerableOwnerContext c) => c.ReadBlock("span");
}
