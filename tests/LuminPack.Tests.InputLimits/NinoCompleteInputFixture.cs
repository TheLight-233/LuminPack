using System.Buffers;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Tests.InputLimits;

/// <summary>Exercises binary document and prefix reads in an assembly that requires complete documents.</summary>
public static class NinoCompleteInputFixture
{
    /// <summary>Reads a scalar using the span document overload.</summary>
    /// <param name="input">The complete binary input.</param>
    /// <returns>The decoded integer.</returns>
    public static int ReadSpanDocument(byte[] input) => LuminPackSerializer.Deserialize<int>(input);

    /// <summary>Reads a nullable record using a binary document overload.</summary>
    /// <param name="input">The complete binary input.</param>
    /// <returns>The decoded record, or null for a null record.</returns>
    public static CompleteInputRecord? ReadNullableDocument(byte[] input) => LuminPackSerializer.Deserialize<CompleteInputRecord?>(input);

    /// <summary>Reads a scalar from the document overload for a possibly segmented sequence.</summary>
    /// <param name="input">The complete binary input.</param>
    /// <param name="segmented">Whether to split the input inside the encoded integer.</param>
    /// <returns>The decoded integer.</returns>
    public static int ReadSequenceDocument(byte[] input, bool segmented)
    {
        var sequence = Sequence(input, segmented);
        return LuminPackSerializer.Deserialize<int>(in sequence);
    }

    /// <summary>Reads a scalar from a reusable buffer.</summary>
    /// <param name="input">The complete binary input.</param>
    /// <returns>The decoded integer.</returns>
    public static int ReadBufferDocument(byte[] input)
    {
        var buffer = LuminBufferWriterPool.Rent();
        try
        {
            var sequence = new ReadOnlySequence<byte>(input);
            buffer.LoadSequence(in sequence);
            return LuminPackSerializer.Deserialize<int>(buffer);
        }
        finally
        {
            LuminBufferWriterPool.Return(buffer);
        }
    }

    /// <summary>Reads a typed binary document with an explicit stateless profile.</summary>
    /// <param name="input">The complete binary input.</param>
    /// <returns>The decoded integer.</returns>
    public static int ReadProfiledDocument(byte[] input) => LuminPackSerializer.Deserialize<int, LuminPackUtf16LengthOptions>(input);

    /// <summary>Uses registered dispatch for an open payload type.</summary>
    /// <typeparam name="T">The registered payload type.</typeparam>
    /// <param name="input">The complete binary input.</param>
    /// <returns>The decoded value.</returns>
    public static T ReadRegisteredDocument<T>(byte[] input) => LuminPackSerializer.Deserialize<T, LuminPackUtf16LengthOptions>(input);

    /// <summary>Reads a scalar using a stream document overload.</summary>
    /// <param name="input">The complete binary input.</param>
    /// <param name="withValueArgument">Whether to call the overload with a compatibility value argument.</param>
    /// <returns>A task containing the decoded integer.</returns>
    public static async Task<int> ReadStreamDocument(byte[] input, bool withValueArgument)
    {
        using var stream = new MemoryStream(input, writable: false);
        return withValueArgument
            ? await LuminPackSerializer.DeserializeAsync<int>(stream, 0).ConfigureAwait(false)
            : await LuminPackSerializer.DeserializeAsync<int, LuminPackUtf16LengthOptions>(stream).ConfigureAwait(false);
    }

    /// <summary>Reads one prefix through the by-reference span overload.</summary>
    /// <param name="input">The input containing a prefix and possible suffix.</param>
    /// <returns>The decoded integer and its byte count.</returns>
    public static (int Value, int Consumed) ReadSpanPrefix(byte[] input)
    {
        int value = default;
        int consumed = LuminPackSerializer.Deserialize(input, ref value);
        return (value, consumed);
    }

    /// <summary>Reads one prefix through the advancing reader overload.</summary>
    /// <param name="input">The input containing a prefix and possible suffix.</param>
    /// <returns>The decoded integer and its byte count.</returns>
    public static (int Value, int Consumed) ReadAdvancingPrefix(byte[] input)
    {
        ReadOnlySpan<byte> span = input;
        var reader = new LuminPackReader(ref span);
        int value = default;
        LuminPackSerializer.Deserialize(ref reader, ref value);
        return (value, reader.Position);
    }

    /// <summary>Builds the sequence for a document read.</summary>
    /// <param name="source">The input bytes.</param>
    /// <param name="segmented">Whether to split the input inside an encoded integer.</param>
    /// <returns>The sequence over the supplied bytes.</returns>
    private static ReadOnlySequence<byte> Sequence(byte[] source, bool segmented)
    {
        if (!segmented)
            return new ReadOnlySequence<byte>(source);
        var first = new Segment(source.AsMemory(0, 2));
        var last = first.Append(source.AsMemory(2));
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    /// <summary>Holds one linked segment of the input sequence.</summary>
    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        /// <summary>Sets the segment's data.</summary>
        /// <param name="memory">The bytes held by the segment.</param>
        internal Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        /// <summary>Appends a second segment.</summary>
        /// <param name="memory">The bytes for the new segment.</param>
        /// <returns>The attached segment.</returns>
        internal Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}

/// <summary>A binary record used to exercise the null-marker document path.</summary>
[LuminPackable]
public partial class CompleteInputRecord
{
    /// <summary>The record's scalar value.</summary>
    public int Number;
}
