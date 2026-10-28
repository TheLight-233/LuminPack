using LuminPack.Core;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects cached capacity, caller storage lifetimes, and original cursor publication boundaries.</summary>
public class WriterCapacityCacheTest
{
    /// <summary>Checks the unpublished collection cursor and refreshes cached limits after repeated growth.</summary>
    [Fact]
    public void ExplicitCursorCheckRefreshesBufferWithoutHotPublication()
    {
        using var buffer = new LuminBufferWriter(true);
        var writer = new LuminPackWriter(buffer);
        Assert.Equal(7, writer.CheckBuffer(7));
        Assert.Equal(0, writer.Position);
        for (int iteration = 0; iteration < 7; iteration++)
        {
            int capacity = buffer.TotalLength;
            int position = capacity - (capacity >> 3) + 1;
            Assert.Equal(position, writer.CheckBuffer(position));
            Assert.True(buffer.TotalLength > capacity);
            Assert.Equal(0, writer.Position);
            global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)position) = (byte)(iteration + 1);
            Assert.Equal((byte)(iteration + 1), buffer.GetFullSpan()[position]);
        }
    }

    /// <summary>Retains the managed interior reference across collection and explicit span replacement.</summary>
    [Fact]
    public void CallerSpanStaysGcTrackedAndCanBeReplaced()
    {
        byte[] first = new byte[128];
        Span<byte> span = first;
        var writer = new LuminPackWriter(ref span);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)31) = 73;
        Assert.Equal((byte)73, first[31]);
        byte[] second = new byte[257];
        span = second;
        var replacement = new LuminPackWriter();
        replacement.SetSpan(ref span);
        replacement.Buffer[231] = 79;
        replacement.Advance(232);
        Assert.Equal((byte)79, replacement.GetSpan()[231]);
        replacement.Flush();
        Assert.Empty(replacement.GetSpan().ToArray());
        Assert.Equal((byte)79, second[231]);
    }

    /// <summary>Allows an unowned span to complete operations without an unrelated buffer owner.</summary>
    [Fact]
    public void UnownedChecksDoNotRequireAnOwner()
    {
        LuminPackWriter empty = default;
        empty.CheckBuffer();
        Span<byte> span = new byte[64];
        var writer = new LuminPackWriter(ref span);
        writer.AdvanceSafe(17);
        Assert.Equal(17, writer.Position);
    }

    /// <summary>A failed dynamic reservation retains its successful cursor and caller storage.</summary>
    [Fact]
    public void FailedReservationDoesNotPublishItsProposedEnd()
    {
        Span<byte> span = new byte[64];
        var writer = new LuminPackWriter(ref span);
        writer.Advance(17);
        bool failed = false;
        try { writer.Reserve(17, 48); }
        catch (Exception) { failed = true; }
        Assert.True(failed);
        Assert.Equal(17, writer.Position);
        Assert.All(span.ToArray(), value => Assert.Equal((byte)0, value));
    }
    /// <summary>Rejects a negative absolute reservation before touching an owned native buffer.</summary>
    [Fact]
    public void NegativeReservationDoesNotAddressBeforeOwnedStorage()
    {
        using var owner = new LuminBufferWriter(true);
        var writer = new LuminPackWriter(owner);
        writer.Write(1729);
        bool rejected = false;
        try { writer.Reserve(-1, 8); }
        catch (Exception) { rejected = true; }
        Assert.True(rejected);
        Assert.Equal(sizeof(int), writer.Position);
        Assert.Equal(1729, BitConverter.ToInt32(writer.GetSpan()));
    }

}
