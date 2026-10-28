using System.Buffers;
using System.Collections.Immutable;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Proves mutable and immutable collection readers borrow the same framework element pool.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class SortedReuseSharedPoolTest
{
    /// <summary>Observes one physical string rental in both set reuse and immutable-list materialization.</summary>
    [Fact]
    public void DifferentCollectionReadersShareTheSameStringRental()
    {
        string[] values = { "alpha", "beta", "gamma" };
        byte[] payload = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(values);
        string[] expected = ArrayPool<string>.Shared.Rent(16);
        ArrayPool<string>.Shared.Return(expected, clearArray: true);
        string[]? returned = null;
        try
        {
            SharedCollectionPoolOptions.Expected = expected;
            SharedCollectionPoolOptions.SawPopulatedRental = false;
            var set = new SortedSet<string> { "old" };
            var original = set;
            Assert.Equal(payload.Length, LuminPackSerializer.Deserialize<SharedCollectionPoolOptions>(payload, ref set));
            Assert.Same(original, set);
            Assert.True(set.SetEquals(values));
            Assert.True(SharedCollectionPoolOptions.SawPopulatedRental);
            Assert.All(expected, value => Assert.Null(value));

            SharedCollectionPoolOptions.SawPopulatedRental = false;
            ImmutableList<string>? list = null;
            Assert.Equal(payload.Length, LuminPackSerializer.Deserialize<SharedCollectionPoolOptions>(payload, ref list));
            Assert.Equal(values, list);
            Assert.True(SharedCollectionPoolOptions.SawPopulatedRental);
            Assert.All(expected, value => Assert.Null(value));
            returned = ArrayPool<string>.Shared.Rent(16);
            Assert.Same(expected, returned);
        }
        finally
        {
            SharedCollectionPoolOptions.Expected = null;
            SharedCollectionPoolOptions.SawPopulatedRental = false;
            if (returned is not null) ArrayPool<string>.Shared.Return(returned, clearArray: true);
            // Restore a captured rental if an assertion exposed the former reader losing it.
            if (returned is not null && !ReferenceEquals(returned, expected)) ArrayPool<string>.Shared.Return(expected, clearArray: true);
        }
    }
}

/// <summary>Observes shared reference storage while returning the unchanged UTF-16 length profile.</summary>
public readonly struct SharedCollectionPoolOptions : ILuminPackOptions
{
    public static string[]? Expected;
    public static bool SawPopulatedRental;

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        get
        {
            if (Expected is { Length: > 0 } values && values[0] == "alpha") SawPopulatedRental = true;
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
