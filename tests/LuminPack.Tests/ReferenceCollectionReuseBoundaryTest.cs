using System.Buffers;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects user-code and failure boundaries of exact mutable reference reuse.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class ReferenceCollectionReuseBoundaryTest
{
    /// <summary>Does not observe or modify a user memory owner even when it exposes an array.</summary>
    /// <param name="exposesArray">Whether the old owner would expose its array if asked.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MemoryManagerKeepsTheFreshFallback(bool exposesArray)
    {
        using var manager = new UnobservedMemoryManager(exposesArray);
        Memory<string> target = manager.GetMemory();
        Memory<string> source = new[] { "new-a", "new-b" };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Equal(0, manager.SpanReads);
        Assert.Equal(0, manager.ArrayReads);
        Assert.Equal(new[] { "old-a", "old-b" }, manager.Items);
        Assert.Equal(source.ToArray(), target.ToArray());
    }

    /// <summary>Never writes base-class values through covariant derived-array storage.</summary>
    [Fact]
    public void CovariantSegmentKeepsTheFreshFallback()
    {
        ReferenceReuseDerived[] backing = [new() { Number = 719, Extra = 981 }];
        ArraySegment<ReferenceReuseBase> target = new(backing);
        ArraySegment<ReferenceReuseBase> source = new(new ReferenceReuseBase[] { new() { Number = 137 } });
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.IsType<ReferenceReuseBase[]>(target.Array);
        Assert.NotSame(backing, target.Array);
        Assert.Equal(137, target[0].Number);
        Assert.Equal(719, backing[0].Number);
        Assert.Equal(981, backing[0].Extra);
    }

    /// <summary>Publishes parsed element changes through aliases while preserving the caller's selected view.</summary>
    [Fact]
    public void MutableViewCallbacksRetainReceiversAndFailurePrefix()
    {
        ReferenceReuseCallback[] backing = [new() { Number = 719 }, new() { Number = 981 }, new() { Number = 319 }];
        Memory<ReferenceReuseCallback> target = backing;
        Memory<ReferenceReuseCallback> source = new ReferenceReuseCallback[] { new() { Number = 137 }, new() { Number = 271 }, new() { Number = 317 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var failure = new InvalidOperationException("The second callback failed.");
        int calls = 0;
        ReferenceReuseCallback.OnRead = value =>
        {
            Assert.Same(backing[calls++], value);
            if (calls == 2) throw failure;
        };
        try
        {
            Exception? actual = Record.Exception(() => LuminPackSerializer.Deserialize(bytes, ref target));
            Assert.Same(failure, actual);
            Assert.Equal(2, calls);
            Assert.Equal(new[] { 137, 271, 319 }, backing.Select(value => value.Number));
            Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<ReferenceReuseCallback>)target, out var storage));
            Assert.Same(backing, storage.Array);
        }
        finally { ReferenceReuseCallback.OnRead = null; }
    }

    /// <summary>Keeps the captured concrete destination if a callback replaces the interface reference and then fails.</summary>
    [Fact]
    public void InterfaceCallbackReplacementDoesNotPublishAnUnfinishedSelectedLocal()
    {
        ReferenceReuseCallback[] backing = [new() { Number = 719 }, new() { Number = 981 }];
        IList<ReferenceReuseCallback> target = backing;
        IList<ReferenceReuseCallback> source = new[] { new ReferenceReuseCallback { Number = 137 }, new ReferenceReuseCallback { Number = 271 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var replacement = new List<ReferenceReuseCallback>();
        var failure = new InvalidOperationException("A callback replaced the target.");
        ReferenceReuseCallback.OnRead = _ => { target = replacement; throw failure; };
        try
        {
            Exception? actual = Record.Exception(() => LuminPackSerializer.Deserialize(bytes, ref target));
            Assert.Same(failure, actual);
            Assert.Same(replacement, target);
            Assert.Equal(137, backing[0].Number);
            Assert.Equal(981, backing[1].Number);
        }
        finally { ReferenceReuseCallback.OnRead = null; }
    }

    private sealed class UnobservedMemoryManager(bool exposesArray) : MemoryManager<string>
    {
        public readonly string[] Items = ["old-a", "old-b"];
        public int SpanReads;
        public int ArrayReads;

        /// <summary>Constructs a memory view without observing the owner's span.</summary>
        /// <returns>The manager-owned view.</returns>
        public Memory<string> GetMemory() => CreateMemory(Items.Length);

        /// <summary>Records unexpected access to the old owner's span.</summary>
        /// <returns>The old storage.</returns>
        public override Span<string> GetSpan() { SpanReads++; return Items; }

        /// <summary>Records unexpected inspection of the owner's array exposure.</summary>
        /// <param name="segment">The optionally exposed original array.</param>
        /// <returns>Whether this manager exposes its backing storage.</returns>
        protected override bool TryGetArray(out ArraySegment<string> segment)
        {
            ArrayReads++;
            segment = exposesArray ? new(Items) : default;
            return exposesArray;
        }

        /// <summary>Rejects an unexpected owner pin.</summary>
        /// <param name="elementIndex">The requested element.</param>
        /// <returns>No native handle is available.</returns>
        public override MemoryHandle Pin(int elementIndex = 0) => throw new InvalidOperationException("The old owner must not be pinned.");

        /// <summary>Rejects an unexpected owner unpin.</summary>
        public override void Unpin() => throw new InvalidOperationException("The old owner must not be unpinned.");

        /// <summary>Releases no resources for this managed test owner.</summary>
        /// <param name="disposing">Whether disposal is explicit.</param>
        protected override void Dispose(bool disposing) { }
    }
}

/// <summary>Provides a base-class contract whose array may be covariant.</summary>
[LuminPackable]
public class ReferenceReuseBase
{
    public int Number;
}

/// <summary>Provides narrower storage that must not receive a newly created base object.</summary>
[LuminPackable]
public sealed class ReferenceReuseDerived : ReferenceReuseBase
{
    public int Extra;
}

/// <summary>Exposes original receiver identity and user failures during a reused element read.</summary>
[LuminPackable]
public sealed class ReferenceReuseCallback
{
    public int Number;
    public static Action<ReferenceReuseCallback>? OnRead;

    /// <summary>Observes the completed selected element before its caller continues.</summary>
    [LuminPackOnDeserialized]
    public void CompleteRead() => OnRead?.Invoke(this);
}
