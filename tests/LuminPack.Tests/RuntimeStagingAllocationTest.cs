using System.Collections;
using System.Collections.ObjectModel;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that JSON collection reads, sorted-list construction and stream input stage without temporary collections.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class RuntimeStagingAllocationTest
{
    /// <summary>Collections staged by count or pooled storage keep their order, shape and element values.</summary>
    [Fact]
    public void CountedJsonCollectionsRoundTrip()
    {
        var stack = new Stack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        var grid = new int[2, 3];
        grid[1, 2] = 7;
        var source = new StagedShapes
        {
            Stack = stack, Bits = new BitArray(new[] { true, false, true }), Grid = grid, Memory = new[] { 4, 5 },
            Set = new HashSet<string> { "a", "b" }, Observable = new(new() { 8, 9 })
        };
        StagedShapes? copy = null;
        LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(source), ref copy);
        Assert.Equal(new[] { 3, 2, 1 }, copy!.Stack.ToArray());
        Assert.Equal(3, copy.Bits.Length);
        Assert.True(copy.Bits[2]);
        Assert.False(copy.Bits[1]);
        Assert.Equal(7, copy.Grid[1, 2]);
        Assert.Equal(3, copy.Grid.GetLength(1));
        Assert.Equal(new[] { 4, 5 }, copy.Memory.ToArray());
        Assert.True(copy.Set.SetEquals(new[] { "a", "b" }));
        Assert.Equal(new[] { 8, 9 }, copy.Observable);
    }

    /// <summary>Unordered keys are sorted in pooled storage and still reject duplicates.</summary>
    [Fact]
    public void UnorderedSortedListKeysAreSortedInPlace()
    {
        var source = new StagedSorted { Sorted = new SortedList<int, string> { [3] = "c", [1] = "a", [2] = "b" } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        // Swap the first two pairs so the input is no longer ordered.
        var unordered = new StagedSortedPairs { Pairs = [new(2, "b"), new(1, "a"), new(3, "c")] };
        byte[] pairs = LuminPackSerializer.Serialize(unordered);
        StagedSorted? copy = null;
        LuminPackSerializer.Deserialize(pairs, ref copy);
        Assert.Equal(new[] { 1, 2, 3 }, copy!.Sorted.Keys);
        Assert.Equal("b", copy.Sorted[2]);
        StagedSorted? ordered = null;
        LuminPackSerializer.Deserialize(bytes, ref ordered);
        Assert.Equal(new[] { 1, 2, 3 }, ordered!.Sorted.Keys);
        var duplicate = new StagedSortedPairs { Pairs = [new(2, "b"), new(2, "a")] };
        StagedSorted? rejected = null;
        Assert.ThrowsAny<ArgumentException>(() => LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(duplicate), ref rejected));
    }

    /// <summary>A Unicode escape decodes through the static hexadecimal table without allocating.</summary>
    [Fact]
    public void UnicodeEscapesDoNotAllocate()
    {
        const string json = "{\"Text\":\"\\u0041\\u00e9\"}";
        var target = new StagedText();
        for (int i = 0; i < 64; i++) LuminPackSerializer.DeserializeJson(json, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) LuminPackSerializer.DeserializeJson(json, ref target);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal("Aé", target!.Text);
    }

    /// <summary>Binary reuse keeps the existing ObservableCollection backing list instead of replacing it.</summary>
    [Fact]
    public void ObservableCollectionBinaryReuseKeepsBackingList()
    {
        var source = new ObservableCollection<int> { 1, 2, 3, 4 };
        byte[] sourceBytes = LuminPackSerializer.Serialize(source);

        var target = new ObservableCollection<int>();
        for (int index = 0; index < 64; index++) target.Add(index);
        target.Clear();
        var before = LuminPackMarshal.As<ObservableCollection<int>, ObservableCollectionView<int>>(ref target).items;
        LuminPackSerializer.Deserialize(sourceBytes, ref target);
        var after = LuminPackMarshal.As<ObservableCollection<int>, ObservableCollectionView<int>>(ref target).items;
        Assert.Same(before, after);
        Assert.Equal(source, target);

        var small = new ObservableCollection<int> { 0, 1, 2, 3 };
        var largeSource = new ObservableCollection<int>();
        for (int index = 0; index < 64; index++) largeSource.Add(index);
        byte[] largeBytes = LuminPackSerializer.Serialize(largeSource);
        var smallBefore = LuminPackMarshal.As<ObservableCollection<int>, ObservableCollectionView<int>>(ref small).items;
        LuminPackSerializer.Deserialize(largeBytes, ref small);
        var smallAfter = LuminPackMarshal.As<ObservableCollection<int>, ObservableCollectionView<int>>(ref small).items;
        Assert.Same(smallBefore, smallAfter);
        Assert.Equal(largeSource, small);

        byte[] emptyBytes = LuminPackSerializer.Serialize(new ObservableCollection<int>());
        var reused = LuminPackMarshal.As<ObservableCollection<int>, ObservableCollectionView<int>>(ref target).items;
        LuminPackSerializer.Deserialize(emptyBytes, ref target);
        var reusedAfter = LuminPackMarshal.As<ObservableCollection<int>, ObservableCollectionView<int>>(ref target).items;
        Assert.Same(reused, reusedAfter);
        Assert.Empty(target);
    }

    /// <summary>Asynchronous stream input is read into pooled storage, including streams that report no length.</summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task StreamInputUsesPooledStorage()
    {
        var source = new StagedText { Text = new string('x', 10_000) };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var copy = await LuminPackSerializer.DeserializeAsync(new MemoryStream(bytes), (StagedText?)null);
        Assert.Equal(source.Text, copy!.Text);
        var chunked = await LuminPackSerializer.DeserializeAsync(new UnseekableStream(bytes), (StagedText?)null);
        Assert.Equal(source.Text, chunked!.Text);
    }

    /// <summary>Known-length input retains its first lease through the required EOF read at pool boundaries.</summary>
    /// <returns>The asynchronous boundary and limit verification.</returns>
    [Fact]
    public async Task SeekableInputKeepsItsLeaseForTheEofRead()
    {
        foreach (int length in new[] { 16, 256, 4096, 65536 })
        {
            byte[] bytes = new byte[length + 7];
            for (int index = 0; index < bytes.Length; index++) bytes[index] = (byte)(index * 19);
            using var stream = new ObservedInputStream(bytes) { Position = 7 };
            using var input = await global::LuminPack.Utility.LuminPackStreamInput.ReadAsync(stream, length, default);
            Assert.Equal(bytes.AsSpan(7).ToArray(), input.Span.ToArray());
            Assert.Same(stream.FirstBuffer, stream.EofBuffer);
            Assert.Equal(2, stream.ReadCount);
            stream.Position = 7;
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                using var rejected = await global::LuminPack.Utility.LuminPackStreamInput.ReadAsync(stream, length - 1, default);
            });
        }
    }

    /// <summary>Observes backing-array identity without allocating inside synchronous stream reads.</summary>
    private sealed class ObservedInputStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal byte[]? FirstBuffer;
        internal byte[]? EofBuffer;
        internal int ReadCount;

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            global::System.Runtime.InteropServices.MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var storage);
            FirstBuffer ??= storage.Array;
            ReadCount++;
            if (Position == Length) EofBuffer = storage.Array;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    /// <summary>Reports no length, so input storage grows while the stream is read.</summary>
    private sealed class UnseekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        /// <inheritdoc/>
        public override bool CanSeek => false;
    }
}

/// <summary>Collections whose JSON reads stage elements by count or pooled storage.</summary>
[LuminPackable]
public partial class StagedShapes
{
    public Stack<int> Stack = new();
    public BitArray Bits = new(0);
    public int[,] Grid = new int[0, 0];
    public ReadOnlyMemory<int> Memory;
    public ISet<string> Set = new HashSet<string>();
    public ReadOnlyObservableCollection<int> Observable = new(new());
}

/// <summary>A sorted list member.</summary>
[LuminPackable]
public partial class StagedSorted
{
    public SortedList<int, string> Sorted = new();
}

/// <summary>Writes the same wire as <see cref="StagedSorted"/> with pairs in a chosen order.</summary>
[LuminPackable]
public partial class StagedSortedPairs
{
    public List<KeyValuePair<int, string>> Pairs = [];
}

/// <summary>A single string member.</summary>
[LuminPackable]
public partial class StagedText
{
    public string Text = "";
}
