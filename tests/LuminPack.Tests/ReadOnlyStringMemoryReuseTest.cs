#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Buffers;
using System.IO;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks detached binary reads of array-backed read-only string slices.</summary>
public sealed class ReadOnlyStringMemoryReuseTest
{
    /// <summary>Reuses matching strings while keeping the new read-only memory detached from the old array.</summary>
    [Fact]
    public void MemoryReusesStringsWithoutReusingArrayStorage()
    {
        string first = new string('a', 12);
        string second = new string('b', 13);
        string?[] backing = ["prefix", first, second, null, "suffix"];
        ReadOnlyMemory<string?> target = backing.AsMemory(1, 3);
        ReadOnlyMemory<string?> source = new string?[] { new string('a', 12), new string('b', 13), null };
        byte[] bytes = LuminPackSerializer.Serialize(source);

        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(first, target.Span[0]);
        Assert.Same(second, target.Span[1]);
        Assert.Null(target.Span[2]);
        backing[1] = "changed";
        Assert.Same(first, target.Span[0]);
    }

    /// <summary>Reads the existing slice in order and publishes no result on truncation.</summary>
    [Fact]
    public void SequenceKeepsThePreviousResultOnTruncation()
    {
        string first = new string('日', 12);
        string second = new string('ß', 13);
        string?[] backing = ["prefix", first, second, "suffix"];
        var target = new ReadOnlySequence<string?>(backing, 1, 2);
        var previous = target;
        var source = new ReadOnlySequence<string?>(new string?[] { new string('日', 12), new string('ß', 13) });
        byte[] bytes = LuminPackSerializer.Serialize(source);

        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        Assert.Equal(previous, target);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(first, target.FirstSpan[0]);
        Assert.Same(second, target.FirstSpan[1]);
        backing[2] = "changed";
        Assert.Same(second, target.FirstSpan[1]);
    }

    /// <summary>Enforces the enclosing string quota even when a prior value is available.</summary>
    [Fact]
    public void EnclosingQuotaStillChecksTheSeededPrefix()
    {
        string overLimit = new string('x', 17);
        ReadOnlyMemory<string?> original = new string?[] { overLimit, "safe" };
        var target = new ReadOnlyStringMemoryQuotaEnvelope { Values = original };
        byte[] bytes = LuminPackSerializer.Serialize(target);

        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.True(original.Equals(target.Values));
    }
}

/// <summary>A bounded root whose read-only memory member can supply prior strings.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxStringLength = 4)]
public sealed partial class ReadOnlyStringMemoryQuotaEnvelope
{
    public ReadOnlyMemory<string?> Values;
}
#endif
