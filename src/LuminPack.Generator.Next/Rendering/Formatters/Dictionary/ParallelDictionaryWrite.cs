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

    /// <summary>Writes a nonempty range, preserving the key-before-value load on writer growth.</summary>
    internal static void Emit(PairedDictionaryContext c, string count, bool allowMixed = true)
    {
        var w = c.W;
        string keySlot = c.Memory + ".Add(ref firstKey, (nint)(uint)index)";
        string valueSlot = c.Memory + ".Add(ref firstValue, (nint)(uint)index)";
        bool mixed = allowMixed && c.UsesRawOrStringEntries && (c.Key == "string") != (c.Value == "string") &&
                     c.Supports("BinaryTextPair");
        string rawWidth = c.Memory + ".SizeOf<" + (c.Key == "string" ? c.Value : c.Key) + ">()";
        if (mixed)
            w.Line("bool utf16Length = " + FixedUtf16LengthProfile + ";");
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        if (mixed)
        {
            w.Line("if (utf16Length)");
            w.Open();
            string text = c.Key == "string" ? keySlot : valueSlot;
            w.Line("string text = " + text + ";");
            w.Line("if ((long)operationState.RecordRoom() - " + rawWidth + " >= " + Writer + ".Utf16RecordBound(text))");
            w.Open();
            w.Line("ref byte record = ref operationState.Cursor;");
            if (c.Key == "string")
            {
                w.Line("int written = " + Writer + ".WriteReservedUtf16Record(ref record, text);");
                w.Line(Writer + ".Write(ref " + c.Memory + ".Add(ref record, (nint)(uint)written), in " + valueSlot + ");");
            }
            else
            {
                w.Line(Writer + ".Write(ref record, in " + keySlot + ");");
                w.Line("int written = " + Writer + ".WriteReservedUtf16Record(ref " + c.Memory + ".Add(ref record, " + rawWidth + "), text);");
            }
            w.Line("operationState.CompleteRun(written + " + rawWidth + ");");
            w.Line("continue;");
            w.Close();
            w.Close();
            w.Line("WritePairSlow(ref operationState, " + keySlot + ", ref " + valueSlot + ");");
        }
        else
        {
            w.Line(c.Key + " key = " + keySlot + ";");
            c.WriteMember(c.Key, "key");
            w.Line(c.Value + " itemValue = " + valueSlot + ";");
            c.WriteMember(c.Value, "itemValue");
        }
        w.Close();
        w.Line("while (++index < " + count + ");");
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
