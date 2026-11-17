using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owners of the closed dictionary interfaces of raw scalar or string entries.</summary>
/// <remarks>The write counts the enumeration of the value. The <c>IDictionary</c> and <c>IReadOnlyDictionary</c> reads forward to
/// the concrete dictionary owner, which an exact non-null destination of that type reuses. The immutable interface reads
/// the empty instance for an empty payload and otherwise adds each entry to a builder.</remarks>
internal static class DictionaryInterfaceOwnerFile
{
    private static readonly PairedDictionarySpec MutableSpec = new PairedDictionarySpec(WriteMutable, ReadMutable,
        "staged.Add(key!, itemValue);",
        c => c.W.Line("var staged = new global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">();"),
        c => c.W.Line("value = staged;"), readsHeader: true, outlineJson: true);

    private static readonly PairedDictionarySpec ImmutableSpec = new PairedDictionarySpec(WriteImmutable, ReadImmutable,
        "staged[key!] = itemValue;",
        c => ImmutableBuilderLifetime.Begin(c.W, "Dictionary", c.Key + ", " + c.Value, c.Supports("ImmutableBuilder"), "staged"),
        AfterImmutableJson, outlineJson: true);

    /// <summary>Renders an <c>IDictionary</c> or <c>IReadOnlyDictionary</c> owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderMutable(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, MutableSpec);

    /// <summary>Renders an <c>IImmutableDictionary</c> owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderImmutable(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, ImmutableSpec);

    /// <summary>Publishes the completed JSON dictionary and releases the optional builder lease.</summary>
    /// <param name="c">The dictionary owner's read context.</param>
    private static void AfterImmutableJson(PairedDictionaryContext c)
    {
        c.W.Line("value = staged.ToImmutable();");
        ImmutableBuilderLifetime.End(c.W, "Dictionary", c.Supports("ImmutableBuilder"), "staged");
    }

    /// <summary>Writes immutable.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void WriteImmutable(PairedDictionaryContext c)
    {
        Write("ImmutableDictionary", "CopyImmutableHashTree");
        Write("ImmutableSortedDictionary", "CopyImmutableSortedTree");
        c.W.Open();
        c.CountedWrite();
        c.W.Close();

        void Write(string family, string capability)
        {
            if (!c.Supports(capability))
                return;
            ImmutableTreeTraversal.Select(c.W, c.Dictionary, c.Key + ", " + c.Value, family, source =>
            {
                c.TreeWrite(batchRaw: true, source: source);
                c.W.Line("goto operationComplete_RootWrite;");
            });
        }
    }

    /// <summary>Writes through proven dense storage, the framework value enumerator or the interface enumerator.</summary>
    /// <param name="c">The dictionary owner's write context.</param>
    private static void WriteMutable(PairedDictionaryContext c)
    {
        var w = c.W;
        string concrete = "global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">";
        string writer = w.A("LuminPackWriter");
        bool rawKernel = c.RawPairs && c.Supports("BinaryRawPairs");
        bool textKernel = c.UsesRawOrStringEntries && (c.Key == "string" || c.Value == "string") && c.Supports("BinaryTextPair");
        bool textPair = c.Key == "string" && c.Value == "string";
        bool denseText = textKernel && textPair && c.Supports("DenseDictionaryView");
        bool denseCompound = c.Key == "string" && c.Supports("DenseDictionaryView") && c.DenseCompoundValue;
        bool dense = denseText || denseCompound;
        const string runtimeWriter = "global::LuminPack.Core.LuminPackWriter";
        const string marshal = "global::LuminPack.Code.LuminPackMarshal";
        const string textPairFits = "operationState.RecordRoom() >= 2 * sizeof(int) + ((long)(key?.Length ?? 0) + (itemValue?.Length ?? 0)) * sizeof(char)";
        string keyWidth = c.Memory + ".SizeOf<" + c.Key + ">()";
        string width = keyWidth + " + " + c.Memory + ".SizeOf<" + c.Value + ">()";
        string rawWidth = c.Memory + ".SizeOf<" + (c.Key == "string" ? c.Value : c.Key) + ">()";
        if (dense)
        {
            w.Line("var dictionary = value.GetType() == typeof(" + concrete + ") ? " + c.Memory + ".As<" + concrete + ">(value) : null;");
            string profile = denseCompound ? ParallelDictionaryWrite.FixedUtf16LengthProfile + " && " : string.Empty;
            w.Line("if (" + profile + "dictionary is not null && TryWriteDense(ref operationState, dictionary))");
            w.Indented(() => w.Line("goto operationComplete_RootWrite;"));
        }
        w.Line("int countPosition = operationState.BeginCollection();");
        w.Line(dense ? "int count = dictionary is not null" : "int count = value.GetType() == typeof(" + concrete + ")");
        w.Indented(() =>
        {
            w.Line("? WriteKnown(ref operationState, " + (dense ? "dictionary" : c.Memory + ".As<" + concrete + ">(value)") + ")");
            w.Line(": WriteInterface(ref operationState, value);");
        });
        w.Line("operationState.EndCollection(countPosition, count);");
        w.Line("operationState.CheckBuffer();");
        w.Blank();
        if (dense)
            EmitDense();
        EmitLoop("WriteKnown", concrete);
        EmitLoop("WriteInterface", c.Dictionary);
        if (rawKernel || textKernel)
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static void WritePairSlow(scoped ref " + writer + " operationState, " + c.Key + " key, " + c.Value + " itemValue)");
            w.Open();
            c.WriteMember(c.Key, "key");
            c.WriteMember(c.Value, "itemValue");
            w.Close();
        }

        void EmitDense()
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static bool TryWriteDense(scoped ref " + writer + " operationState, " + concrete + " items)");
            w.Open();
            w.Line("if (" + c.Memory + ".SizeOf<global::System.IntPtr>() != 8 ||");
            w.Line("    !" + marshal + ".TryGetDictionaryView(items, out var view))");
            w.Indented(() => w.Line("return false;"));
            w.Line("int count = items.Count;");
            w.Line("if (view._count != count || view._freeCount != 0)");
            w.Indented(() => w.Line("return false;"));
            w.Line("var entries = view._entries;");
            w.Line("if (count != 0 && (entries is null || (uint)count > (uint)entries.Length))");
            w.Indented(() => w.Line("return false;"));
            if (denseCompound)
                w.Line("int header = operationState.BeginCollection();");
            else
            {
                w.Line("operationState.EnsureAdditionalCapacity(sizeof(int));");
                w.Line("operationState.WriteCollectionHeader(count);");
            }
            w.Line("if (count != 0)");
            w.Open();
            w.Line("int version = view._version;");
            w.Line("ref var entry = ref " + marshal + ".GetNotNullArrayReference(entries!);");
            w.Line("for (int remaining = count; remaining != 0; entry = ref " + c.Memory + ".Add(ref entry, 1), remaining--)");
            w.Open();
            w.Line("var key = entry.Key;");
            if (denseCompound)
            {
                w.Line("var itemValue = entry.Value;");
                c.WriteMember(c.Key, "key");
                c.WriteMember(c.Value, "itemValue");
            }
            else
            {
                w.Line("if (" + ParallelDictionaryWrite.FixedUtf16LengthProfile + ")");
                w.Open();
                w.Line("var itemValue = entry.Value;");
                w.Line("if (" + textPairFits + ")");
                w.Open();
                EmitTextPairStores();
                w.Line("continue;");
                w.Close();
                w.Close();
                c.WriteMember(c.Key, "key");
                w.Line("var remainingValue = entry.Value;");
                c.WriteMember(c.Value, "remainingValue");
            }
            w.Close();
            w.Line("if (view._version != version || view._count != count || view._freeCount != 0)");
            w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();"));
            w.Close();
            if (denseCompound)
                w.Line("operationState.EndCollection(header, count);");
            w.Line("operationState.CheckBuffer();");
            w.Line("return true;");
            w.Close();
            w.Blank();
        }

        void EmitTextPairStores()
        {
            w.Line("ref byte record = ref operationState.Cursor;");
            w.Line("int written = " + runtimeWriter + ".WriteReservedUtf16Record(ref record, key);");
            w.Line("written += " + runtimeWriter + ".WriteReservedUtf16Record(ref " + c.Memory + ".Add(ref record, (nint)(uint)written), itemValue);");
            w.Line("operationState.CompleteRun(written);");
        }

        void EmitLoop(string method, string input)
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static int " + method + "(scoped ref " + writer + " operationState, " + input + " items)");
            w.Open();
            if (rawKernel && method == "WriteKnown" && c.Supports("BoundedDenseRawPairs"))
            {
                w.Line("if (" + marshal + ".TryWriteBoundedDenseRawPairs(ref operationState, items, out int denseCount))");
                w.Indented(() => w.Line("return denseCount;"));
            }
            w.Line("int count = 0;");
            w.Line("foreach (var item in items)");
            w.Open();
            w.Line("count++;");
            w.Line("var key = item.Key;");
            if (!rawKernel && !textKernel)
                c.WriteMember(c.Key, "key");
            w.Line("var itemValue = item.Value;");
            if (rawKernel)
            {
                w.Line("if (operationState.RecordRoom() >= " + width + ")");
                w.Open();
                w.Line("ref byte record = ref operationState.Cursor;");
                w.Line(runtimeWriter + ".Write(ref record, in key);");
                w.Line(runtimeWriter + ".Write(ref " + c.Memory + ".Add(ref record, " + keyWidth + "), in itemValue);");
                w.Line("operationState.CompleteRun(" + width + ");");
                w.Close();
                w.Line("else");
                w.Indented(() => w.Line("WritePairSlow(ref operationState, key, itemValue);"));
            }
            else if (textKernel && method == "WriteKnown")
            {
                string text = c.Key == "string" ? "key" : "itemValue";
                w.Line("if (" + ParallelDictionaryWrite.FixedUtf16LengthProfile + " &&");
                if (textPair)
                    w.Line("    " + textPairFits + ")");
                else
                    w.Line("    (long)operationState.RecordRoom() - " + rawWidth + " >= " + runtimeWriter + ".Utf16RecordBound(" + text + "))");
                w.Open();
                if (textPair)
                    EmitTextPairStores();
                else if (c.Key == "string")
                {
                    w.Line("ref byte record = ref operationState.Cursor;");
                    w.Line("int written = " + runtimeWriter + ".WriteReservedUtf16Record(ref record, key);");
                    w.Line(runtimeWriter + ".Write(ref " + c.Memory + ".Add(ref record, (nint)(uint)written), in itemValue);");
                    w.Line("operationState.CompleteRun(written + " + rawWidth + ");");
                }
                else
                {
                    w.Line("ref byte record = ref operationState.Cursor;");
                    w.Line(runtimeWriter + ".Write(ref record, in key);");
                    w.Line("int written = " + runtimeWriter + ".WriteReservedUtf16Record(ref " + c.Memory + ".Add(ref record, " + rawWidth + "), itemValue);");
                    w.Line("operationState.CompleteRun(written + " + rawWidth + ");");
                }
                w.Close();
                w.Line("else");
                w.Indented(() => w.Line("WritePairSlow(ref operationState, key, itemValue);"));
            }
            else if (textKernel)
                w.Line("WritePairSlow(ref operationState, key, itemValue);");
            else
                c.WriteMember(c.Value, "itemValue");
            w.Close();
            w.Line("return count;");
            w.Close();
            w.Blank();
        }
    }

    /// <summary>Reads mutable.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void ReadMutable(PairedDictionaryContext c)
    {
        var w = c.W;
        string concrete = "global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">";
        string owner = "global::LuminPack.Binary_" + c.Extension + "_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(concrete));
        // An exact dictionary destination is reused by the concrete read.
        w.Line(concrete + "? dictionary = value is not null && value.GetType() == typeof(" + concrete + ") ? " + c.Memory + ".As<" + concrete + ">(value) : null;");
        w.Line(owner + ".Read<TLuminProfile>(ref operationState, ref dictionary);");
        w.Line("value = dictionary;");
    }

    /// <summary>Reads immutable.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void ReadImmutable(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = global::System.Collections.Immutable.ImmutableDictionary<" + c.Key + ", " + c.Value + ">.Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        bool pooled = c.Supports("ImmutableBuilder");
        ImmutableBuilderLifetime.Begin(w, "Dictionary", c.Key + ", " + c.Value, pooled, "builder");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line("builder.Add(k!, v);");
        w.Close();
        w.Line("while (++index < length);");
        w.Line("value = builder.ToImmutable();");
        ImmutableBuilderLifetime.End(w, "Dictionary", pooled, "builder");
    }
}
