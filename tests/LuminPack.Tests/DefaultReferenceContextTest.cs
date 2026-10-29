using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Separates independent implicit reference scopes while preserving explicitly shared scopes.</summary>
public unsafe class DefaultReferenceContextTest
{
    /// <summary>Materializes private state only when the mutable owner requests it.</summary>
    [Fact]
    public void DefaultStateIsLazyAndPrivate()
    {
        Span<byte> bytes=stackalloc byte[16];
        var writer=new LuminPackWriter(ref bytes);
        var other=new LuminPackWriter(ref bytes);
        Assert.Null(writer.Options);
        Assert.Null(other.Options);
        Assert.Same(writer.OptionState,writer.OptionState);
        Assert.Same(writer.Options,writer.OptionState);
        Assert.NotSame(writer.OptionState,other.OptionState);
        var input=(ReadOnlySpan<byte>)bytes;
        var reader=new LuminPackReader(ref input);
        var otherReader=new LuminPackReader(ref input);
        Assert.Null(reader.Options);
        Assert.Same(reader.OptionState,reader.OptionState);
        Assert.NotSame(reader.OptionState,otherReader.OptionState);
    }

    /// <summary>Writes full independent records and rejects references belonging to another reader.</summary>
    [Fact]
    public void IndependentDefaultRecordsDoNotShareReferences()
    {
        var source=CreateNode();
        byte[] first=Write(source),second=Write(source);
        Assert.Equal(first,second);
        var a=Read(first);var b=Read(first);
        Assert.NotSame(a,b);
        Assert.Same(a,a.Next);
        Assert.Same(b,b.Next);
        Assert.ThrowsAny<Exception>(()=>Read(new byte[]{0xFA,0}));
    }

    /// <summary>Keeps identities across records of one owner and across explicitly shared owner state.</summary>
    [Fact]
    public void ContinuousAndExplicitScopesRemainShared()
    {
        var source=CreateNode();
        Span<byte> bytes=stackalloc byte[1024];
        var writer=new LuminPackWriter(ref bytes);
        LuminPackSerializer.Serialize(ref writer,in source);
        int boundary=writer.Position;
        LuminPackSerializer.Serialize(ref writer,in source);
        Assert.Equal(boundary+2,writer.Position);
        var input=(ReadOnlySpan<byte>)bytes[..writer.Position];
        var reader=new LuminPackReader(ref input);
        ContextNode? first=null,second=null;
        LuminPackSerializer.Deserialize(ref reader,ref first);
        LuminPackSerializer.Deserialize(ref reader,ref second);
        Assert.Same(first,second);
        var sharedWrite=new LuminPackWriterOptionalState();
        byte[] full=Write(source,sharedWrite),reference=Write(source,sharedWrite);
        Assert.Equal(boundary,full.Length);
        Assert.Equal(2,reference.Length);
        var sharedRead=new LuminPackReaderOptionalState();
        Assert.Same(Read(full,sharedRead),Read(reference,sharedRead));
        sharedWrite.Reset();
        Assert.Equal(full,Write(source,sharedWrite));
        LuminPackWriter copied=default;
        writer.CopyTo(ref copied);
        Assert.Same(writer.OptionState,copied.OptionState);
    }

    /// <summary>Preserves the growable buffer's state when its public default-context constructor is used.</summary>
    [Fact]
    public void BufferOwnerBindsItsExistingState()
    {
        using var owner=new LuminBufferWriter(true);
        var source=CreateNode();
        var first=new LuminPackWriter(owner);
        Assert.Same(owner.WriterState,first.OptionState);
        LuminPackSerializer.Serialize(ref first,in source);
        owner.CompleteWrite(first.Position);
        int boundary=first.Position;
        var second=new LuminPackWriter(owner);
        LuminPackSerializer.Serialize(ref second,in source);
        Assert.Equal(boundary+2,second.Position);
        owner.ResetForReuse();
        var reset=new LuminPackWriter(owner);
        LuminPackSerializer.Serialize(ref reset,in source);
        Assert.Equal(boundary,reset.Position);
        var explicitState=new LuminPackWriterOptionalState();
        var overridden=new LuminPackWriter(owner,explicitState);
        Assert.Same(explicitState,overridden.OptionState);
    }

    /// <summary>Resolves JSON writer state from its owner while allowing an explicit override.</summary>
    [Fact]
    public void JsonBufferOwnerResolvesReferenceState()
    {
        using var owner = new LuminBufferWriter(true);
        var writer = new LuminPackJsonWriter(owner);
        Assert.Same(owner.WriterState, writer.Options);
        var explicitState = new LuminPackWriterOptionalState();
        writer.Options = explicitState;
        Assert.Same(explicitState, writer.OptionState);
        var second = new LuminPackJsonWriter(owner);
        Assert.Same(owner.WriterState, second.OptionState);
    }

    /// <summary>Binds both buffer-based readers to the buffer reference context.</summary>
    [Fact]
    public void BufferReadersShareReferenceState()
    {
        using var owner = new LuminBufferWriter(true);
        var reader = new LuminPackReader(owner);
        var jsonReader = new LuminPackJsonReader(owner);
        Assert.Same(owner.ReaderState, reader.OptionState);
        Assert.Same(owner.ReaderState, jsonReader.OptionState);
        LuminPackReader copy = default;
        reader.CopyTo(ref copy);
        Assert.Same(owner.ReaderState, copy.OptionState);
    }

    /// <summary>Shares the parent's implicit context through independently owned staging buffers.</summary>
    [Fact]
    public void StagingKeepsRepeatedAndSelfReferences()
    {
        var item=CreateNode();
        var source=new ContextEnvelope{First=item,Second=item};
        Span<byte> bytes=stackalloc byte[4096];
        var writer=new LuminPackWriter(ref bytes);
        LuminPackSerializer.Serialize(ref writer,in source);
        var input=(ReadOnlySpan<byte>)bytes[..writer.Position];
        var reader=new LuminPackReader(ref input);
        ContextEnvelope? value=null;
        LuminPackSerializer.Deserialize(ref reader,ref value);
        Assert.Same(value!.First,value.Second);
        Assert.Same(value.First,value.First!.Next);
    }

    /// <summary>Does not retain default-scope objects globally or share reference maps between threads.</summary>
    [Fact]
    public void DefaultScopesDoNotRetainOrRace()
    {
        byte[] bytes=Write(CreateNode());
        Parallel.For(0,8,_=>
        {
            for(int i=0;i<100;i++){var value=Read(bytes);Assert.Equal(137,value.Value);Assert.Same(value,value.Next);}
        });
        var weak=CreateWeakKey();
        for(int i=0;i<20&&weak.IsAlive;i++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
        Assert.False(weak.IsAlive);
    }

    /// <summary>Leaves primitive constructor/read/write loops allocation-free after warmup.</summary>
    [Fact]
    public void RawOperationsDoNotMaterializeContext()
    {
        Span<byte> bytes=stackalloc byte[8];
        for(int i=0;i<1000;i++) Raw(bytes);
        long start=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++) Raw(bytes);
        Assert.Equal(start,GC.GetAllocatedBytesForCurrentThread());
    }

    /// <summary>Provides an explicit circular-reference test contract.</summary>
    [LuminPackable(GeneratorType.CircleReference)]
    public sealed class ContextNode
    {
        [LuminPackOrder(0)] public int Value;
        [LuminPackOrder(1)] public ContextNode? Next;
    }

    /// <summary>Provides a separate staging owner containing repeated circular children.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed class ContextEnvelope
    {
        [LuminPackOrder(0)] public ContextNode? First;
        [LuminPackOrder(1)] public ContextNode? Second;
    }

    /// <summary>Creates a self-referencing source.</summary>
    /// <returns>The complete source graph.</returns>
    private static ContextNode CreateNode(){var value=new ContextNode{Value=137};value.Next=value;return value;}

    /// <summary>Writes through a default stack-backed Core owner.</summary>
    /// <param name="value">The complete source graph.</param>
    /// <param name="state">An explicit shared scope, or null for an independent implicit scope.</param>
    /// <returns>The complete original record.</returns>
    private static byte[] Write(ContextNode value,LuminPackWriterOptionalState? state=null)
    {
        Span<byte> bytes=stackalloc byte[1024];
        var writer=new LuminPackWriter(ref bytes,state);
        LuminPackSerializer.Serialize(ref writer,in value);
        return bytes[..writer.Position].ToArray();
    }

    /// <summary>Reads a complete pinned record through a new Core owner.</summary>
    /// <param name="bytes">The complete source bytes.</param>
    /// <param name="state">An explicit shared scope, or null for an independent implicit scope.</param>
    /// <returns>The complete reference graph.</returns>
    private static ContextNode Read(byte[] bytes,LuminPackReaderOptionalState? state=null)
    {
        fixed(byte* pointer=bytes)
        {
            ReadOnlySpan<byte> input=new(pointer,bytes.Length);
            var reader=new LuminPackReader(ref input,state);
            ContextNode? value=null;
            LuminPackSerializer.Deserialize(ref reader,ref value);
            Assert.Equal(bytes.Length,reader.Position);
            return value!;
        }
    }

    /// <summary>Observes a default writer's source outside the collecting test's stack frame.</summary>
    /// <returns>The source's weak lifetime observation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateWeakKey(){var value=CreateNode();_=Write(value);return new WeakReference(value);}

    /// <summary>Exercises the scalar path without requesting optional reference state.</summary>
    /// <param name="bytes">The complete fixed storage window.</param>
    private static void Raw(Span<byte> bytes)
    {
        var writer=new LuminPackWriter(ref bytes);writer.Write(137);
        var input=(ReadOnlySpan<byte>)bytes;var reader=new LuminPackReader(ref input);
        if(reader.Read<int>()!=137)throw new InvalidOperationException("Scalar mismatch.");
    }
}
