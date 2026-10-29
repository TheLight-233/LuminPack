using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Security;

public static partial class LuminPackInputPolicy
{
    private static readonly UTF8Encoding InputUtf8 = new UTF8Encoding(false, true);

    /// <summary>Restores the enclosing value's accounting after a nested original formatter completes.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ValueScope
    {
        internal readonly InputContext? Context;
        internal readonly LuminPackSafeSchema? Schema, Previous;
        internal readonly int Start, Depth, Count, UniqueCount, JsonDepth, LastJsonPosition, LastJsonToken;
        internal readonly ulong Seen0, Seen1, Seen2, Seen3;
        internal readonly bool MarkerSeen;
        internal readonly bool Alias, UniqueRecord;
        internal readonly int PreviousStart;
        internal readonly long UniqueTextWork;
        internal readonly int JsonPairElements;
        internal readonly bool JsonPairActive;
        internal int UniqueCountAfter;
        internal int CountAfter;
        internal int FieldIndex;
        /// <summary>Captures the enclosing value state without retaining an input address.</summary>
        /// <param name="context">The shared root.</param><param name="schema">The next value descriptor.</param><param name="start">The original cursor.</param>
        internal ValueScope(InputContext context, LuminPackSafeSchema schema, int start)
        {
            Context = context; Schema = schema; Previous = context.Value; Start = start; Depth = context.Depth;
            Count = context.Count; UniqueCount = context.UniqueCount; JsonDepth = context.JsonDepth; LastJsonPosition = context.LastJsonPosition;
            LastJsonToken = context.LastJsonToken;
            Seen0 = context.Seen0; Seen1 = context.Seen1; Seen2 = context.Seen2; Seen3 = context.Seen3; MarkerSeen = context.MarkerSeen;
            FieldIndex = context.FieldIndex;
            PreviousStart = context.ValueStart;
            Alias = context.Value != null && start == context.ValueStart && IsContainer(context.Value.Nodes[0].Kind) && IsContainer(schema.Nodes[0].Kind);
            UniqueRecord = context.UniqueRecord; UniqueCountAfter = context.UniqueCount; CountAfter = context.Count;
            UniqueTextWork = context.UniqueTextWork;
            JsonPairElements = context.JsonPairElements; JsonPairActive = context.JsonPairActive;
        }
    }

    /// <summary>Begins a binary value using the original checked Reader and a shared root budget.</summary>
    /// <param name="reader">The original Reader.</param><param name="schema">The value's emitted descriptor.</param>
    /// <returns>The nested value ownership token.</returns>
    public static ValueScope EnterValue(ref LuminPackReader reader, LuminPackSafeSchema schema)
    {
        var current = CurrentInput;
        if (current != null && current.Reading && current.Depth == 0 && schema.Nodes[0].Kind == LuminPackSafeKind.Object)
        {
            current.Budget.Depth(1); current.Depth = 1; current.Value = schema; current.ValueStart = reader.Position;
            current.Count = -1; current.MarkerSeen = false; current.FieldIndex = 0;
            return default;
        }
        var scope = EnterValueCore(schema, reader.Position);
        try
        {
            if (scope.Context != null && FixedWidth(schema, 0, out int depth, out bool validate) is int width && width >= 0)
            {
                scope.Context.Budget.Depth(scope.Context.Depth + depth - 1);
                reader.EnsureReadable(reader.Position, width);
                if (validate) ValidateFixed(reader.Buffer.Slice(reader.Position, width), schema);
            }
            return scope;
        }
        catch (Exception exception) { ExitValue(scope); LuminPackExceptionHelper.Rethrow(exception); return default; }
    }

    /// <summary>Begins one typed value without allocating temporary policy state.</summary>
    /// <param name="schema">The emitted value descriptor.</param><param name="start">The original cursor.</param>
    /// <returns>The value token or an inactive token outside a selected read.</returns>
    private static ValueScope EnterValueCore(LuminPackSafeSchema schema, int start)
    {
        var context = CurrentInput;
        if (context == null || !context.Reading) return default;
        var scope = new ValueScope(context, schema, start);
        context.Budget.Depth(context.Depth + (scope.Alias ? 0 : 1));
        context.Depth += scope.Alias ? 0 : 1;
        context.UniqueRecord = schema.Nodes[0].Kind is LuminPackSafeKind.Set or LuminPackSafeKind.Dictionary || scope.Alias && context.UniqueRecord;
        context.Value = schema; context.ValueStart = start; context.Count = -1; context.MarkerSeen = false; context.FieldIndex = 0;
        if (!scope.Alias) context.UniqueTextWork = 0;
        context.JsonPairElements = 0; context.JsonPairActive = false;
        context.Seen0 = context.Seen1 = context.Seen2 = context.Seen3 = 0;
        return scope;
    }

    /// <summary>Restores the enclosing scope even when validation or application callbacks fail.</summary>
    /// <param name="scope">The matching value token.</param>
    public static void ExitValue(ValueScope scope)
    {
        var context = scope.Context;
        if (context == null) return;
        context.Value = scope.Previous; context.Depth = scope.Depth; context.Count = scope.CountAfter;
        context.UniqueCount = scope.UniqueCountAfter; context.JsonDepth = scope.JsonDepth; context.LastJsonPosition = scope.LastJsonPosition;
        context.LastJsonToken = scope.LastJsonToken;
        context.UniqueRecord = scope.UniqueRecord; context.ValueStart = scope.PreviousStart;
        if (!scope.Alias) context.UniqueTextWork = scope.UniqueTextWork;
        context.JsonPairElements = scope.JsonPairElements; context.JsonPairActive = scope.JsonPairActive;
        context.FieldIndex = scope.FieldIndex;
        context.Seen0 = scope.Seen0; context.Seen1 = scope.Seen1; context.Seen2 = scope.Seen2; context.Seen3 = scope.Seen3; context.MarkerSeen = scope.MarkerSeen;
    }

    /// <summary>Reads the actual collection header once and charges storage before original allocation.</summary>
    /// <param name="reader">The original Reader.</param><param name="minimumElementSize">The original minimum wire width.</param>
    /// <param name="schema">The actual collection descriptor, when its count owns a flat sequence check.</param>
    /// <returns>The checked original count.</returns>
    public static int ReadLength(ref LuminPackReader reader, int minimumElementSize = 1, LuminPackSafeSchema? schema = null)
    {
        int count = reader.ReadLength(minimumElementSize);
        ChargeLength(ref reader, count, schema, reader.Position);
        return count;
    }

    /// <summary>Preserves nonadvancing portable headers while charging their actual captured count.</summary>
    /// <param name="reader">The original reader.</param><param name="length">The captured count.</param>
    /// <param name="minimumElementSize">The original minimum width.</param><param name="schema">The collection descriptor.</param>
    /// <returns>Whether the original header is nonnull.</returns>
    public static bool PeekCollectionHeader(ref LuminPackReader reader, out int length, int minimumElementSize, LuminPackSafeSchema schema)
    {
        bool present = reader.PeekCollectionHeader(out length, minimumElementSize);
        ChargeLength(ref reader, length, schema, checked(reader.Position + 4));
        return present;
    }

    /// <summary>Charges the same captured header for advancing and nonadvancing original paths.</summary>
    /// <param name="reader">The original reader.</param><param name="count">The captured count.</param>
    /// <param name="schema">The actual descriptor.</param><param name="payloadStart">The checked header end.</param>
    private static void ChargeLength(ref LuminPackReader reader, int count, LuminPackSafeSchema? schema, int payloadStart)
    {
        var context = CurrentInput;
        bool alias = context?.Value != null && IsContainer(context.Value.Nodes[0].Kind) && context.ValueStart == payloadStart - 4;
        if (context != null && context.Reading && schema != null && !ReferenceEquals(context.Value, schema) && !alias && schema.Nodes[0].Kind == LuminPackSafeKind.Sequence)
        {
            if (count >= 0)
            {
                var node = schema.Nodes[0];
                int child = node.Children[0];
                context.Budget.Collection(count, schema.Nodes[child].Storage);
                int width = FixedWidth(schema, child, out int depth, out bool validate);
                context.Budget.Depth(context.Depth + 1 + (count == 0 ? 0 : depth));
                if (width >= 0 && validate)
                    ValidateFixed(reader.Buffer.Slice(payloadStart - 4, checked(4 + count * width)), schema);
            }
        }
        else ObserveCount(count);
        if (count > 0 && context?.Reading == true && schema?.Nodes[0].Kind == LuminPackSafeKind.Queue)
        {
            reader.EnsureReadable(payloadStart, 12);
            var metadata = reader.Buffer.Slice(payloadStart, 12);
            if (System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(metadata) != 0 ||
                System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(metadata.Slice(4)) != 0 ||
                System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(metadata.Slice(8)) != count)
                Invalid("The canonical queue storage header is invalid.");
        }
    }

    /// <summary>Preserves the original collection-head API while charging the consumed count.</summary>
    /// <param name="reader">The original Reader.</param><param name="length">The consumed count.</param>
    /// <param name="minimumElementSize">The original minimum wire width.</param><param name="schema">The actual collection descriptor.</param>
    /// <returns>Whether the record is nonnull.</returns>
    public static bool TryReadCollectionHead(ref LuminPackReader reader, out int length, int minimumElementSize = 1, LuminPackSafeSchema? schema = null)
    { length = ReadLength(ref reader, minimumElementSize, schema); return length >= 0; }

    /// <summary>Completes a flat sequence using its original reader and following fixed field batch.</summary>
    /// <param name="reader">The original completed reader.</param><param name="schema">The sequence descriptor.</param>
    public static void CompleteDetachedCollection(ref LuminPackReader reader, LuminPackSafeSchema schema)
    {
        var context = CurrentInput;
        if (context?.Reading == true && context.Value?.Nodes[0].Kind == LuminPackSafeKind.Object)
        {
            if (context.CompletedCollectionPosition == reader.Position && ReferenceEquals(context.CompletedCollectionOwner, context.Value)) return;
            context.CompletedCollectionPosition = reader.Position; context.CompletedCollectionOwner = context.Value;
            context.FieldIndex = ValidateFixedPrefix(ref reader, context.Value, context.FieldIndex + 1, context.Depth);
        }
    }

    /// <summary>Charges one original collection header before allocation or hash reconstruction.</summary>
    /// <param name="count">The actual consumed count.</param>
    private static void ObserveCount(int count)
    {
        var context = CurrentInput;
        if (context == null || !context.Reading || context.Value == null || context.Count >= 0) return;
        context.Count = count;
        if (count < 0) return;
        var node = context.Value.Nodes[0];
        int storage = 0;
        foreach (int child in node.Children) storage = checked(storage + context.Value.Nodes[child].Storage);
        context.Budget.Collection(count, storage);
        if (count != 0) context.Budget.Depth(context.Depth + 1);
        if (context.UniqueRecord)
        { context.Budget.Work((long)count * count); context.UniqueCount = count; }
    }

    /// <summary>Checks object and nullable markers at their actual consumed position.</summary>
    /// <param name="reader">The original checked Reader.</param><returns>The original marker.</returns>
    public static byte ReadMarker(ref LuminPackReader reader)
    {
        byte marker = reader.Read<byte>();
        CheckMarker(ref reader, marker, reader.Position);
        return marker;
    }

    /// <summary>Checks a nonadvancing object marker while retaining the original later checked Advance.</summary>
    /// <param name="reader">The original reader.</param><returns>Whether the value is nonnull.</returns>
    public static bool PeekObjectHeader(ref LuminPackReader reader)
    {
        reader.EnsureReadable(reader.Position, 1);
        byte marker = reader.Buffer[reader.Position];
        CheckMarker(ref reader, marker, reader.Position + 1);
        return marker != 255;
    }

    /// <summary>Preserves advancing object-header consumption.</summary>
    /// <param name="reader">The original reader.</param><returns>Whether the value is nonnull.</returns>
    public static bool TryReadObjectHead(ref LuminPackReader reader) => ReadMarker(ref reader) != 255;

    /// <summary>Preserves advancing object-header consumption and its captured member marker.</summary>
    /// <param name="reader">The original reader.</param><param name="marker">The captured marker.</param>
    /// <returns>Whether the value is nonnull.</returns>
    public static bool TryReadObjectHead(ref LuminPackReader reader, out byte marker)
    { marker = ReadMarker(ref reader); return marker != 255; }

    /// <summary>Checks the one captured object, nullable or array marker.</summary>
    /// <param name="reader">The original reader.</param><param name="marker">The captured marker.</param>
    /// <param name="payloadStart">The start of the following fields.</param>
    private static void CheckMarker(ref LuminPackReader reader, byte marker, int payloadStart)
    {
        var context = CurrentInput;
        if (context == null || !context.Reading || context.Value == null || context.MarkerSeen) return;
        var node = context.Value.Nodes[0];
        if (node.Kind is not (LuminPackSafeKind.Object or LuminPackSafeKind.Nullable or LuminPackSafeKind.MultiArray)) return;
        context.MarkerSeen = true;
        if (node.Kind == LuminPackSafeKind.Object)
        {
            if (marker == 255 && node.Rank == 0) return;
            if (marker != node.Children.Length) Invalid("The object member header disagrees with its schema.");
            if (node.Rank == 0) { context.Budget.Object(); context.Budget.Allocate(node.Storage + 64L); }
            if (node.Children.Length != 0) context.Budget.Depth(context.Depth + 1);
            context.FieldIndex = ValidateFixedPrefix(ref reader, context.Value, 0, context.Depth, payloadStart);
        }
        else if (marker != 0 && marker != 255) Invalid("The nullable or array marker is invalid.");
        if (marker == 0 && node.Kind == LuminPackSafeKind.Nullable)
        {
            int child = node.Children[0];
            int width = FixedWidth(context.Value, child, out int depth, out bool validate);
            context.Budget.Depth(context.Depth + depth);
            if (width >= 0)
            {
                reader.EnsureReadable(payloadStart, width);
                if (validate)
                {
                    context.DomainBudget.Reset(UnlimitedDomainLimits);
                    SafeBinaryValidator.Validate(reader.Buffer.Slice(payloadStart, width), context.Value, context.DomainBudget, child);
                }
            }
        }
        if (marker != 255 && node.Kind == LuminPackSafeKind.MultiArray)
        {
            reader.EnsureReadable(payloadStart, checked(node.Rank * 4));
            for (int i = 0; i < node.Rank; i++)
            {
                int dimension = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(reader.Buffer.Slice(payloadStart + i * 4, 4));
                if (dimension < 0 || dimension > context.Budget.Limits.MaxCollectionLength) SafeReadBudget.Fail("array dimension");
            }
        }
    }

    /// <summary>Reads a bounded string using the existing payload decoder and immutable reuse kernels.</summary>
    /// <typeparam name="TOptions">The exact original binary string profile.</typeparam>
    /// <param name="reader">The original advancing Reader.</param><param name="current">The existing candidate string.</param>
    /// <returns>The original reused or decoded value.</returns>
    public static string? ReadString<TOptions>(ref LuminPackReader reader, string? current) where TOptions : struct, ILuminPackOptions
    {
        var context = CurrentInput;
        if (context == null || !context.Reading) return reader.Read<TOptions>(current);
        bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
        bool token = default(TOptions).StringRecording == LuminPackStringRecording.Token;
        int start = reader.Position, header = token ? 0 : utf8 ? 8 : 4, bytes, characters;
        bool latin1 = false;
        if (token)
        {
            var remaining = reader.Buffer.Slice(start);
            int units = utf8 ? remaining.IndexOf((byte)0) : MemoryMarshal.Cast<byte, ushort>(remaining.Slice(0, remaining.Length & ~1)).IndexOf((ushort)0);
            if (units < 0) Invalid("A string token terminator is missing.");
            bytes = checked(units * (utf8 ? 1 : 2));
            characters = utf8 ? InputUtf8.GetCharCount(remaining.Slice(0, bytes)) : units;
        }
        else
        {
            if (default(TOptions).StringRecording == LuminPackStringRecording.VarInt)
                LuminPackReader.ReadVarIntRecordHeader<TOptions>(reader.Buffer.Slice(start), out header, out bytes, out characters, out latin1);
            else
            {
                reader.EnsureReadable(start, header);
                var first = reader.Buffer.Slice(start, header);
                int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(first);
                if (length < -1) Invalid("A string length is negative.");
                if (utf8)
                {
                    bytes = length;
                    characters = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(first.Slice(4));
                    if (length != -1 && characters < 0) Invalid("A string character count is negative.");
                }
                else { latin1 = (length & 1) != 0; characters = length >> 1; bytes = latin1 ? characters : length; }
            }
            if (bytes == -1)
            {
                reader.Advance(header);
                if (context.Value?.Nodes[0].Kind == LuminPackSafeKind.Object)
                    context.FieldIndex = ValidateFixedPrefix(ref reader, context.Value, context.FieldIndex + 1, context.Depth);
                return null;
            }
        }
        reader.EnsureReadable(start + header, checked(bytes + (token ? utf8 ? 1 : 2 : 0)));
        var payload = reader.Buffer.Slice(start + header, bytes);
        if (utf8)
        {
            int actual = InputUtf8.GetCharCount(payload);
            if (!token && actual != characters) Invalid("UTF-8 byte and character counts disagree.");
            characters = actual;
        }
        if (characters > context.Budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
        context.Budget.Depth(context.Depth + (context.Value?.Nodes[0].Kind == LuminPackSafeKind.String ? 0 : 1));
        context.Budget.Work(bytes + (long)header);
        if (context.UniqueCount != 0) context.Budget.Work(2L * context.UniqueCount * characters);
        if (characters != 0) { context.Budget.Object(); context.Budget.Allocate(characters * 4L + 128); }
        if (!utf8 && !latin1) ValidateUnicode(payload);
        string result;
        if (bytes == 0) result = string.Empty;
        else if (utf8)
            result = current != null && current.Length == characters && StringSerializer.MatchesUtf8(payload, current) ? current : StringSerializer.DeserializeUtf8(ref MemoryMarshal.GetReference(payload), bytes, characters);
        else if (latin1) result = StringSerializer.ReadLatin1PayloadCore(ref MemoryMarshal.GetReference(payload), bytes, current);
        else result = StringSerializer.ReadUtf16Payload(ref MemoryMarshal.GetReference(payload), bytes, current);
        reader.Advance(checked(header + bytes + (token ? utf8 ? 1 : 2 : 0)));
        if (context.Value?.Nodes[0].Kind == LuminPackSafeKind.Object)
            context.FieldIndex = ValidateFixedPrefix(ref reader, context.Value, context.FieldIndex + 1, context.Depth);
        return result;
    }

    /// <summary>Preserves ref-string publication through the original bounded payload reader.</summary>
    /// <typeparam name="TOptions">The fixed binary profile.</typeparam><param name="reader">The original reader.</param>
    /// <param name="value">The caller's immutable candidate, replaced only after successful decoding.</param>
    public static void ReadStringRecord<TOptions>(ref LuminPackReader reader, ref string? value) where TOptions : struct, ILuminPackOptions
        => value = ReadString<TOptions>(ref reader, value);

    /// <summary>Validates only potential surrogate runs through the established strict scalar fallback.</summary>
    /// <param name="bytes">The already bounded UTF-16 bytes.</param>
    private static void ValidateUnicode(ReadOnlySpan<byte> bytes)
    {
        if ((bytes.Length & 1) != 0) Invalid("A UTF-16 payload has an incomplete code unit.");
#if NET8_0_OR_GREATER
        int surrogate = MemoryMarshal.Cast<byte, char>(bytes).IndexOfAnyInRange('\uD800', '\uDFFF');
        if (surrogate < 0) return;
        bytes = bytes.Slice(surrogate * 2);
#endif
        SafeBinaryValidator.ValidateUtf16(bytes);
    }

    /// <summary>Checks domains of bounded raw fixed-width collection payloads after the original bulk read.</summary>
    /// <param name="reader">The original completed Reader.</param><param name="scope">The typed value scope.</param>
    public static void CompleteValue(ref LuminPackReader reader, ref ValueScope scope)
    {
        if (scope.Context == null || scope.Schema == null) return;
        if (scope.Alias) { scope.CountAfter = scope.Context.Count; scope.UniqueCountAfter = scope.Context.UniqueCount; }
        var node = scope.Schema.Nodes[0];
        if (node.Kind is LuminPackSafeKind.Sequence or LuminPackSafeKind.Queue or LuminPackSafeKind.MultiArray or LuminPackSafeKind.Set && node.Children.Length == 1 &&
            FixedWidth(scope.Schema, node.Children[0], out _, out bool validate) >= 0 && validate)
        {
            ValidateFixed(reader.Buffer.Slice(scope.Start, reader.Position - scope.Start), scope.Schema);
            if (node.Kind == LuminPackSafeKind.Set) { scope.Context.ValidatedUniqueSchema = scope.Schema; scope.Context.ValidatedUniqueStart = scope.Start; }
        }
        if (node.Kind == LuminPackSafeKind.Dictionary && node.Children.Length == 2 &&
            FixedWidth(scope.Schema, node.Children[0], out _, out bool keyDomain) >= 0 &&
            FixedWidth(scope.Schema, node.Children[1], out _, out bool valueDomain) >= 0 && (keyDomain || valueDomain))
        {
            ValidateFixed(reader.Buffer.Slice(scope.Start, reader.Position - scope.Start), scope.Schema);
            scope.Context.ValidatedUniqueSchema = scope.Schema; scope.Context.ValidatedUniqueStart = scope.Start;
        }
        if (scope.Previous?.Nodes[0].Kind == LuminPackSafeKind.Object)
            scope.FieldIndex = ValidateFixedPrefix(ref reader, scope.Previous, scope.FieldIndex + 1, scope.Depth);
    }

    /// <summary>Rejects duplicate values in the original completed standard set without a payload prepass.</summary>
    /// <typeparam name="T">The reviewed framework element.</typeparam><param name="values">The original result.</param>
    /// <param name="scope">The count and shared accounting scope.</param>
    public static void CompleteSet<T>(IEnumerable<T>? values, ValueScope scope)
    {
        if (scope.Context == null || values == null || scope.Context.Count < 0) return;
        if (ReferenceEquals(scope.Context.ValidatedUniqueSchema, scope.Schema) && scope.Context.ValidatedUniqueStart == scope.Start) return;
        var seen = new HashSet<T>(); int count = 0;
        foreach (var value in values)
        { scope.Context.Budget.Work(1); if (!seen.Add(value)) Invalid("A set contains duplicate elements."); count++; }
        if (count != scope.Context.Count) Invalid("A set changed its encoded element count.");
    }

    /// <summary>Rejects duplicate or null keys in the original completed standard map.</summary>
    /// <typeparam name="TKey">The reviewed key.</typeparam><typeparam name="TValue">The original value type.</typeparam>
    /// <param name="values">The original result.</param><param name="scope">The shared value scope.</param>
    public static void CompleteMap<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>>? values, ValueScope scope)
    {
        if (scope.Context == null || values == null || scope.Context.Count < 0) return;
        if (ReferenceEquals(scope.Context.ValidatedUniqueSchema, scope.Schema) && scope.Context.ValidatedUniqueStart == scope.Start) return;
        var seen = new HashSet<TKey>(); int count = 0;
        foreach (var value in values)
        { scope.Context.Budget.Work(1); if (value.Key is null || !seen.Add(value.Key)) Invalid("A dictionary contains null or duplicate keys."); count++; }
        if (count != scope.Context.Count) Invalid("A dictionary changed its encoded element count.");
    }

    /// <summary>Gets the complete padding-free width of a fixed value and whether its bits require domain checks.</summary>
    /// <param name="schema">The descriptor.</param><param name="index">The node index.</param>
    /// <param name="depth">The nested fixed value depth.</param><param name="validate">Whether semantic checks are required.</param>
    /// <returns>The fixed width, or minus one for variable records.</returns>
    private static int FixedWidth(LuminPackSafeSchema schema, int index, out int depth, out bool validate)
    {
        var node = schema.Nodes[index]; depth = 1; validate = false;
        if (node.Kind == LuminPackSafeKind.Struct)
        {
            int width = 0;
            foreach (int child in node.Children)
            {
                int size = FixedWidth(schema, child, out int childDepth, out bool childValidate);
                if (size < 0) return -1;
                width = checked(width + size); depth = Math.Max(depth, childDepth + 1); validate |= childValidate;
            }
            return width;
        }
        if (node.Kind is LuminPackSafeKind.Boolean or LuminPackSafeKind.Enum or LuminPackSafeKind.Decimal or LuminPackSafeKind.DateTime) validate = true;
        if (node.Kind is >= LuminPackSafeKind.Boolean and <= LuminPackSafeKind.Char || node.Kind is LuminPackSafeKind.Guid or LuminPackSafeKind.TimeSpan || validate)
            return node.Kind == LuminPackSafeKind.Enum ? node.Rank : node.Storage;
        return -1;
    }

    /// <summary>Reuses a bounded fixed-value validator without charging the already budgeted allocation twice.</summary>
    /// <param name="payload">The already bounded original wire bytes.</param><param name="schema">The exact fixed contract.</param>
    private static void ValidateFixed(ReadOnlySpan<byte> payload, LuminPackSafeSchema schema)
    {
        var context = CurrentInput!;
        context.DomainBudget.Reset(UnlimitedDomainLimits);
        SafeBinaryValidator.Validate(payload, schema, context.DomainBudget);
    }

    /// <summary>Checks semantic domains of only the immediately upcoming fixed field batch.</summary>
    /// <param name="reader">The original Reader at the batch boundary.</param><param name="schema">The enclosing object.</param>
    /// <param name="index">The next declared field.</param><param name="depth">The enclosing value depth.</param>
    /// <param name="start">An explicit batch start for nonadvancing marker paths.</param>
    /// <returns>The first subsequent variable field, without consuming any original data.</returns>
    private static int ValidateFixedPrefix(ref LuminPackReader reader, LuminPackSafeSchema schema, int index, int depth, int start = -1)
    {
        var context = CurrentInput!;
        var node = schema.Nodes[0];
        int offset = start < 0 ? reader.Position : start;
        for (; index < node.Children.Length; index++)
        {
            int child = node.Children[index];
            int width = FixedWidth(schema, child, out int childDepth, out bool validate);
            if (width < 0) break;
            context.Budget.Depth(depth + childDepth);
            reader.EnsureReadable(offset, width);
            if (validate)
            {
                context.DomainBudget.Reset(UnlimitedDomainLimits);
                SafeBinaryValidator.Validate(reader.Buffer.Slice(offset, width), schema, context.DomainBudget, child);
            }
            offset = checked(offset + width);
        }
        return index;
    }

    private static readonly LuminPackReadLimits UnlimitedDomainLimits = new LuminPackReadLimits(int.MaxValue, int.MaxValue, long.MaxValue,
        int.MaxValue, int.MaxValue, int.MaxValue, 64, long.MaxValue);

    /// <summary>Rejects trailing data before returning an incompletely consumed framed value.</summary>
    public static void RejectIncompleteValue() => Invalid("The schema did not consume the complete framed value.");

    /// <summary>Identifies records whose generated wrappers can share the same collection header.</summary>
    /// <param name="kind">The compiled wire kind.</param><returns>Whether this is a collection record.</returns>
    private static bool IsContainer(LuminPackSafeKind kind) => kind is LuminPackSafeKind.Sequence or LuminPackSafeKind.Set or LuminPackSafeKind.Dictionary or LuminPackSafeKind.Queue or LuminPackSafeKind.MultiArray;
}
