using System.Buffers;
using System.Collections.ObjectModel;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks counted binary writes from exact string dictionary wrappers.</summary>
public sealed class ReadOnlyDictionaryWriteTest
{
    /// <summary>Preserves sparse pair order, record framing, and the original fixed-buffer boundary.</summary>
    [Fact]
    public void BinaryStringPairsKeepWireAcrossWriterWindows()
    {
        var backing = new Dictionary<string, string?>
        {
            ["\u4e2d"] = "\u6587",
            ["removed"] = "discarded",
            ["empty"] = "",
            ["null"] = null,
            ["last"] = new string('\u00e9', 71)
        };
        backing.Remove("removed");

        var wrapper = new ReadOnlyDictionary<string, string?>(backing);
        byte[] expected = LuminPackSerializer.Serialize(backing);
        Assert.Equal(expected, LuminPackSerializer.Serialize(wrapper));

        using var output = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(wrapper, output);
        Assert.Equal(expected, output.GetSpan().ToArray());

        // The Latin-1 value fits exactly even though its raw UTF-16 upper bound does not.
        var fixedBytes = new byte[expected.Length + 2];
        Array.Fill(fixedBytes, (byte)0xa3);
        Assert.Equal(expected.Length, WriteToFixedBuffer(wrapper, fixedBytes, expected.Length));
        Assert.Equal(expected, fixedBytes.AsSpan(1, expected.Length).ToArray());
        Assert.Equal((byte)0xa3, fixedBytes[0]);
        Assert.Equal((byte)0xa3, fixedBytes[^1]);

        Array.Fill(fixedBytes, (byte)0xa3);
        Assert.ThrowsAny<Exception>(() => WriteToFixedBuffer(wrapper, fixedBytes, expected.Length - 1));
        Assert.Equal((byte)0xa3, fixedBytes[0]);
        Assert.Equal((byte)0xa3, fixedBytes[^2]);
        Assert.Equal((byte)0xa3, fixedBytes[^1]);
    }

    /// <summary>The key callback observes the captured pair and can change the next pair.</summary>
    [Fact]
    public void CustomStringProfileKeepsTheCapturedPair()
    {
        var backing = new Dictionary<string, string?>
        {
            ["first"] = "original",
            ["second"] = "next"
        };
        var wrapper = new ReadOnlyDictionary<string, string?>(backing);
        byte[] expected = LuminPackSerializer.Serialize(new Dictionary<string, string?>
        {
            ["first"] = "original",
            ["second"] = "updated next"
        });

        MutatingStringProfile.OnEncoding = () =>
        {
            backing["first"] = "updated current";
            backing["second"] = "updated next";
        };
        try
        {
            using var output = new LuminBufferWriter(true);
            LuminPackSerializer.Serialize<ReadOnlyDictionary<string, string?>, MutatingStringProfile>(wrapper, output);
            Assert.Equal("updated current", backing["first"]);
            Assert.Equal(expected, output.GetSpan().ToArray());
        }
        finally
        {
            MutatingStringProfile.OnEncoding = null;
        }
    }

    /// <summary>Writes sparse object pairs in the same order through bounded keys and the original fallback.</summary>
    [Fact]
    public void BinaryObjectPairsKeepWireAcrossWriterWindows()
    {
        var backing = new Dictionary<string, ReadOnlyDictionaryWritePayload>
        {
            ["before"] = new() { Id = 1, Name = "left", Values = [1, 2, 3] },
            ["removed"] = new() { Id = 2 },
            [new string('a', 71)] = new() { Id = 3 }
        };
        backing.Remove("removed");
        var source = new ReadOnlyDictionary<string, ReadOnlyDictionaryWritePayload>(backing);
        var fallback = new ReadOnlyDictionary<string, ReadOnlyDictionaryWritePayload>(new SortedDictionary<string, ReadOnlyDictionaryWritePayload>(backing,
            Comparer<string>.Create((left, right) => StringComparer.Ordinal.Compare(right, left))));
        byte[] expected = LuminPackSerializer.Serialize(fallback);
        Assert.Equal(expected, LuminPackSerializer.Serialize(source));

        foreach (int capacity in new[] { expected.Length, expected.Length + 128 })
        {
            var bytes = new byte[capacity + 2];
            Array.Fill(bytes, (byte)0xa3);
            Span<byte> window = bytes.AsSpan(1, capacity);
            var writer = new LuminPackWriter(ref window);
            LuminPackSerializer.Serialize<ReadOnlyDictionary<string, ReadOnlyDictionaryWritePayload>, LuminPackUtf16LengthOptions>(ref writer, source);
            Assert.Equal(expected.Length, writer.Position);
            Assert.Equal(expected, window[..writer.Position].ToArray());
            Assert.Equal((byte)0xa3, bytes[0]);
            Assert.Equal((byte)0xa3, bytes[^1]);
        }
    }

    /// <summary>A key callback can replace dictionary entries and mutate the captured object's fields.</summary>
    [Fact]
    public void CustomStringProfileKeepsTheCapturedObjectPair()
    {
        var current = new ReadOnlyDictionaryWritePayload { Id = 1, Name = "before", Values = [2, 3, 5] };
        var next = new ReadOnlyDictionaryWritePayload { Id = 2, Name = "next", Values = [7] };
        var replacement = new ReadOnlyDictionaryWritePayload { Id = 3, Name = "replacement", Values = [11] };
        var backing = new Dictionary<string, ReadOnlyDictionaryWritePayload>(StringComparer.OrdinalIgnoreCase)
        {
            ["first"] = current,
            ["second"] = next
        };
        var wrapper = new ReadOnlyDictionary<string, ReadOnlyDictionaryWritePayload>(backing);
        var expected = new ReadOnlyDictionary<string, ReadOnlyDictionaryWritePayload>(new Dictionary<string, ReadOnlyDictionaryWritePayload>
        {
            ["first"] = new() { Id = 1, Name = "written after key", Values = [2, 3, 5, 13] },
            ["second"] = replacement
        });

        MutatingStringProfile.OnEncoding = () =>
        {
            backing["first"] = replacement;
            backing["second"] = replacement;
            current.Name = "written after key";
            current.Values.Add(13);
        };
        try
        {
            using var output = new LuminBufferWriter(true);
            LuminPackSerializer.Serialize<ReadOnlyDictionary<string, ReadOnlyDictionaryWritePayload>, MutatingStringProfile>(wrapper, output);
            Assert.Null(MutatingStringProfile.OnEncoding);
            Assert.Same(replacement, backing["first"]);
            Assert.Equal(LuminPackSerializer.Serialize(expected), output.GetSpan().ToArray());
        }
        finally
        {
            MutatingStringProfile.OnEncoding = null;
        }
    }

    /// <summary>Writes sparse mixed pairs through both complete and short fixed windows.</summary>
    [Fact]
    public void BinaryMixedPairsKeepWireAcrossWriterWindows()
    {
        var backing = new Dictionary<string, int>
        {
            ["before"] = 17,
            ["removed"] = 11,
            [new string('z', 71)] = int.MinValue,
            ["\u4e2d"] = -4
        };
        backing.Remove("removed");
        var source = new ReadOnlyDictionary<string, int>(backing);
        var fallback = new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(backing, StringComparer.Ordinal));
        byte[] expected = LuminPackSerializer.Serialize(fallback);
        Assert.Equal(expected, LuminPackSerializer.Serialize(source));

        foreach (int capacity in new[] { expected.Length, expected.Length + 128 })
        {
            var bytes = new byte[capacity + 2];
            Array.Fill(bytes, (byte)0xa3);
            Assert.Equal(expected.Length, WriteMixedToFixedBuffer(source, bytes, capacity));
            Assert.Equal(expected, bytes.AsSpan(1, expected.Length).ToArray());
            Assert.Equal((byte)0xa3, bytes[0]);
            Assert.Equal((byte)0xa3, bytes[^1]);
        }

        var shortBuffer = new byte[expected.Length + 1];
        Array.Fill(shortBuffer, (byte)0xa3);
        Assert.ThrowsAny<Exception>(() => WriteMixedToFixedBuffer(source, shortBuffer, expected.Length - 1));
        Assert.Equal((byte)0xa3, shortBuffer[0]);
        Assert.Equal((byte)0xa3, shortBuffer[^1]);
    }

    /// <summary>Preserves a complete wide unmanaged value immediately after its string key.</summary>
    [Fact]
    public void BinaryMixedPairsKeepWideValues()
    {
        var backing = new Dictionary<string, long>
        {
            ["a"] = 0x102030405060708L,
            [new string('z', 71)] = long.MinValue,
            ["\u4e2d"] = long.MaxValue
        };
        var source = new ReadOnlyDictionary<string, long>(backing);
        var fallback = new ReadOnlyDictionary<string, long>(new SortedDictionary<string, long>(backing, StringComparer.Ordinal));
        byte[] expected = LuminPackSerializer.Serialize(fallback);
        Assert.Equal(expected, LuminPackSerializer.Serialize(source));

        using var output = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(source, output);
        Assert.Equal(expected, output.GetSpan().ToArray());
    }

    /// <summary>A custom key profile observes the captured integer and subsequent dictionary mutations.</summary>
    [Fact]
    public void CustomStringProfileKeepsTheCapturedMixedPair()
    {
        var backing = new Dictionary<string, int> { ["first"] = 17, ["second"] = 31 };
        var source = new ReadOnlyDictionary<string, int>(backing);
        var expected = new ReadOnlyDictionary<string, int>(new Dictionary<string, int> { ["first"] = 17, ["second"] = 42 });
        MutatingStringProfile.OnEncoding = () =>
        {
            backing["first"] = 18;
            backing["second"] = 42;
        };
        try
        {
            using var output = new LuminBufferWriter(true);
            LuminPackSerializer.Serialize<ReadOnlyDictionary<string, int>, MutatingStringProfile>(source, output);
            Assert.Null(MutatingStringProfile.OnEncoding);
            Assert.Equal(18, backing["first"]);
            Assert.Equal(LuminPackSerializer.Serialize(expected), output.GetSpan().ToArray());
        }
        finally
        {
            MutatingStringProfile.OnEncoding = null;
        }
    }

    /// <summary>Writes an exact mixed wrapper through its closed fixed-window entry.</summary>
    /// <param name="source">The wrapped dictionary.</param>
    /// <param name="destination">The destination with leading and trailing canaries.</param>
    /// <param name="capacity">The length of the writer's window.</param>
    /// <returns>The writer position after success.</returns>
    private static int WriteMixedToFixedBuffer(ReadOnlyDictionary<string, int> source, byte[] destination, int capacity)
    {
        Span<byte> window = destination.AsSpan(1, capacity);
        var writer = new LuminPackWriter(ref window);
        LuminPackSerializer.Serialize<ReadOnlyDictionary<string, int>, LuminPackUtf16LengthOptions>(ref writer, source);
        return writer.Position;
    }

    /// <summary>Writes the wrapper to the requested guarded window.</summary>
    /// <param name="source">The wrapper to write.</param>
    /// <param name="destination">The storage including leading and trailing canaries.</param>
    /// <param name="capacity">The bytes available to the writer.</param>
    /// <returns>The published writer position.</returns>
    private static int WriteToFixedBuffer(ReadOnlyDictionary<string, string?> source, byte[] destination, int capacity)
    {
        Span<byte> window = destination.AsSpan(1, capacity);
        var writer = new LuminPackWriter(ref window);
        LuminPackSerializer.Serialize<ReadOnlyDictionary<string, string?>, LuminPackUtf16LengthOptions>(ref writer, source);
        return writer.Position;
    }

    /// <summary>Runs a one-shot callback before returning the normal binary string profile.</summary>
    public readonly struct MutatingStringProfile : ILuminPackOptions
    {
        private global::LuminPack.Option.LuminPackUtf16LengthStrings ObservedCodec
        {
            get
            {
                _ = StringEncoding;
                return default;
            }
        }

        /// <inheritdoc/>
        public int RecordBound(string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).RecordBound(value);

        /// <inheritdoc/>
        public ref byte WriteRecord(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
            ref ObservedCodec.WriteRecord(ref writer, ref cursor, value);

        /// <inheritdoc/>
        public void Write(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
            ObservedCodec.Write(ref writer, value);

        /// <inheritdoc/>
        public string? Read(scoped ref global::LuminPack.Core.LuminPackReader reader, string? current) =>
            ObservedCodec.Read(ref reader, current);

        /// <inheritdoc/>
        public void ReadPair(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
            ObservedCodec.ReadPair(ref reader, ref first, ref second);

        /// <inheritdoc/>
        public void ReadSlots(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped global::System.Span<string?> slots) =>
            ObservedCodec.ReadSlots(ref reader, slots);

        /// <inheritdoc/>
        public global::System.Buffers.OperationStatus Encode(global::System.ReadOnlySpan<char> source, global::System.Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

        /// <inheritdoc/>
        public int Measure(string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Measure(value);

        /// <inheritdoc/>
        public void WriteReserved(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
            ObservedCodec.WriteReserved(ref writer, value);

        /// <inheritdoc/>
        public void WriteText(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<char> text) =>
            ObservedCodec.WriteText(ref writer, text);

        /// <inheritdoc/>
        public global::System.Buffers.OperationStatus Decode(global::System.ReadOnlySpan<byte> source, global::System.Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

        /// <inheritdoc/>
        public bool SupportsUtf16Runs() =>
            false;

        /// <inheritdoc/>
        public void WriteSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            ObservedCodec.WriteSlots(ref writer, values);

        /// <inheritdoc/>
        public void WriteCheckedSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            ObservedCodec.WriteCheckedSlots(ref writer, values);

        /// <inheritdoc/>
        public void WritePairs(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values) =>
            ObservedCodec.WritePairs(ref writer, keys, values);

        /// <inheritdoc/>
        public void WritePairRuns(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values, bool allowRuns) =>
            ObservedCodec.WritePairRuns(ref writer, keys, values, allowRuns);

        /// <inheritdoc/>
        public ref byte WriteAtCursor(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
            ref ObservedCodec.WriteAtCursor(ref writer, ref cursor, value);

        /// <inheritdoc/>
        public int WriteSlotPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteSlotPrefix(ref writer, values);

        /// <inheritdoc/>
        public int WritePairPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, string?>> pairs) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairPrefix(ref writer, pairs);

        /// <inheritdoc/>
        public bool TryPeekCharacterCount(scoped ref global::LuminPack.Core.LuminPackReader reader, out int count) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekCharacterCount(ref reader, out count);

        /// <inheritdoc/>
        public bool TryPeekUtf16Payload(scoped ref global::LuminPack.Core.LuminPackReader reader, int length, out int bytes, out bool latin1) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

        /// <inheritdoc/>
        public global::LuminPack.Option.LuminPackStringEncoding Encoding => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encoding;

        /// <inheritdoc/>
        public global::LuminPack.Option.LuminPackStringRecording Recording => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Recording;

        /// <inheritdoc/>
        public byte BinaryProfile => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).BinaryProfile;

        [ThreadStatic]
        internal static Action? OnEncoding;

        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding
        {
            get
            {
                Action? callback = OnEncoding;
                OnEncoding = null;
                callback?.Invoke();
                return LuminPackStringEncoding.UTF16;
            }
        }

        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
        /// <inheritdoc/>
        public StandardFormat StandardFormat => new('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 1024;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }
}

/// <summary>Provides one ordinary object value with a mutable nested raw list.</summary>
[LuminPackable]
public sealed partial class ReadOnlyDictionaryWritePayload
{
    /// <summary>Gets the value identity stored in the binary record.</summary>
    public int Id;
    /// <summary>Gets or sets the value name written after the dictionary key.</summary>
    public string Name = "";
    /// <summary>Gets the values observed by the nested list writer.</summary>
    public List<int> Values = [];
}
