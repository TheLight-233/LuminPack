using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the exact fixed-case tests at the dictionary Union cursor call site against the ordinary virtual dispatch.</summary>
public class UnionCursorChainTest
{
    /// <summary>Checks inherited fixed cases, a variable-length case and null values write the bytes of the non-cursor path and round trip.</summary>
    [Fact]
    public void ExactCasesMatchTheVirtualPath()
    {
        var values = new SortedDictionary<int, ChainRoot?>
        {
            [1] = new ChainA { Id = 1, X = 10 },
            [2] = new ChainB { Id = 2, X = 20, Y = long.MaxValue },
            [3] = new ChainText { Id = 3, Text = "text" },
            [4] = null,
            [5] = new ChainB { Id = 5, X = -5, Y = -1 },
            [6] = new ChainA { Id = 6, X = 60 }
        };
        // SortedDictionary writes each value through the root's virtual slot; Dictionary uses the cursor call site.
        byte[] expected = LuminPackSerializer.Serialize(values);
        var dictionary = new Dictionary<int, ChainRoot?>(values);
        Assert.Equal(expected, LuminPackSerializer.Serialize(dictionary));

        var copy = LuminPackSerializer.Deserialize<Dictionary<int, ChainRoot?>>(expected)!;
        Assert.IsType<ChainA>(copy[1]);
        Assert.IsType<ChainB>(copy[2]);
        Assert.IsType<ChainText>(copy[3]);
        Assert.Null(copy[4]);
        Assert.Equal(long.MaxValue, ((ChainB)copy[2]!).Y);
        Assert.Equal(expected, LuminPackSerializer.Serialize(copy));
    }

    /// <summary>Checks a subtype that is not a declared case keeps the original slot's outcome instead of being written as its base case.</summary>
    [Fact]
    public void UndeclaredSubtypesKeepTheSlotOutcome()
    {
        var sorted = new SortedDictionary<int, ChainRoot?> { [1] = new ChainUndeclared { Id = 1, X = 7, Z = 9 } };
        var dictionary = new Dictionary<int, ChainRoot?>(sorted);
        byte[]? expected = null;
        Exception? expectedFailure = null;
        try { expected = LuminPackSerializer.Serialize(sorted); }
        catch (Exception exception) { expectedFailure = exception; }
        if (expectedFailure is null)
            Assert.Equal(expected, LuminPackSerializer.Serialize(dictionary));
        else
            Assert.Equal(expectedFailure.GetType(), Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Serialize(dictionary)).GetType());
    }
}

/// <summary>A Union root with inherited fixed cases.</summary>
[LuminPackable, LuminPackUnion(0, typeof(ChainA)), LuminPackUnion(1, typeof(ChainB)), LuminPackUnion(2, typeof(ChainText))]
public abstract partial class ChainRoot
{
    /// <summary>The identifier.</summary>
    public int Id;
}

/// <summary>A fixed case with a derived fixed case.</summary>
[LuminPackable]
public partial class ChainA : ChainRoot
{
    /// <summary>A value.</summary>
    public int X;
}

/// <summary>A fixed case deriving from another case.</summary>
[LuminPackable]
public partial class ChainB : ChainA
{
    /// <summary>A value.</summary>
    public long Y;
}

/// <summary>A variable-length case, which keeps the cursor slot.</summary>
[LuminPackable]
public sealed partial class ChainText : ChainRoot
{
    /// <summary>The text.</summary>
    public string? Text;
}

/// <summary>A subtype of a fixed case that is not a declared Union case.</summary>
public sealed class ChainUndeclared : ChainA
{
    /// <summary>A value outside the declared case.</summary>
    public int Z;
}
