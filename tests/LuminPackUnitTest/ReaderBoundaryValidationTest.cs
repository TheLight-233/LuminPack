using System.Buffers;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPackUnitTest;

internal static class ReaderBoundaryValidationTest
{
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
        RunCase(results, nameof(NullLengthStringRoundTrips), NullLengthStringRoundTrips);
        RunCase(results, nameof(MultiSegmentSequenceIsReadCompletely), MultiSegmentSequenceIsReadCompletely);
        RunCase(results, nameof(MultiDimensionalArrayHeaderAndLengthsAreValidated),
            MultiDimensionalArrayHeaderAndLengthsAreValidated);
        RunDebugOnlyCase(results, nameof(TruncatedMultiDimensionalArrayPayloadIsRejected),
            TruncatedMultiDimensionalArrayPayloadIsRejected);
    }

    private static void TruncatedCollectionHeaderIsRejected()
    {
        AssertThrows(() => ReadCollectionHeader(new byte[3]),
            "A three-byte collection header was accepted.");

        static void ReadCollectionHeader(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var localReader = new LuminPackReader(ref span);
            int localIndex = 0;
            localReader.TryReadCollectionHead(ref localIndex, out _);
        }
    }

    private static void NegativeCollectionCountIsRejected()
    {
        AssertThrows(() => ReadCollectionHeader(BitConverter.GetBytes(-2)),
            "A negative collection count other than the null marker was accepted.");

        static void ReadCollectionHeader(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            int index = 0;
            reader.TryReadCollectionHead(ref index, out _);
        }
    }

    private static void Utf8TokenWithoutTerminatorIsRejected()
    {
        var option = LuminPackSerializerOption.Token with { StringEncoding = LuminPackStringEncoding.UTF8 };
        AssertThrows(() => ScanToken(new byte[] { 1, 2, 3 }, option),
            "An unterminated UTF-8 token string was accepted.");
    }

    private static void Utf16TokenWithoutTerminatorIsRejected()
    {
        var option = LuminPackSerializerOption.Token with { StringEncoding = LuminPackStringEncoding.UTF16 };
        AssertThrows(() => ScanToken(new byte[] { 0x41, 0, 0x42, 0, 0x43 }, option),
            "An unterminated/odd-sized UTF-16 token string was accepted.");
    }

    private static void ScanToken(byte[] bytes, LuminPackSerializerOption option)
    {
        using var state = new LuminPackReaderOptionalState(option);
        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackReader(ref span, state);
        int index = 0;
        reader.ReadStringLength(ref index, out _);
    }

    private static void OversizedUnmanagedPayloadIsRejectedBeforeAllocation()
    {
        byte[] bytes = BitConverter.GetBytes(int.MaxValue);
        AssertThrows(() => ReadArray(bytes),
            "An overflowing unmanaged-array byte count was accepted.");

        static void ReadArray(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            int index = 0;
            int[]? value = null;
            reader.DangerousReadUnmanagedArray(ref index, ref value, out _);
        }
    }

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

    private static void UnionRefIndexOverloadUsesSuppliedIndex()
    {
        ReadOnlySpan<byte> span = new byte[] { LuminPackCode.NullObject, 42, 7 };
        var reader = new LuminPackReader(ref span);
        int index = 2;

        bool hasValue = reader.TryPeekUnionHeader(ref index, out ushort tag);

        Assert(hasValue && tag == 7, $"Union tag was {tag}; expected the byte at the supplied index.");
        Assert(index == 3, $"Union index advanced to {index}; expected 3.");
    }

    private static void TruncatedWideUnionTagIsRejected()
    {
        AssertThrows(() => ReadWideTag(new byte[] { LuminPackCode.WideTag }),
            "A wide-union marker without its ushort payload was accepted.");

        static void ReadWideTag(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            reader.TryPeekWideUnionHeader(out _);
        }
    }

    private static void InvalidCompressedLengthIsRejectedBeforeAllocation()
    {
        byte[] bytes = new byte[8];
        BitConverter.GetBytes(1).CopyTo(bytes, 0);
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 4);

        AssertThrows(() => ReadCompressed(bytes),
            "An out-of-range compressed payload length was accepted.");

        static void ReadCompressed(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            int index = 0;
            int[]? value = null;
            reader.DangerousReadUnmanagedArrayWithCompress(ref index, ref value, out _);
        }
    }

    private static void NegativeAdvanceIsRejected()
    {
        AssertThrows(() => Advance(new byte[1]), "A negative reader advance was accepted.");

        static void Advance(byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes;
            var reader = new LuminPackReader(ref span);
            reader.Advance(-1);
        }
    }

    private static void NullLengthStringRoundTrips()
    {
        byte[] bytes = new byte[sizeof(int) * 2];
        BitConverter.GetBytes(LuminPackCode.NullCollection).CopyTo(bytes, 0);
        ReadOnlySpan<byte> span = bytes;
        var reader = new LuminPackReader(ref span);
        ref int index = ref reader.GetCurrentSpanOffset();

        reader.ReadStringLength(ref index, out int length);
        string? value = reader.ReadString(length);
        reader.Advance(length + reader.StringRecordLength());

        Assert(value is null, "A null length-prefixed string did not deserialize as null.");
        Assert(reader.GetCurrentSpanIndex() == bytes.Length,
            $"Null string consumed {reader.GetCurrentSpanIndex()} bytes; expected {bytes.Length}.");
    }

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
        AssertThrows(() => LuminPackSerializer.Deserialize<int[,]>(forgedLength),
            "A multidimensional array whose element count disagreed with its dimensions was accepted.");

    }

    private static void TruncatedMultiDimensionalArrayPayloadIsRejected()
    {
        var payload = LuminPackSerializer.Serialize(new int[255, 1]);
        AssertThrows(() => LuminPackSerializer.Deserialize<int[,]>(payload.AsSpan(0, payload.Length - 1)),
            "A truncated multidimensional array payload was read past the input boundary.");
    }

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

    private static void RunDebugOnlyCase(List<string> results, string name, Action test)
    {
#if DEBUG
        RunCase(results, name, test);
#else
        results.Add($"? {name} - SKIPPED: EnsureReadable is compiled out in Release.");
#endif
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class TestSequenceSegment : ReadOnlySequenceSegment<byte>
    {
        public TestSequenceSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

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
