using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects bucket placement and duplicate rejection around the unsigned remainder shortcut.</summary>
public class DictionaryBucketRangeTest
{
    /// <summary>Checks that batched dictionary writes exclude entry alignment gaps but preserve raw value padding.</summary>
    [Fact]
    public void DensePairsRetainIndependentStorageWidths()
    {
        var mixed = new Dictionary<byte, long> { [1] = long.MaxValue, [2] = long.MinValue, [3] = 71 };
        byte[] mixedBytes = LuminPackSerializer.Serialize(mixed);
        VerifyPairBytes(mixed, mixedBytes);
        Assert.Equal(mixed.ToArray(), LuminPackSerializer.Deserialize<Dictionary<byte, long>>(mixedBytes).ToArray());
        for (int prefix = 0; prefix < mixedBytes.Length; prefix++)
        {
            byte[] truncated = mixedBytes.AsSpan(0, prefix).ToArray();
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<Dictionary<byte, long>>(truncated));
        }
        PaddedPairValue raw = default;
        Unsafe.InitBlockUnaligned(ref Unsafe.As<PaddedPairValue, byte>(ref raw), 0xA5, 16);
        raw.Tag = 3;
        raw.Number = -987654321;
        var padded = new Dictionary<int, PaddedPairValue> { [11] = raw, [13] = raw };
        byte[] paddedBytes = LuminPackSerializer.Serialize(padded);
        VerifyPairBytes(padded, paddedBytes);
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, PaddedPairValue>>(paddedBytes);
        Assert.Equal(paddedBytes, LuminPackSerializer.Serialize(copy));
        mixed.Remove(2);
        VerifyPairBytes(mixed, LuminPackSerializer.Serialize(mixed));
        padded.Remove(11);
        VerifyPairBytes(padded, LuminPackSerializer.Serialize(padded));
    }

    /// <summary>Builds expected bytes through the original independent key and value cores.</summary>
    /// <typeparam name="TKey">The raw key storage.</typeparam>
    /// <typeparam name="TValue">The raw value storage.</typeparam>
    /// <param name="source">The dictionary in its existing enumeration order.</param>
    /// <param name="actual">The generated writer's output.</param>
    private static void VerifyPairBytes<TKey, TValue>(Dictionary<TKey, TValue> source, byte[] actual)
        where TKey : unmanaged where TValue : unmanaged
    {
        Span<byte> span = new byte[1024];
        var writer = new LuminPackWriter(ref span);
        int index = 0;
        writer.WriteCollectionHeader(ref index, source.Count);
        writer.Advance(4);
        foreach (var pair in source)
        {
            index = writer.GetCurrentSpanIndex();
            writer.Advance(writer.WriteUnmanaged(ref index, pair.Key));
            index = writer.GetCurrentSpanIndex();
            writer.Advance(writer.WriteUnmanaged(ref index, pair.Value));
        }
        Assert.Equal(span[..writer.GetCurrentSpanIndex()].ToArray(), actual);
    }

    /// <summary>Exposes explicit padding that must remain part of an unmanaged value's representation.</summary>
    [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PaddedPairValue
    {
        [FieldOffset(0)] public byte Tag;
        [FieldOffset(8)] public long Number;
    }

    /// <summary>Checks small hashes, boundary hashes, negative hashes, and collision chains against normal dictionary lookup.</summary>
    [Fact]
    public void SmallAndLargeHashesPreserveDictionaryLookups()
    {
        var source = new Dictionary<int, int>();
        foreach (int key in new[] { 0, 1, 2, 3, 16, 17, 18, 34, 51, 1009, int.MaxValue, -1, int.MinValue })
            source.Add(key, unchecked(key * 31 + 7));
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, int>>(LuminPackSerializer.Serialize(source));
        Assert.Equal(source.Count, copy.Count);
        foreach (var pair in source) Assert.Equal(pair.Value, copy[pair.Key]);
        copy.Add(91, 857);
        Assert.Equal(857, copy[91]);
        Assert.True(copy.Remove(17));
        Assert.False(copy.ContainsKey(17));
    }

    /// <summary>Checks that colliding and repeated small hashes retain the normal duplicate-key failure.</summary>
    [Fact]
    public void DuplicateSmallHashesStillRejectDuplicateKeys()
    {
        byte[] payload = new byte[20];
        BitConverter.TryWriteBytes(payload.AsSpan(0), 2);
        BitConverter.TryWriteBytes(payload.AsSpan(4), 1);
        BitConverter.TryWriteBytes(payload.AsSpan(8), 11);
        BitConverter.TryWriteBytes(payload.AsSpan(12), 1);
        BitConverter.TryWriteBytes(payload.AsSpan(16), 13);
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize<Dictionary<int, int>>(payload));
    }
}
