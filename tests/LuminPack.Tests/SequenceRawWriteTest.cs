using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects exact raw sequence segments, source selection, capacity and owner observations.</summary>
public class SequenceRawWriteTest
{
    /// <summary>Preserves segment order, empty segments and slices in all binary profiles.</summary>
    [Fact]
    public void SegmentedAndSlicedRecordsKeepExactBytesAcrossProfiles()
    {
        Check<LuminPackUtf8LengthOptions>();Check<LuminPackUtf8TokenOptions>();
        Check<LuminPackUtf16LengthOptions>();Check<LuminPackUtf16TokenOptions>();
    }
    /// <summary>Compares each selected segmented record with independent contiguous element storage.</summary>
    /// <typeparam name="T">The original binary profile.</typeparam>
    private static void Check<T>() where T:struct,ILuminPackOptions
    {
        foreach(int length in new[]{0,1,3,17,1000,17000})
        {
            int[] values=Enumerable.Range(0,length).Select(i=>unchecked(i*719+137)).ToArray();
            byte[] expected=LuminPackSerializer.Serialize<T>(values);
            foreach(int split in new[]{0,length/2,length})
            {
                var head=new Segment<int>(values.AsMemory(0,split));
                var empty=head.Append(Memory<int>.Empty);
                var tail=empty.Append(values.AsMemory(split));
                ReadOnlySequence<int> sequence=new(head,0,tail,tail.Memory.Length);
                Assert.Equal(expected,LuminPackSerializer.Serialize<T>(sequence));
                ReadOnlySequence<int> single=new(values);
                Assert.Equal(expected,LuminPackSerializer.Serialize<T>(single));
                if(length>2)
                {
                    Assert.Equal(LuminPackSerializer.Serialize<T>(values[1..^1]),LuminPackSerializer.Serialize<T>(sequence.Slice(1,length-2)));
                }
            }
        }
    }
    /// <summary>Retains all original bits for small and wide scalar segments.</summary>
    [Fact]
    public void WideScalarsKeepEveryNativeStorageByte()
    {
        byte[] bytes=Enumerable.Range(0,257).Select(i=>(byte)i).ToArray();
        ReadOnlySequence<byte> sequence=new(bytes);
        Assert.Equal(LuminPackSerializer.Serialize(bytes),LuminPackSerializer.Serialize(sequence));
        Guid[] ids=[Guid.Empty,Guid.Parse("2ED4AAC7-847C-4BB5-8EC4-E0212F6ABD91"),Guid.NewGuid()];
        var head=new Segment<Guid>(ids.AsMemory(0,1));var tail=head.Append(ids.AsMemory(1));
        ReadOnlySequence<Guid> wide=new(head,0,tail,2);
        Assert.Equal(LuminPackSerializer.Serialize(ids),LuminPackSerializer.Serialize(wide));
        decimal[] decimals=[decimal.MinValue,1.0000000000000000000000000001m,decimal.MaxValue];
        ReadOnlySequence<decimal> values=new(decimals);
        Assert.Equal(LuminPackSerializer.Serialize(decimals),LuminPackSerializer.Serialize(values));
    }
    /// <summary>Keeps original memory-owner observations and rejects fixed-output overflow within its canaries.</summary>
    [Fact]
    public void SegmentOwnersKeepOriginalObservationCountAndOutputRemainsBounded()
    {
        using var manager=new ObservedManager();
        ReadOnlySequence<int> sequence=new(manager.View());
        manager.Observations=0;
        int observedLength=0;
        foreach(var memory in sequence) observedLength+=memory.Span.Length;
        int originalObservations=manager.Observations;
        Assert.Equal(manager.Items.Length,observedLength);
        manager.Observations=0;
        byte[] actual=LuminPackSerializer.Serialize(sequence);
        Assert.Equal(originalObservations,manager.Observations);
        Assert.Equal(LuminPackSerializer.Serialize(manager.Items),actual);
        int[] numbers=Enumerable.Range(0,1000).ToArray();
        var first=new Segment<int>(numbers.AsMemory(0,500));var last=first.Append(numbers.AsMemory(500));
        sequence=new(first,0,last,500);
        byte[] destination=Enumerable.Repeat((byte)0xA5,129).ToArray();
        Span<byte> storage=destination.AsSpan(1,127);
        var writer=new LuminPackWriter(ref storage);
        Exception? failure=null;
        try{LuminPackSerializer.Serialize(ref writer,sequence);}catch(Exception exception){failure=exception;}
        Assert.NotNull(failure);Assert.Equal(0,writer.Position);
        Assert.Equal((byte)0xA5,destination[0]);Assert.Equal((byte)0xA5,destination[^1]);
    }
    /// <summary>Preserves an append prefix and complete elements when segment writes grow owned storage.</summary>
    [Fact]
    public void OwnedBufferGrowthKeepsItsExistingPrefixAndCompletePayload()
    {
        using var buffer=new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(137,buffer);
        int[] values=Enumerable.Range(0,17000).ToArray();
        var first=new Segment<int>(values.AsMemory(0,8000));var last=first.Append(values.AsMemory(8000));
        ReadOnlySequence<int> sequence=new(first,0,last,9000);
        LuminPackSerializer.Serialize(sequence,buffer);
        byte[] expected=LuminPackSerializer.Serialize(137).Concat(LuminPackSerializer.Serialize(values)).ToArray();
        Assert.Equal(expected,buffer.GetSpan().ToArray());
    }
    /// <summary>Retains the source snapshot when a memory owner replaces the caller slot.</summary>
    [Fact]
    public void MemoryOwnerCannotReplaceTheSelectedSourceDuringSerialization()
    {
        using var manager=new ObservedManager();
        ReadOnlySequence<int> source=new(manager.View());
        manager.OnObserve=()=>source=new ReadOnlySequence<int>(new[]{719});
        byte[] expected=LuminPackSerializer.Serialize(manager.Items);
        byte[] bytes=LuminPackSerializer.Serialize(in source);
        Assert.Equal(expected,bytes);
        Assert.Equal(1,source.Length);
    }
    /// <summary>Preserves the original element order when source and caller output overlap.</summary>
    [Fact]
    public void OverlappingCallerStorageKeepsTheOriginalElementOrder()
    {
        foreach(var positions in new[]{(0,0),(0,3),(8,0),(0,10)})
        foreach(bool segmented in new[]{false,true})
        {
            byte[] expected=Enumerable.Range(0,80).Select(i=>(byte)(i+17)).ToArray();
            byte[] actual=(byte[])expected.Clone();
            Span<byte> oldWindow=expected.AsSpan(positions.Item2,25);
            var original=new LuminPackWriter(ref oldWindow);
            original.Write(17);
            for(int i=0;i<17;i++){byte item=expected[positions.Item1+i];original.CheckBuffer();original.Write(item);}
            original.CheckBuffer();
            ReadOnlySequence<byte> sequence=new(actual.AsMemory(positions.Item1,17));
            if(segmented)
            {
                var first=new Segment<byte>(actual.AsMemory(positions.Item1,3));
                var last=first.Append(actual.AsMemory(positions.Item1+3,14));
                sequence=new(first,0,last,14);
            }
            Span<byte> window=actual.AsSpan(positions.Item2,25);
            var writer=new LuminPackWriter(ref window);
            LuminPackSerializer.Serialize(ref writer,sequence);
            Assert.Equal(expected,actual);Assert.Equal(original.Position,writer.Position);
        }
    }
    /// <summary>Preserves the original count-header publication before observing a memory-owner span.</summary>
    [Fact]
    public void MemoryOwnerObservesTheCountHeaderBeforeItsSpanIsRequested()
    {
        using var manager=new ObservedManager();
        ReadOnlySequence<int> source=new(manager.View());
        byte[] output=new byte[64];
        manager.OnObserve=()=>Assert.Equal(manager.Items.Length,BinaryPrimitives.ReadInt32LittleEndian(output));
        Span<byte> span=output;
        var writer=new LuminPackWriter(ref span);
        LuminPackSerializer.Serialize(ref writer,source);
        Assert.Equal(16,writer.Position);
        Assert.Equal(LuminPackSerializer.Serialize(manager.Items),output.AsSpan(0,writer.Position).ToArray());
    }
    /// <summary>Represents one original sequence segment.</summary>
    /// <typeparam name="T">The stored scalar type.</typeparam>
    private sealed class Segment<T>:ReadOnlySequenceSegment<T>
    {
        /// <summary>Stores the supplied immutable memory view.</summary>
        /// <param name="memory">The selected segment storage.</param>
        public Segment(ReadOnlyMemory<T> memory){Memory=memory;}
        /// <summary>Links the next segment at the original running index.</summary>
        /// <param name="memory">The next immutable memory view.</param>
        /// <returns>The linked segment.</returns>
        public Segment<T> Append(ReadOnlyMemory<T> memory)
        {
            var next=new Segment<T>(memory){RunningIndex=RunningIndex+Memory.Length};Next=next;return next;
        }
    }
    /// <summary>Records span access and optional source replacement through a supported memory owner.</summary>
    private sealed class ObservedManager:MemoryManager<int>
    {
        public readonly int[] Items=[137,719,981];public int Observations;public Action? OnObserve;
        /// <summary>Creates a view without observing the owner span.</summary>
        /// <returns>The complete manager-backed view.</returns>
        public Memory<int> View()=>CreateMemory(Items.Length);
        /// <summary>Records each original observing operation.</summary>
        /// <returns>The complete backing span.</returns>
        public override Span<int> GetSpan(){Observations++;OnObserve?.Invoke();return Items;}
        /// <summary>Rejects pinning that the original sequence writer does not require.</summary>
        /// <param name="index">The requested element index.</param>
        /// <returns>No handle is created.</returns>
        public override MemoryHandle Pin(int index=0)=>throw new InvalidOperationException();
        /// <summary>Rejects an unexpected unpin operation.</summary>
        public override void Unpin()=>throw new InvalidOperationException();
        /// <summary>Releases no resources outside this test owner.</summary>
        /// <param name="disposing">Whether disposal was requested explicitly.</param>
        protected override void Dispose(bool disposing){}
    }
}
