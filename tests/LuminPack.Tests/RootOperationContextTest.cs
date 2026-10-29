using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the in-place leases of thread-cached operation contexts and the fixed-record root entries.</summary>
/// <remarks>Every serializer call names its concrete type, so it binds the generated concrete overload.</remarks>
[Collection(nameof(RootOperationContextTest))]
public class RootOperationContextTest
{
    /// <summary>Checks that a nested rent receives another writer, which then serves the thread's fast path.</summary>
    [Fact]
    public void NestedRentReplacesTheActiveThreadWriter()
    {
        LuminBufferWriterPool.ClearThreadLocalCache();
        var outer = LuminBufferWriterPool.Rent();
        var inner = LuminBufferWriterPool.Rent();
        Assert.NotSame(outer, inner);
        LuminBufferWriterPool.Return(inner);
        var again = LuminBufferWriterPool.Rent();
        Assert.Same(inner, again);
        LuminBufferWriterPool.Return(again);
        LuminBufferWriterPool.Return(outer);
        var next = LuminBufferWriterPool.Rent();
        Assert.Same(inner, next);
        Assert.Equal(0, next.CurrentIndex);
        LuminBufferWriterPool.Return(next);
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Checks that a writer held for a long time does not push later rents off the thread's fast path.</summary>
    [Fact]
    public void LongHeldWriterDoesNotDisableTheThreadFastPath()
    {
        LuminBufferWriterPool.ClearThreadLocalCache();
        var held = LuminBufferWriterPool.Rent();
        var first = LuminBufferWriterPool.Rent();
        LuminBufferWriterPool.Return(first);
        for (int i = 0; i < 100; i++)
        {
            var writer = LuminBufferWriterPool.Rent();
            Assert.Same(first, writer);
            LuminBufferWriterPool.Return(writer);
        }
        LuminBufferWriterPool.Return(held);
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Checks that a writer returned on another thread is reset and reused by the thread that cached it.</summary>
    [Fact]
    public void WriterReturnedOnAnotherThreadIsReusedByItsThread()
    {
        LuminBufferWriterPool.ClearThreadLocalCache();
        var writer = LuminBufferWriterPool.Rent();
        LuminPackSerializer.Serialize(new FixedPoint { X = 1 }, writer);
        Assert.NotEqual(0, writer.CurrentIndex);
        var thread = new Thread(() => LuminBufferWriterPool.Return(writer));
        thread.Start();
        thread.Join();
        var again = LuminBufferWriterPool.Rent();
        Assert.Same(writer, again);
        Assert.Equal(0, again.CurrentIndex);
        LuminBufferWriterPool.Return(again);
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Checks that clearing the thread cache detaches, rather than disposes, a writer that is still rented.</summary>
    [Fact]
    public void ClearingTheThreadCacheKeepsARentedWriterUsable()
    {
        LuminBufferWriterPool.ClearThreadLocalCache();
        var writer = LuminBufferWriterPool.Rent();
        LuminBufferWriterPool.ClearThreadLocalCache();
        Assert.True(writer.UseFirstBuffer);
        var value = new FixedPoint { X = -7, Y = 2.5f, Z = true, Day = DayOfWeek.Friday };
        LuminPackSerializer.Serialize(value, writer);
        Assert.Equal(LuminPackSerializer.Serialize(value), writer.GetSpan().ToArray());
        LuminBufferWriterPool.Return(writer);
        var next = LuminBufferWriterPool.Rent();
        Assert.Same(writer, next);
        LuminBufferWriterPool.Return(next);
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Checks that an oversized thread writer releases its native storage and stays usable.</summary>
    [Fact]
    public void OversizedThreadWriterReleasesOnlyItsStorage()
    {
        LuminBufferWriterPool.ClearThreadLocalCache();
        var writer = LuminBufferWriterPool.Rent();
        writer.EnsureCapacity(LuminBufferWriterPool.MaxPooledBufferSize);
        LuminBufferWriterPool.Return(writer);
        Assert.False(writer.UseFirstBuffer);
        var again = LuminBufferWriterPool.Rent();
        Assert.Same(writer, again);
        var value = new FixedPoint { X = 3, Y = -1f, Day = DayOfWeek.Monday };
        LuminPackSerializer.Serialize(value, again);
        Assert.Equal(LuminPackSerializer.Serialize(value), again.GetSpan().ToArray());
        Assert.True(again.TotalLength < LuminBufferWriterPool.MaxPooledBufferSize);
        LuminBufferWriterPool.Return(again);
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Checks that a nested lease receives an independent state, which becomes the cached state once released.</summary>
    [Fact]
    public void NestedLeasesKeepIndependentReferenceTables()
    {
        LuminPackReaderOptionalState? readers = null;
        var outerReader = LuminPackReaderOptionalState.Lease(ref readers);
        Assert.Same(outerReader, readers);
        var marker = new object();
        outerReader.AddObjectReference(1, marker);
        var innerReader = LuminPackReaderOptionalState.Lease(ref readers);
        Assert.NotSame(outerReader, innerReader);
        Assert.Same(innerReader, readers);
        Assert.ThrowsAny<Exception>(() => innerReader.GetObjectReference(1));
        innerReader.AddObjectReference(2, marker);
        innerReader.Release();
        Assert.Same(marker, outerReader.GetObjectReference(1));
        var reused = LuminPackReaderOptionalState.Lease(ref readers);
        Assert.Same(innerReader, reused);
        Assert.ThrowsAny<Exception>(() => reused.GetObjectReference(2));
        reused.Release();
        outerReader.Release();
        Assert.Same(innerReader, readers);

        LuminPackWriterOptionalState? writers = null;
        var outerWriter = LuminPackWriterOptionalState.Lease(ref writers);
        Assert.False(outerWriter.GetOrAddReference(marker).existsReference);
        var innerWriter = LuminPackWriterOptionalState.Lease(ref writers);
        Assert.False(innerWriter.GetOrAddReference(marker).existsReference);
        innerWriter.Release();
        Assert.True(outerWriter.GetOrAddReference(marker).existsReference);
        outerWriter.Release();
        var cachedWriter = LuminPackWriterOptionalState.Lease(ref writers);
        Assert.False(cachedWriter.GetOrAddReference(marker).existsReference);
        cachedWriter.Release();
    }

    /// <summary>Checks that a failed allocating write returns the thread writer clean and on the fast path.</summary>
    [Fact]
    public void FailedWriteReturnsTheThreadWriterClean()
    {
        LuminBufferWriterPool.ClearThreadLocalCache();
        var cached = LuminBufferWriterPool.Rent();
        LuminBufferWriterPool.Return(cached);
        var failing = new ThrowingRecord { Name = "fails", Fail = true };
        Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Serialize(failing));
        var after = LuminBufferWriterPool.Rent();
        Assert.Same(cached, after);
        Assert.Equal(0, after.CurrentIndex);
        LuminBufferWriterPool.Return(after);

        var value = new ThrowingRecord { Name = "works" };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        ThrowingRecord? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
        Assert.Equal("works", copy!.Name);
        LuminBufferWriterPool.ClearThreadLocalCache();
    }

    /// <summary>Checks reference-tracked roots after failed reads and with callbacks that reenter the serializer.</summary>
    [Fact]
    public void ReferenceTrackedRootsSurviveFailuresAndReentrancy()
    {
        var first = new ReentrantNode { Name = "first" };
        first.Next = new ReentrantNode { Name = "second", Next = first };
        byte[] bytes = LuminPackSerializer.Serialize(first);
        for (int round = 0; round < 3; round++)
        {
            ReentrantNode? truncated = null;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length / 2), ref truncated));
            ReentrantNode? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal("first", copy!.Name);
            Assert.Equal("second", copy.Next!.Name);
            Assert.Same(copy, copy.Next.Next);
        }

        ReentrantNode.Reenter = true;
        try
        {
            byte[] nested = LuminPackSerializer.Serialize(first);
            Assert.Equal(bytes, nested);
            ReentrantNode? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(nested.AsSpan(), ref copy));
            Assert.Same(copy, copy!.Next!.Next);
        }
        finally
        {
            ReentrantNode.Reenter = false;
        }
    }

    /// <summary>Checks the direct fixed-record entries against the reusable-buffer records, including null roots.</summary>
    [Fact]
    public void FixedRecordsMatchTheReusableBufferRecords()
    {
        foreach (var point in new[] { new FixedPoint { X = int.MinValue, Y = float.NaN, Z = true, Day = DayOfWeek.Saturday }, new FixedPoint(), null })
        {
            var value = point;
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
            FixedPoint? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value is null, copy is null);
            if (value is not null)
                Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
            // An ordinary null root leaves an existing instance untouched.
            var existing = new FixedPoint { X = 99 };
            var target = existing;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref target));
            Assert.Same(existing, target);
            Assert.Equal(value?.X ?? 99, target!.X);
        }

        var pair = new FixedPair { A = long.MaxValue, B = -2, C = 'z' };
        byte[] pairBytes = LuminPackSerializer.Serialize(pair);
        Pooled(pairBytes, buffer => LuminPackSerializer.Serialize(pair, buffer));
        FixedPair pairCopy = default;
        Assert.Equal(pairBytes.Length, LuminPackSerializer.Deserialize(pairBytes.AsSpan(), ref pairCopy));
        Assert.Equal(pair, pairCopy);

        var derived = new FixedDerived { BaseValue = 11, Extra = 4.25 };
        byte[] derivedBytes = LuminPackSerializer.Serialize(derived);
        Pooled(derivedBytes, buffer => LuminPackSerializer.Serialize(derived, buffer));
        FixedDerived? derivedCopy = null;
        Assert.Equal(derivedBytes.Length, LuminPackSerializer.Deserialize(derivedBytes.AsSpan(), ref derivedCopy));
        Assert.Equal(11, derivedCopy!.BaseValue);
        Assert.Equal(4.25, derivedCopy.Extra);
    }

    /// <summary>Compares a direct result with the record the pooled reusable-buffer entry writes.</summary>
    /// <param name="direct">The allocating concrete entry's result.</param>
    /// <param name="write">Writes the same value through the reusable-buffer entry.</param>
    private static void Pooled(byte[] direct, Action<LuminBufferWriter> write)
    {
        var buffer = LuminBufferWriterPool.Rent();
        try
        {
            write(buffer);
            Assert.Equal(buffer.GetSpan().ToArray(), direct);
        }
        finally
        {
            LuminBufferWriterPool.Return(buffer);
        }
    }
}

/// <summary>A class whose members are all primitives or enums.</summary>
[LuminPackable]
public sealed partial class FixedPoint
{
    /// <summary>Stores the x.</summary>
    public int X;
    /// <summary>Stores the y.</summary>
    public float Y { get; set; }
    /// <summary>Stores the z.</summary>
    public bool Z;
    /// <summary>Stores the day.</summary>
    public DayOfWeek Day;
}

/// <summary>A struct whose members are all primitives.</summary>
[LuminPackable]
public partial struct FixedPair : IEquatable<FixedPair>
{
    /// <summary>Stores a.</summary>
    public long A;
    /// <summary>Stores b.</summary>
    public short B;
    /// <summary>Stores c.</summary>
    public char C;

    /// <inheritdoc />
    public readonly bool Equals(FixedPair other) => A == other.A && B == other.B && C == other.C;
    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is FixedPair other && Equals(other);
    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(A, B, C);
}

/// <summary>A base class with a primitive member.</summary>
[LuminPackable]
public partial class FixedBase
{
    /// <summary>Stores the base value.</summary>
    public int BaseValue;
}

/// <summary>A derived class whose own and inherited members are primitives.</summary>
[LuminPackable]
public sealed partial class FixedDerived : FixedBase
{
    /// <summary>Stores the extra value.</summary>
    public double Extra;
}

/// <summary>A string-bearing record whose serialization callback can fail.</summary>
[LuminPackable]
public sealed partial class ThrowingRecord
{
    /// <summary>Stores the name.</summary>
    public string? Name;

    /// <summary>Whether the serialization callback throws.</summary>
    [LuminPackIgnore]
    public bool Fail;

    /// <summary>Throws when <see cref="Fail"/> is set.</summary>
    [LuminPackOnSerializing]
    public void OnSerializing()
    {
        if (Fail) throw new InvalidOperationException("Serialization callback failed.");
    }
}

/// <summary>A reference-tracked node whose callbacks can run nested reference-tracked operations.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public sealed partial class ReentrantNode
{
    /// <summary>Whether the callbacks reenter the serializer.</summary>
    [LuminPackIgnore]
    public static bool Reenter;

    /// <summary>Stores the name.</summary>
    [LuminPackOrder(0)]
    public string? Name;

    /// <summary>Stores the next node.</summary>
    [LuminPackOrder(1)]
    public ReentrantNode? Next;

    /// <summary>Serializes and reads an unrelated cycle while the outer operation is active.</summary>
    [LuminPackOnSerializing]
    public void OnSerializing() => RunNested();

    /// <summary>Serializes and reads an unrelated cycle while the outer read is active.</summary>
    [LuminPackOnDeserialized]
    public void OnDeserialized() => RunNested();

    /// <summary>Runs one nested cycle round trip.</summary>
    private static void RunNested()
    {
        if (!Reenter) return;
        Reenter = false;
        try
        {
            var nested = new ReentrantNode { Name = "nested" };
            nested.Next = nested;
            byte[] bytes = LuminPackSerializer.Serialize(nested);
            ReentrantNode? copy = null;
            LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy);
            if (!ReferenceEquals(copy, copy!.Next)) throw new InvalidOperationException("Nested cycle lost its identity.");
        }
        finally
        {
            Reenter = true;
        }
    }
}
