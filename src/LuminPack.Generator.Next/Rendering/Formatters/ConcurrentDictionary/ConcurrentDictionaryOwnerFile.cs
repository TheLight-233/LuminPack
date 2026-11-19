using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ConcurrentDictionary&lt;TKey, TValue&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The write counts the enumeration of the dictionary as it writes. The read creates a new dictionary from the staged
/// pairs through the collection-access helper, or clears and refills a non-null destination with <c>TryAdd</c>.</remarks>
internal static class ConcurrentDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(Write, Read,
        "value.TryAdd(key, itemValue);", outlineJson: true, boundedJsonSnapshot: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Writes pairs with fixed UTF-16 text and eligible scalars in a proven output window.</summary>
    /// <param name="c">The generated dictionary write context.</param>
    private static void Write(PairedDictionaryContext c)
    {
        var w = c.W;
        if (c.RawPairStorage && c.Supports("ConcurrentRawPairs") && c.Supports("ConcurrentTraversal"))
        {
            w.Line("int countPosition = operationState.BeginCollection();");
            w.Line("int count = global::LuminPack.Code.LuminPackCollectionAccess.WriteConcurrentRawPairs(ref operationState, value);");
            w.Line("operationState.EndCollection(countPosition, count);");
            w.Line("operationState.CheckBuffer();");
            return;
        }
        bool pairCursor = c.Supports("BinaryStringPairCursor");
        if (!c.UsesRawOrStringEntries || !(pairCursor || c.Supports("ConcurrentBinaryTextPair")))
        {
            c.CountedWrite();
            return;
        }

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
        if (pairCursor)
        {
            w.Line("ref byte cursor = ref operationState.Cursor;");
            w.Line("int room = operationState.RecordRoom(ref cursor);");
        }
        w.Line("foreach (var item in " + c.Source + ")");
        w.Open();
        w.Line("count++;");
        w.Line("var key = item.Key;");
        w.Line("var itemValue = item.Value;");
        if (pairCursor)
        {
            // Publish the captured window before the original pair operation can grow the writer.
            if (c.Key == "string")
                ParallelDictionaryWrite.WriteStringKeyPairIfFits(w, c.Memory);
            else
                WriteIntegerKeyPairIfFits();
            w.Line("operationState.CommitRecords(ref cursor);");
            w.Line("WritePairSlow(ref operationState, key, in itemValue);");
            w.Line("cursor = ref operationState.Cursor;");
            w.Line("room = operationState.RecordRoom(ref cursor);");
        }
        else
        {
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
        }
        w.Close();
        if (pairCursor)
            w.Line("operationState.CommitRecords(ref cursor);");
        w.Line("operationState.EndCollection(countPosition, count);");
        w.Line("operationState.CheckBuffer();");
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void WritePairSlow(scoped ref " + writer + " operationState, " + c.Key + " key, scoped in " + c.Value + " itemValue)");
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

        void WriteIntegerKeyPairIfFits()
        {
            string keyWidth = c.Memory + ".SizeOf<" + c.Key + ">()";
            w.Line("if ((long)room - " + keyWidth + " >= sizeof(int) + (long)(itemValue?.Length ?? 0) * sizeof(char))");
            w.Open();
            w.Line("int keyBytes = " + writer + ".Write(ref cursor, in key);");
            w.Line("ref byte text = ref " + c.Memory + ".Add(ref cursor, (nint)(uint)keyBytes);");
            w.Line("int pairBytes = keyBytes + " + writer + ".WriteReservedUtf16Record(ref text, itemValue);");
            w.Line("cursor = ref " + c.Memory + ".Add(ref cursor, (nint)(uint)pairBytes);");
            w.Line("room -= pairBytes;");
            w.Line("continue;");
            w.Close();
        }
    }

    /// <summary>Stages fresh pairs and refills reused targets in wire order.</summary>
    /// <param name="c">The generated dictionary read context.</param>
    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        // Advance the complete range only when the stashed reader is published after the whole operation succeeds.
        if (c.UsesRawOrStringEntries && c.RawPairReadStorage && c.ConcurrentDictionaryBuilder &&
            c.ConcurrentDictionaryRefill && c.Supports("ReadBoundedRawDictionary"))
        {
            w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryReadConcurrentRawPairs(ref operationState, ref value, length))");
            w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        }
        w.Line("if (value is null)");
        w.Open();
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = global::LuminPack.Code.LuminPackCollectionAccess.CreateConcurrentDictionary(global::System.Array.Empty<" + c.Pair + ">(), length);");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        if (c.ConcurrentDictionaryBuilder)
        {
            // Decode and insert each pair into the unpublished dictionary; it is assigned only after the last pair.
            w.Line("var builder = global::LuminPack.Code.LuminPackCollectionAccess.BeginConcurrentDictionary<" + c.Key + ", " + c.Value + ">(length);");
            w.Line("int pending = length;");
            w.Line("do");
            w.Open();
            w.Line(c.Key + " k = default !;");
            w.Line(c.Value + " v = default !;");
            c.ReadMember(c.Key, "k", fresh: true);
            c.ReadMember(c.Value, "v", fresh: true);
            w.Line("builder.Add(k!, v);");
            w.Close();
            w.Line("while (--pending != 0);");
            w.Line("value = builder.Complete();");
        }
        else
            c.ReadPairs(() => w.Line("value = global::LuminPack.Code.LuminPackCollectionAccess.CreateConcurrentDictionary(pairs, length);"),
                knownNonEmpty: c.UsesRawOrStringEntries);
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        if (c.ConcurrentDictionaryRefill)
        {
            // Pairs are read over the existing ones in the dictionary's own order: unchanged keys and values keep their
            // identity and nodes, and keys missing from the record are removed at the end.
            w.Line("if (length == 0)");
            w.Open();
            w.Line("value.Clear();");
            w.Line("goto operationComplete_RootRead;");
            w.Close();
            if (c.Key == "string" && (c.OrdinaryObjectValue || c.Payload is { IsStruct: false }) &&
                c.Supports("ConcurrentDictionaryKeyRefill"))
                ReadKeyRefill(c);
            // Only objects read in place are worth staging once the order no longer matches.
            bool reuseValues = c.ReferenceValue && c.Value != "string";
            w.Line("var refill = global::LuminPack.Code.LuminPackCollectionAccess.BeginConcurrentDictionaryRefill(value, length, " +
                (reuseValues ? "true" : "false") + ");");
            // One loop keeps a single expansion of each member read; once the order no longer matches, the pairs are read
            // as the plain refill reads them.
            w.Line("bool ordered = refill.Ordered;");
            w.Line("int refillRemaining = length;");
            w.Line("do");
            w.Open();
            // Only a string key is read over the candidate; other reference keys must not be filled in place.
            w.Line(c.Key + " k = default !;");
            if (c.Key == "string")
                w.Line("if (ordered) k = refill.NextKey();");
            else
                w.Line("if (ordered) refill.NextKey();");
            c.ReadMember(c.Key, "k", fresh: c.Key != "string");
            w.Line(c.Value + " v = default !;");
            w.Line(c.Value + " previous = default !;");
            w.Line("bool found = false;");
            if (!reuseValues)
            {
                w.Line("if (ordered)");
                w.Open();
            }
            w.Line("found = refill.Find(k!, out v);");
            w.Line("previous = v;");
            w.Line("ordered = found;");
            if (!reuseValues)
                w.Close();
            c.ReadMember(c.Value, "v");
            if (reuseValues)
                w.Line("refill.Store(k!, v, found, previous);");
            else
            {
                w.Line("if (found)");
                w.Indented(() => w.Line("refill.Store(k!, v, true, previous);"));
                w.Line("else");
                w.Indented(() => w.Line("value.TryAdd(k!, v);"));
            }
            w.Close();
            w.Line("while (--refillRemaining != 0);");
            w.Line("refill.Complete();");
            return;
        }
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

    /// <summary>Refills a larger fixed-profile record by matching decoded keys against the target.</summary>
    /// <param name="c">The generated dictionary read context.</param>
    private static void ReadKeyRefill(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (length > 64 && " + ParallelDictionaryWrite.FixedUtf16LengthProfile + " &&");
        w.Line("    TryRefillByKey(ref operationState, value, length))");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static bool TryRefillByKey(scoped ref global::LuminPack.Core.LuminPackReader operationState, " + c.Dictionary + " value, int length)");
        w.Open();
        w.Line("if (!global::LuminPack.Code.LuminPackCollectionAccess.TryBeginConcurrentDictionaryKeyRefill(value, length, out var keyRefill))");
        w.Indented(() => w.Line("return false;"));
        w.Line("try");
        w.Open();
        w.Line("int keysRemaining = length;");
        w.Line("do");
        w.Open();
        w.Line("string key = keyRefill.ReadKey(ref operationState, out var previousValue);");
        w.Line("var updatedValue = previousValue;");
        c.ReadMember(c.Value, "updatedValue");
        w.Line("keyRefill.Store(key, updatedValue, previousValue);");
        w.Close();
        w.Line("while (--keysRemaining != 0);");
        w.Line("keyRefill.Complete();");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("keyRefill.Dispose();");
        w.Close();
        w.Line("return true;");
        w.Close();
        w.Blank();
    }
}
