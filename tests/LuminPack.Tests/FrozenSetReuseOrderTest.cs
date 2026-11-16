#if NET10_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks identity and byte order when reusing an immutable binary frozen set.</summary>
public sealed class FrozenSetReuseOrderTest
{
    /// <summary>Requires a prior verified result and preserves it when a later record is truncated.</summary>
    [Fact]
    public void StableRawInputKeepsTheVerifiedSet()
    {
        var external = Enumerable.Range(0, 4096).Select(i => i * 17).ToFrozenSet();
        byte[] bytes = LuminPackSerializer.Serialize(external);
        FrozenSet<int>? target = external;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.NotSame(external, target);

        var prior = target;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        bool supported = RuntimeFeature.IsDynamicCodeCompiled && Environment.Version is { Major: 10 or 11, Minor: 0 } &&
            typeof(FrozenSet<int>).Assembly.GetName().Version?.Major == Environment.Version.Major;
        Assert.Equal(supported, ReferenceEquals(prior, target));
        Assert.Equal(bytes, LuminPackSerializer.Serialize(target));

        prior = target;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        Assert.Same(prior, target);
    }

    /// <summary>Retains the stock reordering of colliding values even when input matches the previous set's item order.</summary>
    [Fact]
    public void CollisionOrderMatchesAStockRead()
    {
        var random = new Random(1729);
        FrozenSet<int>? target = Enumerable.Range(0, 64).Select(_ => random.Next()).ToFrozenSet();
        for (int read = 0; read < 4; read++)
        {
            byte[] bytes = LuminPackSerializer.Serialize(target);
            FrozenSet<int>? fresh = null;
            LuminPackSerializer.Deserialize(bytes, ref fresh);
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.Equal(LuminPackSerializer.Serialize(fresh), LuminPackSerializer.Serialize(target));
        }
    }
}
#endif
