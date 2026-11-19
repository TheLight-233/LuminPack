#if LUMINPACK_NEXT_GENERATOR
using LuminPack.Attribute;
using Xunit;

namespace LuminPack.Tests;

/// <summary>Checks comparer-dependent key preservation in binary dictionary refill.</summary>
public class NinoDictionaryRegressionTest
{
    /// <summary>An equivalent input key keeps the retained spelling as unmatched keys are removed.</summary>
    [Fact]
    public void RefillKeepsExistingStringKeySpelling()
    {
        var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["stale"] = "remove",
            ["KEEP"] = "previous",
        };
        var original = target;
        var input = new Dictionary<string, string> { ["keep"] = "updated" };

        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(input), ref target);

        Assert.Same(original, target);
        Assert.Equal("KEEP", Assert.Single(target).Key);
        Assert.Equal("updated", target["keep"]);
    }

    /// <summary>Equivalent keys and existing instance values can both be reused when the order changes.</summary>
    /// <param name="customComparer">Whether the comparer lacks the framework alternate lookup.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefillKeepsEquivalentKeysAndTheirExistingValuesInWireOrder(bool customComparer)
    {
        var first = new DictionaryRefillValue { Number = -1 };
        var second = new DictionaryRefillValue { Number = -2 };
        IEqualityComparer<string> comparer = customComparer ? new IgnoreCaseComparer() : StringComparer.OrdinalIgnoreCase;
        var target = new Dictionary<string, DictionaryRefillValue>(comparer)
        {
            ["FIRST"] = first,
            ["SECOND"] = second,
            ["STALE"] = new DictionaryRefillValue(),
        };
        var input = new Dictionary<string, DictionaryRefillValue>
        {
            ["second"] = new DictionaryRefillValue { Number = 2 },
            ["first"] = new DictionaryRefillValue { Number = 1 },
            ["new"] = new DictionaryRefillValue { Number = 3 },
        };

        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(input), ref target);

        Assert.Equal(new[] { "SECOND", "FIRST", "new" }, target.Keys);
        Assert.Same(second, target["SECOND"]);
        Assert.Same(first, target["FIRST"]);
        Assert.Equal(2, second.Number);
        Assert.Equal(1, first.Number);
        Assert.Equal(3, target["new"].Number);
    }

    /// <summary>The destination comparer still rejects duplicate incoming entries.</summary>
    [Fact]
    public void RefillRejectsDuplicateIncomingKeysUnderTheDestinationComparer()
    {
        var target = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["KEEP"] = -1 };
        var input = new Dictionary<string, int> { ["keep"] = 1, ["KEEP"] = 2 };
        byte[] source = LuminPackSerializer.Serialize(input);

        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(source, ref target));
    }

    /// <summary>Supplies case-insensitive comparison through the ordinary comparer contract.</summary>
    private sealed class IgnoreCaseComparer : IEqualityComparer<string>
    {
        /// <inheritdoc/>
        public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);

        /// <inheritdoc/>
        public int GetHashCode(string value) => StringComparer.OrdinalIgnoreCase.GetHashCode(value);
    }
}

/// <summary>A mutable dictionary value with observable instance reuse.</summary>
[LuminPackable]
public partial class DictionaryRefillValue
{
    /// <summary>The deserialized number.</summary>
    public int Number;
}
#endif
