using System.Collections.Immutable;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks known-scalar collection entries preserve record bytes and reuse semantics.</summary>
public class ScalarCollectionEntryTest
{
    /// <summary>Checks concrete and generic result widths, wire bytes and fresh reads.</summary>
    /// <param name="count">The number of elements.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(256)]
    public void ConcreteCollectionEntriesMatchTheirGenericRecord(int count)
    {
        int[] items = Enumerable.Range(0, count).Select(index => index * 7919 % 100003 - 50000).ToArray();
        Stack<int> stack = new Stack<int>(items);
        byte[] StackBytes = LuminPackSerializer.Serialize(stack);
        Stack<int> StackCopy = null!;
        Assert.Equal(StackBytes.Length, LuminPackSerializer.Deserialize(StackBytes.AsSpan(), ref StackCopy));
        Assert.Equal(StackBytes.Length, LuminPackSerializer.Sizeof(stack));
        Assert.Equal(StackBytes, LuminPackSerializer.Serialize<Stack<int>>(stack));
        Assert.Equal(stack, StackCopy);
        Queue<int> queue = new Queue<int>(items);
        byte[] QueueBytes = LuminPackSerializer.Serialize(queue);
        Queue<int> QueueCopy = null!;
        Assert.Equal(QueueBytes.Length, LuminPackSerializer.Deserialize(QueueBytes.AsSpan(), ref QueueCopy));
        Assert.Equal(QueueBytes.Length, LuminPackSerializer.Sizeof(queue));
        Assert.Equal(QueueBytes, LuminPackSerializer.Serialize<Queue<int>>(queue));
        Assert.Equal(queue, QueueCopy);
        LinkedList<int> linkedList = new LinkedList<int>(items);
        byte[] LinkedListBytes = LuminPackSerializer.Serialize(linkedList);
        LinkedList<int> LinkedListCopy = null!;
        Assert.Equal(LinkedListBytes.Length, LuminPackSerializer.Deserialize(LinkedListBytes.AsSpan(), ref LinkedListCopy));
        Assert.Equal(LinkedListBytes.Length, LuminPackSerializer.Sizeof(linkedList));
        Assert.Equal(LinkedListBytes, LuminPackSerializer.Serialize<LinkedList<int>>(linkedList));
        Assert.Equal(linkedList, LinkedListCopy);
        SortedSet<int> sortedSet = new SortedSet<int>(items);
        byte[] SortedSetBytes = LuminPackSerializer.Serialize(sortedSet);
        SortedSet<int> SortedSetCopy = null!;
        Assert.Equal(SortedSetBytes.Length, LuminPackSerializer.Deserialize(SortedSetBytes.AsSpan(), ref SortedSetCopy));
        Assert.Equal(SortedSetBytes.Length, LuminPackSerializer.Sizeof(sortedSet));
        Assert.Equal(SortedSetBytes, LuminPackSerializer.Serialize<SortedSet<int>>(sortedSet));
        Assert.Equal(sortedSet, SortedSetCopy);
        ImmutableHashSet<int> immutableHashSet = items.ToImmutableHashSet();
        byte[] ImmutableHashSetBytes = LuminPackSerializer.Serialize(immutableHashSet);
        ImmutableHashSet<int> ImmutableHashSetCopy = null!;
        Assert.Equal(ImmutableHashSetBytes.Length, LuminPackSerializer.Deserialize(ImmutableHashSetBytes.AsSpan(), ref ImmutableHashSetCopy));
        Assert.Equal(ImmutableHashSetBytes.Length, LuminPackSerializer.Sizeof(immutableHashSet));
        Assert.Equal(ImmutableHashSetBytes, LuminPackSerializer.Serialize<ImmutableHashSet<int>>(immutableHashSet));
        Assert.Equal(immutableHashSet, ImmutableHashSetCopy);
        ImmutableSortedSet<int> immutableSortedSet = items.ToImmutableSortedSet();
        byte[] ImmutableSortedSetBytes = LuminPackSerializer.Serialize(immutableSortedSet);
        ImmutableSortedSet<int> ImmutableSortedSetCopy = null!;
        Assert.Equal(ImmutableSortedSetBytes.Length, LuminPackSerializer.Deserialize(ImmutableSortedSetBytes.AsSpan(), ref ImmutableSortedSetCopy));
        Assert.Equal(ImmutableSortedSetBytes.Length, LuminPackSerializer.Sizeof(immutableSortedSet));
        Assert.Equal(ImmutableSortedSetBytes, LuminPackSerializer.Serialize<ImmutableSortedSet<int>>(immutableSortedSet));
        Assert.Equal(immutableSortedSet, ImmutableSortedSetCopy);
    }

    /// <summary>Checks stack, wrapped queue, linked nodes and custom-comparer set reuse.</summary>
    [Fact]
    public void MutableScalarCollectionsRetainTheirDestinationAndComparer()
    {
        int[] items = [3, 7, 11, 19];
        var stack = new Stack<int>(items);
        var stackTarget = new Stack<int>(new[] { 99 });
        var originalStack = stackTarget;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(stack).AsSpan(), ref stackTarget);
        Assert.Same(originalStack, stackTarget);
        Assert.Equal(stack, stackTarget);

        var queue = new Queue<int>(items);
        queue.Dequeue(); queue.Dequeue(); queue.Enqueue(23); queue.Enqueue(29);
        var queueTarget = new Queue<int>(new[] { 99 });
        var originalQueue = queueTarget;
        byte[] queueBytes = LuminPackSerializer.Serialize(queue);
        Assert.Equal(16 + items.Length * 4, queueBytes.Length);
        Assert.Equal(queueBytes.Length, LuminPackSerializer.Sizeof(queue));
        LuminPackSerializer.Deserialize(queueBytes.AsSpan(), ref queueTarget);
        Assert.Same(originalQueue, queueTarget);
        Assert.Equal(queue, queueTarget);

        var linked = new LinkedList<int>(items);
        var linkedTarget = new LinkedList<int>(new[] { 99 });
        var originalLinked = linkedTarget;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(linked).AsSpan(), ref linkedTarget);
        Assert.Same(originalLinked, linkedTarget);
        Assert.Equal(linked, linkedTarget);

        var set = new SortedSet<int>(items);
        var comparer = Comparer<int>.Create((left, right) => right.CompareTo(left));
        var setTarget = new SortedSet<int>(comparer) { 99 };
        var originalSet = setTarget;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(set).AsSpan(), ref setTarget);
        Assert.Same(originalSet, setTarget);
        Assert.Same(comparer, setTarget.Comparer);
        Assert.Equal(items.AsEnumerable().Reverse(), setTarget);
    }

    /// <summary>Checks a null concrete collection remains distinct from an empty record.</summary>
    [Fact]
    public void NullCollectionsKeepTheirFourByteMarker()
    {
        Stack<int> stack = null!;
        Queue<int> queue = null!;
        LinkedList<int> linked = null!;
        SortedSet<int> sorted = null!;
        ImmutableHashSet<int> hashed = null!;
        ImmutableSortedSet<int> immutableSorted = null!;
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize(stack));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize(queue));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize(linked));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize(sorted));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize(hashed));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize(immutableSorted));
    }
}

