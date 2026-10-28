using System.Globalization;
using System.Reflection;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects ordered fresh construction, public-constructor fallbacks and unchanged reuse boundaries.</summary>
public sealed class SortedSetConstructionTest
{
    /// <summary>Matches the public constructor's nodes, colors and later mutations around every small tree size.</summary>
    [Fact]
    public void OrderedFrameworkValuesKeepTheirTreeAndMutationBehavior()
    {
        for (int count = 0; count <= 257; count++)
        {
            Check(Enumerable.Range(0, count).Select(index => index * 17 - 3000).ToArray());
            Check(Enumerable.Range(0, count).Select(index => (long)index * 7919 - 50000).ToArray());
            Check(Enumerable.Range(0, count).Select(index => new Guid(index, 17, 29, 3, 5, 7, 11, 13, 17, 19, 23)).ToArray());
        }
    }

    /// <summary>Preserves public sorting and duplicate selection when the ascending proof declines.</summary>
    [Fact]
    public void UnorderedDuplicateAndSpecialValuesKeepPublicConstruction()
    {
        Check(new[] { 9, 7, 5, 3, 1 });
        Check(new[] { 3, 3, 3 });
        Check(new[] { 4, -1, 7, 2, 4 });
        Check(new[] { int.MinValue, 0, int.MaxValue });
        Check(new string?[] { null, "a", "b", "c" });
        Check(new string?[] { "b", null, "a", null, "b" });
        Check(new[] { double.NaN, double.NegativeInfinity, -1d, -0d, 1d, double.PositiveInfinity });
        Check(new[] { 0d, -0d, double.NaN, double.NaN, 2d });
        Check(new[] { Half.NaN, Half.NegativeInfinity, (Half)(-2), (Half)0, (Half)2, Half.PositiveInfinity });
        Check(new[] { decimal.MinValue, -1m, 0m, 1m, decimal.MaxValue });
        Check(new[] { DateTime.MinValue, DateTime.UnixEpoch, DateTime.MaxValue });
        Check(new[] { DateOnly.MinValue, DateOnly.FromDayNumber(17), DateOnly.MaxValue });
        Check(new[] { TimeOnly.MinValue, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(17)), TimeOnly.MaxValue });
    }

    /// <summary>Uses the current default string comparer without retaining the caller's rented array.</summary>
    [Fact]
    public void CultureAndInputOwnershipMatchThePublicConstructor()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "en-US", "tr-TR", "sv-SE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var values = new[] { "I", "i", "\u0130", "\u0131", "a", "\u00e4", "z" };
                Array.Sort(values);
                Check(values);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
        var source = new[] { 2, 5, 8, 11, 17, 23 };
        var result = LuminTreeStorage.CreateSortedSet(source, source.Length);
        source[0] = -123;
        Assert.DoesNotContain(-123, result);
    }

    /// <summary>Checks every binary string profile and rejects every incomplete record before fresh publication.</summary>
    [Fact]
    public void ProfilesAndTruncationKeepTheirPublicationBoundary()
    {
        CheckProfile<LuminPackUtf8LengthOptions>();
        CheckProfile<LuminPackUtf8TokenOptions>();
        CheckProfile<LuminPackUtf16LengthOptions>();
        CheckProfile<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Preserves existing custom comparers and the original partial failure through a reused range view.</summary>
    [Fact]
    public void ReusedSetsKeepTheirIdentityComparerAndViewBoundary()
    {
        byte[] complete = LuminPackSerializer.Serialize(new SortedSet<int> { -1, 3, 9 });
        var reverse = new SortedSet<int>(Comparer<int>.Create((left, right) => right.CompareTo(left))) { 77 };
        var identity = reverse;
        Assert.Equal(complete.Length, LuminPackSerializer.Deserialize(complete, ref reverse));
        Assert.Same(identity, reverse);
        Assert.Equal(new[] { 9, 3, -1 }, reverse);

        var parent = new SortedSet<int> { 0, 5, 100 };
        SortedSet<int> view = parent.GetViewBetween(-10, 10);
        var originalView = view;
        byte[] outsideRange = LuminPackSerializer.Serialize(new SortedSet<int> { 1, 3, 99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => LuminPackSerializer.Deserialize(outsideRange, ref view));
        Assert.Same(originalView, view);
        Assert.Equal(new[] { 1, 3 }, view);
        Assert.Equal(new[] { 1, 3, 100 }, parent);
    }

    /// <summary>Reads arbitrary valid ordering and duplicate records through the original set semantics.</summary>
    [Fact]
    public void WireOrderingDuplicatesAndNullArePreserved()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new[] { 5, 3, 3, 9, -1 });
        SortedSet<int>? value = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref value));
        Assert.Equal(new[] { -1, 3, 5, 9 }, value!);
        var nullBytes = new byte[] { 255, 255, 255, 255 };
        Assert.Equal(4, LuminPackSerializer.Deserialize(nullBytes, ref value));
        Assert.Null(value);
    }

    /// <summary>Rejects invalid public helper counts before constructing an invalid set.</summary>
    [Fact]
    public void InvalidHelperCountsCannotPublishAnInvalidSet()
    {
        Assert.ThrowsAny<Exception>(() => LuminTreeStorage.CreateSortedSet(new[] { 1, 2 }, -1));
        Assert.ThrowsAny<Exception>(() => LuminTreeStorage.CreateSortedSet(new[] { 1, 2 }, 3));
    }

    /// <summary>Checks a concrete string root under one compile-time binary profile.</summary>
    /// <typeparam name="TOptions">The complete binary profile.</typeparam>
    private static void CheckProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = new SortedSet<string> { "alpha", "\u00e9clair", "\u4e2d\u6587" };
        byte[] complete = LuminPackSerializer.Serialize<TOptions>(source);
        SortedSet<string>? copy = null;
        int consumed = LuminPackSerializer.Deserialize<TOptions>(complete, ref copy);
        Assert.Equal(complete.Length, consumed);
        Assert.True(copy!.SetEquals(source));
        for (int prefix = 0; prefix < complete.Length; prefix++)
        {
            SortedSet<string>? target = null;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<TOptions>(complete.AsSpan(0, prefix), ref target));
            Assert.Null(target);
        }
    }

    /// <summary>Compares complete trees and verifies lookup and mutations after construction.</summary>
    /// <typeparam name="T">The original framework element type.</typeparam>
    /// <param name="values">The complete input in its original order.</param>
    private static void Check<T>(T[] values)
    {
        var expected = new SortedSet<T>(new ArraySegment<T>(values));
        var actual = LuminTreeStorage.CreateSortedSet(values, values.Length);
        Assert.Equal(expected.Count, actual.Count);
        Assert.True(actual.SequenceEqual(expected));
        Assert.Same(Comparer<T>.Default, actual.Comparer);
        var root = typeof(SortedSet<T>).GetField("root", BindingFlags.NonPublic | BindingFlags.Instance)!;
        CheckShape(root.GetValue(expected), root.GetValue(actual));
        foreach (var item in expected.ToArray())
        {
            Assert.Contains(item, actual);
            Assert.False(actual.Add(item));
        }
        int index = 0;
        foreach (var item in expected.ToArray())
            if (index++ % 2 == 0)
            {
                Assert.True(actual.Remove(item));
                expected.Remove(item);
            }
        Assert.True(actual.SequenceEqual(expected));
        foreach (var item in values)
        {
            actual.Add(item);
            expected.Add(item);
        }
        Assert.True(actual.SequenceEqual(expected));
        actual.Clear();
        Assert.Empty(actual);
    }

    /// <summary>Checks item/color fields and both child links without assuming an object-header offset.</summary>
    /// <param name="expected">The public constructor's current node.</param>
    /// <param name="actual">The candidate constructor's corresponding node.</param>
    private static void CheckShape(object? expected, object? actual)
    {
        if (expected is null || actual is null)
        {
            Assert.Null(expected);
            Assert.Null(actual);
            return;
        }
        var type = expected.GetType();
        Assert.Equal(type, actual.GetType());
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        foreach (string name in new[] { "<Item>k__BackingField", "<Color>k__BackingField" })
        {
            var field = type.GetField(name, flags)!;
            Assert.Equal(field.GetValue(expected), field.GetValue(actual));
        }
        foreach (string name in new[] { "<Left>k__BackingField", "<Right>k__BackingField" })
        {
            var field = type.GetField(name, flags)!;
            CheckShape(field.GetValue(expected), field.GetValue(actual));
        }
    }
}
