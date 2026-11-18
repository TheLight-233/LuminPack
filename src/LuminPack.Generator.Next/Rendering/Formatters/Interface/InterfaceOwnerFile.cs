using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owners of the closed sequence interfaces of raw scalars.</summary>
/// <remarks>The write tests the exact runtime type of the common implementations first and counts the enumeration of every other
/// one. The enumerable, collection and read-only collection interfaces read into an array, the list interfaces into a fresh list
/// filled in place, and the set interfaces into a presized hash set whose entries are read directly before its buckets are
/// rebuilt. JSON list-like reads refill exact lists and matching arrays. Set reads build a hash set.</remarks>
internal static class InterfaceOwnerFile
{
    private static readonly EnumerableOwnerSpec ArraySpec = new EnumerableOwnerSpec(c => WriteLines(c, false), ReadArray, "staged.Add(item);",
        jsonCount: false, interfaceListReuse: true, readInlining: "NoInlining", outlineJson: true);

    private static readonly EnumerableOwnerSpec CountedArraySpec = new EnumerableOwnerSpec(c => WriteLines(c, false), ReadArray, "staged.Add(item);",
        interfaceListReuse: true, readInlining: "NoInlining", outlineJson: true);

    private static readonly EnumerableOwnerSpec ListSpec = new EnumerableOwnerSpec(c => WriteLines(c, false), ReadList, "staged.Add(item);",
        interfaceListReuse: true, readInlining: "NoInlining", outlineJson: true);

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
        string sorted = "global::System.Collections.Generic.SortedSet<" + scalar + ">";
        string frozen = "global::System.Collections.Frozen.FrozenSet<" + scalar + ">";
        bool frozenSet = setInterface && c.Supports("FrozenSetInterfaceTraversal");
        const string immutable = "global::System.Collections.Immutable.";
        var immutableSets = new[] { "ImmutableHashSet", "ImmutableSortedSet" };
        bool stringSpan = !setInterface && c.Text && c.Supports("WriteInterfaceStrings");
        var reservedElement = setInterface ? null : c.ReservableListElement();
        bool rawSpan = !setInterface && (!c.Counted || c.NativeNullableElement);
        bool wrappedRaw = rawSpan && c.Supports("InterfaceWrapperStorage");
        bool wrappedText = c.Text && !setInterface && c.Supports("WriteWrappedStrings");
        bool outlineFallback = c.Counted || setInterface || wrappedRaw;
        const string collections = "global::System.Collections.Generic.";
        string[] concreteSequences = c.Sequence.StartsWith(collections + "IEnumerable<", System.StringComparison.Ordinal) ||
            c.Sequence.StartsWith(collections + "IReadOnlyCollection<", System.StringComparison.Ordinal)
            ? new[] { "Queue", "Stack", "LinkedList" }
            : c.Sequence.StartsWith(collections + "ICollection<", System.StringComparison.Ordinal) ? new[] { "LinkedList" } : System.Array.Empty<string>();
        if (rawSpan)
            w.Line("global::System.ReadOnlySpan<" + scalar + "> items;");
        w.Line("if (value.GetType() == typeof(" + type + "))");
        w.Open();
        if (stringSpan)
            w.Line("WriteStringSpan(ref operationState, global::LuminPack.Code.LuminPackMarshal.GetListSpan(" + c.Memory + ".As<" + type + ">(value)));");
        else if (reservedElement != null)
        {
            string list = c.Memory + ".As<" + type + ">(value)";
            w.Line("if (operationState.Owner is not null && " + ParallelDictionaryWrite.FixedUtf16LengthProfile + ")");
            w.Open();
            c.WriteReservedList(list, reservedElement);
            w.Close();
            w.Line("else");
            w.Indented(() => w.Line("WriteKnown(ref operationState, " + list + ");"));
        }
        else if (setInterface && c.Text && c.Supports("StringSetWriter"))
        {
            string set = c.Memory + ".As<" + type + ">(value)";
            w.Line("if (" + ParallelDictionaryWrite.FixedUtf16LengthProfile +
                " && global::LuminPack.Code.LuminPackMarshal.TryWriteStringSet<TLuminProfile>(ref operationState, " + set + "))");
            w.Indented(() => w.Line("goto operationComplete_RootWrite;"));
            w.Line("WriteKnown(ref operationState, " + set + ");");
        }
        else if (rawSpan)
            w.Line("items = global::LuminPack.Code.LuminPackMarshal.GetListSpan(" + c.Memory + ".As<" + type + ">(value));");
        else if (setInterface && c.Supports("WriteNullableHashSetRaw") && c.NativeNullableElement)
            w.Line("global::LuminPack.Code.LuminPackMarshal.WriteNullableHashSetRaw(ref operationState, " + c.Memory + ".As<" + type + ">(value));");
        else if (!c.Counted)
            w.Line("global::LuminPack.Code.LuminPackMarshal.WriteHashSetRaw(ref operationState, " + c.Memory + ".As<" + type + ">(value));");
        else
            w.Line("WriteKnown(ref operationState, " + c.Memory + ".As<" + type + ">(value));");
        w.Close();
        if (!setInterface)
        {
            w.Line("else if (value.GetType() == typeof(" + arrayType + "))");
            w.Open();
            string array = c.Memory + ".As<" + arrayType + ">(value)";
            if (stringSpan)
                w.Line("WriteStringSpan(ref operationState, new global::System.ReadOnlySpan<string>(" + array + "));");
            else if (rawSpan)
                w.Line("items = " + array + ";");
            else
                w.Line("WriteArray(ref operationState, " + array + ");");
            w.Close();
        }
        foreach (string name in concreteSequences)
        {
            string sequenceType = ConcreteSequence(name);
            w.Line("else if (value.GetType() == typeof(" + sequenceType + "))");
            w.Open();
            // The counted writer starts the collection before it captures the public struct enumerator.
            w.Line("Write" + name + "(ref operationState, " + c.Memory + ".As<" + sequenceType + ">(value));");
            if (rawSpan)
                w.Line("goto operationComplete_RootWrite;");
            w.Close();
        }
        if (setInterface)
        {
            w.Line("else if (value.GetType() == typeof(" + sorted + "))");
            w.Indented(() => w.Line("WriteSorted(ref operationState, " + c.Memory + ".As<" + sorted + ">(value));"));
            if (frozenSet)
            {
                w.Line("#if NET8_0_OR_GREATER");
                w.Line("else if (value is " + frozen + " frozen)");
                w.Indented(() => w.Line("WriteFrozen(ref operationState, frozen);"));
                w.Line("#endif");
            }
            foreach (string family in immutableSets)
            {
                if (!c.Supports(family + "InterfaceTraversal"))
                    continue;
                string profile = c.Text ? ParallelDictionaryWrite.FixedUtf16LengthProfile + " && " : string.Empty;
                w.Line("else if (" + profile + "value is " + immutable + family + "<" + scalar + "> " + family + "Items)");
                w.Indented(() => w.Line("Write" + family + "(ref operationState, " + family + "Items);"));
            }
        }
        w.Line("else");
        if (outlineFallback)
        {
            w.Open();
            w.Line("WriteInterface(ref operationState, value);");
            if (rawSpan)
                w.Line("goto operationComplete_RootWrite;");
            w.Close();
        }
        else
        {
            w.Open();
            c.CountedWrite("value");
            w.Line("goto operationComplete_RootWrite;");
            w.Close();
        }
        if (c.NativeNullableElement && rawSpan)
            c.WriteNativeNullableSpan("items");
        else if (rawSpan)
        {
            w.Line("operationState.Write<" + scalar + ">(items, items.Length);");
            w.Line("operationState.CheckBuffer();");
        }
        w.Blank();

        if (stringSpan)
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static void WriteStringSpan(scoped ref " + writer + " operationState, scoped global::System.ReadOnlySpan<string> items)");
            w.Open();
            w.Line("operationState.EnsureAdditionalCapacity(sizeof(int));");
            w.Line("operationState.WriteCollectionHeader(items.Length);");
            w.Line("operationState.WriteStrings<TLuminProfile>(items);");
            w.Line("operationState.CheckBuffer();");
            w.Close();
            w.Blank();
        }
        else if (c.Counted && !rawSpan && !(setInterface && c.NativeNullableElement && c.Supports("WriteNullableHashSetRaw")))
        {
            WriteCounted("WriteKnown", type);
            if (!setInterface)
                WriteCounted("WriteArray", arrayType);
        }
        foreach (string name in concreteSequences)
            WriteCounted("Write" + name, ConcreteSequence(name));
        if (outlineFallback)
            WriteCounted("WriteInterface", c.Sequence);
        if (setInterface)
        {
            WriteCounted("WriteSorted", sorted, "Sorted");
            foreach (string family in immutableSets)
            {
                if (c.Supports(family + "InterfaceTraversal"))
                    WriteCounted("Write" + family, immutable + family + "<" + scalar + ">", family);
            }
        }
        if (frozenSet)
        {
            w.Line("#if NET8_0_OR_GREATER");
            WriteCounted("WriteFrozen", frozen, "Frozen");
            w.Line("#endif");
        }
        if (c.Text && !c.Forwarded)
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static void WriteStringItem(scoped ref " + writer + " operationState, string item)");
            w.Indented(() => w.Line("=> operationState.Write<TLuminProfile>(item);"));
        }

        string ConcreteSequence(string name) => collections + name + "<" + scalar + ">";

        void WriteCounted(string method, string input, string? setFamily = null)
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static void " + method + "(scoped ref " + writer + " operationState, " + input + " items)");
            w.Open();
            if (method == "WriteInterface")
            {
                if (wrappedText)
                {
                    w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteWrappedStrings<TLuminProfile>(ref operationState, items))");
                    w.Indented(() => w.Line("return;"));
                }
                if (wrappedRaw)
                    WriteWrappedRaw();
            }
            if (setFamily != null)
                WriteSetRun(setFamily);
            c.CountedWrite("items", outlineText: c.Text && (!setInterface || method == "WriteInterface" ||
                setFamily is "ImmutableHashSet" or "ImmutableSortedSet"));
            w.Close();
            w.Blank();
        }

        void WriteWrappedRaw()
        {
            w.Line("var storage = global::LuminPack.Code.LuminPackCollectionAccess.GetSequenceStorage(items);");
            w.Line("if (storage is not null)");
            w.Open();
            w.Line("global::System.ReadOnlySpan<" + scalar + "> backing = storage is " + type + " list");
            w.Line("    ? global::LuminPack.Code.LuminPackMarshal.GetListSpan(list) : (" + arrayType + ")storage;");
            if (c.NativeNullableElement)
                c.WriteNativeNullableSpan("backing");
            else
            {
                w.Line("operationState.Write<" + scalar + ">(backing, backing.Length);");
                w.Line("operationState.CheckBuffer();");
            }
            w.Line("return;");
            w.Close();
        }

        void WriteSetRun(string family)
        {
            bool isFrozen = family == "Frozen";
            if (c.RawTreeElements && c.Supports("WriteSetInterfaceRaw" + family))
            {
                w.Line("if (items.Count >= " + (isFrozen ? "4" : "16") + " && sizeof(int) + (long)items.Count * " +
                    c.Memory + ".SizeOf<" + scalar + ">() <= operationState.RecordRoom())");
                w.Open();
                if (isFrozen)
                {
                    w.Line("var frozenItems = items.Items.AsSpan();");
                    w.Line("if (!global::System.MemoryExtensions.Overlaps(global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(frozenItems), operationState.Buffer))");
                    w.Open();
                    w.Line("operationState.Write<" + scalar + ">(frozenItems, frozenItems.Length);");
                    w.Line("operationState.CheckBuffer();");
                    w.Line("return;");
                    w.Close();
                }
                else
                {
                    w.Line("global::LuminPack.Utility.LuminTreeStorage.WriteRaw(ref operationState, items);");
                    w.Line("return;");
                }
                w.Close();
            }
            if (!c.Text || !c.Supports("WriteSetInterfaceStrings" + family))
                return;

            w.Line("if (items.Count >= " + (isFrozen ? "1" : "16") + " && " + ParallelDictionaryWrite.FixedUtf16LengthProfile + ")");
            w.Open();
            w.Line("int capturedHeader = operationState.BeginCollection();");
            if (isFrozen)
                w.Line("operationState.WriteStrings<TLuminProfile>(items.Items.AsSpan());");
            else
                ImmutableTreeTraversal.Lease(w, "items", "string", () =>
                    w.Line("operationState.WriteStrings<TLuminProfile>(" + ImmutableTreeTraversal.CapturedSpan + ");"));
            w.Line("operationState.EndCollection(capturedHeader, items.Count);");
            w.Line("operationState.CheckBuffer();");
            w.Line("return;");
            w.Close();
        }
    }

    /// <summary>Reads array.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void ReadArray(EnumerableOwnerContext c)
    {
        if (c.ReadObjectInterfaceItems())
            return;
        var w = c.W;
        c.ReadInterfaceTargets();
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = global::System.Array.Empty<" + c.Scalar + ">();");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("var array = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<" + c.Scalar + ">(length);");
        w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(array);");
        w.Line("var span = global::System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref first, length);");
        Block(c);
        w.Line("value = array;");
    }

    /// <summary>Reads list.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void ReadList(EnumerableOwnerContext c)
    {
        if (c.ReadObjectInterfaceItems(freshList: true))
            return;
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

    /// <summary>Reads set.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
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
        w.Line(c.HashSetRefill
            ? "var hashSet = global::LuminPack.Code.LuminPackCollectionAccess.PrepareHashSetForRefill(value as " + set + ", length);"
            : "var hashSet = new " + set + "(length);");
        if (c.NativeNullableElement && c.Supports("ReadNullableHashSetRaw") || !c.Counted && c.Supports("ReadRawHashSet"))
        {
            string method = c.NativeNullableElement ? "ReadNullableHashSetRaw" : "ReadRawHashSet";
            w.Line("global::LuminPack.Code.LuminPackMarshal." + method + "(ref operationState, hashSet, length);");
            w.Line("value = hashSet;");
            return;
        }
        w.Line("var setView = global::LuminPack.Code.LuminPackMarshal.GetHashSetView(hashSet);");
        w.Line("ref var entryRef = ref global::LuminPack.Code.LuminPackMarshal.GetArrayReference(setView._entries);");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("ref var entry = ref " + c.Memory + ".Add(ref entryRef, (nint)(uint)index);");
        c.ReadElement("entry.Value");
        w.Close();
        w.Line("while (++index < length);");
        w.Line("global::LuminPack.Code.LuminPackMarshal.RebuildHashSetBuckets(setView, length);");
        w.Line("value = hashSet;");
    }

    /// <summary>Handles block for <c>InterfaceOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Block(EnumerableOwnerContext c) => c.ReadBlock("span");
}
