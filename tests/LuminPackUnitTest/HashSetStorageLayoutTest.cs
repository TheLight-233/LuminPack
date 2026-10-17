using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks sparse entry scans and bucket publication on both modern pointer sizes.</summary>
public class HashSetStorageLayoutTest
{
    /// <summary>Preserves public enumeration bytes and live-count state after rebuilding value-type buckets.</summary>
    [Fact]
    public void SparseValueEntriesPreserveWireAndRemainMutableAfterReading()
    {
        var values = new HashSet<int>(Enumerable.Range(-8, 24));
        foreach (int value in Enumerable.Range(-8, 12)) values.Remove(value);
        values.Add(int.MinValue);
        using var expected = new MemoryStream();
        expected.Write(BitConverter.GetBytes(values.Count));
        foreach (int value in values) expected.Write(BitConverter.GetBytes(value));
        byte[] payload = LuminPackSerializer.Serialize(values);
        Assert.Equal(expected.ToArray(), payload);
        var copy = LuminPackSerializer.Deserialize<HashSet<int>>(payload);
        Assert.Equal(values.Count, copy.Count);
        Assert.True(values.SetEquals(copy));
        Assert.Equal(payload, LuminPackSerializer.Serialize(copy));
        Assert.True(copy.Remove(int.MinValue));
        Assert.True(copy.Add(int.MaxValue));
        Assert.Equal(values.Count, copy.Count);
        Assert.Contains(int.MaxValue, copy);
        Assert.DoesNotContain(int.MinValue, copy);
    }

    /// <summary>Preserves reference/null entries and rehashes them through the original default-comparer read path.</summary>
    [Fact]
    public void SparseReferenceEntriesPreserveNullsAndOriginalReadComparer()
    {
        var values = new HashSet<string?>(StringComparer.OrdinalIgnoreCase) { "Alpha", "Beta", null, "Gamma", "Delta" };
        values.Remove("beta");
        values.Remove("alpha");
        values.Add("Last");
        using var expected = new MemoryStream();
        expected.Write(BitConverter.GetBytes(values.Count));
        foreach (string? value in values) expected.Write(LuminPackSerializer.Serialize(value));
        byte[] payload = LuminPackSerializer.Serialize(values);
        Assert.Equal(expected.ToArray(), payload);
        var copy = LuminPackSerializer.Deserialize<HashSet<string?>>(payload);
        Assert.Equal(values.Count, copy.Count);
        Assert.True(values.SetEquals(copy));
        Assert.Equal(payload, LuminPackSerializer.Serialize(copy));
        Assert.Contains(null, copy);
        Assert.Same(EqualityComparer<string?>.Default, copy.Comparer);
        Assert.True(copy.Remove("Gamma"));
        Assert.True(copy.Add("replacement"));
        Assert.Equal(values.Count, copy.Count);
    }
}
