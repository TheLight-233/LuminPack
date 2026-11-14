namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
internal static partial class SortedListOwnerFile
{
    /// <summary>Preserves same-position key matching, staged values and failed-read prefixes of a reused sorted list.</summary>
    /// <param name = "c">The family-specific operation context.</param>
    private static void MatchingPairs(PairedDictionaryContext c)
    {
        var w = c.W;
        const string access = "global::LuminPack.Code.LuminPackCollectionAccess";
        w.Line("if (value is not null && value.GetType() == typeof(" + c.Dictionary + ") && length != 0 && value.Count != 0)");
        w.Open();
        w.Line("int reuseCount = global::System.Math.Min(length, value.Count);");
        w.Line("var keys = global::System.Buffers.ArrayPool<" + c.Key + ">.Shared.Rent(length);");
        w.Line(ContiguousOwnerFile.ArrayType(c.Value) + "? values = null;");
        w.Line("int completed = 0;");
        w.Line("try");
        w.Open();
        w.Line("values = global::System.Buffers.ArrayPool<" + c.Value + ">.Shared.Rent(length);");
        w.Line("ref var firstKey = ref " + ArrayReference + "(keys);");
        w.Line("ref var firstValue = ref " + ArrayReference + "(values);");
        w.Line("if (" + access + ".TryGetSortedListStorage(value, out var priorKeys, out var priorValues))");
        w.Open();
        w.Line("global::System.Array.Copy(priorKeys!, keys, reuseCount);");
        w.Line("global::System.Array.Copy(priorValues!, values, reuseCount);");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("if (reuseCount != 0)");
        w.Open();
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line(c.Memory + ".Add(ref firstKey, (nint)(uint)index) = value.Keys[index];");
        w.Line(c.Memory + ".Add(ref firstValue, (nint)(uint)index) = value.Values[index];");
        w.Close();
        w.Line("while (++index < reuseCount);");
        w.Close();
        w.Close();
        w.Line("value.Clear();");
        w.Line("int pairIndex = 0;");
        w.Line("do");
        w.Open();
        w.Line("ref var keySlot = ref " + c.Memory + ".Add(ref firstKey, (nint)(uint)pairIndex);");
        w.Line("ref var valueSlot = ref " + c.Memory + ".Add(ref firstValue, (nint)(uint)pairIndex);");
        if (c.Key == "string" || c.Key.StartsWith("global::System.Nullable<", System.StringComparison.Ordinal))
        {
            w.Line("string? priorKey = pairIndex < reuseCount ? keySlot : null;");
            w.Line("string key = priorKey!;");
            c.ReadMember(c.Key, "key");
            w.Line(c.Value + " item = priorKey is not null && global::System.String.Equals(key, priorKey, global::System.StringComparison.Ordinal) ? valueSlot : default!;");
        }
        else
        {
            w.Line(c.Key + " key = default;");
            c.ReadMember(c.Key, "key");
            w.Line(c.Value + " item = pairIndex < reuseCount && global::System.Collections.Generic.EqualityComparer<" + c.Key + ">.Default.Equals(key, keySlot) ? valueSlot : default!;");
        }

        c.ReadMember(c.Value, "item");
        if (c.Key == "string" || c.Key.StartsWith("global::System.Nullable<", System.StringComparison.Ordinal))
            w.Line("if (key is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(\"key\");");
        w.Line("keySlot = key;");
        w.Line("valueSlot = item;");
        w.Line("completed = pairIndex + 1;");
        w.Close();
        w.Line("while (++pairIndex < length);");
        w.Line("completed = -1;");
        w.Line("if (!" + access + ".TryFillSortedList(value, keys, values, length))");
        w.Indented(() => w.Line("ReplayPairs(value, keys, values, length);"));
        w.Close();
        w.Line("catch (global::System.Exception error) when (completed >= 0)");
        w.Open();
        w.Line("if (completed != 0) ReplayPairs(value, keys, values!, completed);");
        w.Line("global::LuminPack.Code.LuminPackExceptionHelper.Rethrow(error);");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + c.Key + ">.Shared.Return(keys, true);");
        w.Line("if (values is not null) global::System.Buffers.ArrayPool<" + c.Value + ">.Shared.Return(values, true);");
        w.Close();
        w.Line("goto operationComplete_RootRead;");
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void ReplayPairs(" + c.Dictionary + " destination, " + ContiguousOwnerFile.ArrayType(c.Key) + " keys, " +
            ContiguousOwnerFile.ArrayType(c.Value) + " values, int count)");
        w.Open();
        w.Line("ref var firstKey = ref " + ArrayReference + "(keys);");
        w.Line("ref var firstValue = ref " + ArrayReference + "(values);");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("destination.Add(" + c.Memory + ".Add(ref firstKey, (nint)(uint)index), " + c.Memory + ".Add(ref firstValue, (nint)(uint)index));");
        w.Close();
        w.Line("while (++index < count);");
        w.Close();
        w.Close();
    }
}
