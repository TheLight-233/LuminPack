#if LUMINPACK_NEXT_GENERATOR
using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the publication and fallback rules of array-backed binary wrapper reuse.</summary>
public sealed class ReadOnlyCollectionReuseTest
{
    /// <summary>Refills an eligible wrapper only after the complete raw payload has been checked.</summary>
    [Fact]
    public void ExactArrayWrapperRefillsACompletePayload()
    {
        int[] initial = [7, 13, 17];
        int[] incoming = [11, 19, 23];
        byte[] bytes = LuminPackSerializer.Serialize(new ReadOnlyCollection<int>(incoming));
        int[] backingArray = (int[])initial.Clone();
        var prior = new ReadOnlyCollection<int>(backingArray);
        var target = prior;

        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        byte[] forged = (byte[])bytes.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(forged, int.MaxValue);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(forged, ref target));
        Assert.Same(prior, target);
        Assert.Equal(initial, backingArray);

        ReadOnlySpan<byte> payload = bytes.AsSpan(sizeof(int));
        var probeReader = new LuminPackReader(ref payload);
        var probeTarget = new ReadOnlyCollection<int>(new int[incoming.Length]);
        bool supported = LuminPackCollectionAccess.TryReadArrayBackedWrapper(ref probeReader, probeTarget, incoming.Length);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Equal(incoming, target.ToArray());
        Assert.Equal(supported, ReferenceEquals(prior, target));
        Assert.Equal(supported ? incoming : initial, backingArray);
    }

    /// <summary>Replaces incompatible wrappers and keeps an overlapping input buffer untouched.</summary>
    [Fact]
    public void IncompatibleStorageKeepsReplacement()
    {
        int[] incoming = [11, 19, 23];
        byte[] bytes = LuminPackSerializer.Serialize(new ReadOnlyCollection<int>(incoming));
        foreach (var prior in new ReadOnlyCollection<int>[]
        {
            new(new List<int> { 7, 13, 17 }),
            new(new int[2]),
            new DerivedWrapper(new int[3]),
            new(new ReadOnlyCollection<int>(new int[3])),
        })
        {
            var target = prior;
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.NotSame(prior, target);
            Assert.Equal(incoming, target.ToArray());
        }

        int[] shared = [7, 13, 17];
        ReadOnlySpan<byte> overlap = MemoryMarshal.AsBytes(shared.AsSpan());
        var reader = new LuminPackReader(ref overlap);
        Assert.False(LuminPackCollectionAccess.TryReadArrayBackedWrapper(ref reader, new ReadOnlyCollection<int>(shared), shared.Length));
        Assert.Equal(0, reader.Position);
        Assert.Equal(new[] { 7, 13, 17 }, shared);
    }

    private sealed class DerivedWrapper(int[] items) : ReadOnlyCollection<int>(items);
}
#endif
