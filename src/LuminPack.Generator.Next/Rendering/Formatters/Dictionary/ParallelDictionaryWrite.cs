using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits binary pair records from independently bounded key and value storage.</summary>
internal static class ParallelDictionaryWrite
{
    private const string Writer = "global::LuminPack.Core.LuminPackWriter";

    /// <summary>Tests the built-in profiles for which the writer can skip reading option properties.</summary>
    internal const string FixedUtf16LengthProfile =
        "(typeof(TLuminProfile) == typeof(global::LuminPack.Option.LuminPackUtf16LengthOptions) || " +
        "typeof(TLuminProfile) == typeof(global::LuminPack.Option.LuminPackDefaultOptions) || " +
        "typeof(TLuminProfile) == typeof(global::LuminPack.Option.LuminPackFixedOptions<global::LuminPack.Option.LuminPackUtf16LengthOptions>) || " +
        "typeof(TLuminProfile) == typeof(global::LuminPack.Option.LuminPackFixedOptions<global::LuminPack.Option.LuminPackDefaultOptions>))";

    /// <summary>Checks whether both members can use the fixed mixed-record writer.</summary>
    /// <param name="c">The dictionary writer's contracts.</param>
    /// <returns>Whether a string and a native value can share a bounded record.</returns>
    internal static bool SupportsMixed(PairedDictionaryContext c) => c.UsesRawOrStringEntries &&
        (c.Key == "string") != (c.Value == "string") && c.Supports("BinaryTextPair");

    /// <summary>Emits the bounded stores and local cursor advance for a captured string-key pair.</summary>
    /// <param name="w">The owner source writer.</param>
    /// <param name="memory">The unsafe operations alias.</param>
    /// <param name="rawWidth">The raw value width, or null for a string value.</param>
    internal static void WriteStringKeyPairIfFits(CodeWriter w, string memory, string? rawWidth = null)
    {
        string valueBound = rawWidth ?? Writer + ".Utf16RecordBound(itemValue)";
        w.Line("if ((long)room >= (long)" + Writer + ".Utf16RecordBound(key) + " + valueBound + ")");
        w.Open();
        w.Line("int keyBytes = " + Writer + ".WriteReservedUtf16Record(ref cursor, key);");
        w.Line("ref byte valueStart = ref " + memory + ".Add(ref cursor, (nint)(uint)keyBytes);");
        w.Line(rawWidth is null
            ? "int valueBytes = " + Writer + ".WriteReservedUtf16Record(ref valueStart, itemValue);"
            : "int valueBytes = " + Writer + ".Write(ref valueStart, in itemValue);");
        w.Line("int width = keyBytes + valueBytes;");
        w.Line("cursor = ref " + memory + ".Add(ref cursor, (nint)(uint)width);");
        w.Line("room -= width;");
        w.Line("continue;");
        w.Close();
    }

    /// <summary>Writes a complete mixed record when the fixed profile and current room permit it.</summary>
    /// <param name="c">The dictionary writer's contracts.</param>
    /// <param name="key">The current key expression.</param>
    /// <param name="value">The current value expression.</param>
    /// <param name="byRef">Whether the native member expression denotes a storage location.</param>
    internal static void WriteMixedIfFits(PairedDictionaryContext c, string key, string value, bool byRef)
    {
        var w = c.W;
        bool textKey = c.Key == "string";
        string rawWidth = c.Memory + ".SizeOf<" + (textKey ? c.Value : c.Key) + ">()";
        string native = (byRef ? "in " : "") + (textKey ? value : key);
        w.Line("if (utf16Length)");
        w.Open();
        w.Line("string text = " + (textKey ? key : value) + ";");
        w.Line("if ((long)operationState.RecordRoom() - " + rawWidth + " >= sizeof(int) + (long)(text?.Length ?? 0) * sizeof(char))");
        w.Open();
        w.Line("ref byte record = ref operationState.Cursor;");
        if (textKey)
        {
            w.Line("int written = " + Writer + ".WriteReservedUtf16Record(ref record, text);");
            w.Line(Writer + ".Write(ref " + c.Memory + ".Add(ref record, (nint)(uint)written), " + native + ");");
        }
        else
        {
            w.Line(Writer + ".Write(ref record, " + native + ");");
            w.Line("int written = " + Writer + ".WriteReservedUtf16Record(ref " + c.Memory + ".Add(ref record, " + rawWidth + "), text);");
        }
        w.Line("operationState.CompleteRun(written + " + rawWidth + ");");
        w.Line("continue;");
        w.Close();
        w.Close();
    }

    /// <summary>Writes a captured string key directly when its complete raw bound and the growth threshold fit.</summary>
    /// <param name="c">The paired writer context.</param>
    internal static void WriteBoundedKey(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (utf16Length && operationState.RecordRoom() >= " + Writer + ".Utf16RecordBound(key))");
        w.Open();
        w.Line("ref byte record = ref operationState.Cursor;");
        w.Line("operationState.CompleteRun(" + Writer + ".WriteReservedUtf16Record(ref record, key));");
        w.Close();
        w.Line("else");
        w.Indented(() => w.Line("WriteKeySlow(ref operationState, key);"));
    }

    /// <summary>Outlines the original checked key write for other profiles or short windows.</summary>
    /// <param name="c">The paired writer context.</param>
    internal static void WriteBoundedKeyFallback(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void WriteKeySlow(scoped ref " + Writer + " operationState, string key)");
        w.Open();
        c.WriteMember(c.Key, "key");
        w.Close();
    }

    /// <summary>Writes a nonempty range, preserving the key-before-value load on writer growth.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    /// <param name="count">The number of items to process.</param>
    /// <param name="allowMixed">Whether to enable allow mixed for this operation.</param>
    /// <param name="allowBoundedKey">Whether the storage-backed traversal can reserve an ordinary object pair's string key.</param>
    internal static void Emit(PairedDictionaryContext c, string count, bool allowMixed = true, bool allowBoundedKey = false)
    {
        var w = c.W;
        string keySlot = c.Memory + ".Add(ref firstKey, (nint)(uint)index)";
        string valueSlot = c.Memory + ".Add(ref firstValue, (nint)(uint)index)";
        bool mixed = allowMixed && SupportsMixed(c);
        bool boundedKey = allowBoundedKey && c.Key == "string" && c.OrdinaryObjectValue &&
            c.Supports("SortedListBinaryBoundedKey");
        if (mixed || boundedKey)
            w.Line("bool utf16Length = " + FixedUtf16LengthProfile + ";");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        if (mixed)
        {
            WriteMixedIfFits(c, keySlot, valueSlot, byRef: true);
            w.Line("WritePairSlow(ref operationState, " + keySlot + ", ref " + valueSlot + ");");
        }
        else
        {
            w.Line(c.Key + " key = " + keySlot + ";");
            if (boundedKey)
                WriteBoundedKey(c);
            else
                c.WriteMember(c.Key, "key");
            w.Line(c.Value + " itemValue = " + valueSlot + ";");
            c.WriteMember(c.Value, "itemValue");
        }
        w.Close();
        w.Line("while (++index < " + count + ");");
        if (boundedKey)
            WriteBoundedKeyFallback(c);
        if (!mixed)
            return;
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void WritePairSlow(scoped ref " + Writer + " operationState, " + c.Key + " key, scoped ref " + c.Value + " valueSlot)");
        w.Open();
        c.WriteMember(c.Key, "key");
        w.Line(c.Value + " itemValue = valueSlot;");
        c.WriteMember(c.Value, "itemValue");
        w.Close();
    }
}
