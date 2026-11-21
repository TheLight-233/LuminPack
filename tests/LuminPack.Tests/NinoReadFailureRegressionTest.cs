#if LUMINPACK_NEXT_GENERATOR
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks storage publication and growth when reading incomplete input.</summary>
public sealed class NinoReadFailureRegressionTest
{
    /// <summary>A custom stream's length hint cannot force a large initial request.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Fact]
    public async Task StreamLengthHintCannotForceALargeInitialRental()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new[] { 1, 2, 3 });
        using var stream = new ClaimedLengthStream(bytes);

        Assert.Equal(new[] { 1, 2, 3 }, await LuminPackSerializer.DeserializeAsync<int[]>(stream));
        Assert.InRange(stream.FirstRequest, 1, 128 * 1024);
    }

    /// <summary>A custom stream continues to grow its storage as actual input arrives.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Fact]
    public async Task StreamCanGrowBeyondItsInitialLengthHint()
    {
        byte[] values = Enumerable.Repeat((byte)5, 256 * 1024).ToArray();
        byte[] bytes = LuminPackSerializer.Serialize(values);
        using var stream = new ClaimedLengthStream(bytes);

        Assert.Equal(values, await LuminPackSerializer.DeserializeAsync<byte[]>(stream));
        Assert.InRange(stream.FirstRequest, 1, 128 * 1024);
    }

    /// <summary>A truncated replacement leaves the original rectangular array available for another read.</summary>
    [Fact]
    public void TruncatedRectangularReadKeepsItsOriginalArray()
    {
        var original = new NinoRectangularVersionedValue[1, 1];
        original[0, 0].Text = "old";
        var target = original;
        var values = new NinoRectangularVersionedValue[1, 2];
        values[0, 0].Text = "first";
        values[0, 1].Text = "last";
        byte[] bytes = LuminPackSerializer.Serialize(values);

        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        Assert.Same(original, target);
        Assert.Equal("old", target[0, 0].Text);

        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Equal(2, target.GetLength(1));
        Assert.Equal("first", target[0, 0].Text);
        Assert.Equal("last", target[0, 1].Text);
    }

    /// <summary>Reports a length unrelated to the supplied bytes and records the initial read request.</summary>
    /// <param name="bytes">The actual stream payload.</param>
    private sealed class ClaimedLengthStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal int FirstRequest;

        /// <inheritdoc/>
        public override long Length => 64 * 1024 * 1024;

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (FirstRequest == 0)
                FirstRequest = buffer.Length;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}

/// <summary>Provides a variable-width value for the rectangular array replacement path.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public partial struct NinoRectangularVersionedValue
{
    public long Number;
    public string? Text;
}
#endif
