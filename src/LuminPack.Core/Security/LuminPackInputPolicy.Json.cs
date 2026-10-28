using System.Text;
using System.Runtime.CompilerServices;
using LuminPack.Core;
using LuminPack.Code;
using LuminPack.Option;

namespace LuminPack.Security;

public static partial class LuminPackInputPolicy
{
    private static readonly UnicodeEncoding InputUtf16 = new UnicodeEncoding(false, false, true);

    /// <summary>Opens the canonical JSON envelope without copying or scanning its value.</summary>
    /// <param name="input">Caller-owned stable document bytes.</param><param name="schema">The expected contract.</param>
    /// <returns>The borrowed remaining document beginning with the original JSON value.</returns>
    public static ReadOnlySpan<byte> OpenStreamingJson(ReadOnlySpan<byte> input, LuminPackSafeSchema schema)
    {
        schema.EnsureJsonSupported();
        CheckInput(input.Length, schema.Limits.MaxInputBytes);
        int suffix = schema.JsonEncoding == 0 ? 1 : 2;
        if (input.Length < schema.JsonPrefix.Length + suffix || !input.Slice(0, schema.JsonPrefix.Length).SequenceEqual(schema.JsonPrefix))
            Invalid("The JSON schema/profile envelope is invalid or incompatible.");
        var budget = CurrentInput!.Budget;
        budget.Work(input.Length); budget.Allocate(input.Length * 12L + 4096);
        return input.Slice(schema.JsonPrefix.Length);
    }

    /// <summary>Checks the closing envelope after the original decoder consumes its value.</summary>
    /// <param name="input">The borrowed value and envelope suffix.</param><param name="position">The completed value cursor.</param>
    /// <param name="schema">The encoding and payload limits.</param><returns>The complete envelope width.</returns>
    public static int CompleteStreamingJson(ReadOnlySpan<byte> input, int position, LuminPackSafeSchema schema)
    {
        int width = schema.JsonEncoding == 0 ? 1 : 2;
        CheckInput(position, schema.Limits.MaxDecompressedBytes);
        while (position <= input.Length - width && input[position] is 9 or 10 or 13 or 32 && (width == 1 || input[position + 1] == 0)) position += width;
        if (position > input.Length - width || input[position] != '}' || width == 2 && input[position + 1] != 0)
            Invalid("The JSON envelope is incomplete or has unexpected members.");
        return checked(schema.JsonPrefix.Length + position + width);
    }

    /// <summary>Begins a typed JSON value under the same original token reader and shared budget.</summary>
    /// <param name="reader">The original JSON reader.</param><param name="schema">The emitted type descriptor.</param>
    /// <returns>The nested value ownership token.</returns>
    public static ValueScope EnterValue(ref LuminPackJsonReader reader, LuminPackSafeSchema schema)
    {
        var scope = EnterValueCore(schema, reader.Position);
        if (scope.Context == null) return scope;
        try
        {
            var context = scope.Context;
            CheckInput(reader.Position, context.Root!.Limits.MaxDecompressedBytes);
            var node = schema.Nodes[0]; context.JsonDepth = reader.Depth; context.LastJsonPosition = -1; context.LastJsonToken = -1;
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.Null)
            {
                if (node.Kind is LuminPackSafeKind.Struct || node.Kind == LuminPackSafeKind.Object && node.Rank != 0)
                    Invalid("Null is invalid for this JSON value type.");
                return scope;
            }
            if (node.Kind is LuminPackSafeKind.Struct or LuminPackSafeKind.Object)
            {
                if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectStart) Invalid("A JSON object is required by this schema.");
                if (node.Kind == LuminPackSafeKind.Object && node.Rank == 0) { context.Budget.Object(); context.Budget.Allocate(node.Storage + 64L); }
            }
            else if (node.Kind is LuminPackSafeKind.Sequence or LuminPackSafeKind.Queue or LuminPackSafeKind.Set or LuminPackSafeKind.Dictionary)
            {
                if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ArrayStart) Invalid("A JSON array is required by this schema.");
                context.Count = 0;
                if (!scope.Alias) { context.Budget.Object(); context.Budget.Allocate(256); }
            }
            else if (node.Kind is LuminPackSafeKind.Guid or LuminPackSafeKind.DateTime or LuminPackSafeKind.TimeSpan or LuminPackSafeKind.MultiArray)
                Invalid("This specialized JSON contract has no reviewed streaming input shape.");
            return scope;
        }
        catch (Exception exception) { ExitValue(scope); LuminPackExceptionHelper.Rethrow(exception); return default; }
    }

    /// <summary>Consumes the original strict next token and charges its collection/property boundaries.</summary>
    /// <typeparam name="TOptions">The compiled document encoding.</typeparam><param name="reader">The original reader.</param>
    /// <returns>Whether a complete next token exists.</returns>
    public static bool ReadJsonToken<TOptions>(ref LuminPackJsonReader reader) where TOptions : struct, ILuminPackOptions
    {
        if (CurrentInput == null || !CurrentInput.Reading) return reader.Read<TOptions>();
        bool found = reader.Read<StreamingJsonOptions<TOptions>>();
        if (found) ObserveJsonToken<TOptions>(ref reader);
        return found;
    }

    /// <summary>Reads the selected scalar through the original strict typed JSON kernel.</summary>
    /// <typeparam name="T">The original scalar.</typeparam><typeparam name="TOptions">The fixed profile.</typeparam>
    /// <param name="reader">The original reader.</param><returns>The original scalar value.</returns>
    public static T ReadJsonValue<T, TOptions>(ref LuminPackJsonReader reader) where TOptions : struct, ILuminPackOptions
    {
        if (CurrentInput == null || !CurrentInput.Reading) return reader.Read<T, TOptions>();
        if (reader.CurrentTokenType is not (LuminPackJsonReader.JsonTokenType.String or LuminPackJsonReader.JsonTokenType.Number or
            LuminPackJsonReader.JsonTokenType.True or LuminPackJsonReader.JsonTokenType.False or LuminPackJsonReader.JsonTokenType.Null or LuminPackJsonReader.JsonTokenType.PropertyName) &&
            !ReadJsonToken<TOptions>(ref reader)) Invalid("A JSON scalar value is missing.");
        ObserveJsonToken<TOptions>(ref reader);
        var context = CurrentInput;
        var token = reader.CurrentTokenType;
        var kind = context!.Value?.Nodes[0].Kind;
        context.Budget.Depth(context.Depth + (kind is LuminPackSafeKind.Object or LuminPackSafeKind.Struct or LuminPackSafeKind.Nullable or
            LuminPackSafeKind.Sequence or LuminPackSafeKind.Queue or LuminPackSafeKind.Set or LuminPackSafeKind.Dictionary ? 1 : 0));
        if (typeof(T) == typeof(string))
        {
            string text = reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.Null ? null! : ReadJsonText<TOptions>(ref reader);
            reader.CompleteValue();
            return Unsafe.As<string, T>(ref text);
        }
        if (typeof(T) == typeof(char))
        {
            char character;
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                var bytes = ReadJsonNameUtf8<TOptions>(ref reader);
                if (InputUtf8.GetCharCount(bytes) != 1) Invalid("A Char requires exactly one UTF-16 code unit.");
                Span<char> output = stackalloc char[1];
                _ = InputUtf8.GetChars(bytes, output); character = output[0];
            }
            else
            {
                var chars = ReadJsonNameUtf16<TOptions>(ref reader);
                if (chars.Length != 1) Invalid("A Char requires exactly one UTF-16 code unit.");
                character = chars[0];
            }
            reader.CompleteValue();
            return Unsafe.As<char, T>(ref character);
        }
        if (typeof(T) == typeof(bool) ? token is not (LuminPackJsonReader.JsonTokenType.True or LuminPackJsonReader.JsonTokenType.False) :
            token != LuminPackJsonReader.JsonTokenType.Number) Invalid("The JSON scalar token disagrees with its schema.");
        T value = reader.Read<T, StreamingJsonOptions<TOptions>>();
        CheckInput(reader.Position, context.Root!.Limits.MaxDecompressedBytes);
        if (context != null && context.Reading && context.Value?.Nodes[0].Kind == LuminPackSafeKind.Enum)
        {
            var node = context.Value.Nodes[0];
            ulong bits = node.Signed ? unchecked((ulong)Convert.ToInt64(value)) : Convert.ToUInt64(value);
            if (node.Rank < 8) bits &= (1UL << (node.Rank * 8)) - 1;
            SafeBinaryValidator.ValidateEnum(node, bits, context.Budget);
        }
        return value;
    }

    /// <summary>Preserves strict property lookahead while observing its actual consumed token.</summary>
    /// <typeparam name="TOptions">The document profile.</typeparam><param name="reader">The original reader.</param>
    /// <param name="utf8Name">The expected UTF-8 name.</param><param name="utf16Name">The expected UTF-16 name.</param>
    /// <returns>The original lookahead result.</returns>
    public static bool TryReadJsonProperty<TOptions>(ref LuminPackJsonReader reader, ReadOnlySpan<byte> utf8Name, ReadOnlySpan<char> utf16Name) where TOptions : struct, ILuminPackOptions
    {
        if (CurrentInput == null || !CurrentInput.Reading) return reader.TryReadPropertyName<TOptions>(utf8Name, utf16Name);
        bool matched = reader.TryReadPropertyName<StreamingJsonOptions<TOptions>>(utf8Name, utf16Name);
        if (matched) ObserveJsonToken<TOptions>(ref reader);
        return matched;
    }

    /// <summary>Retains the original ordered-name matcher with strict grammar and duplicate accounting.</summary>
    /// <typeparam name="TOptions">The fixed document profile.</typeparam><param name="reader">The original reader.</param>
    /// <param name="index">The original generated member index.</param><param name="schema">The original name table.</param>
    /// <returns>The original fast-match result.</returns>
    public static bool TryReadOrderedJsonProperty<TOptions>(ref LuminPackJsonReader reader, int index, LuminPackSafeSchema schema) where TOptions : struct, ILuminPackOptions
    {
        var context = CurrentInput;
        var node = schema.Nodes[0];
        if ((uint)index >= (uint)node.Names.Length) return false;
        if (context == null || !context.Reading) return reader.TryReadPropertyName<TOptions>(node.JsonNames[index], node.Names[index].AsSpan());
        if (!reader.TryReadPropertyName<StreamingJsonOptions<TOptions>>(node.JsonNames[index], node.Names[index].AsSpan())) return false;
        CheckJsonPropertyIndex(index, schema);
        context.Budget.Work(node.Names[index].Length + 1L);
        ObserveJsonToken<TOptions>(ref reader);
        return true;
    }

    /// <summary>Allows the original fused trusted entry kernel only outside a selected external read.</summary>
    public static bool CanUseTrustedJsonEntry => CurrentInput == null || !CurrentInput.Reading;

    /// <summary>Checks the index produced by the original generated property matcher.</summary>
    /// <param name="index">The original zero-based member match.</param><param name="schema">The fixed member table.</param>
    /// <returns>The unchanged member index.</returns>
    public static int CheckJsonPropertyIndex(int index, LuminPackSafeSchema schema)
    {
        var context = CurrentInput;
        if (context == null || !context.Reading) return index;
        var node = schema.Nodes[0];
        if ((uint)index >= (uint)node.Names.Length) { Invalid("An unknown JSON member is outside the declared schema."); return index; }
        if (node.Names[index].Length > context.Budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
        ulong bit = 1UL << (index & 63);
        ref ulong seen = ref (index < 64 ? ref context.Seen0 : ref index < 128 ? ref context.Seen1 : ref index < 192 ? ref context.Seen2 : ref context.Seen3);
        if ((seen & bit) != 0) Invalid("A JSON object contains a duplicate member.");
        seen |= bit;
        return index;
    }

    /// <summary>Reads one original UTF-8 property token with strict decoding and no temporary string.</summary>
    /// <typeparam name="TOptions">The document profile.</typeparam><param name="reader">The original token reader.</param>
    /// <returns>The original decoded property bytes.</returns>
    public static ReadOnlySpan<byte> ReadJsonNameUtf8<TOptions>(ref LuminPackJsonReader reader) where TOptions : struct, ILuminPackOptions
    {
        if (CurrentInput == null || !CurrentInput.Reading) return reader.ReadStringUtf8<TOptions>();
        var bytes = reader.ReadStringUtf8<StreamingJsonOptions<TOptions>>();
        CheckInput(reader.Position, CurrentInput.Root!.Limits.MaxDecompressedBytes);
        var budget = CurrentInput.Budget;
        int length = InputUtf8.GetCharCount(bytes);
        if (length > budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
        int comparisons = reader.GrammarState == LuminPackJsonReader.JsonStateColon ? CurrentInput.Value?.Nodes[0].Names.Length ?? 0 : 0;
        budget.Work((bytes.Length + 1L) * (comparisons + 1L));
        return bytes;
    }

    /// <summary>Reads one original UTF-16 property token with strict Unicode and no temporary string.</summary>
    /// <typeparam name="TOptions">The document profile.</typeparam><param name="reader">The original token reader.</param>
    /// <returns>The original decoded property code units.</returns>
    public static ReadOnlySpan<char> ReadJsonNameUtf16<TOptions>(ref LuminPackJsonReader reader) where TOptions : struct, ILuminPackOptions
    {
        if (CurrentInput == null || !CurrentInput.Reading) return reader.ReadStringUtf16<TOptions>();
        var chars = reader.ReadStringUtf16<StreamingJsonOptions<TOptions>>();
        CheckInput(reader.Position, CurrentInput.Root!.Limits.MaxDecompressedBytes);
        var budget = CurrentInput.Budget;
        if (chars.Length > budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
        _ = InputUtf16.GetByteCount(chars);
        int comparisons = reader.GrammarState == LuminPackJsonReader.JsonStateColon ? CurrentInput.Value?.Nodes[0].Names.Length ?? 0 : 0;
        budget.Work((chars.Length * 2L + 1) * (comparisons + 1L));
        return chars;
    }

    /// <summary>Preserves the original next-float kernel with strict token accounting.</summary>
    /// <typeparam name="TOptions">The document profile.</typeparam><param name="reader">The original reader.</param>
    /// <returns>The decoded finite value.</returns>
    public static float ReadNextJsonFloat<TOptions>(ref LuminPackJsonReader reader) where TOptions : struct, ILuminPackOptions
    {
        if (CurrentInput == null || !CurrentInput.Reading) return reader.ReadNextFloatValue<TOptions>();
        if (!ReadJsonToken<TOptions>(ref reader)) Invalid("A JSON number is missing.");
        return ReadJsonValue<float, TOptions>(ref reader);
    }

    /// <summary>Rejects unrecognized external JSON members instead of using the trusted skip path.</summary>
    /// <typeparam name="TOptions">The fixed profile.</typeparam><param name="reader">The original reader.</param>
    public static void RejectUnknownJson<TOptions>(ref LuminPackJsonReader reader) where TOptions : struct, ILuminPackOptions
    {
        if (CurrentInput == null || !CurrentInput.Reading) reader.Skip<TOptions>();
        else Invalid("An unknown JSON member is outside the declared schema.");
    }

    /// <summary>Charges a completed original JSON scope without reparsing its document.</summary>
    /// <param name="reader">The original completed reader.</param><param name="scope">The matching value scope.</param>
    public static void CompleteValue(ref LuminPackJsonReader reader, ref ValueScope scope)
    {
        if (scope.Context != null && scope.Alias) { scope.CountAfter = scope.Context.Count; scope.UniqueCountAfter = scope.Context.UniqueCount; }
        if (scope.Context != null && reader.Depth > scope.Context.Root!.JsonDepth) SafeReadBudget.Fail("JSON container depth");
    }

    /// <summary>Accounts for each original selected token once in its typed scope.</summary>
    /// <typeparam name="TOptions">The exact document encoding.</typeparam><param name="reader">The selected token reader.</param>
    private static void ObserveJsonToken<TOptions>(ref LuminPackJsonReader reader) where TOptions : struct, ILuminPackOptions
    {
        var context = CurrentInput;
        if (context == null || !context.Reading || context.Value == null ||
            context.LastJsonPosition == reader.Position && context.LastJsonToken == (int)reader.CurrentTokenType) return;
        context.LastJsonPosition = reader.Position; context.LastJsonToken = (int)reader.CurrentTokenType;
        var node = context.Value.Nodes[0];
        CheckInput(reader.Position, context.Root!.Limits.MaxDecompressedBytes);
        if (reader.Depth > context.Root!.JsonDepth) SafeReadBudget.Fail("JSON container depth");
        context.Budget.Work(1);
        var token = reader.CurrentTokenType;
        if (node.Kind == LuminPackSafeKind.Dictionary) ObserveJsonPair(ref reader, context);
        if (node.Kind is LuminPackSafeKind.Sequence or LuminPackSafeKind.Queue or LuminPackSafeKind.Set or LuminPackSafeKind.Dictionary &&
            token is not (LuminPackJsonReader.JsonTokenType.ArrayEnd or LuminPackJsonReader.JsonTokenType.ObjectEnd) &&
            (reader.Depth == context.JsonDepth && token is not (LuminPackJsonReader.JsonTokenType.ArrayStart or LuminPackJsonReader.JsonTokenType.ObjectStart) ||
             reader.Depth == context.JsonDepth + 1 && token is LuminPackJsonReader.JsonTokenType.ArrayStart or LuminPackJsonReader.JsonTokenType.ObjectStart))
        {
            int count = checked(context.Count + 1);
            if (count > context.Budget.Limits.MaxCollectionLength) SafeReadBudget.Fail("collection length");
            int storage = 0; foreach (int child in node.Children) storage = checked(storage + context.Value.Nodes[child].Storage);
            context.Budget.Allocate(Math.Max(storage, 1) * 4L + 128);
            context.Budget.Depth(context.Depth + 1);
            if (context.UniqueRecord)
            { context.Budget.Work(2L * count - 1); context.UniqueCount = count; }
            context.Count = count;
        }
    }

    /// <summary>Checks dictionary entry arity as its original tokens are consumed.</summary>
    /// <param name="reader">The original strict reader.</param><param name="context">The active dictionary record.</param>
    private static void ObserveJsonPair(ref LuminPackJsonReader reader, InputContext context)
    {
        var token = reader.CurrentTokenType;
        bool start = token is LuminPackJsonReader.JsonTokenType.ArrayStart or LuminPackJsonReader.JsonTokenType.ObjectStart;
        bool end = token is LuminPackJsonReader.JsonTokenType.ArrayEnd or LuminPackJsonReader.JsonTokenType.ObjectEnd;
        if (context.JsonPairActive)
        {
            if (token == LuminPackJsonReader.JsonTokenType.ArrayEnd && reader.Depth == context.JsonDepth)
            {
                if (context.JsonPairElements != 2) Invalid("A dictionary entry must contain exactly two values.");
                context.JsonPairActive = false;
            }
            else if (!end && (start ? reader.Depth == context.JsonDepth + 2 : reader.Depth == context.JsonDepth + 1))
            {
                if (++context.JsonPairElements > 2) Invalid("A dictionary entry must contain exactly two values.");
                if (context.JsonPairElements == 1 && token == LuminPackJsonReader.JsonTokenType.Null) Invalid("A dictionary key is null.");
            }
            return;
        }
        if (end) return;
        if (start && reader.Depth == context.JsonDepth + 1)
        {
            if (token != LuminPackJsonReader.JsonTokenType.ArrayStart) Invalid("A dictionary entry must be a key/value array.");
            context.JsonPairActive = true; context.JsonPairElements = 0;
        }
        else if (!start && reader.Depth == context.JsonDepth) Invalid("A dictionary entry must be a key/value array.");
    }

    /// <summary>Uses the original escape decoder and checks its bounded text before application publication.</summary>
    /// <typeparam name="TOptions">The fixed document profile.</typeparam><param name="reader">The private token view.</param>
    /// <returns>The decoded token text.</returns>
    private static string ReadJsonText<TOptions>(ref LuminPackJsonReader reader) where TOptions : struct, ILuminPackOptions
    {
        if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
        {
            var bytes = reader.ReadStringUtf8<StreamingJsonOptions<TOptions>>();
            CheckInput(reader.Position, CurrentInput!.Root!.Limits.MaxDecompressedBytes);
            int count = InputUtf8.GetCharCount(bytes);
            ChargeJsonText(count);
            return LuminPackJsonReader.CreateUtf8String(bytes);
        }
        else
        {
            var chars = reader.ReadStringUtf16<StreamingJsonOptions<TOptions>>();
            CheckInput(reader.Position, CurrentInput!.Root!.Limits.MaxDecompressedBytes);
            ChargeJsonText(chars.Length);
            _ = InputUtf16.GetByteCount(chars);
            return new string(chars);
        }
    }

    /// <summary>Charges decoded text before allocating the original immutable string.</summary>
    /// <param name="length">The validated UTF-16 code-unit count.</param>
    private static void ChargeJsonText(int length)
    {
        var context = CurrentInput!;
        var budget = context.Budget;
        if (length > budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
        budget.Work(length);
        if (context.UniqueRecord && context.UniqueCount != 0)
        {
            budget.Work(2L * context.UniqueCount * length);
            budget.Work(2L * context.UniqueTextWork);
            context.UniqueTextWork = checked(context.UniqueTextWork + length);
        }
        if (length != 0) { budget.Object(); budget.Allocate(length * 4L + 128); }
    }

    /// <summary>Uses the original strict tokenizer with the compiled encoding and a bounded natural depth.</summary>
    /// <typeparam name="TOptions">The existing fixed document profile.</typeparam>
    internal readonly struct StreamingJsonOptions<TOptions> : ILuminPackOptions where TOptions : struct, ILuminPackOptions
    {
        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => default(TOptions).StringEncoding;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => default(TOptions).StringRecording;
        /// <inheritdoc/>
        public System.Buffers.StandardFormat StandardFormat => default(TOptions).StandardFormat;
        /// <inheritdoc/>
        public int MaxJsonDepth => default(TOptions).MaxJsonDepth > 0 ? Math.Min(64, default(TOptions).MaxJsonDepth) : 64;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => true;
    }
}
