using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ConcurrentDictionary&lt;TKey, TValue&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The write counts the enumeration of the dictionary as it writes. The read creates a new dictionary from the staged
/// pairs through the collection-access helper, or clears and refills a non-null destination with <c>TryAdd</c>.</remarks>
internal static class ConcurrentDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(Write, Read,
        "value.TryAdd(key, itemValue);", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Writes each fixed UTF-16 key and raw value after one complete pair capacity check.</summary>
    /// <param name="c">The generated dictionary write context.</param>
    private static void Write(PairedDictionaryContext c)
    {
        if (!c.UsesRawOrStringEntries || !c.Supports("ConcurrentBinaryTextPair"))
        {
            c.CountedWrite();
            return;
        }

        var w = c.W;
        const string writer = "global::LuminPack.Core.LuminPackWriter";
        string width = c.Memory + ".SizeOf<" + c.Value + ">()";
        w.Line("if (!" + ParallelDictionaryWrite.FixedUtf16LengthProfile + ")");
        w.Open();
        w.Line("WriteCounted(ref operationState, value);");
        w.Line("goto operationComplete_RootWrite;");
        w.Close();
        w.Blank();
        w.Line("int countPosition = operationState.BeginCollection();");
        w.Line("int count = 0;");
        w.Line("foreach (var item in " + c.Source + ")");
        w.Open();
        w.Line("count++;");
        w.Line("var key = item.Key;");
        w.Line("var itemValue = item.Value;");
        // Widen before subtracting so a cursor past the checked limit cannot wrap into a valid room count.
        w.Line("if ((long)operationState.RecordRoom() - " + width + " >= " + writer + ".Utf16RecordBound(key))");
        w.Open();
        w.Line("ref byte cursor = ref operationState.Cursor;");
        w.Line("int written = " + writer + ".WriteReservedUtf16Record(ref cursor, key);");
        w.Line(writer + ".Write(ref " + c.Memory + ".Add(ref cursor, unchecked((nint)(uint)written)), in itemValue);");
        w.Line("operationState.CompleteRun(written + " + width + ");");
        w.Close();
        w.Line("else");
        w.Indented(() => w.Line("WritePairSlow(ref operationState, key, in itemValue);"));
        w.Close();
        w.Line("operationState.EndCollection(countPosition, count);");
        w.Line("operationState.CheckBuffer();");
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void WritePairSlow(scoped ref " + writer + " operationState, string key, scoped in " + c.Value + " itemValue)");
        w.Open();
        c.WriteMember(c.Key, "key");
        c.WriteMember(c.Value, "itemValue");
        w.Close();
        w.Blank();
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("static void WriteCounted(scoped ref " + writer + " operationState, " + c.Dictionary + " value)");
        w.Open();
        c.CountedWrite();
        w.Close();
    }

    /// <summary>Stages fresh pairs and refills reused targets in wire order.</summary>
    /// <param name="c">The generated dictionary read context.</param>
    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Open();
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = global::LuminPack.Code.LuminPackCollectionAccess.CreateConcurrentDictionary(global::System.Array.Empty<" + c.Pair + ">(), length);");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        c.ReadPairs(() => w.Line("value = global::LuminPack.Code.LuminPackCollectionAccess.CreateConcurrentDictionary(pairs, length);"),
            knownNonEmpty: c.UsesRawOrStringEntries);
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("value.Clear();");
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Line("int remaining = length;");
        w.Line("do");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line("value.TryAdd(k!, v);");
        w.Close();
        w.Line("while (--remaining != 0);");
    }
}
