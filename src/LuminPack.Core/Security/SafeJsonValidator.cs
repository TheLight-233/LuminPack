using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;
using Token = LuminPack.Core.LuminPackJsonReader.JsonTokenType;

namespace LuminPack.Security;

/// <summary>Checks schema, strict grammar and shared quotas through the existing token and scalar reader.</summary>
internal static unsafe class SafeJsonValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly UnicodeEncoding StrictUtf16 = new UnicodeEncoding(false, false, true);

    /// <summary>Validates one complete document before any application object is constructed or reused.</summary>
    /// <typeparam name="TOptions">The strict document encoding.</typeparam>
    /// <param name="payload">The immutable original JSON payload.</param>
    /// <param name="schema">The generated closed schema.</param>
    /// <param name="budget">The cumulative root resource budget.</param>
    /// <param name="prefix">Whether the envelope owns trailing bytes after this complete value.</param>
    /// <returns>The complete validated value width.</returns>
    internal static int Validate<TOptions>(ReadOnlySpan<byte> payload, LuminPackSafeSchema schema, SafeReadBudget budget, bool prefix = false)
        where TOptions : struct, ILuminPackOptions
    {
        budget.Work(payload.Length);
        // This covers all token scratch and validation strings before the reader can allocate them.
        budget.Allocate(payload.Length * 12L + 2048);
        _ = (schema.JsonEncoding == 0 ? (Encoding)StrictUtf8 : StrictUtf16).GetCharCount(payload);
        fixed (byte* pinned = payload)
        {
            ReadOnlySpan<byte> input = new ReadOnlySpan<byte>(pinned, payload.Length);
            var reader = new LuminPackJsonReader(ref input, new LuminPackReaderOptionalState());
            Next<TOptions>(ref reader);
            Read<TOptions>(ref reader, schema, budget, 0, 1, false);
            if (reader.Depth != 0) LuminPackInputPolicy.Invalid("The JSON document has an unfinished container.");
            if (prefix) reader.SkipWhitespace<TOptions>();
            else reader.EnsureEndOfDocument<TOptions>();
            return reader.Position;
        }
    }

    /// <summary>Requires a complete next token through the canonical strict reader.</summary>
    /// <typeparam name="TOptions">The strict encoding.</typeparam>
    /// <param name="reader">The checked token reader.</param>
    private static void Next<TOptions>(ref LuminPackJsonReader reader) where TOptions : struct, ILuminPackOptions
    { if (!reader.Read<TOptions>()) LuminPackInputPolicy.Invalid("A required JSON token is missing."); }

    /// <summary>Checks the selected value and returns framework-only keys when uniqueness requires them.</summary>
    /// <typeparam name="TOptions">The strict encoding.</typeparam>
    /// <param name="reader">The selected-token reader.</param>
    /// <param name="schema">The generated graph.</param>
    /// <param name="budget">The shared quota state.</param>
    /// <param name="index">The node index.</param>
    /// <param name="depth">The value depth.</param>
    /// <param name="key">Whether a canonical duplicate key is needed.</param>
    /// <returns>The framework-only key or an unused default.</returns>
    private static SafeInputKey Read<TOptions>(ref LuminPackJsonReader reader, LuminPackSafeSchema schema, SafeReadBudget budget, int index, int depth, bool key)
        where TOptions : struct, ILuminPackOptions
    {
        budget.Depth(depth);
        if (reader.Depth > schema.JsonDepth) SafeReadBudget.Fail("JSON container depth");
        var node = schema.Nodes[index];
        var token = reader.CurrentTokenType;
        if (token == Token.Null)
        {
            if (node.Kind is not (LuminPackSafeKind.String or LuminPackSafeKind.Nullable or LuminPackSafeKind.Sequence or LuminPackSafeKind.Set or LuminPackSafeKind.Dictionary or LuminPackSafeKind.Queue or LuminPackSafeKind.MultiArray) &&
                !(node.Kind == LuminPackSafeKind.Object && node.Rank == 0)) LuminPackInputPolicy.Invalid("Null is invalid for this JSON contract.");
            reader.CompleteValue();
            return new SafeInputKey(LuminPackSafeKind.String, 0, 0, null);
        }
        if (node.Kind == LuminPackSafeKind.Nullable) return Read<TOptions>(ref reader, schema, budget, node.Children[0], depth + 1, key);
        if (node.Kind is LuminPackSafeKind.Object or LuminPackSafeKind.Struct)
        {
            if (token != Token.ObjectStart) LuminPackInputPolicy.Invalid("An object token is required by this JSON schema.");
            if (node.Rank == 0 && node.Kind == LuminPackSafeKind.Object) { budget.Object(); budget.Allocate(node.Storage + 64L); }
            budget.Allocate(node.Names.Length + 32L);
            var seen = new bool[node.Names.Length];
            while (true)
            {
                Next<TOptions>(ref reader);
                if (reader.CurrentTokenType == Token.ObjectEnd) { reader.CompleteValue(); return default; }
                if (reader.CurrentTokenType is not (Token.PropertyName or Token.String) || reader.GrammarState != LuminPackJsonReader.JsonStateColon)
                    LuminPackInputPolicy.Invalid("A JSON property name is required.");
                string property = Text<TOptions>(ref reader, budget);
                int member = -1;
                for (int i = 0; i < node.Names.Length; i++)
                {
                    budget.Work(property.Length + node.Names[i].Length + 1L);
                    if (string.Equals(property, node.Names[i], StringComparison.Ordinal)) { member = i; break; }
                }
                if (member < 0) LuminPackInputPolicy.Invalid("An unknown JSON member is outside the declared schema.");
                if (seen[member]) LuminPackInputPolicy.Invalid("A JSON object contains a duplicate member.");
                seen[member] = true;
                Next<TOptions>(ref reader);
                Read<TOptions>(ref reader, schema, budget, node.JsonChildren[member], depth + 1, false);
            }
        }
        if (node.Kind is LuminPackSafeKind.Sequence or LuminPackSafeKind.Queue or LuminPackSafeKind.Set or LuminPackSafeKind.Dictionary)
        {
            if (token != Token.ArrayStart) LuminPackInputPolicy.Invalid("An array token is required by this JSON collection schema.");
            budget.Object(); budget.Allocate(256);
            int count = 0, storage = 0;
            foreach (int child in node.Children) storage = checked(storage + schema.Nodes[child].Storage);
            bool unique = node.Kind is LuminPackSafeKind.Set or LuminPackSafeKind.Dictionary;
            var keys = unique ? new HashSet<SafeInputKey>(new SafeInputKeyComparer(budget)) : null;
            long previousText = 0;
            while (true)
            {
                Next<TOptions>(ref reader);
                if (reader.CurrentTokenType == Token.ArrayEnd) { reader.CompleteValue(); return default; }
                if (++count > budget.Limits.MaxCollectionLength) SafeReadBudget.Fail("collection length");
                budget.Work(1); budget.Allocate(Math.Max(storage, 1) * 4L + 128);
                if (node.Kind == LuminPackSafeKind.Dictionary)
                {
                    if (reader.CurrentTokenType != Token.ArrayStart) LuminPackInputPolicy.Invalid("A dictionary entry must be a key/value pair.");
                    Next<TOptions>(ref reader);
                }
                var item = Read<TOptions>(ref reader, schema, budget, node.Children[0], depth + 1, unique);
                if (unique)
                {
                    long length = item.Text?.Length ?? 0;
                    budget.Work(2L * count - 1 + 2L * (count - 1) * length + 2L * previousText);
                    previousText += length;
                }
                if (unique && (node.Kind == LuminPackSafeKind.Dictionary && item.IsNull || !keys!.Add(item)))
                    LuminPackInputPolicy.Invalid("A JSON set or dictionary contains a null key or duplicate entry.");
                if (node.Kind == LuminPackSafeKind.Dictionary)
                {
                    Next<TOptions>(ref reader);
                    Read<TOptions>(ref reader, schema, budget, node.Children[1], depth + 1, false);
                    Next<TOptions>(ref reader);
                    if (reader.CurrentTokenType != Token.ArrayEnd) LuminPackInputPolicy.Invalid("A dictionary entry must contain exactly two values.");
                    reader.CompleteValue();
                }
            }
        }
        if (node.Kind == LuminPackSafeKind.String)
        {
            if (token != Token.String) LuminPackInputPolicy.Invalid("A JSON string token is required.");
            return new SafeInputKey(node.Kind, 0, 0, Text<TOptions>(ref reader, budget));
        }
        if (node.Kind == LuminPackSafeKind.Boolean)
        {
            if (token is not (Token.True or Token.False)) LuminPackInputPolicy.Invalid("A JSON Boolean token is required.");
            return SafeInputKey.Number(node.Kind, reader.Read<bool, TOptions>() ? 1UL : 0UL, 0);
        }
        if (node.Kind == LuminPackSafeKind.Char)
        {
            if (token != Token.String) LuminPackInputPolicy.Invalid("A JSON character string is required.");
            string text = Text<TOptions>(ref reader, budget);
            if (text.Length != 1) LuminPackInputPolicy.Invalid("A Char requires exactly one UTF-16 code unit.");
            return SafeInputKey.Number(node.Kind, text[0], 0);
        }
        if (token != Token.Number) LuminPackInputPolicy.Invalid("A JSON numeric token is required.");
        ulong bits;
        switch (node.Kind)
        {
            case LuminPackSafeKind.Byte: bits = reader.Read<byte, TOptions>(); break;
            case LuminPackSafeKind.SByte: bits = unchecked((ulong)reader.Read<sbyte, TOptions>()); break;
            case LuminPackSafeKind.Int16: bits = unchecked((ulong)reader.Read<short, TOptions>()); break;
            case LuminPackSafeKind.UInt16: bits = reader.Read<ushort, TOptions>(); break;
            case LuminPackSafeKind.Int32: bits = unchecked((ulong)reader.Read<int, TOptions>()); break;
            case LuminPackSafeKind.UInt32: bits = reader.Read<uint, TOptions>(); break;
            case LuminPackSafeKind.Int64: bits = unchecked((ulong)reader.Read<long, TOptions>()); break;
            case LuminPackSafeKind.UInt64: bits = reader.Read<ulong, TOptions>(); break;
            case LuminPackSafeKind.Single: bits = unchecked((uint)BitConverter.SingleToInt32Bits(reader.Read<float, TOptions>())); break;
            case LuminPackSafeKind.Double: bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(reader.Read<double, TOptions>())); break;
            case LuminPackSafeKind.Decimal: _ = reader.Read<decimal, TOptions>(); return default;
            case LuminPackSafeKind.Enum:
                bits = node.Signed ? unchecked((ulong)reader.Read<long, TOptions>()) : reader.Read<ulong, TOptions>();
                if (node.Rank < 8)
                {
                    ulong mask = (1UL << (node.Rank * 8)) - 1;
                    long signed = unchecked((long)bits);
                    if (node.Signed ? signed < -(1L << (node.Rank * 8 - 1)) || signed >= (1L << (node.Rank * 8 - 1)) : bits > mask)
                        LuminPackInputPolicy.Invalid("The JSON enum exceeds its underlying width.");
                    bits &= mask;
                }
                SafeBinaryValidator.ValidateEnum(node, bits, budget); break;
            default: LuminPackInputPolicy.Invalid("This specialized JSON contract has no reviewed external-input shape."); return default;
        }
        return SafeInputKey.Number(node.Kind, bits, 0);
    }

    /// <summary>Checks decoded text limits and Unicode through the original escape/string kernels.</summary>
    /// <typeparam name="TOptions">The strict encoding.</typeparam>
    /// <param name="reader">The selected string or property-name reader.</param>
    /// <param name="budget">The cumulative root budget.</param>
    /// <returns>The validated UTF-16 string.</returns>
    private static string Text<TOptions>(ref LuminPackJsonReader reader, SafeReadBudget budget) where TOptions : struct, ILuminPackOptions
    {
        string value;
        if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
        {
            var text = reader.ReadStringUtf8<TOptions>();
            int count = StrictUtf8.GetCharCount(text);
            if (count > budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
            value = StrictUtf8.GetString(text);
        }
        else
        {
            var text = reader.ReadStringUtf16<TOptions>();
            if (text.Length > budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
            _ = StrictUtf16.GetByteCount(text);
            value = text.ToString();
        }
        budget.Work(value.Length); budget.Object(); budget.Allocate(value.Length * 4L + 128);
        reader.CompleteValue();
        return value;
    }
}
