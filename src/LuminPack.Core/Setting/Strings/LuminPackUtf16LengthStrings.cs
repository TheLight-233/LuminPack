using System.Buffers;
using System.Runtime.CompilerServices;
using LuminPack.Core;
using LuminPack.Utility;

namespace LuminPack.Option;

/// <summary>Implements UTF-16 strings with fixed-length framing.</summary>
public readonly struct LuminPackUtf16LengthStrings : ILuminPackStringCodec, ILuminPackStringReadLimit
{
    /// <inheritdoc/>
    StringLengthCheck ILuminPackStringReadLimit.Check =>
        static (scoped ref LuminPackReader reader, int maximum) => reader.CheckStringLength<char, LengthStringFraming>(maximum);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult ReadJsonValue<TOperation, TResult>(scoped ref LuminPackJsonReader reader)
        where TOperation : struct, ILuminPackJsonReadOperation<TResult> =>
        default(TOperation).Read<char>(ref reader);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteJsonValue<TOperation, TValue>(scoped ref LuminPackJsonWriter writer, TValue value)
        where TOperation : struct, ILuminPackJsonWriteOperation<TValue> =>
        default(TOperation).Write<char>(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int JsonByteCount(int units) =>
        units * sizeof(char);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadJsonUnit(scoped ref LuminPackJsonReader reader, out char value) =>
        reader.TryPeekJsonUnit<char>(out value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteJsonAscii(scoped ref LuminPackJsonWriter writer, byte value) =>
        writer.WriteJsonAsciiChar(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        LuminPackWriter.Utf16RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref Unsafe.Add(ref cursor, (nint)(uint)LuminPackWriter.WriteReservedUtf16Record(ref cursor, value));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        writer.WriteUtf16LengthString(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        reader.ReadUtf16LengthString(current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        reader.SkipVersionLengthString<char>(maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        reader.ReadUtf16LengthPair(ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        reader.ReadUtf16LengthSlots(slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        StringSerializer.SerializeAsUtf16(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        checked((string.IsNullOrEmpty(value) ? 0 : StringSerializer.Utf16RecordPayloadLength(value)) + sizeof(int));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        writer.WriteReservedUtf16LengthString(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        writer.WriteTextSnapshot<char, LengthStringFraming>(text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        StringSerializer.DeserializeFromUtf16(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        writer.WriteSlots<LuminPackUtf16LengthStrings, char, LengthStringFraming>(values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        writer.WriteCheckedSlots<LuminPackUtf16LengthStrings, char, LengthStringFraming>(values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        writer.WritePairs<LuminPackUtf16LengthStrings, char, LengthStringFraming>(keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        writer.WritePairRuns<LuminPackUtf16LengthStrings, char, LengthStringFraming>(keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref writer.WriteAtCursor<LuminPackUtf16LengthStrings, char, LengthStringFraming>(ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        writer.WriteSlotPrefix(values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        writer.WritePairPrefix(pairs);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count) =>
        reader.TryPeekCharacterCount<char>(out count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        reader.TryPeekUtf16Payload(length, out bytes, out latin1);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringEncoding.UTF16;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LuminPackStringRecording.Length;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 2;
    }
}
