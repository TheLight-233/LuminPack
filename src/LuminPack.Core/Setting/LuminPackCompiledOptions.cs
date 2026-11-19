using System.Buffers;
using LuminPack.Core;
using System.Runtime.CompilerServices;

namespace LuminPack.Option;

/// <summary>Selects each known type's declared profile inside the generated generic dispatch.</summary>
/// <remarks>This marker is used by generated forwarding methods; it is never a mutable operation configuration.</remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public readonly struct LuminPackDeclaredOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        default(LuminPackUtf16LengthStrings).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf16LengthStrings).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf16LengthStrings).Write(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        default(LuminPackUtf16LengthStrings).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        default(LuminPackUtf16LengthStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(LuminPackUtf16LengthStrings).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        default(LuminPackUtf16LengthStrings).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf16LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        default(LuminPackUtf16LengthStrings).Measure(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf16LengthStrings).WriteReserved(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        default(LuminPackUtf16LengthStrings).WriteText(ref writer, text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf16LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        default(LuminPackUtf16LengthStrings).SupportsUtf16Runs();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16LengthStrings).WriteSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16LengthStrings).WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16LengthStrings).WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        default(LuminPackUtf16LengthStrings).WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf16LengthStrings).WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16LengthStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        default(LuminPackUtf16LengthStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count) =>
        default(LuminPackUtf16LengthStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(LuminPackUtf16LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).Encoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).Recording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}

/// <summary>Provides the fixed UTF-8 length profile for generated or caller-authored kernels.</summary>
public readonly struct LuminPackUtf8LengthOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        default(LuminPackUtf8LengthStrings).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf8LengthStrings).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf8LengthStrings).Write(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        default(LuminPackUtf8LengthStrings).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        default(LuminPackUtf8LengthStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(LuminPackUtf8LengthStrings).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        default(LuminPackUtf8LengthStrings).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf8LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        default(LuminPackUtf8LengthStrings).Measure(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf8LengthStrings).WriteReserved(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        default(LuminPackUtf8LengthStrings).WriteText(ref writer, text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf8LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        default(LuminPackUtf8LengthStrings).SupportsUtf16Runs();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8LengthStrings).WriteSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8LengthStrings).WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8LengthStrings).WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        default(LuminPackUtf8LengthStrings).WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf8LengthStrings).WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8LengthStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        default(LuminPackUtf8LengthStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count) =>
        default(LuminPackUtf8LengthStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(LuminPackUtf8LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8LengthStrings).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8LengthStrings).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8LengthStrings).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8LengthStrings).Encoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8LengthStrings).Recording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}

/// <summary>Provides the fixed UTF-8 token profile for generated or caller-authored kernels.</summary>
public readonly struct LuminPackUtf8TokenOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        default(LuminPackUtf8TokenStrings).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf8TokenStrings).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf8TokenStrings).Write(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        default(LuminPackUtf8TokenStrings).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        default(LuminPackUtf8TokenStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(LuminPackUtf8TokenStrings).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        default(LuminPackUtf8TokenStrings).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf8TokenStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        default(LuminPackUtf8TokenStrings).Measure(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf8TokenStrings).WriteReserved(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        default(LuminPackUtf8TokenStrings).WriteText(ref writer, text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf8TokenStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        default(LuminPackUtf8TokenStrings).SupportsUtf16Runs();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8TokenStrings).WriteSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8TokenStrings).WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8TokenStrings).WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        default(LuminPackUtf8TokenStrings).WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf8TokenStrings).WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8TokenStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        default(LuminPackUtf8TokenStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count) =>
        default(LuminPackUtf8TokenStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(LuminPackUtf8TokenStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8TokenStrings).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8TokenStrings).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8TokenStrings).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8TokenStrings).Encoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8TokenStrings).Recording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}

/// <summary>Provides the fixed UTF-16 length profile for generated or caller-authored kernels.</summary>
public readonly struct LuminPackUtf16LengthOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        default(LuminPackUtf16LengthStrings).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf16LengthStrings).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf16LengthStrings).Write(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        default(LuminPackUtf16LengthStrings).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        default(LuminPackUtf16LengthStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(LuminPackUtf16LengthStrings).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        default(LuminPackUtf16LengthStrings).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf16LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        default(LuminPackUtf16LengthStrings).Measure(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf16LengthStrings).WriteReserved(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        default(LuminPackUtf16LengthStrings).WriteText(ref writer, text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf16LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        default(LuminPackUtf16LengthStrings).SupportsUtf16Runs();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16LengthStrings).WriteSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16LengthStrings).WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16LengthStrings).WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        default(LuminPackUtf16LengthStrings).WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf16LengthStrings).WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16LengthStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        default(LuminPackUtf16LengthStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count) =>
        default(LuminPackUtf16LengthStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(LuminPackUtf16LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).Encoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16LengthStrings).Recording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}

/// <summary>Provides the fixed UTF-16 token profile for generated or caller-authored kernels.</summary>
public readonly struct LuminPackUtf16TokenOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        default(LuminPackUtf16TokenStrings).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf16TokenStrings).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf16TokenStrings).Write(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        default(LuminPackUtf16TokenStrings).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        default(LuminPackUtf16TokenStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(LuminPackUtf16TokenStrings).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        default(LuminPackUtf16TokenStrings).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf16TokenStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        default(LuminPackUtf16TokenStrings).Measure(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf16TokenStrings).WriteReserved(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        default(LuminPackUtf16TokenStrings).WriteText(ref writer, text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf16TokenStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        default(LuminPackUtf16TokenStrings).SupportsUtf16Runs();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16TokenStrings).WriteSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16TokenStrings).WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16TokenStrings).WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        default(LuminPackUtf16TokenStrings).WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf16TokenStrings).WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16TokenStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        default(LuminPackUtf16TokenStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count) =>
        default(LuminPackUtf16TokenStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(LuminPackUtf16TokenStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16TokenStrings).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16TokenStrings).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16TokenStrings).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16TokenStrings).Encoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16TokenStrings).Recording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}

/// <summary>Provides opt-in base-128 UTF-8 length headers with distinct null and empty records.</summary>
public readonly struct LuminPackUtf8VarIntOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        default(LuminPackUtf8VarIntStrings).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf8VarIntStrings).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf8VarIntStrings).Write(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        default(LuminPackUtf8VarIntStrings).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        default(LuminPackUtf8VarIntStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(LuminPackUtf8VarIntStrings).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        default(LuminPackUtf8VarIntStrings).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf8VarIntStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        default(LuminPackUtf8VarIntStrings).Measure(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf8VarIntStrings).WriteReserved(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        default(LuminPackUtf8VarIntStrings).WriteText(ref writer, text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf8VarIntStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        default(LuminPackUtf8VarIntStrings).SupportsUtf16Runs();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8VarIntStrings).WriteSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8VarIntStrings).WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8VarIntStrings).WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        default(LuminPackUtf8VarIntStrings).WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf8VarIntStrings).WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf8VarIntStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        default(LuminPackUtf8VarIntStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count) =>
        default(LuminPackUtf8VarIntStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(LuminPackUtf8VarIntStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8VarIntStrings).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8VarIntStrings).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8VarIntStrings).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8VarIntStrings).Encoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf8VarIntStrings).Recording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}

/// <summary>Provides opt-in base-128 UTF-16 length headers while retaining compact Latin-1 payloads.</summary>
public readonly struct LuminPackUtf16VarIntOptions : ILuminPackOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        default(LuminPackUtf16VarIntStrings).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf16VarIntStrings).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf16VarIntStrings).Write(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        default(LuminPackUtf16VarIntStrings).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        default(LuminPackUtf16VarIntStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(LuminPackUtf16VarIntStrings).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        default(LuminPackUtf16VarIntStrings).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf16VarIntStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        default(LuminPackUtf16VarIntStrings).Measure(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        default(LuminPackUtf16VarIntStrings).WriteReserved(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        default(LuminPackUtf16VarIntStrings).WriteText(ref writer, text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(LuminPackUtf16VarIntStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        default(LuminPackUtf16VarIntStrings).SupportsUtf16Runs();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16VarIntStrings).WriteSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16VarIntStrings).WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16VarIntStrings).WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        default(LuminPackUtf16VarIntStrings).WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(LuminPackUtf16VarIntStrings).WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(LuminPackUtf16VarIntStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        default(LuminPackUtf16VarIntStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count) =>
        default(LuminPackUtf16VarIntStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(LuminPackUtf16VarIntStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16VarIntStrings).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16VarIntStrings).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16VarIntStrings).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16VarIntStrings).Encoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(LuminPackUtf16VarIntStrings).Recording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}

/// <summary>Supplies a stateless standard-format constant.</summary>
public interface ILuminPackFormatOptions
{
    /// <summary>Gets the selected format.</summary>
    StandardFormat Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
}

/// <summary>Supplies a stateless JSON depth constant.</summary>
public interface ILuminPackDepthOptions
{
    /// <summary>Gets the maximum depth.</summary>
    int Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
}

/// <summary>Supplies a stateless JSON grammar policy.</summary>
public interface ILuminPackValidationOptions
{
    /// <summary>Gets whether strict grammar is required.</summary>
    bool Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
}

/// <summary>Provides the standard general format.</summary>
public readonly struct LuminPackGeneralFormat : ILuminPackFormatOptions
{
    /// <inheritdoc/>
    public StandardFormat Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new('G');
    }
}

/// <summary>Provides the default maximum JSON depth.</summary>
public readonly struct LuminPackDefaultDepth : ILuminPackDepthOptions
{
    /// <inheritdoc/>
    public int Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1024;
    }
}

/// <summary>Selects the established trusted JSON parser.</summary>
public readonly struct LuminPackTrustedJson : ILuminPackValidationOptions
{
    /// <inheritdoc/>
    public bool Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }
}

/// <summary>Selects the established strict JSON parser.</summary>
public readonly struct LuminPackStrictJson : ILuminPackValidationOptions
{
    /// <inheritdoc/>
    public bool Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => true;
    }
}

/// <summary>Composes declaration constants without generating another profile implementation for each combination.</summary>
/// <typeparam name="TStrings">The fixed binary string representation and JSON encoding.</typeparam>
/// <typeparam name="TFormat">The fixed standard format.</typeparam>
/// <typeparam name="TDepth">The complete JSON document depth bound.</typeparam>
/// <typeparam name="TValidation">The complete JSON document grammar policy.</typeparam>
public readonly struct LuminPackOptions<TStrings, TFormat, TDepth, TValidation> : ILuminPackOptions
    where TStrings : struct, ILuminPackStringCodec
    where TFormat : struct, ILuminPackFormatOptions
    where TDepth : struct, ILuminPackDepthOptions
    where TValidation : struct, ILuminPackValidationOptions
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RecordBound(string? value) =>
        default(TStrings).RecordBound(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(TStrings).WriteRecord(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ref LuminPackWriter writer, string? value) =>
        default(TStrings).Write(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? Read(scoped ref LuminPackReader reader, string? current) =>
        default(TStrings).Read(ref reader, current);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(scoped ref LuminPackReader reader, int maxStringLength = int.MaxValue) =>
        default(TStrings).Skip(ref reader, maxStringLength);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
        default(TStrings).ReadPair(ref reader, ref first, ref second);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots) =>
        default(TStrings).ReadSlots(ref reader, slots);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(TStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Measure(string? value) =>
        default(TStrings).Measure(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteReserved(scoped ref LuminPackWriter writer, string? value) =>
        default(TStrings).WriteReserved(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text) =>
        default(TStrings).WriteText(ref writer, text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
        default(TStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsUtf16Runs() =>
        default(TStrings).SupportsUtf16Runs();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(TStrings).WriteSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(TStrings).WriteCheckedSlots(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) =>
        default(TStrings).WritePairs(ref writer, keys, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns) =>
        default(TStrings).WritePairRuns(ref writer, keys, values, allowRuns);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value) =>
        ref default(TStrings).WriteAtCursor(ref writer, ref cursor, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values) =>
        default(TStrings).WriteSlotPrefix(ref writer, values);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) =>
        default(TStrings).WritePairPrefix(ref writer, pairs);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count) =>
        default(TStrings).TryPeekCharacterCount(ref reader, out count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1) =>
        default(TStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

    /// <inheritdoc/>
    public LuminPackStringEncoding Encoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TStrings).Encoding;
    }

    /// <inheritdoc/>
    public LuminPackStringRecording Recording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TStrings).Recording;
    }

    /// <inheritdoc/>
    public byte BinaryProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TStrings).BinaryProfile;
    }

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TStrings).Encoding;
    }
    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TStrings).Recording;
    }
    /// <inheritdoc/>
    public StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TFormat).Value;
    }
    /// <inheritdoc/>
    public int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TDepth).Value;
    }
    /// <inheritdoc/>
    public bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(TValidation).Value;
    }
}
