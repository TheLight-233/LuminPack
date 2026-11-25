using System.Buffers;
using LuminPack.Core;
using System.Runtime.CompilerServices;

namespace LuminPack.Option;

/// <summary>Defines stateless string, JSON grammar and depth operations for a compiled profile.</summary>
/// <remarks>Implementations have no instance state. String operations delegate to the selected codec. JSON policies apply to the whole document.</remarks>
public interface ILuminPackOptions : ILuminPackStringCodec
{
    /// <summary>Advances the document reader with the selected grammar.</summary>
    /// <typeparam name="TProfile">The complete document profile.</typeparam>
    /// <param name="reader">The active document reader.</param>
    /// <returns>Whether a token was selected.</returns>
    bool ReadJsonToken<TProfile>(scoped ref LuminPackJsonReader reader) where TProfile : struct, ILuminPackOptions;

    /// <summary>Determines whether speculative reads must retain strict grammar state.</summary>
    /// <returns>Whether strict grammar state is required.</returns>
    bool TracksJsonGrammar();

    /// <summary>Validates the UTF-8 bytes of a JSON string when required.</summary>
    /// <param name="text">The bytes between validated token boundaries.</param>
    void ValidateJsonUtf8(scoped ReadOnlySpan<byte> text);

    /// <summary>Checks the depth after entering a JSON container.</summary>
    /// <param name="depth">The depth after consuming the opening token.</param>
    void CheckJsonDepth(int depth);

    /// <summary>Gets the selected string encoding.</summary>
    LuminPackStringEncoding StringEncoding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
    /// <summary>Gets the binary string boundary representation.</summary>
    LuminPackStringRecording StringRecording
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
    /// <summary>Gets the standard format for supported formatted values.</summary>
    StandardFormat StandardFormat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
    /// <summary>Gets the complete JSON document's maximum depth.</summary>
    int MaxJsonDepth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
    /// <summary>Gets whether the complete JSON document requires strict grammar validation.</summary>
    bool ValidateJsonSyntax
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
}

/// <summary>Provides the fixed UTF-16, length-prefixed, trusted-JSON library defaults.</summary>
public readonly struct LuminPackDefaultOptions : ILuminPackOptions, ILuminPackStringReadLimit
{
    /// <inheritdoc/>
    StringLengthCheck ILuminPackStringReadLimit.Check =>
        StringReadLimit<LuminPackUtf16LengthStrings>.Check;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult ReadJsonValue<TOperation, TResult>(scoped ref LuminPackJsonReader reader)
        where TOperation : struct, ILuminPackJsonReadOperation<TResult> =>
        default(LuminPackUtf16LengthStrings).ReadJsonValue<TOperation, TResult>(ref reader);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteJsonValue<TOperation, TValue>(scoped ref LuminPackJsonWriter writer, TValue value)
        where TOperation : struct, ILuminPackJsonWriteOperation<TValue> =>
        default(LuminPackUtf16LengthStrings).WriteJsonValue<TOperation, TValue>(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int JsonByteCount(int units) =>
        default(LuminPackUtf16LengthStrings).JsonByteCount(units);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadJsonUnit(scoped ref LuminPackJsonReader reader, out char value) =>
        default(LuminPackUtf16LengthStrings).TryReadJsonUnit(ref reader, out value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteJsonAscii(scoped ref LuminPackJsonWriter writer, byte value) =>
        default(LuminPackUtf16LengthStrings).WriteJsonAscii(ref writer, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadJsonToken<TProfile>(scoped ref LuminPackJsonReader reader) where TProfile : struct, ILuminPackOptions =>
        default(LuminPackTrustedJson).ReadJsonToken<TProfile>(ref reader);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TracksJsonGrammar() =>
        default(LuminPackTrustedJson).TracksJsonGrammar();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ValidateJsonUtf8(scoped ReadOnlySpan<byte> text) =>
        default(LuminPackTrustedJson).ValidateJsonUtf8(text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CheckJsonDepth(int depth) =>
        default(LuminPackDefaultDepth).CheckJsonDepth(depth);

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
