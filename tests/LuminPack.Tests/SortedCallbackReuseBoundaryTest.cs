using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects tree ordering when a callback reinserts an existing mutable element.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class SortedCallbackReuseBoundaryTest
{
    /// <summary>A future old element reinserted by user code must not later be mutated as a positional seed.</summary>
    [Fact]
    public void ReinsertedFutureElementRetainsItsKeyAndTreePosition()
    {
        var comparer = Comparer<ReferenceReuseCallback>.Create((a, b) => (a?.Number ?? -1).CompareTo(b?.Number ?? -1));
        ReferenceReuseCallback[] original = [new() { Number = 719 }, new() { Number = 981 }, new() { Number = 1981 }];
        var target = new SortedSet<ReferenceReuseCallback>(original, comparer);
        byte[] bytes = LuminPackSerializer.Serialize(new ReferenceReuseCallback[] { new() { Number = 137 }, new() { Number = 271 }, new() { Number = 3 } });
        ReferenceReuseCallback.OnRead = item =>
        {
            if (item.Number == 137) target.Add(original[2]);
        };
        try
        {
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.Equal(new[] { 3, 137, 271, 1981 }, target.Select(item => item.Number));
            Assert.Equal(1981, original[2].Number);
            Assert.Contains(original[2], target);
            Assert.Equal(4, target.Count);
        }
        finally { ReferenceReuseCallback.OnRead = null; }
    }
}
