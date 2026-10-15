using System.Buffers;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPackUnitTest;

/// <summary>Provides reader boundary validation test functionality.</summary>
internal static class ReaderBoundaryValidationTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunDebugOnlyCase(results, nameof(TruncatedCollectionHeaderIsRejected), TruncatedCollectionHeaderIsRejected);
        RunCase(results, nameof(NegativeCollectionCountIsRejected), NegativeCollectionCountIsRejected);
        RunCase(results, nameof(Utf8TokenWithoutTerminatorIsRejected), Utf8TokenWithoutTerminatorIsRejected);
        RunCase(results, nameof(Utf16TokenWithoutTerminatorIsRejected), Utf16TokenWithoutTerminatorIsRejected);
        RunCase(results, nameof(OversizedUnmanagedPayloadIsRejectedBeforeAllocation),
            OversizedUnmanagedPayloadIsRejectedBeforeAllocation);
        RunCase(results, nameof(HeaderlessPayloadStartsAtSuppliedIndex), HeaderlessPayloadStartsAtSuppliedIndex);
        RunCase(results, nameof(UnionRefIndexOverloadUsesSuppliedIndex), UnionRefIndexOverloadUsesSuppliedIndex);
        RunDebugOnlyCase(results, nameof(TruncatedWideUnionTagIsRejected), TruncatedWideUnionTagIsRejected);
        RunCase(results, nameof(InvalidCompressedLengthIsRejectedBeforeAllocation),
            InvalidCompressedLengthIsRejectedBeforeAllocation);
        RunDebugOnlyCase(results, nameof(NegativeAdvanceIsRejected), NegativeAdvanceIsRejected);
        RunCase(results, nameof(LengthStringsRoundTrip), LengthStringsRoundTrip);
        RunCase(results, nameof(NullLengthStringRoundTrips), NullLengthStringRoundTrips);
        RunCase(results, nameof(LengthStringPayloadBoundsAreValidated), LengthStringPayloadBoundsAreValidated);
        RunCase(results, nameof(NegativeStringLengthsAreRejected), NegativeStringLengthsAreRejected);
        RunCase(results, nameof(OverflowingStringLengthIsRejected), OverflowingStringLengthIsRejected);
        RunCase(results, nameof(MultiSegmentSequenceIsReadCompletely), MultiSegmentSequenceIsReadCompletely);
        RunCase(results, nameof(MultiDimensionalArrayHeaderAndLengthsAreValidated),
            MultiDimensionalArrayHeaderAndLengthsAreValidated);
        RunDebugOnlyCase(results, nameof(TruncatedMultiDimensionalArrayPayloadIsRejected),
            TruncatedMultiDimensionalArrayPayloadIsRejected);
    }

    /// <summary>Verifies truncated collection header is rejected.</summary>
    private static void TruncatedCollectionHeaderIsRejected()
    {
        AssertThrows(() => ReadCollectionHeader(new byte[3]),
            "A three-byte collection header was accepted.");

        // <summary>Reads collection header from the current input.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        static void ReadCollectionHeader(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var localReader = new LuminPackReader(ref span);
            int localIndex = 0;
            localReader.TryReadCollectionHead(ref localIndex, out _);
        }
    }

    /// <summary>Verifies negative collection count is rejected.</summary>
    private static void NegativeCollectionCountIsRejected()
    {
        AssertThrows(() => ReadCollectionHeader(BitConverter.GetBytes(-2)),
            "A negative collection count other than the null marker was accepted.");

        // <summary>Reads collection header from the current input.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        static void ReadCollectionHeader(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            int index = 0;
            reader.TryReadCollectionHead(ref index, out _);
        }
    }

    /// <summary>Verifies utf8 token without terminator is rejected.</summary>
    private static void Utf8TokenWithoutTerminatorIsRejected()
    {
        var option = LuminPackSerializerOption.Token with { StringEncoding = LuminPackStringEncoding.UTF8 };
        AssertThrows(() => ScanToken(new byte[] { 1, 2, 3 }, option),
            "An unterminated UTF-8 token string was accepted.");
    }

    /// <summary>Verifies utf16 token without terminator is rejected.</summary>
    private static void Utf16TokenWithoutTerminatorIsRejected()
    {
        var option = LuminPackSerializerOption.Token with { StringEncoding = LuminPackStringEncoding.UTF16 };
        AssertThrows(() => ScanToken(new byte[] { 0x41, 0, 0x42, 0, 0x43 }, option),
            "An unterminated/odd-sized UTF-16 token string was accepted.");
    }

    /// <summary>Verifies scan token.</summary>
    /// <param name="bytes">The bytes used by this operation.</param>
    /// <param name="option">The configuration to apply to this operation.</param>
    private static void ScanToken(byte[] bytes, LuminPackSerializerOption option)
    {
        using var state = new LuminPackReaderOptionalState(option);
        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackReader(ref span, state);
        int index = 0;
        reader.ReadStringLength(ref index, out _);
    }

    /// <summary>Verifies oversized unmanaged payload is rejected before allocation.</summary>
    private static void OversizedUnmanagedPayloadIsRejectedBeforeAllocation()
    {
        byte[] bytes = BitConverter.GetBytes(int.MaxValue);
        AssertThrows(() => ReadArray(bytes),
            "An overflowing unmanaged-array byte count was accepted.");

        // <summary>Reads array from the current input.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        static void ReadArray(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            int index = 0;
            int[]? value = null;
            reader.DangerousReadUnmanagedArray(ref index, ref value, out _);
        }
    }

    /// <summary>Verifies headerless payload starts at supplied index.</summary>
    private static void HeaderlessPayloadStartsAtSuppliedIndex()
    {
        const int expected = 0x12345678;
        byte[] bytes = new byte[12];
        BitConverter.GetBytes(unchecked((int)0xAABBCCDD)).CopyTo(bytes, 0);
        BitConverter.GetBytes(expected).CopyTo(bytes, 4);
        BitConverter.GetBytes(unchecked((int)0xEEFF0011)).CopyTo(bytes, 8);

        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackReader(ref span);
        int index = sizeof(int);
        int[]? value = null;
        reader.ReadUnmanagedArray(ref index, ref value, 1, out int bytesRead);

        Assert(bytesRead == sizeof(int), $"Headerless read reported {bytesRead} bytes instead of four.");
        Assert(value is { Length: 1 } && value[0] == expected,
            "Headerless read skipped or re-read the four-byte collection header.");
    }

    /// <summary>Verifies union ref index overload uses supplied index.</summary>
    private static void UnionRefIndexOverloadUsesSuppliedIndex()
    {
        ReadOnlySpan<byte> span = new byte[] { LuminPackCode.NullObject, 42, 7 };
        var reader = new LuminPackReader(ref span);
        int index = 2;

        bool hasValue = reader.TryPeekUnionHeader(ref index, out ushort tag);

        Assert(hasValue && tag == 7, $"Union tag was {tag}; expected the byte at the supplied index.");
        Assert(index == 3, $"Union index advanced to {index}; expected 3.");
    }

    /// <summary>Verifies truncated wide union tag is rejected.</summary>
    private static void TruncatedWideUnionTagIsRejected()
    {
        AssertThrows(() => ReadWideTag(new byte[] { LuminPackCode.WideTag }),
            "A wide-union marker without its ushort payload was accepted.");

        // <summary>Reads wide tag from the current input.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        static void ReadWideTag(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            reader.TryPeekWideUnionHeader(out _);
        }
    }

    /// <summary>Verifies invalid compressed length is rejected before allocation.</summary>
    private static void InvalidCompressedLengthIsRejectedBeforeAllocation()
    {
        byte[] bytes = new byte[8];
        BitConverter.GetBytes(1).CopyTo(bytes, 0);
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 4);

        AssertThrows(() => ReadCompressed(bytes),
            "An out-of-range compressed payload length was accepted.");

        // <summary>Reads compressed from the current input.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        static void ReadCompressed(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            int index = 0;
            int[]? value = null;
            reader.DangerousReadUnmanagedArrayWithCompress(ref index, ref value, out _);
        }
    }

    /// <summary>Verifies negative advance is rejected.</summary>
    private static void NegativeAdvanceIsRejected()
    {
        AssertThrows(() => Advance(new byte[1]), "A negative reader advance was accepted.");

        // <summary>Advances the current buffer position by the supplied number of bytes.</summary>
        // <param name="bytes">The bytes used by this operation.</param>
        static void Advance(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            reader.Advance(-1);
        }
    }

    /// <summary>Verifies null length string round trips.</summary>
    private static void NullLengthStringRoundTrips()
    {
        byte[] bytes = new byte[sizeof(int) * 2];
        BitConverter.GetBytes(LuminPackCode.NullCollection).CopyTo(bytes, 0);
        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackReader(ref span);
        ref int index = ref reader.GetCurrentSpanOffset();

        reader.ReadStringLength(ref index, out int length);
        string? value = reader.ReadString(length);
        reader.Advance((length < 0 ? 0 : length) + reader.StringRecordLength());

        Assert(value is null, "A null length-prefixed string did not deserialize as null.");
        Assert(reader.GetCurrentSpanIndex() == bytes.Length,
            $"Null string consumed {reader.GetCurrentSpanIndex()} bytes; expected {bytes.Length}.");
    }

    /// <summary>Verifies length strings round trip.</summary>
    private static void LengthStringsRoundTrip()
    {
        const string value = "ASCII \u4E2D\u6587 \uD83D\uDE00";

        foreach (var option in new[] { LuminPackSerializerOption.Utf8, LuminPackSerializerOption.Utf16WithLength })
        {
            var payload = LuminPackSerializer.Serialize(value, option);
            var result = LuminPackSerializer.Deserialize<string>(payload, option);
            Assert(result == value, $"Length-prefixed {option.StringEncoding} string did not round-trip.");
        }
    }

    /// <summary>Verifies length string payload bounds are validated.</summary>
    private static void LengthStringPayloadBoundsAreValidated()
    {
        AssertThrows(() => ReadForgedStringLength(CreateLengthString(100, 0, utf8: true, prefix: 5),
                LuminPackSerializerOption.Utf8, 5),
            "A UTF-8 string length larger than the remaining payload was accepted.");

        AssertThrows(() => ReadForgedStringLength(CreateLengthString(3, 2, utf8: false),
                LuminPackSerializerOption.Utf16WithLength, 0),
            "A UTF-16 string payload missing one byte was accepted.");
    }

    /// <summary>Verifies negative string lengths are rejected.</summary>
    private static void NegativeStringLengthsAreRejected()
    {
        foreach (int length in new[] { -2, int.MinValue })
        {
            AssertInvalidData(() => ReadForgedStringLength(CreateLengthString(length, 0, utf8: true),
                    LuminPackSerializerOption.Utf8, 0),
                $"Invalid string length {length} was accepted.");
        }
    }

    /// <summary>Verifies overflowing string length is rejected.</summary>
    private static void OverflowingStringLengthIsRejected()
    {
        const int prefix = 16;
        AssertThrows(() => ReadForgedStringLength(CreateLengthString(int.MaxValue, 0, utf8: true, prefix: prefix),
                LuminPackSerializerOption.Utf8, prefix),
            "A string length whose index/header/length sum overflows Int32 was accepted.");
    }

    /// <summary>Creates length string from the supplied input.</summary>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <param name="payloadLength">The payload length used by this operation.</param>
    /// <param name="utf8">The utf8 used by this operation.</param>
    /// <param name="prefix">The prefix used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static byte[] CreateLengthString(int length, int payloadLength, bool utf8, int prefix = 0)
    {
        int headerSize = utf8 ? sizeof(int) * 2 : sizeof(int);
        byte[] bytes = new byte[prefix + headerSize + payloadLength];
        BitConverter.GetBytes(length).CopyTo(bytes, prefix);
        return bytes;
    }

    /// <summary>Reads forged string length from the current input.</summary>
    /// <param name="bytes">The bytes used by this operation.</param>
    /// <param name="option">The configuration to apply to this operation.</param>
    /// <param name="index">The position at which the operation starts.</param>
    private static void ReadForgedStringLength(byte[] bytes, LuminPackSerializerOption option, int index)
    {
        using var state = new LuminPackReaderOptionalState(option);
        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackReader(ref span, state);
        reader.ReadStringLength(ref index, out _);
    }

    /// <summary>Verifies multi segment sequence is read completely.</summary>
    private static void MultiSegmentSequenceIsReadCompletely()
    {
        byte[] payload =
        {
            3, 0, 0, 0, // UTF-8 byte length
            3, 0, 0, 0, // UTF-16 character length
            (byte)'a', (byte)'b', (byte)'c'
        };

        var first = new TestSequenceSegment(payload.AsMemory(0, 2));
        TestSequenceSegment last = first
            .Append(payload.AsMemory(2, 3))
            .Append(payload.AsMemory(5));
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

        var reader = new LuminPackReader(ref sequence);
        ref int index = ref reader.GetCurrentSpanOffset();
        reader.ReadStringLength(ref index, out int length);
        string? value = reader.ReadString(length);
        reader.Advance(length + reader.StringRecordLength());

        Assert(value == "abc", $"Multi-segment sequence decoded '{value}' instead of 'abc'.");
        Assert(reader.GetCurrentSpanIndex() == payload.Length,
            $"Multi-segment sequence consumed {reader.GetCurrentSpanIndex()} bytes; expected {payload.Length}.");
    }

    /// <summary>Verifies multi dimensional array header and lengths are validated.</summary>
    private static void MultiDimensionalArrayHeaderAndLengthsAreValidated()
    {
        var value = new int[255, 1];
        value[254, 0] = 42;
        var payload = LuminPackSerializer.Serialize(value);
        var result = LuminPackSerializer.Deserialize<int[,]>(payload);

        Assert(result is not null && result.GetLength(0) == 255 && result[254, 0] == 42,
            "A first dimension whose low byte is 0xFF was confused with the null marker.");

        var forgedLength = payload.ToArray();
        BitConverter.GetBytes(value.Length - 1).CopyTo(forgedLength, 1 + sizeof(int) * 2);
        AssertFormatException(
            () => LuminPackSerializer.Deserialize<int[,]>(forgedLength),
            "The multidimensional array element count does not match its dimensions.",
            "A multidimensional array whose element count disagreed with its dimensions was accepted.");

        var negativeDimension = LuminPackSerializer.Serialize(new int[1, 1]);
        BitConverter.GetBytes(-1).CopyTo(negativeDimension, 1);
        AssertFormatException(
            () => LuminPackSerializer.Deserialize<int[,]>(negativeDimension),
            "A multidimensional array dimension cannot be negative.",
            "A negative multidimensional-array dimension was accepted.");

        var overflowingDimensions = LuminPackSerializer.Serialize(new int[1, 1]);
        BitConverter.GetBytes(int.MaxValue).CopyTo(overflowingDimensions, 1);
        BitConverter.GetBytes(2).CopyTo(overflowingDimensions, 1 + sizeof(int));
        BitConverter.GetBytes(0).CopyTo(overflowingDimensions, 1 + sizeof(int) * 2);
        AssertFormatException(
            () => LuminPackSerializer.Deserialize<int[,]>(overflowingDimensions),
            "The multidimensional array dimensions are too large.",
            "Overflowing rank-2 multidimensional-array dimensions reached allocation.");

        var overflowingRankThreeDimensions = LuminPackSerializer.Serialize(new int[1, 1, 1]);
        BitConverter.GetBytes(int.MaxValue).CopyTo(overflowingRankThreeDimensions, 1);
        BitConverter.GetBytes(2).CopyTo(overflowingRankThreeDimensions, 1 + sizeof(int));
        BitConverter.GetBytes(1).CopyTo(overflowingRankThreeDimensions, 1 + sizeof(int) * 2);
        BitConverter.GetBytes(0).CopyTo(overflowingRankThreeDimensions, 1 + sizeof(int) * 3);
        AssertFormatException(
            () => LuminPackSerializer.Deserialize<int[,,]>(overflowingRankThreeDimensions),
            "The multidimensional array dimensions are too large.",
            "Overflowing rank-3 multidimensional-array dimensions reached allocation.");

    }

    /// <summary>Verifies truncated multi dimensional array payload is rejected.</summary>
    private static void TruncatedMultiDimensionalArrayPayloadIsRejected()
    {
        var payload = LuminPackSerializer.Serialize(new int[255, 1]);
        AssertThrows(() => LuminPackSerializer.Deserialize<int[,]>(payload.AsSpan(0, payload.Length - 1)),
            "A truncated multidimensional array payload was read past the input boundary.");
    }

    /// <summary>Verifies assert throws.</summary>
    /// <param name="action">The action used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>Verifies assert format exception.</summary>
    /// <param name="action">The action used by this operation.</param>
    /// <param name="expectedMessage">The expected message used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void AssertFormatException(Action action, string expectedMessage, string message)
    {
        try
        {
            action();
        }
        catch (FormatException exception)
        {
            if (exception.Message == expectedMessage)
                return;

            throw new InvalidOperationException(
                $"{message} Expected '{expectedMessage}', received '{exception.Message}'.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{message} Expected FormatException, received {exception.GetType().Name}.");
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>Verifies assert invalid data.</summary>
    /// <param name="action">The action used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void AssertInvalidData(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>Verifies run case.</summary>
    /// <param name="results">The results used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="test">The test used by this operation.</param>
    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies run debug only case.</summary>
    /// <param name="results">The results used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="test">The test used by this operation.</param>
    private static void RunDebugOnlyCase(List<string> results, string name, Action test)
    {
#if DEBUG
        RunCase(results, name, test);
#else
        results.Add($"? {name} - SKIPPED: EnsureReadable is compiled out in Release.");
#endif
    }

    /// <summary>Verifies assert.</summary>
    /// <param name="condition">The condition used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>Provides test sequence segment functionality.</summary>
    private sealed class TestSequenceSegment : ReadOnlySequenceSegment<byte>
    {
        /// <summary>Initializes a new instance of <c>TestSequenceSegment</c>.</summary>
        /// <param name="memory">The memory used by this operation.</param>
        public TestSequenceSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        /// <summary>Verifies append.</summary>
        /// <param name="memory">The memory used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public TestSequenceSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new TestSequenceSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = next;
            return next;
        }
    }
}
