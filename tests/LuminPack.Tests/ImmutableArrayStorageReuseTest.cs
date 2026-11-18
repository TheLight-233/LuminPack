#if LUMINPACK_NEXT_GENERATOR && NET8_0_OR_GREATER
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Exercises immutable storage reuse through emitted binary entries.</summary>
public sealed class ImmutableArrayStorageReuseTest
{
    /// <summary>Retains the exact backing array only when every incoming byte matches.</summary>
    [Fact]
    public void RawImmutableArrayReusesIdenticalStorage()
    {
        var target = ImmutableArray.Create(1, 2, 3);
        var original = ImmutableCollectionsMarshal.AsArray(target);
        byte[] equal = LuminPackSerializer.Serialize(ImmutableArray.Create(1, 2, 3));
        Assert.Equal(equal.Length, LuminPackSerializer.Deserialize(equal, ref target));
        Assert.Same(original, ImmutableCollectionsMarshal.AsArray(target));

        byte[] changed = LuminPackSerializer.Serialize(ImmutableArray.Create(1, 2, 4));
        Assert.Equal(changed.Length, LuminPackSerializer.Deserialize(changed, ref target));
        Assert.NotSame(original, ImmutableCollectionsMarshal.AsArray(target));
        Assert.Equal(new[] { 1, 2, 4 }, target.AsSpan().ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, original);

        byte[] empty = LuminPackSerializer.Serialize(ImmutableArray<int>.Empty);
        Assert.Equal(empty.Length, LuminPackSerializer.Deserialize(empty, ref target));
        Assert.True(target.IsEmpty);
        byte[] missing = LuminPackSerializer.Serialize(default(ImmutableArray<int>));
        Assert.Equal(missing.Length, LuminPackSerializer.Deserialize(missing, ref target));
        Assert.True(target.IsDefault);
    }
}
#endif
