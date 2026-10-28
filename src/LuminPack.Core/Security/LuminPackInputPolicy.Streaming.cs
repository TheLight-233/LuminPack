using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Code;
using LuminPack.Utility;

namespace LuminPack.Security;

public static partial class LuminPackInputPolicy
{
    [ThreadStatic] private static InputContext? CurrentInput;
    [ThreadStatic] private static InputContext? IdleInput;
    [ThreadStatic] private static object? CurrentWriterOwner;

    /// <summary>Retains an exclusive synchronous root's budget without retaining consumer metadata after completion.</summary>
    internal sealed class InputContext
    {
        internal InputContext? Parent;
        internal object? Owner;
        internal LuminPackSafeSchema? Root;
        internal LuminPackSafeSchema? Value;
        internal readonly SafeReadBudget Budget = new SafeReadBudget(LuminPackReadLimits.Default);
        internal readonly SafeReadBudget DomainBudget = new SafeReadBudget(LuminPackReadLimits.Default);
        internal int Depth, Count, UniqueCount, JsonDepth, LastJsonPosition, LastJsonToken, FieldIndex, ValueStart;
        internal long UniqueTextWork;
        internal int JsonPairElements;
        internal bool JsonPairActive;
        internal int CompletedCollectionPosition;
        internal LuminPackSafeSchema? CompletedCollectionOwner;
        internal LuminPackSafeSchema? ValidatedUniqueSchema;
        internal int ValidatedUniqueStart;
        internal ulong Seen0, Seen1, Seen2, Seen3;
        internal bool Reading, MarkerSeen, UniqueRecord;
    }

    /// <summary>Owns one generated synchronous boundary and restores a reentrant parent on every exit.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct OperationScope
    {
        internal readonly InputContext? Context;
        internal readonly object? PreviousWriter;
        internal readonly bool Writing;
        /// <summary>Captures the exclusive synchronous root.</summary><param name="context">The owned root.</param>
        internal OperationScope(InputContext context) { Context = context; PreviousWriter = null; Writing = false; }
        /// <summary>Captures only writer ownership, without initializing read budgets.</summary><param name="previous">The enclosing writer.</param>
        internal OperationScope(object? previous) { Context = null; PreviousWriter = previous; Writing = true; }
        /// <summary>Whether this operation owns framing instead of sharing its enclosing root.</summary>
        public bool OwnsBoundary => Context != null || Writing;
    }

    /// <summary>Begins a bounded synchronous root while preserving nested and reentrant ownership.</summary>
    /// <param name="owner">The existing exclusive optional state.</param>
    /// <param name="schema">The selected root contract.</param>
    /// <param name="reading">Whether read budgets are active.</param>
    /// <returns>The boundary's exception-safe ownership token.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OperationScope BeginOperation(object owner, LuminPackSafeSchema schema, bool reading)
    {
        if (!reading)
        {
            if (ReferenceEquals(CurrentWriterOwner, owner)) return default;
            var previous = CurrentWriterOwner; CurrentWriterOwner = owner;
            return new OperationScope(previous);
        }
        var context = IdleInput ?? new InputContext();
        IdleInput = context.Parent;
        context.Parent = CurrentInput; context.Owner = owner; context.Root = schema;
        context.Value = null; context.Reading = reading; context.Depth = 0; context.Count = -1;
        context.UniqueCount = 0; context.LastJsonPosition = -1; context.LastJsonToken = -1; context.JsonDepth = 0;
        context.UniqueTextWork = 0;
        context.JsonPairElements = 0; context.JsonPairActive = false;
        context.CompletedCollectionPosition = -1; context.CompletedCollectionOwner = null;
        context.ValidatedUniqueSchema = null;
        context.Seen0 = context.Seen1 = context.Seen2 = context.Seen3 = 0;
        context.Budget.Reset(schema.Limits);
        CurrentInput = context;
        return new OperationScope(context);
    }

    /// <summary>Clears consumer state and restores the original synchronous root after success or failure.</summary>
    /// <param name="scope">The token returned by the matching begin.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EndOperation(OperationScope scope)
    {
        if (scope.Writing) { CurrentWriterOwner = scope.PreviousWriter; return; }
        var context = scope.Context;
        if (context == null) return;
        CurrentInput = context.Parent;
        context.Owner = null; context.Root = context.Value = context.CompletedCollectionOwner = context.ValidatedUniqueSchema = null; context.Reading = false;
        context.Budget.Reset(LuminPackReadLimits.Default);
        context.Parent = IdleInput; IdleInput = context;
    }

    /// <summary>Rejects legacy member-compressed operations outside the reviewed external envelope codec.</summary>
    public static void RejectLegacyInputCompression()
        => LuminPackExceptionHelper.ThrowNotSupportedException("External input supports bounded envelope compression, not legacy member-compressed operations.");

    /// <summary>Writes the canonical binary prefix directly into the existing writer.</summary>
    /// <param name="writer">The caller's original writer.</param>
    /// <param name="schema">The frozen selected protocol.</param>
    /// <returns>The absolute header position, retained across owner growth.</returns>
    public static int BeginWrite(ref LuminPackWriter writer, LuminPackSafeSchema schema)
    {
        schema.EnsureSupported();
        int start = writer.Position;
        writer.EnsureAdditionalCapacity(HeaderSize);
        writer.Write(ref MemoryMarshal.GetReference(schema.BinaryPrefix.AsSpan()), HeaderSize);
        writer.CheckBuffer();
        return start;
    }

    /// <summary>Patches lengths after the original formatter finishes, without rereading its payload.</summary>
    /// <param name="writer">The same writer, possibly grown by its owner.</param>
    /// <param name="schema">The output framing limits.</param>
    /// <param name="start">The absolute header position.</param>
    public static void EndWrite(ref LuminPackWriter writer, LuminPackSafeSchema schema, int start)
    {
        int length = checked(writer.Position - start - HeaderSize);
        CheckInput(length, schema.Limits.MaxDecompressedBytes);
        CheckInput((long)length + HeaderSize, schema.Limits.MaxInputBytes);
        BinaryPrimitives.WriteInt32LittleEndian(writer.Buffer.Slice(start + 12, 4), length);
        BinaryPrimitives.WriteInt32LittleEndian(writer.Buffer.Slice(start + 16, 4), length);
    }

    /// <summary>Writes the JSON envelope prefix around the original writer's value and separator state.</summary>
    /// <param name="writer">The original JSON writer.</param>
    /// <param name="schema">The fixed protocol encoding.</param>
    /// <returns>The absolute start used for output size limits.</returns>
    public static long BeginWriteJson(ref LuminPackJsonWriter writer, LuminPackSafeSchema schema)
    {
        schema.EnsureJsonSupported();
        long start = writer.CommittedBytes + writer.Position;
        if (!writer.IsFirstElement)
        {
            if (schema.JsonEncoding == 0) writer.WriteByteRaw<LuminPack.Option.LuminPackUtf8LengthOptions>((byte)',');
            else writer.WriteByteRaw<LuminPack.Option.LuminPackUtf16LengthOptions>((byte)',');
        }
        writer.WriteRaw(schema.JsonPrefix);
        writer.IsFirstElement = true;
        return start;
    }

    /// <summary>Completes an envelope after the original JSON writer has completed its value.</summary>
    /// <param name="writer">The same JSON writer.</param>
    /// <param name="schema">The fixed protocol encoding and output limits.</param>
    /// <param name="start">The absolute envelope start.</param>
    public static void EndWriteJson(ref LuminPackJsonWriter writer, LuminPackSafeSchema schema, long start)
    {
        if (schema.JsonEncoding == 0) writer.WriteByteRaw<LuminPack.Option.LuminPackUtf8LengthOptions>((byte)'}');
        else writer.WriteByteRaw<LuminPack.Option.LuminPackUtf16LengthOptions>((byte)'}');
        writer.IsFirstElement = false;
        CheckInput(writer.CommittedBytes + writer.Position - start, schema.Limits.MaxInputBytes);
    }

    /// <summary>Opens a bounded borrowed binary payload; only compressed input owns a decompression allocation.</summary>
    /// <param name="input">Caller-owned stable input.</param>
    /// <param name="schema">The expected schema and protocol.</param>
    /// <param name="consumed">The complete encoded frame width.</param>
    /// <param name="decoded">An owned decompression buffer, or null for borrowed raw input.</param>
    /// <returns>The bounded original-format payload.</returns>
    public static ReadOnlySpan<byte> OpenStreaming(ReadOnlySpan<byte> input, LuminPackSafeSchema schema, out int consumed, out byte[]? decoded)
    {
        schema.EnsureSupported(); decoded = null; consumed = 0;
        if (input.Length < HeaderSize) Invalid("The external-data header is truncated.");
        int encoded = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(12, 4));
        int length = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(16, 4));
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(8, 4));
        if (encoded < 0 || encoded > input.Length - HeaderSize || length < 0 || flags > 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(20, 4)) != 0 ||
            !input.Slice(0, 8).SequenceEqual(Magic) || !input.Slice(24, 32).SequenceEqual(schema.Fingerprint))
            Invalid("The external-data schema, flags or frame length is invalid.");
        consumed = HeaderSize + encoded;
        CheckInput(consumed, schema.Limits.MaxInputBytes);
        CheckInput(length, schema.Limits.MaxDecompressedBytes);
        var budget = CurrentInput!.Budget;
        budget.Allocate(4L * consumed + 4096); budget.Work(consumed);
        var payload = input.Slice(HeaderSize, encoded);
        if (flags == 0)
        {
            if (length != encoded) Invalid("An uncompressed frame must have matching lengths.");
            return payload;
        }
        if (LuminCompressor.GetDecompressedSize(payload) != length) Invalid("The compressed payload length disagrees with its envelope.");
        budget.Allocate(length + 24L); budget.Work(length);
        decoded = LuminCompressor.Decompress(payload);
        return decoded;
    }
}
