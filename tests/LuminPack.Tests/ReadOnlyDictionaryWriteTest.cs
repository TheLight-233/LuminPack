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
