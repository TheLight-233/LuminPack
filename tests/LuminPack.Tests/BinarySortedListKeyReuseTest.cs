#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.IO;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks checked ordered reads of binary scalar sorted lists with existing string keys.</summary>
public sealed class BinarySortedListKeyReuseTest
{
    /// <summary>Retains equal keys while updating values and invalidating the previous enumerator.</summary>
    [Fact]
    public void EqualKeysCanRetainIdentity()
    {
        string key = new string('a', 12);
        var target = new SortedList<string, long> { [key] = -1 };
        var prior = target;
        bool supported = LuminPackCollectionAccess.CanAppendSortedListOrdered(target);
        var enumerator = target.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        var incoming = new SortedList<string, long> { [new string('a', 12)] = 4_000_000_000 };
        byte[] bytes = LuminPackSerializer.Serialize(incoming);

        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(prior, target);
        Assert.Equal(4_000_000_000L, target[key]);
        Assert.Equal(supported, ReferenceEquals(key, target.Keys[0]));
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    /// <summary>Exposes only complete pairs before a duplicate key in the input.</summary>
    [Fact]
    public void DuplicateKeyKeepsTheCompletedPrefix()
    {
        var target = new SortedList<string, int> { ["AA"] = -1, ["BB"] = -2 };
        var prior = target;
        var incoming = new SortedList<string, int> { ["AA"] = 7, ["BB"] = 11 };
        byte[] bytes = LuminPackSerializer.Serialize(incoming);
        byte[] key = LuminPackSerializer.Serialize("AA");
        Assert.Equal(key, bytes.AsSpan(sizeof(int), key.Length).ToArray());
        key.CopyTo(bytes, sizeof(int) + key.Length + sizeof(int));

        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(prior, target);
        Assert.Equal(new KeyValuePair<string, int>("AA", 7), Assert.Single(target));
    }

    /// <summary>Enforces the enclosing key quota in the existing sequential reader.</summary>
    [Fact]
    public void EnclosingQuotaChecksKeysBeforeAppending()
    {
        const string overLimit = "over-limit";
        var incoming = new SortedListKeyQuotaEnvelope
        {
            Values = new() { ["a"] = 7, [overLimit] = 11 },
        };
        byte[] bytes = LuminPackSerializer.Serialize(incoming);
        var prior = new SortedList<string, int> { ["a"] = -1, [overLimit] = -2 };
        var target = new SortedListKeyQuotaEnvelope { Values = prior };

        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(prior, target!.Values);
        Assert.Equal(new KeyValuePair<string, int>("a", 7), Assert.Single(prior));
    }
}

/// <summary>A bounded root whose sorted-list keys must respect its string quota.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxStringLength = 4)]
public sealed partial class SortedListKeyQuotaEnvelope
{
    public SortedList<string, int> Values = new();
}
#endif
