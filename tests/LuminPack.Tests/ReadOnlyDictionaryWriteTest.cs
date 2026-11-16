using System.Buffers;
using System.Collections.ObjectModel;
using LuminPack;
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
