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
        Assert.Equal(0, writer.GetCurrentSpanIndex());
        for (int iteration = 0; iteration < 7; iteration++)
        {
            int capacity = buffer.TotalLength;
            int position = capacity - (capacity >> 3) + 1;
            Assert.Equal(position, writer.CheckBuffer(position));
            Assert.True(buffer.TotalLength > capacity);
            Assert.Equal(position, writer.GetCurrentSpanIndex());
            writer.GetSpanReference(position) = (byte)(iteration + 1);
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
        writer.GetSpanReference(31) = 73;
        Assert.Equal((byte)73, first[31]);
        byte[] second = new byte[257];
        span = second;
        var replacement = new LuminPackWriter();
        replacement.SetSpan(ref span);
        replacement.GetSpanReference(231) = 79;
        replacement.Advance(232);
        Assert.Equal((byte)79, replacement.GetSpan()[231]);
        replacement.Flush();
        Assert.Empty(replacement.GetSpan().ToArray());
        Assert.Equal((byte)79, second[231]);
    }

    /// <summary>Keeps an unowned/default writer's existing check failure instead of treating zero as cached capacity.</summary>
    [Fact]
    public void DefaultWriterStillRequiresAnOwnerForChecks()
    {
        Assert.Throws<NullReferenceException>(CheckDefaultWriter);
    }

    /// <summary>Retains the starting cursor when an owner-dependent advance fails.</summary>
    [Fact]
    public void FailedAdvanceDoesNotPublishItsProposedEnd()
    {
        Span<byte> span = new byte[64];
        var writer = new LuminPackWriter(ref span);
        bool failed = false;
        try { writer.AdvanceSafe(17); }
        catch (NullReferenceException) { failed = true; }
        Assert.True(failed);
        Assert.Equal(0, writer.GetCurrentSpanIndex());
    }

    /// <summary>Invokes the original owner-dependent check on a default state.</summary>
    private static void CheckDefaultWriter()
    {
        LuminPackWriter writer = default;
        writer.CheckBuffer();
    }
}
