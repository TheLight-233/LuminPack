namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
internal sealed partial class PairedDictionaryContext
{
    /// <summary>Whether the key is a non-nullable value keyword, which needs no null check before the lookup.</summary>
    private bool ValueKey => Key != "string" && Key != "object" && !Key.EndsWith("?", System.StringComparison.Ordinal);

    /// <summary>Stages new entries while keeping the original dictionary and matching object values alive through decoding.</summary>
    internal void ReadExistingDictionary()
    {
        if ((_contract ?? _set.FindFormatter(Dictionary)) is { } model)
            DictionarySlotReuseEmitter.Emit(W, _set, model, "operationState", "value", "length", RawPairs);
        W.Line("var pairs = global::System.Buffers.ArrayPool<" + Pair + ">.Shared.Rent(length);");
        W.Line("try");
        W.Open();
        if (Key == "string")
        {
            W.Line("var previous = value.GetEnumerator();");
            if (Supports("EnumeratedStringKey"))
                W.Line("bool useEnumeratedValue = operationState.CanReuseEnumeratedStringKey<TLuminProfile>(value.Comparer);");
        }

        W.Line("ref var firstPair = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(pairs);");
        W.Line("int index = 0;");
        W.Line("do");
        W.Open();
        if (Key == "string")
            W.Line("var prior = previous.MoveNext() ? previous.Current : default;");
        W.Line(Key == "string" ? "string key = prior.Key;" : Key + " key = default!;");
        ReadMember(Key, "key");
        W.Line(Value + " item = " + (Key == "string" && Supports("EnumeratedStringKey") ? "useEnumeratedValue && key is not null && global::System.Object.ReferenceEquals(key, prior.Key) ? prior.Value : " : "") + (ValueKey ? "" : "key is not null && ") + "value.TryGetValue(key, out var existing) ? existing : default!;");
        ReadMember(Value, "item");
        W.Line(Memory + ".Add(ref firstPair, (nint)(uint)index) = new " + Pair + "(key!, item);");
        W.Close();
        W.Line("while (++index < length);");
        W.Line("value.Clear();");
        W.Line("index = 0;");
        W.Line("do");
        W.Open();
        W.Line("ref var pair = ref " + Memory + ".Add(ref firstPair, (nint)(uint)index);");
        W.Line("value.Add(pair.Key, pair.Value);");
        W.Close();
        W.Line("while (++index < length);");
        W.Close();
        W.Line("finally");
        W.Open();
        W.Line("global::System.Buffers.ArrayPool<" + Pair + ">.Shared.Return(pairs, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + Pair + ">());");
        W.Close();
    }
}
