#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the publication and fallback rules of binary read-only dictionary reuse.</summary>
public sealed class ReadOnlyDictionaryReuseTest
{
    /// <summary>Updates existing values only when the complete ordered key sequence matches.</summary>
    [Fact]
    public void OrderedStringKeysCanKeepTheWrapper()
    {
        string unchanged = new string('a', 8);
        var initial = new Dictionary<string, string?> { ["first"] = unchanged, ["second"] = "old" };
        var alias = new ReadOnlyDictionary<string, string?>(initial);
        var target = alias;
        var source = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>
        {
            ["first"] = new string('a', 8), ["second"] = null,
        });
        byte[] bytes = LuminPackSerializer.Serialize(source);

        ReadOnlySpan<byte> payload = bytes;
        var reader = new LuminPackReader(ref payload);
        var probe = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(initial));
        bool supported = LuminPackCollectionAccess.TryReadStableReadOnlyDictionary<string?, LuminPackUtf16LengthOptions>(ref reader, probe);

        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Equal(supported, ReferenceEquals(alias, target));
        Assert.Equal(unchanged, target!["first"]);
        Assert.Null(target["second"]);
        Assert.Equal(supported ? null : "old", initial["second"]);
        if (supported)
            Assert.Same(unchanged, target["first"]);

        var reordered = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>
        {
            ["second"] = "new", ["first"] = "changed",
        });
        var previous = target;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(reordered), ref target);
        Assert.NotSame(previous, target);
        Assert.Equal("new", target!["second"]);
        Assert.Null(previous["second"]);
    }

    /// <summary>Preserves the original backing if the input ends early or contains duplicate keys.</summary>
    [Fact]
    public void InvalidInputLeavesTheExistingBackingUnchanged()
    {
        var backing = new Dictionary<string, int> { ["AA"] = -1, ["BB"] = -2 };
        var alias = new ReadOnlyDictionary<string, int>(backing);
        var target = alias;
        var source = new ReadOnlyDictionary<string, int>(new Dictionary<string, int> { ["AA"] = 1, ["BB"] = 2 });
        byte[] bytes = LuminPackSerializer.Serialize(source);
        byte[] key = LuminPackSerializer.Serialize("AA");
        Assert.Equal(key, bytes.AsSpan(sizeof(int), key.Length).ToArray());
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        key.CopyTo(bytes, sizeof(int) + key.Length + sizeof(int));
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(alias, target);
        Assert.Equal(-1, backing["AA"]);
        Assert.Equal(-2, backing["BB"]);
    }

    /// <summary>Preserves the root's string quota when the dictionary is reached through another object.</summary>
    [Fact]
    public void EnclosingStringQuotaStillApplies()
    {
        var source = new ReadOnlyDictionaryQuotaEnvelope
        {
            Child = new() { Map = new(new Dictionary<string, string> { ["key"] = "over-limit" }) },
        };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var alias = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["key"] = "old" });
        var target = new ReadOnlyDictionaryQuotaEnvelope { Child = new() { Map = alias } };

        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(alias, target!.Child.Map);
        Assert.Equal("old", alias["key"]);
    }

    /// <summary>Projects the root's collection quota into a dictionary nested in a list.</summary>
    [Fact]
    public void EnclosingCollectionQuotaStillApplies()
    {
        var source = new ReadOnlyDictionaryCollectionQuotaEnvelope
        {
            Maps = [new(new Dictionary<string, int> { ["one"] = 1, ["two"] = 2, ["three"] = 3 })],
        };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var alias = new ReadOnlyDictionary<string, int>(new Dictionary<string, int> { ["one"] = -1, ["two"] = -2, ["three"] = -3 });
        var target = new ReadOnlyDictionaryCollectionQuotaEnvelope { Maps = [alias] };

        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(alias, target!.Maps[0]);
        Assert.Equal(-1, alias["one"]);
    }

    /// <summary>Declines an input span borrowed directly from an existing backing value.</summary>
    [Fact]
    public void OverlappingBackingIsRejectedBeforeReadingTheCount()
    {
        var backing = new Dictionary<string, int> { ["key"] = 1 };
        var target = new ReadOnlyDictionary<string, int>(backing);
        ref int value = ref CollectionsMarshal.GetValueRefOrNullRef(backing, "key");
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));

        for (int position = 0; position <= sizeof(int); position += sizeof(int))
        {
            var reader = new LuminPackReader(ref bytes);
            reader.Advance(position);
            Assert.False(LuminPackCollectionAccess.TryReadStableReadOnlyDictionary<int, LuminPackUtf16LengthOptions>(ref reader, target));
            Assert.Equal(position, reader.Position);
        }
        Assert.Equal(1, backing["key"]);
    }
}

/// <summary>A bounded root with a dictionary reachable through an ordinary child.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxStringLength = 4)]
public partial class ReadOnlyDictionaryQuotaEnvelope
{
    public ReadOnlyDictionaryQuotaChild Child = new();
}

/// <summary>An ordinary object that owns a binary read-only dictionary.</summary>
[LuminPackable]
public partial class ReadOnlyDictionaryQuotaChild
{
    public ReadOnlyDictionary<string, string> Map = new(new Dictionary<string, string>());
}

/// <summary>A bounded root with a dictionary reachable through a list.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxCollectionLength = 2)]
public partial class ReadOnlyDictionaryCollectionQuotaEnvelope
{
    public List<ReadOnlyDictionary<string, int>> Maps = [];
}
#endif
