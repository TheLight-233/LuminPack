using System.Collections.Immutable;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the cutoff and input ownership of the direct immutable queue read.</summary>
public sealed class ImmutableQueueWindowTest
{
    /// <summary>Checks a truncated and a complete unaligned payload across the direct-read cutoff.</summary>
    /// <param name="count">The number of queued elements.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(9)]
    public void UnalignedInputIsValidatedAndNeverRetained(int count)
    {
        var expected = ImmutableQueue.CreateRange(Enumerable.Range(-4, count));
        byte[] payload = LuminPackSerializer.Serialize(expected);
        byte[] unaligned = new byte[1 + payload.Length];
        payload.CopyTo(unaligned, 1);
        var target = ImmutableQueue.Create(71);
        var original = target;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(unaligned.AsSpan(1, payload.Length - 1), ref target));
        Assert.Same(original, target);
        Assert.Equal(payload.Length, LuminPackSerializer.Deserialize(unaligned.AsSpan(1), ref target));
        Array.Clear(unaligned);
        Assert.Equal(expected.ToArray(), target.ToArray());
    }
}
