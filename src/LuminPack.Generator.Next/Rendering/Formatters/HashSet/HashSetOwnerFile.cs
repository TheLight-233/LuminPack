using System;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of one closed <c>HashSet&lt;T&gt;</c> of raw scalars.</summary>
/// <remarks>The members mirror the legacy hash-set operations. The binary write copies the live entries through the shared raw
/// kernel. The binary read creates a presized set and rebuilds its buckets in entry order. Fixed callback-free elements
/// can build the buckets while loading the entries. An empty payload reuses and clears a non-null destination.</remarks>
internal static class HashSetOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(Write, Read, "value.Add(item);",
        jsonBefore: PrepareJson, readInlining: "NoInlining", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec, nonEmptyRawHeaderBytes: sizeof(int), directRawWrite: WriteDirect);

    /// <summary>Uses a bounded structural hint to size a fresh set before reading its first element.</summary>
    /// <param name="c">The JSON owner being emitted.</param>
    private static void PrepareJson(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        if (c.Supports("HashSetJsonCapacity"))
        {
            const string options = "global::LuminPack.Option.";
            w.Open();
            w.Line("int capacity = typeof(TLuminProfile) == typeof(" + options + "LuminPackUtf16LengthOptions) ||");
            w.Line("    typeof(TLuminProfile) == typeof(" + options + "LuminPackUtf8LengthOptions) ||");
            w.Line("    typeof(TLuminProfile) == typeof(" + options + "LuminPackDefaultOptions)");
            w.Line("    ? reader.GetArrayCapacityHint<TLuminProfile>(128) : 0;");
            w.Line("value = new " + c.Sequence + "((uint)capacity <= 4096u ? capacity : 0);");
            w.Close();
        }
        else
            w.Indented(() => w.Line("value = new " + c.Sequence + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
    }

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
        if (c.NativeNullableElement && c.Supports("WriteNullableHashSetRaw"))
        {
            c.W.Line("global::LuminPack.Code.LuminPackMarshal.WriteNullableHashSetRaw(ref operationState, value);");
            return;
        }
        // The raw kernel needs unmanaged elements; strings are counted while the set is enumerated.
        if (c.Counted)
        {
            bool stringStorage = c.Text && c.Supports("StringSetWriter");
            bool entryStorage = c.Forwarded && c.Supports("HashSetTraversal");
            if (stringStorage)
            {
                c.W.Line("if (" + ParallelDictionaryWrite.FixedUtf16LengthProfile +
                    " && global::LuminPack.Code.LuminPackMarshal.TryWriteStringSet<TLuminProfile>(ref operationState, value))");
                c.W.Indented(() => c.W.Line("goto operationComplete_RootWrite;"));
            }
            if (entryStorage)
                WriteEntries(c);
            if (entryStorage || stringStorage)
                c.CountedWriteFallback("value");
            else
                c.CountedWrite("value");
            return;
        }
        WriteRaw(c, "operationState", "value");
    }

    /// <summary>Writes checked live entries, retaining the enumerator's behavior across callbacks.</summary>
    /// <param name="c">The element formatter and output context.</param>
    private static void WriteEntries(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (global::LuminPack.Code.LuminPackMarshal.TryGetHashSetTraversal(value, out var view))");
        w.Open();
        w.Line("int version = view._version;");
        w.Line("int expected = value.Count;");
        w.Line("operationState.EnsureAdditionalCapacity(sizeof(int));");
        w.Line("int position = operationState.Position;");
        w.Line("operationState.WriteCollectionHeader(expected);");
        w.Line("int written = 0;");
        WriteLiveEntries(w, () =>
        {
            c.WriteElement("item");
            w.Line("written++;");
        });
        // Remove and Clear can succeed without invalidating a modern HashSet enumerator.
        w.Line("if (written != expected) operationState.EndCollection(position, written);");
        w.Line("operationState.CheckBuffer();");
        w.Line("goto operationComplete_RootWrite;");
        w.Close();
    }

    /// <summary>Writes each live item in the checked layout view's current enumeration order.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="writeItem">Emits the original element operation for the captured item.</param>
    internal static void WriteLiveEntries(CodeWriter w, Action writeItem)
    {
        w.Line("for (int index = 0; ; index++)");
        w.Open();
        w.Line("if (version != view._version)");
        w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();"));
        w.Line("if (index >= view._count) break;");
        w.Line("ref var entry = ref view._entries[index];");
        w.Line("if (entry.Next < -1) continue;");
        w.Line("var item = entry.Value;");
        writeItem();
        w.Close();
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
        bool nativeBlock = c.NativeNullableElement && c.Supports("ReadNullableHashSetRaw");
        if (nativeBlock)
            c.ValidateNativeNullableElements();
        w.Line(c.HashSetRefill
            ? "value = global::LuminPack.Code.LuminPackCollectionAccess.PrepareHashSetForRefill(value, length);"
            : "value = new " + c.Sequence + "(length);");
        w.Blank();
        if (nativeBlock || !c.Counted && c.Supports("ReadRawHashSet"))
        {
            string method = c.NativeNullableElement ? "ReadNullableHashSetRaw" : "ReadRawHashSet";
            w.Line("global::LuminPack.Code.LuminPackMarshal." + method + "(ref operationState, value, length);");
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
