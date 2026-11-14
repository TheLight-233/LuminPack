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
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(Write, Read, "value.Add(key!, itemValue);", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Write(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("int count = value.Count;");
        w.Line("int countPosition = operationState.BeginCollection();");
        w.Line("if (count != 0)");
        w.Open();
        w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetSortedListStorage(value, out var storedKeys, out var storedValues))");
        w.Open();
        w.Line("var keySpan = new global::System.ReadOnlySpan<" + c.Key + ">(storedKeys, 0, count);");
        w.Line("var valueSpan = new global::System.ReadOnlySpan<" + c.Value + ">(storedValues, 0, count);");
        bool fused = c.BeginRawRun("keySpan[index]", "valueSpan[index]");
        w.Line("for (int index = 0; index < keySpan.Length; index++)");
        w.Open();
        w.Line(c.Key + " key = keySpan[index];");
        c.WriteMember(c.Key, "key");
        w.Line(c.Value + " itemValue = valueSpan[index];");
        c.WriteMember(c.Value, "itemValue");
        w.Close();
        if (fused)
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
        w.Line("for (int index = 0; index < count; index++)");
        w.Open();
        w.Line(c.Key + " key = keys[index];");
        c.WriteMember(c.Key, "key");
        w.Line(c.Value + " itemValue = values[index];");
        c.WriteMember(c.Value, "itemValue");
        w.Close();
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

    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        string list = c.Dictionary;
        if (c.ReferenceValue && c.Supports("MatchSortedListKey"))
            MatchingPairs(c);
        w.Line("if (value is null && length > 1)");
        w.Open();
        w.Line("var keys = global::System.Buffers.ArrayPool<" + c.Key + ">.Shared.Rent(length);");
        w.Line(c.Value + "[]? values = null;");
        w.Line("try");
        w.Open();
        w.Line("values = global::System.Buffers.ArrayPool<" + c.Value + ">.Shared.Rent(length);");
        w.Line("for (int index = 0; index < length; index++)");
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
        w.Line("keys[index] = k!;");
        w.Line("values[index] = v;");
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
        bool append = c.Supports("AppendSortedList");
        if (append)
        {
            w.Line("var appendDestination = value;");
            w.Line("var appendCulture = global::System.Globalization.CultureInfo.CurrentCulture;");
            w.Line("bool canAppendOrdered = global::LuminPack.Code.LuminPackCollectionAccess.CanAppendSortedListOrdered(value);");
        }
        w.Line("for (int index = 0; index < length; index++)");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line(append ? "if (!canAppendOrdered || !global::System.Object.ReferenceEquals(value, appendDestination) || !global::System.Object.ReferenceEquals(global::System.Globalization.CultureInfo.CurrentCulture, appendCulture) || !global::LuminPack.Code.LuminPackCollectionAccess.TryAppendSortedListOrdered(value, k!, v, true)) value.Add(k!, v);" : "value.Add(k!, v);");
        w.Close();
    }
}
