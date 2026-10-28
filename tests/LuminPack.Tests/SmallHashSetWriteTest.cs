using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects grouped raw stores, scalar tails and unchanged sparse hash-set traversal.</summary>
public sealed class SmallHashSetWriteTest
{
    /// <summary>Preserves native byte and Boolean values through every dense tail length.</summary>
    [Fact]
    public void OneByteDenseAndSparseRecordsKeepTheirBytes()
    {
        foreach (int count in Counts())
        {
            Check(Enumerable.Range(0, count).Select(index => (byte)index).ToHashSet());
            Check(Enumerable.Range(0, count).Select(index => (sbyte)index).ToHashSet());
            Check(Enumerable.Range(0, count).Select(index => index % 2 == 0).ToHashSet());
        }
    }

    /// <summary>Preserves signed, character and floating-point bits without numerical conversion.</summary>
    [Fact]
    public void TwoByteDenseAndSparseRecordsKeepTheirBits()
    {
        foreach (int count in Counts())
        {
            Check(Enumerable.Range(0, count).Select(index => (short)(index * 31 - 3000)).ToHashSet());
            Check(Enumerable.Range(0, count).Select(index => (ushort)(index * 31)).ToHashSet());
            Check(Enumerable.Range(0, count).Select(index => (char)(index + 32)).ToHashSet());
            Check(Enumerable.Range(0, count).Select(index => (Half)(index * 1.25f)).ToHashSet());
        }
        Check(new HashSet<Half> { BitConverter.UInt16BitsToHalf(0x7E01), BitConverter.UInt16BitsToHalf(0x8000), (Half)1.25f });
    }

    /// <summary>Keeps small raw aggregate storage available without requiring a generated formatter.</summary>
    [Fact]
    public void SmallRawStructsKeepTheirCompleteStorage()
    {
        foreach (int count in Counts())
        {
            Check(Enumerable.Range(0, count).Select(index => new Tiny { Tag = (byte)index, Flags = (byte)~index }).ToHashSet());
        }
    }

    /// <summary>Retains the original scalar behavior for element widths outside the grouped path.</summary>
    [Fact]
    public void WiderNativeRecordsKeepTheirOriginalStorage()
    {
        foreach (int count in Counts())
        {
            Check(Enumerable.Range(0, count).Select(index => (long)index << 36).ToHashSet());
            Check(Enumerable.Range(0, count).Select(index => new Guid(index, 17, 29, 3, 5, 7, 11, 13, 17, 19, 23)).ToHashSet());
        }
    }

    /// <summary>Rejects insufficient storage before writing even the count header or a grouped payload.</summary>
    [Fact]
    public void CapacityFailureLeavesTheCompleteDestinationUntouched()
    {
        var set = Enumerable.Range(0, 33).Select(index => (byte)index).ToHashSet();
        var bytes = Enumerable.Repeat((byte)0xAC, 4 + set.Count).ToArray();
        Assert.ThrowsAny<Exception>(() => WriteInto(set, bytes, 0, bytes.Length - 1));
        Assert.All(bytes, item => Assert.Equal((byte)0xAC, item));
    }

    /// <summary>Refreshes an owned cursor after complete reservation grows the buffer without changing the existing prefix.</summary>
    [Fact]
    public void OwnedGrowthKeepsThePrefixAndCompleteRecord()
    {
        var set = Enumerable.Range(0, 20000).Select(index => (ushort)index).ToHashSet();
        using var owner = new LuminBufferWriter(true);
        var writer = new LuminPackWriter(owner);
        int initialCapacity = writer.Buffer.Length;
        writer.Write((byte)0xAC);
        writer.Write((byte)0xBD);
        writer.Write((byte)0xCE);
        LuminPackMarshal.WriteSmallHashSetRaw(ref writer, set);
        Assert.True(writer.Buffer.Length > initialCapacity);
        var bytes = writer.GetSpan();
        Assert.True(bytes[..3].SequenceEqual(new byte[] { 0xAC, 0xBD, 0xCE }));
        Assert.Equal(set.Count, BinaryPrimitives.ReadInt32LittleEndian(bytes[3..]));
        Assert.Equal(7 + set.Count * sizeof(ushort), writer.Position);
        Assert.True(MemoryMarshal.AsBytes(set.ToArray().AsSpan()).SequenceEqual(bytes[7..]));
    }

    /// <summary>Provides element counts covering every four-element remainder and multiple loop boundaries.</summary>
    /// <returns>The complete tested counts.</returns>
    private static int[] Counts() => [0, 1, 2, 3, 4, 5, 7, 8, 9, 31, 32, 33, 127, 128, 129, 199, 200, 257];

    /// <summary>Checks dense storage and then the original sparse-entry fallback on the same element family.</summary>
    /// <typeparam name="T">The raw element storage.</typeparam>
    /// <param name="source">The complete original set.</param>
    private static void Check<T>(HashSet<T> source) where T : unmanaged
    {
        AssertRecord(source);
        if (source.Count == 0) return;
        source.Remove(source.First());
        AssertRecord(source);
    }

    /// <summary>Checks complete bytes at an unaligned cursor with untouched prefix and tail guards.</summary>
    /// <typeparam name="T">The raw element storage.</typeparam>
    /// <param name="source">The original set in its current enumeration order.</param>
    private static void AssertRecord<T>(HashSet<T> source) where T : unmanaged
    {
        T[] values = source.ToArray();
        int length = checked(4 + values.Length * Unsafe.SizeOf<T>());
        var bytes = Enumerable.Repeat((byte)0xAC, length + 12).ToArray();
        Assert.Equal(length, WriteInto(source, bytes, 3, length));
        Assert.Equal(source.Count, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(3)));
        Assert.True(MemoryMarshal.AsBytes(values.AsSpan()).SequenceEqual(bytes.AsSpan(7, length - 4)));
        Assert.All(bytes[..3], item => Assert.Equal((byte)0xAC, item));
        Assert.All(bytes[(length + 3)..], item => Assert.Equal((byte)0xAC, item));
    }

    /// <summary>Writes one complete raw set while the managed storage remains stationary for pointer runtimes.</summary>
    /// <typeparam name="T">The raw element storage.</typeparam>
    /// <param name="source">The complete original set.</param>
    /// <param name="bytes">The managed output and its guard storage.</param>
    /// <param name="offset">The first writable byte.</param>
    /// <param name="length">The complete writable length.</param>
    /// <returns>The completed writer position.</returns>
    private static unsafe int WriteInto<T>(HashSet<T> source, byte[] bytes, int offset, int length) where T : unmanaged
    {
        fixed (byte* pointer = bytes)
        {
            Span<byte> span = new(pointer + offset, length);
            var writer = new LuminPackWriter(ref span);
            LuminPackMarshal.WriteSmallHashSetRaw(ref writer, source);
            return writer.Position;
        }
    }

    /// <summary>Provides two bytes of caller-selected raw storage.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Tiny
    {
        internal byte Tag;
        internal byte Flags;
    }
}
