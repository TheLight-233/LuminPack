using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>SortedList&lt;TKey, TValue&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The write stores the list's own sorted key and value arrays in place when the runtime storage is recognized, and
/// copies the public key and value lists into pooled arrays otherwise. A read into a null destination of more than one entry
/// stages the pairs in pooled arrays and fills the list directly when the keys are strictly ascending, sorts them first when
/// they are not, and falls back to the bulk construction that rejects duplicates; every other read clears or creates the list and
/// adds each pair.</remarks>
internal static partial class SortedListOwnerFile
{
    private const string ArrayReference = "global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference";
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(Write, c => Read(c, seedKeys: true),
        "value.Add(key!, itemValue);", outlineJson: true, capturesJsonPairs: true);
    private static readonly PairedDictionarySpec InputSpec = new PairedDictionarySpec(Write, c => Read(c, seedKeys: false),
        "value.Add(key!, itemValue);", outlineJson: true, capturesJsonPairs: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Renders the original parser for the enclosed input policy projection.</summary>
    /// <param name="set">The shared publication facts.</param>
    /// <param name="fact">The closed sorted list owner.</param>
    /// <returns>The stock binary source for a checked copy.</returns>
    internal static string RenderInputRead(SharedFormatterSet set, DictionaryOwnerFact fact) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, GeneratedOperations.BinaryRead, InputSpec);

    /// <summary>Handles write for <c>SortedListOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Write(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("int count = value.Count;");
        w.Line("int countPosition = operationState.BeginCollection();");
        w.Line("if (count != 0)");
        w.Open();
        w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetSortedListStorage(value, out var storedKeys, out var storedValues))");
        w.Open();
        w.Line("if ((uint)count > (uint)storedKeys!.Length || (uint)count > (uint)storedValues!.Length)");
        w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();"));
        bool interleave = c.RawPairStorage && c.Supports("InterleavedRawPairs");
        if (interleave)
        {
            w.Line("if (!global::LuminPack.Utility.LuminFrozenConstruction.TryWriteInterleavedPairs(ref operationState, storedKeys, storedValues, count))");
            w.Open();
        }
        w.Line("ref var firstKey = ref " + ArrayReference + "(storedKeys);");
        w.Line("ref var firstValue = ref " + ArrayReference + "(storedValues);");
        bool fused = c.BeginRawRun(c.Memory + ".Add(ref firstKey, (nint)(uint)index)",
            c.Memory + ".Add(ref firstValue, (nint)(uint)index)");
        ParallelDictionaryWrite.Emit(c, "count", allowBoundedKey: true);
        if (fused)
            w.Close();
        if (interleave)
            w.Close();
        w.Close();
        w.Line("else");
        w.Open();
        // Unrecognized storage copies through the public key and value lists.
        w.Line("var keys = global::System.Buffers.ArrayPool<" + c.Key + ">.Shared.Rent(count);");
        w.Line(c.Value + "[]? values = null;");
        w.Line("try");
        w.Open();
        w.Line("values = global::System.Buffers.ArrayPool<" + c.Value + ">.Shared.Rent(count);");
        w.Line("value.Keys.CopyTo(keys, 0);");
        w.Line("value.Values.CopyTo(values, 0);");
        w.Line("ref var firstKey = ref " + ArrayReference + "(keys);");
        w.Line("ref var firstValue = ref " + ArrayReference + "(values);");
        ParallelDictionaryWrite.Emit(c, "count", allowMixed: false);
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + c.Key + ">.Shared.Return(keys,");
        w.Indented(() => w.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + c.Key + ">());"));
        w.Line("if (values != null)");
        w.Indented(() =>
        {
            w.Line("global::System.Buffers.ArrayPool<" + c.Value + ">.Shared.Return(values,");
            w.Indented(() => w.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + c.Value + ">());"));
        });
        w.Close();
        w.Close();
        w.Close();
        w.Line("operationState.EndCollection(countPosition, count);");
        w.Line("operationState.CheckBuffer();");
    }

    /// <summary>Handles read for <c>SortedListOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    /// <param name="seedKeys">Whether the ordinary reader can use the owning scalar helper.</param>
    private static void Read(PairedDictionaryContext c, bool seedKeys)
    {
        var w = c.W;
        string list = c.Dictionary;
        if (seedKeys && !c.ReferenceValue && c.Supports("SeededSortedListKeys"))
        {
            w.Line("if (value is not null && global::LuminPack.Code.LuminPackCollectionAccess.TryReadSeededSortedListKeys<" +
                c.Value + ", TLuminProfile>(ref operationState, value, length))");
            w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        }
        if (c.ReferenceValue && c.Supports("MatchSortedListKey"))
            MatchingPairs(c);
        w.Line("if (value is null && length > 1)");
        w.Open();
        w.Line("var keys = global::System.Buffers.ArrayPool<" + c.Key + ">.Shared.Rent(length);");
        w.Line(c.Value + "[]? values = null;");
        w.Line("try");
        w.Open();
        w.Line("values = global::System.Buffers.ArrayPool<" + c.Value + ">.Shared.Rent(length);");
        bool split = seedKeys && c.RawPairReadStorage && c.Supports("ReadInterleavedRawPairs");
        if (split)
        {
            w.Line("if (!global::LuminPack.Utility.LuminFrozenConstruction.TryReadInterleavedPairs(ref operationState, keys, values, length))");
            w.Open();
        }
        w.Line("ref var firstKey = ref " + ArrayReference + "(keys);");
        w.Line("ref var firstValue = ref " + ArrayReference + "(values);");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        if (c.Key == "string" || c.Key.StartsWith("global::System.Nullable<", System.StringComparison.Ordinal))
        {
            w.Line("if (k is null)");
            w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(\"key\");"));
        }
        w.Line(c.Memory + ".Add(ref firstKey, (nint)(uint)index) = k!;");
        w.Line(c.Memory + ".Add(ref firstValue, (nint)(uint)index) = v;");
        w.Close();
        w.Line("while (++index < length);");
        if (split)
            w.Close();
        // Keys already in order are stored directly; other input is sorted first, and duplicates reach the bulk construction.
        w.Line("value = new " + list + "(length);");
        w.Line("if (!global::LuminPack.Code.LuminPackCollectionAccess.TryFillSortedList(value, keys, values, length))");
        w.Open();
        w.Line("global::System.Array.Sort(keys, values, 0, length);");
        w.Line("if (!global::LuminPack.Code.LuminPackCollectionAccess.TryFillSortedList(value, keys, values, length))");
        w.Indented(() => w.Line("value = new " + list + "(new global::LuminPack.Code.LuminSortedListSource<" + c.Key + ", " + c.Value + ">(keys, values, length));"));
        w.Close();
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + c.Key + ">.Shared.Return(keys,");
        w.Indented(() => w.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + c.Key + ">());"));
        w.Line("if (values is not null)");
        w.Indented(() =>
        {
            w.Line("global::System.Buffers.ArrayPool<" + c.Value + ">.Shared.Return(values,");
            w.Indented(() => w.Line("global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + c.Value + ">());"));
        });
        w.Close();
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + list + "(length);"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Line("if (length == 0) goto operationComplete_RootRead;");
        bool append = c.Supports("AppendSortedList");
        if (append)
        {
            w.Line("var appendDestination = value;");
            w.Line("var appendCulture = global::System.Globalization.CultureInfo.CurrentCulture;");
            w.Line("bool canAppendOrdered = global::LuminPack.Code.LuminPackCollectionAccess.CanAppendSortedListOrdered(value);");
        }
        w.Line("int remaining = length;");
        w.Line("do");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line(append ? "if (!canAppendOrdered || !global::System.Object.ReferenceEquals(value, appendDestination) || !global::System.Object.ReferenceEquals(global::System.Globalization.CultureInfo.CurrentCulture, appendCulture) || !global::LuminPack.Code.LuminPackCollectionAccess.TryAppendSortedListOrdered(value, k!, v, true)) value.Add(k!, v);" : "value.Add(k!, v);");
        w.Close();
        w.Line("while (--remaining != 0);");
    }
}
