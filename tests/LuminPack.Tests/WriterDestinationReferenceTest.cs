using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;
/// <summary>Protects destination identity when a writer receives the mutable span destination.</summary>
public class WriterDestinationReferenceTest
{
    /// <summary>Resetting a writer or reader releases its span without modifying caller memory or dereferencing an empty span.</summary>
    [Fact]
    public void FlushReleasesStorageWithoutWritingToTheFirstByte()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCC, 24).ToArray();
        Span<byte> destination = bytes;
        var writer = new LuminPackWriter(ref destination);
        writer.Flush();
        Assert.True(writer.GetSpan().IsEmpty);
        Assert.All(bytes, value => Assert.Equal((byte)0xCC, value));
        ReadOnlySpan<byte> source = bytes;
        var reader = new LuminPackReader(ref source);
        reader.Flush();
        Assert.True(reader.GetSpan().IsEmpty);
        Assert.All(bytes, value => Assert.Equal((byte)0xCC, value));
        writer.Flush();
        reader.Flush();
        Span<byte> empty = Span<byte>.Empty;
        var emptyWriter = new LuminPackWriter(ref empty);
        emptyWriter.Flush();
        ReadOnlySpan<byte> emptySource = ReadOnlySpan<byte>.Empty;
        var emptyReader = new LuminPackReader(ref emptySource);
        emptyReader.Flush();
    }

    /// <summary>The tracked Span data reference survives compacting GC and replacement with another caller destination.</summary>
    [Fact]
    public void DestinationReferenceSurvivesCollectionAndSpanReplacement()
    {
        Span<byte> destination = new byte[32];
        scoped Span<byte> replacement = new byte[32];
        var writer = new LuminPackWriter(ref destination);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        int index = 0;
        global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)index), 123456789);
        writer.Advance(sizeof(int));
        Assert.Equal(123456789, BitConverter.ToInt32(destination));
        writer.SetSpan(ref replacement);
        global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)index), 987654321);
        Assert.Equal(987654321, BitConverter.ToInt32(replacement));
        Assert.Equal(123456789, BitConverter.ToInt32(destination));
    }

    /// <summary>Checks aliasing without writing through an incorrectly constructed writer.</summary>
    [Fact]
    public void SpanConstructorRetainsTheOriginalDestinationReference()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCC, 24).ToArray();
        Span<byte> destination = bytes.AsSpan(3, 16);
        var writer = new LuminPackWriter(ref destination);
        Assert.True(Unsafe.AreSame(ref Unsafe.AsRef(in destination.GetPinnableReference()), ref MemoryMarshal.GetReference(writer.GetSpan())));
        int index = 0;
        global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)index), 123456789);
        writer.Advance(sizeof(int));
        Assert.Equal(123456789, BitConverter.ToInt32(bytes, 3));
        Assert.All(bytes.Take(3).Concat(bytes.Skip(7)), value => Assert.Equal((byte)0xCC, value));
    }

    /// <summary>Checks that an empty destination is represented without dereferencing its null data reference.</summary>
    [Fact]
    public void EmptySpanConstructorDoesNotReadAnElement()
    {
        Span<byte> destination = Span<byte>.Empty;
        var writer = new LuminPackWriter(ref destination);
        Assert.True(writer.GetSpan().IsEmpty);
        Assert.Equal(0, writer.Position);
    }
}
