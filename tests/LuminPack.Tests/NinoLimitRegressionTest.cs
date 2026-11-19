#if LUMINPACK_NEXT_GENERATOR
extern alias NinoLimits;

using Xunit;
using Fixture = NinoLimits::LuminPack.Tests.InputLimits.NinoLimitRegressionFixture;

namespace LuminPack.Tests;

/// <summary>Checks the assembly's binary input policy on framework roots and their nested elements.</summary>
public class NinoLimitRegressionTest
{
    /// <summary>Each formatter rejects a count greater than the declared collection limit.</summary>
    /// <param name="shape">The overlong collection format.</param>
    [Theory]
    [InlineData("array")]
    [InlineData("boolean array")]
    [InlineData("list")]
    [InlineData("boolean list")]
    [InlineData("dictionary")]
    [InlineData("concurrent dictionary")]
    [InlineData("sorted dictionary")]
    [InlineData("immutable dictionary")]
    [InlineData("set")]
    [InlineData("queue")]
    [InlineData("linked list")]
    [InlineData("read-only collection")]
    [InlineData("immutable array")]
    [InlineData("memory")]
    [InlineData("array segment")]
    [InlineData("string array")]
    [InlineData("rectangular array")]
    [InlineData("jagged array element")]
    public void OverlongRootCollectionsRejectTheDeclaredLength(string shape) =>
        Assert.Contains("collection length", Assert.Throws<InvalidDataException>(() => Fixture.ReadOverlong(shape)).Message);

    /// <summary>Reused storage still observes the configured collection bound.</summary>
    [Fact]
    public void ReusedArraysStillRejectTheDeclaredLength()
    {
        Assert.Throws<InvalidDataException>(Fixture.ReadOverlongArrayIntoExisting);
        Assert.Throws<InvalidDataException>(Fixture.ReadOverlongRectangularArrayIntoExisting);
    }

    /// <summary>The count bound applies even when the element storage is empty.</summary>
    [Fact]
    public void ZeroWidthRootArrayChecksCountBeforeAllocating()
    {
        Assert.Equal(3, Fixture.ReadForgedZeroWidthArray(3).Length);
        Assert.Contains("collection length", Assert.Throws<InvalidDataException>(() => Fixture.ReadForgedZeroWidthArray(1_000_000)).Message);
    }

    /// <summary>The same character bound applies to a string root and a nested string element.</summary>
    [Fact]
    public void RootStringAndStringArrayElementsCheckTheCharacterLimit()
    {
        Assert.Equal("four", Fixture.ReadString("four"));
        Assert.Equal(new[] { "four" }, Fixture.ReadStringArray("four"));
        Assert.Contains("string length", Assert.Throws<InvalidDataException>(() => Fixture.ReadString("hello")).Message);
        Assert.Contains("string length", Assert.Throws<InvalidDataException>(() => Fixture.ReadStringArray("hello")).Message);
    }

    /// <summary>Sequence and open-generic entries use the checked framework read.</summary>
    [Fact]
    public void RootLimitsApplyToSequenceAndRegisteredReads()
    {
        int[] values = [1, 2, 3];
        Assert.Equal(values, Fixture.ReadArray(values));
        Assert.Equal(values, Fixture.ReadSequence(values));
        Assert.Equal(values, Fixture.ReadRegisteredArray(values));
        Assert.Equal("four", Fixture.ReadRegisteredString("four"));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadSequence([1, 2, 3, 4]));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadRegisteredArray([1, 2, 3, 4]));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadRegisteredString("hello"));
    }

    /// <summary>A type that reads trusted input keeps its own policy.</summary>
    [Fact]
    public void TrustedTypeKeepsItsOwnInputPolicy()
    {
        int[] values = [1, 2, 3, 4];
        Assert.Equal(values, Fixture.ReadTrustedArray(values));
    }

    /// <summary>Explicit hostile-input settings reject every noncanonical Boolean byte under test.</summary>
    /// <param name="value">The noncanonical Boolean byte.</param>
    [Theory]
    [InlineData(2)]
    [InlineData(128)]
    [InlineData(255)]
    public void ExplicitUntrustedRootsRejectNoncanonicalBooleans(byte value)
    {
        Assert.Throws<InvalidDataException>(() => Fixture.ReadBoolean(value));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadBooleanArray(value));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadBooleanList(value));
    }
}
#endif
