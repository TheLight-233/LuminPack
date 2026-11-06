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

    /// <summary>Checks versioning for empty and populated equal-size spans, including writes through the returned storage.</summary>
    /// <param name="count">The existing list count.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void SameSizeSpanPublishesTheExistingStorage(int count)
    {
        var list = Enumerable.Range(0, count).ToList();
        int capacity = list.Capacity;
        var enumerator = list.GetEnumerator();
        var span = LuminPackMarshal.GetListSpan(list, count);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Equal(capacity, list.Capacity);
        Assert.Equal(count, span.Length);
        for (int i = 0; i < count; i++) span[i] += 10;
        Assert.Equal(Enumerable.Range(10, count), list);
    }

    /// <summary>Checks exact growth and clearing of reference slots when a resized span is requested.</summary>
    [Fact]
    public void ResizedSpanClearsRemovedReferencesAndKeepsExactCapacity()
    {
        var list = new List<string?>(2) { "first", "second" };
        Assert.Equal("first", LuminPackMarshal.GetListSpan(list, 1)[0]);
        var grown = LuminPackMarshal.GetListSpan(list, 3);
        Assert.Equal(3, list.Capacity);
        Assert.Equal(new string?[] { "first", null, null }, grown.ToArray());
    }
}
