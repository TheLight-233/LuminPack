using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects list versioning when generated fresh reads publish an unchanged count.</summary>
public class ListSizeVersionTest
{
    /// <summary>Checks the modern SetCount contract without growing or clearing existing storage.</summary>
    [Fact]
    public void PublishingTheSameCountInvalidatesExistingEnumerators()
    {
        var value = new List<string> { "first", "second" };
        int capacity = value.Capacity;
        var enumerator = value.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        LuminPackMarshal.SetListSize(value, value.Count);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Equal(capacity, value.Capacity);
        Assert.Equal(new[] { "first", "second" }, value);
    }
}
