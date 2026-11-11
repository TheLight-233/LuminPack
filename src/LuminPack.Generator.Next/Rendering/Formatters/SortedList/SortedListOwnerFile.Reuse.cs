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
        w.Line("if (" + access + ".TryGetSortedListStorage(value, out var priorKeys, out var priorValues))");
        w.Open();
        w.Line("global::System.Array.Copy(priorKeys!, keys, reuseCount);");
        w.Line("global::System.Array.Copy(priorValues!, values, reuseCount);");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("for (int index = 0; index < reuseCount; index++)");
        w.Open();
        w.Line("keys[index] = value.Keys[index];");
        w.Line("values[index] = value.Values[index];");
        w.Close();
        w.Close();
        w.Line("value.Clear();");
        w.Line("for (int index = 0; index < length; index++)");
        w.Open();
        if (c.Key == "string")
        {
            w.Line("string? priorKey = index < reuseCount ? keys[index] : null;");
            w.Line("string key = priorKey!;");
            c.ReadMember(c.Key, "key");
            w.Line(c.Value + " item = priorKey is not null && global::System.String.Equals(key, priorKey, global::System.StringComparison.Ordinal) ? values[index] : default!;");
        }
        else
        {
            w.Line(c.Key + " key = default;");
            c.ReadMember(c.Key, "key");
            w.Line(c.Value + " item = index < reuseCount && global::System.Collections.Generic.EqualityComparer<" + c.Key + ">.Default.Equals(key, keys[index]) ? values[index] : default!;");
        }

        c.ReadMember(c.Value, "item");
        if (c.Key == "string")
            w.Line("if (key is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(\"key\");");
        w.Line("keys[index] = key;");
        w.Line("values[index] = item;");
        w.Line("completed = index + 1;");
        w.Close();
        w.Line("completed = -1;");
        w.Line("if (!" + access + ".TryFillSortedList(value, keys, values, length))");
        w.Line("    for (int index = 0; index < length; index++) value.Add(keys[index], values[index]);");
        w.Close();
        w.Line("catch (global::System.Exception error) when (completed >= 0)");
        w.Open();
        w.Line("for (int index = 0; index < completed; index++) value.Add(keys[index], values![index]);");
        w.Line("global::LuminPack.Code.LuminPackExceptionHelper.Rethrow(error);");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + c.Key + ">.Shared.Return(keys, true);");
        w.Line("if (values is not null) global::System.Buffers.ArrayPool<" + c.Value + ">.Shared.Return(values, true);");
        w.Close();
        w.Line("goto operationComplete_RootRead;");
        w.Close();
    }
}
