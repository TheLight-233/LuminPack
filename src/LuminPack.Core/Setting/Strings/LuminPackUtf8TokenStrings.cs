using System.Buffers;
using System.Runtime.CompilerServices;
using LuminPack.Core;
using LuminPack.Utility;

namespace LuminPack.Option;

/// <summary>Implements UTF-8 strings with token framing.</summary>
public readonly struct LuminPackUtf8TokenStrings : ILuminPackStringCodec, ILuminPackStringReadLimit
{
    /// <inheritdoc/>
    StringLengthCheck ILuminPackStringReadLimit.Check =>
        static (scoped ref LuminPackReader reader, int maximum) => reader.CheckStringLength<byte, TokenStringFraming>(maximum);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult ReadJsonValue<TOperation, TResult>(scoped ref LuminPackJsonReader reader)
        where TOperation : struct, ILuminPackJsonReadOperation<TResult> =>
        default(TOperation).Read<byte>(ref reader);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteJsonValue<TOperation, TValue>(scoped ref LuminPackJsonWriter writer, TValue value)
        where TOperation : struct, ILuminPackJsonWriteOperation<TValue> =>
        default(TOperation).Write<byte>(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int JsonByteCount(int units) =>
        units;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadJsonUnit(scoped ref LuminPackJsonReader reader, out char value) =>
        reader.TryPeekJsonUnit<byte>(out value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteJsonAscii(scoped ref LuminPackJsonWriter writer, byte value) =>
        writer.WriteJsonAsciiByte(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        checked((value?.Length ?? 0) * 3 + 1);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value)
    {
        writer.CommitRecords(ref cursor);
        writer.WriteReservedUtf8TokenString(value);
        return ref writer.CurrentReference;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        writer.WriteUtf8TokenString(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        reader.ReadUtf8TokenString(current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        reader.SkipVersionTokenString<byte>(maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second)
    {
        reader.Read<LuminPackUtf8TokenOptions>(ref first);
        reader.Read<LuminPackUtf8TokenOptions>(ref second);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        reader.ReadStringSlots<LuminPackUtf8TokenOptions>(slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        StringSerializer.SerializeAsUtf8(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        checked((value is null ? 0 : LuminPack.Code.LuminPackMarshal.CalculateStringByteCount(value)) + 1);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        writer.WriteReservedUtf8TokenString(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        writer.WriteTextSnapshot<byte, TokenStringFraming>(text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        StringSerializer.DecodeUtf8(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        writer.WriteSlots<LuminPackUtf8TokenStrings, byte, TokenStringFraming>(values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        writer.WriteCheckedSlots<LuminPackUtf8TokenStrings, byte, TokenStringFraming>(values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        writer.WritePairs<LuminPackUtf8TokenStrings, byte, TokenStringFraming>(keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        writer.WritePairRuns<LuminPackUtf8TokenStrings, byte, TokenStringFraming>(keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref writer.WriteAtCursor<LuminPackUtf8TokenStrings, byte, TokenStringFraming>(ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count)
    {
        count = 0;
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1)
    {
        bytes = 0;
        latin1 = false;
        return false;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringEncoding.UTF8;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringRecording.Token;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1;
    }
}
