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

    /// <summary>Gets the character encoding.</summary>
    LuminPackStringEncoding Encoding { get; }

    /// <summary>Gets the record framing.</summary>
    LuminPackStringRecording Recording { get; }

    /// <summary>Gets the wire profile identifier.</summary>
    byte BinaryProfile { get; }
}
