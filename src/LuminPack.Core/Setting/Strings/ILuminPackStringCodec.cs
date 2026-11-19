using System.Buffers;
using System.Runtime.CompilerServices;
using LuminPack.Core;

namespace LuminPack.Option;

/// <summary>Defines the operations of one stateless binary string representation.</summary>
public interface ILuminPackStringCodec
{
    /// <summary>Returns the reserved upper bound in bytes for a complete record.</summary>
    /// <param name="value">The nullable string to encode.</param>
    /// <returns>The number of bytes sufficient to hold the complete record.</returns>
    int RecordBound(string? value);

    /// <summary>Writes to reserved storage and returns the next cursor, preserving profile-specific writer publication.</summary>
    /// <param name="cursor">The caller-held cursor followed by the proven record bound.</param>
    /// <param name="value">The nullable string to encode.</param>
    /// <param name="writer">The writer that owns the reserved storage and published cursor.</param>
    /// <returns>The cursor immediately after the encoded record.</returns>
    ref byte WriteRecord(scoped ref LuminPackWriter writer, ref byte cursor, string? value);

    /// <summary>Writes a record, including its capacity checks and cursor publication.</summary>
    /// <param name="value">The nullable string to encode.</param>
    /// <param name="writer">The writer that owns the reserved storage and published cursor.</param>
    void Write(scoped ref LuminPackWriter writer, string? value);

    /// <summary>Reads a record and retains an equal existing string.</summary>
    /// <param name="current">The immutable string available for reuse.</param>
    /// <param name="reader">The reader that owns the input window and cursor.</param>
    /// <returns>The decoded string, retaining an equal existing instance.</returns>
    string? Read(scoped ref LuminPackReader reader, string? current);

    /// <summary>Reads and publishes two fields in wire order.</summary>
    /// <param name="first">The first destination and its retained string.</param>
    /// <param name="reader">The reader that owns the input window and cursor.</param>
    /// <param name="second">The second destination, read after publishing the first.</param>
    void ReadPair(scoped ref LuminPackReader reader, scoped ref string? first, scoped ref string? second);

    /// <summary>Reads and publishes each string slot in wire order.</summary>
    /// <param name="reader">The reader that owns the input window and cursor.</param>
    /// <param name="slots">The destination slots, in wire order.</param>
    void ReadSlots(scoped ref LuminPackReader reader, scoped Span<string?> slots);

    /// <summary>Encodes unframed text with the selected character encoding.</summary>
    /// <param name="bytesWritten">Receives the number of bytes produced.</param>
    /// <param name="charsRead">Receives the number of input characters consumed.</param>
    /// <param name="destination">The bounded destination for the encoded bytes.</param>
    /// <param name="isFinalBlock">Whether the input is the final block.</param>
    /// <param name="replaceInvalidSequences">Whether invalid input sequences may be replaced.</param>
    /// <param name="source">The UTF-16 characters to encode.</param>
    /// <returns>The transcoder status for the supplied input and destination.</returns>
    OperationStatus Encode(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock);

    /// <summary>Measures the exact record width, including framing.</summary>
    /// <param name="value">The nullable string.</param>
    /// <returns>The exact encoded width.</returns>
    int Measure(string? value);

    /// <summary>Writes and publishes a record at the reserved writer position.</summary>
    /// <param name="writer">The writer whose storage and cursor are used.</param>
    /// <param name="value">The nullable string.</param>
    void WriteReserved(scoped ref LuminPackWriter writer, string? value);

    /// <summary>Writes non-null character data without materializing a string.</summary>
    /// <param name="writer">The writer whose storage and cursor are used.</param>
    /// <param name="text">The current character snapshot.</param>
    void WriteText(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<char> text);

    /// <summary>Decodes unframed bytes into the supplied character window.</summary>
    /// <param name="source">The encoded input.</param>
    /// <param name="destination">The bounded destination.</param>
    /// <param name="bytesRead">Receives the input bytes consumed.</param>
    /// <param name="charsWritten">Receives the characters produced.</param>
    /// <param name="replaceInvalidSequences">Whether malformed sequences may be replaced.</param>
    /// <param name="isFinalBlock">Whether the input is complete.</param>
    /// <returns>The bounded transcoder result.</returns>
    OperationStatus Decode(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock);

    /// <summary>Reports whether raw length-prefixed UTF-16 collection kernels can write this representation.</summary>
    /// <returns>Whether the raw collection kernels are compatible.</returns>
    bool SupportsUtf16Runs();

    /// <summary>Writes consecutive records in slot order.</summary>
    /// <param name="writer">The writer whose storage and cursor are used.</param>
    /// <param name="values">The value slots in wire order.</param>
    void WriteSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values);

    /// <summary>Writes slots with the original per-element capacity check.</summary>
    /// <param name="writer">The writer whose storage and cursor are used.</param>
    /// <param name="values">The value slots in wire order.</param>
    void WriteCheckedSlots(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values);

    /// <summary>Writes corresponding keys and values with the original buffer checks.</summary>
    /// <param name="writer">The writer whose storage and cursor are used.</param>
    /// <param name="keys">The key slots in wire order.</param>
    /// <param name="values">The value slots in wire order.</param>
    void WritePairs(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values);

    /// <summary>Writes key and value slots in enumeration order.</summary>
    /// <param name="writer">The writer whose storage and cursor are used.</param>
    /// <param name="keys">The key slots in wire order.</param>
    /// <param name="values">The value slots in wire order.</param>
    /// <param name="allowRuns">Whether the traversal permits a reserved run.</param>
    void WritePairRuns(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, bool allowRuns);

    /// <summary>Writes at a caller-held cursor, growing storage when required.</summary>
    /// <param name="writer">The writer whose storage and cursor are used.</param>
    /// <param name="cursor">The caller-held output cursor.</param>
    /// <param name="value">The nullable string.</param>
    /// <returns>The next writer cursor.</returns>
    ref byte WriteAtCursor(scoped ref LuminPackWriter writer, ref byte cursor, string? value);

    /// <summary>Writes the leading slots that fit in the current window.</summary>
    /// <param name="writer">The writer whose storage and cursor are used.</param>
    /// <param name="values">The value slots in wire order.</param>
    /// <returns>The number of complete records or pairs.</returns>
    int WriteSlotPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<string?> values);

    /// <summary>Writes the leading slots that fit in the current window.</summary>
    /// <param name="writer">The writer whose storage and cursor are used.</param>
    /// <param name="pairs">The captured pairs in enumeration order.</param>
    /// <returns>The number of complete records or pairs.</returns>
    int WritePairPrefix(scoped ref LuminPackWriter writer, scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs);

    /// <summary>Peeks at the character count when the framing provides a fixed prefix.</summary>
    /// <param name="reader">The reader positioned at the string header.</param>
    /// <param name="count">Receives the character-count hint.</param>
    /// <returns>Whether a complete hint was available.</returns>
    bool TryPeekCharacterCount(scoped ref LuminPackReader reader, out int count);

    /// <summary>Validates a raw UTF-16 or compact Latin-1 payload for matching retained text.</summary>
    /// <param name="reader">The reader positioned at the string header.</param>
    /// <param name="length">The retained character count.</param>
    /// <param name="bytes">Receives the validated payload width.</param>
    /// <param name="latin1">Whether the validated payload is compact Latin-1.</param>
    /// <returns>Whether the payload matches the retained character count.</returns>
    bool TryPeekUtf16Payload(scoped ref LuminPackReader reader, int length, out int bytes, out bool latin1);

    /// <summary>Gets the character encoding.</summary>
    LuminPackStringEncoding Encoding { get; }

    /// <summary>Gets the record framing.</summary>
    LuminPackStringRecording Recording { get; }

    /// <summary>Gets the wire profile identifier.</summary>
    byte BinaryProfile { get; }
}
