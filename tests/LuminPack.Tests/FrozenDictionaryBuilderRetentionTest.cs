using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks rounded allocation retention in the frozen dictionary builder pool.</summary>
public sealed class FrozenDictionaryBuilderRetentionTest
{
    /// <summary>Retains a rounded builder only inside the count window and clears it before reuse.</summary>
    [Fact]
    public void RoundedBuilderRespectsCountLimits()
    {
        foreach ((int capacity, int count) in new[] { (4049, 4049), (4050, 4049), (4050, 4050), (4096, 4096), (4097, 4097) })
        {
            var builder = new Dictionary<int, CapacityValue>(capacity);
            for (int index = 0; index < count; index++)
                builder.Add(index, new CapacityValue());
            int tableCapacity = builder.EnsureCapacity(0);
            int retentionLimit = count is > 4049 and <= 4096 ? 4861 : 4096;
            LuminFrozenConstruction.ReturnDictionary(builder);
            var next = LuminFrozenConstruction.RentDictionary<int, CapacityValue>(capacity);

            Assert.Equal(tableCapacity <= retentionLimit, ReferenceEquals(builder, next));
            Assert.Empty(builder);
            Assert.Empty(next);
            Assert.Equal(tableCapacity, next.EnsureCapacity(0));
        }
    }

    /// <summary>Keeps the existing slot bound and excludes builders with a custom comparer.</summary>
    [Fact]
    public void RoundedBuildersKeepTheOriginalPoolConstraints()
    {
        const int count = 4096;
        var builders = new Dictionary<int, SlotValue>[5];
        for (int slot = 0; slot < builders.Length; slot++)
        {
            var builder = new Dictionary<int, SlotValue>(count);
            for (int index = 0; index < count; index++)
                builder.Add(index, new SlotValue());
            builders[slot] = builder;
        }

        bool recognizedCapacity = builders[0].EnsureCapacity(0) <= 4861;
        foreach (var builder in builders)
            LuminFrozenConstruction.ReturnDictionary(builder);
        var observed = new HashSet<Dictionary<int, SlotValue>>(ReferenceEqualityComparer.Instance);
        for (int slot = 0; slot < builders.Length; slot++)
        {
            var rented = LuminFrozenConstruction.RentDictionary<int, SlotValue>(count);
            Assert.Empty(rented);
            Assert.True(observed.Add(rented));
        }
        Assert.Equal(recognizedCapacity ? 4 : 0, builders.Count(observed.Contains));

        var custom = new Dictionary<int, SlotValue>(count, new CustomIntComparer());
        for (int index = 0; index < count; index++)
            custom.Add(index, new SlotValue());
        LuminFrozenConstruction.ReturnDictionary(custom);
        Assert.Empty(custom);
        Assert.NotSame(custom, LuminFrozenConstruction.RentDictionary<int, SlotValue>(count));
    }

    private sealed class CapacityValue;
    private sealed class SlotValue;

    private sealed class CustomIntComparer : EqualityComparer<int>
    {
        /// <inheritdoc />
        public override bool Equals(int x, int y) => x == y;

        /// <inheritdoc />
        public override int GetHashCode(int obj) => obj;
    }
}
