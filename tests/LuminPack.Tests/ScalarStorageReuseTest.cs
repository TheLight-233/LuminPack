using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Option;
using Xunit;

/// <summary>Verifies the isolated proposal's scalar destination selection and input boundaries.</summary>
public sealed class ScalarStorageReuseTest
{
    /// <summary>Checks unchanged raw bits across the supported scalar storage widths.</summary>
    [Fact]
    public void FrameworkWidthsKeepTheirCompleteRawPayload()
    {
        CheckWidth<byte>([0, 255, 17], static (ReadOnlySpan<byte> bytes, ref IEnumerable<byte> value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckWidth<short>([short.MinValue, -1, short.MaxValue], static (ReadOnlySpan<byte> bytes, ref IEnumerable<short> value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckWidth<int>([int.MinValue, 0, int.MaxValue], static (ReadOnlySpan<byte> bytes, ref IEnumerable<int> value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckWidth<long>([long.MinValue, -1, long.MaxValue], static (ReadOnlySpan<byte> bytes, ref IEnumerable<long> value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckWidth<Vector3>([new(1, -2, 3), new(4, 5, -6)], static (ReadOnlySpan<byte> bytes, ref IEnumerable<Vector3> value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckWidth<decimal>([decimal.MinValue, 1.0000000000000000000000000001m, decimal.MaxValue], static (ReadOnlySpan<byte> bytes, ref IEnumerable<decimal> value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckWidth<Matrix3x2>([Matrix3x2.Identity, new(1, 2, 3, 4, 5, 6)], static (ReadOnlySpan<byte> bytes, ref IEnumerable<Matrix3x2> value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckWidth<Matrix4x4>([Matrix4x4.Identity, Matrix4x4.CreateScale(137)], static (ReadOnlySpan<byte> bytes, ref IEnumerable<Matrix4x4> value) => LuminPackSerializer.Deserialize(bytes, ref value));
    }

    /// <summary>Reads a concrete generated sequence entry.</summary>
    /// <typeparam name="T">The closed raw framework scalar.</typeparam>
    /// <param name="bytes">The complete input window.</param>
    /// <param name="value">The selected destination storage.</param>
    /// <returns>The consumed record width.</returns>
    private delegate int ReadSequence<T>(ReadOnlySpan<byte> bytes, ref IEnumerable<T> value);

    /// <summary>Builds an independent raw fixture and checks array identity, bits and count transitions.</summary>
    /// <typeparam name="T">The raw framework scalar.</typeparam>
    /// <param name="values">The complete nonempty raw payload.</param>
    /// <param name="read">The concrete generated read operation.</param>
    private static void CheckWidth<T>(T[] values, ReadSequence<T> read) where T : unmanaged
    {
        ReadOnlySpan<byte> payload = MemoryMarshal.AsBytes(values.AsSpan());
        byte[] bytes = new byte[sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, values.Length);
        payload.CopyTo(bytes.AsSpan(sizeof(int)));
        T[] storage = new T[values.Length];
        IEnumerable<T> target = storage;
        Assert.Equal(bytes.Length, read(bytes, ref target));
        Assert.Same(storage, target);
        Assert.True(payload.SequenceEqual(MemoryMarshal.AsBytes(storage.AsSpan())));
        Assert.Equal(4, read(new byte[4], ref target));
        Assert.Empty(target);
        Assert.Equal(4, read([255, 255, 255, 255], ref target));
        Assert.Null(target);
    }

    /// <summary>Never observes an existing custom memory manager while selecting scalar storage.</summary>
    [Fact]
    public void MemoryManagerRemainsUnobserved()
    {
        using var manager = new UnobservedManager();
        Memory<int> target = manager.View();
        Memory<int> source = new[] { 3, 5, 7 };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Equal(source.ToArray(), target.ToArray());
        Assert.Equal(0, manager.Observations);
        Assert.Equal(new[] { 101, 103, 107 }, manager.Items);
    }
    /// <summary>Checks exact List and array destinations through all profiles.</summary>
    [Fact]
    public void ExactInterfaceStorageRetainsIdentity()
    {
        CheckInterfaces<LuminPackUtf8LengthOptions>();
        CheckInterfaces<LuminPackUtf8TokenOptions>();
        CheckInterfaces<LuminPackUtf16LengthOptions>();
        CheckInterfaces<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Runs every selected interface family with changed contents and complete widths.</summary>
    /// <typeparam name="TOptions">The unchanged compile-time binary profile.</typeparam>
    private static void CheckInterfaces<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        IList<int> source = new List<int> { 3, 5, 7 };
        byte[] bytes = LuminPackSerializer.Serialize<TOptions>(source);
        IEnumerable<int> sequence = new int[3];
        ICollection<int> collection = new List<int> { 101 };
        IList<int> list = new List<int> { 103 };
        IReadOnlyList<int> readOnlyList = new int[3];
        IReadOnlyCollection<int> readOnlyCollection = new List<int> { 107 };
        object[] original = [sequence, collection, list, readOnlyList, readOnlyCollection];
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TOptions>(bytes, ref sequence));
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TOptions>(bytes, ref collection));
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TOptions>(bytes, ref list));
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TOptions>(bytes, ref readOnlyList));
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TOptions>(bytes, ref readOnlyCollection));
        object[] actual = [sequence, collection, list, readOnlyList, readOnlyCollection];
        for (int i = 0; i < actual.Length; i++)
        {
            Assert.Same(original[i], actual[i]);
            Assert.Equal(source, (IEnumerable<int>)actual[i]);
        }
    }

    /// <summary>Preserves exact mutable slice offsets and surrounding storage.</summary>
    [Fact]
    public void MutableSlicesKeepOffsetsAndCanaries()
    {
        Memory<int> source = new[] { 11, 13, 17 };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        int[] storage = [101, 0, 0, 0, 103];
        Memory<int> memory = storage.AsMemory(1, 3);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref memory));
        Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<int>)memory, out var slice));
        Assert.Same(storage, slice.Array);
        Assert.Equal(1, slice.Offset);
        Assert.Equal(new[] { 101, 11, 13, 17, 103 }, storage);
        ArraySegment<int> segment = new(storage, 1, 3);
        byte[] segmentBytes = LuminPackSerializer.Serialize(new ArraySegment<int>(new[] { 19, 23, 29 }));
        Assert.Equal(segmentBytes.Length, LuminPackSerializer.Deserialize(segmentBytes, ref segment));
        Assert.Same(storage, segment.Array);
        Assert.Equal(1, segment.Offset);
        Assert.Equal(new[] { 101, 19, 23, 29, 103 }, storage);
    }

    /// <summary>Rejects incomplete scalar payloads before modifying reused destinations.</summary>
    [Fact]
    public void TruncationDoesNotMutateSelectedStorage()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new[] { 3, 5, 7 });
        for (int length = 0; length < bytes.Length; length++)
        {
            int[] storage = [101, 103, 107];
            IEnumerable<int> target = storage;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, length), ref target));
            Assert.Same(storage, target);
            Assert.Equal(new[] { 101, 103, 107 }, storage);
            Memory<int> memory = storage;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, length), ref memory));
            Assert.Equal(new[] { 101, 103, 107 }, storage);
        }
    }

    /// <summary>Uses fresh storage for different shapes and retains matrix identity for matching shapes.</summary>
    [Fact]
    public void MatrixSelectionChecksEveryDimensionAndLowerBound()
    {
        int[,] source = { { 3, 5 }, { 7, 11 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        int[,] matching = new int[2, 2];
        object original = matching;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref matching));
        Assert.Same(original, matching);
        Assert.Equal(source.Cast<int>(), matching.Cast<int>());
        int[,] different = new int[1, 4];
        original = different;
        LuminPackSerializer.Deserialize(bytes, ref different);
        Assert.NotSame(original, different);
        int[,] lowerBounds = (int[,])Array.CreateInstance(typeof(int), [2, 2], [1, 1]);
        original = lowerBounds;
        LuminPackSerializer.Deserialize(bytes, ref lowerBounds);
        Assert.NotSame(original, lowerBounds);
        Assert.Equal(0, lowerBounds.GetLowerBound(0));
        for (int length = 0; length < bytes.Length; length++)
        {
            int[,] target = { { 101, 103 }, { 107, 109 } };
            object old = target;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, length), ref target));
            Assert.Same(old, target);
            Assert.Equal(new[] { 101, 103, 107, 109 }, target.Cast<int>());
        }
    }

    /// <summary>A derived List remains an excluded implementation.</summary>
    [Fact]
    public void DerivedListsKeepFreshReplacement()
    {
        IList<int> source = new[] { 3, 5, 7 };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var derived = new DerivedList { 101 };
        IList<int> target = derived;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.NotSame(derived, target);
        Assert.IsType<List<int>>(target);
        Assert.Equal(101, derived[0]);
    }

    /// <summary>Represents an implementation excluded by the exact framework type gate.</summary>
    private sealed class DerivedList : List<int>;

    /// <summary>Detects observing calls on an excluded memory owner.</summary>
    private sealed class UnobservedManager : MemoryManager<int>
    {
        public readonly int[] Items = [101, 103, 107];
        public int Observations;

        /// <summary>Constructs the view without invoking GetSpan.</summary>
        /// <returns>The existing manager-owned memory.</returns>
        public Memory<int> View() => CreateMemory(Items.Length);

        /// <summary>Records observing span access.</summary>
        /// <returns>The old backing storage.</returns>
        public override Span<int> GetSpan() { Observations++; return Items; }

        /// <summary>Records observing backing-array inspection.</summary>
        /// <param name="segment">The exposed old array.</param>
        /// <returns>Always true for this deliberately observable owner.</returns>
        protected override bool TryGetArray(out ArraySegment<int> segment) { Observations++; segment = new(Items); return true; }

        /// <summary>Rejects observing pin operations.</summary>
        /// <param name="elementIndex">The requested element offset.</param>
        /// <returns>No pin handle is created.</returns>
        public override MemoryHandle Pin(int elementIndex = 0) => throw new InvalidOperationException("The old memory owner must remain unobserved.");

        /// <summary>Rejects unbalanced unpin operations.</summary>
        public override void Unpin() => throw new InvalidOperationException("The old memory owner must remain unobserved.");

        /// <summary>Releases no resources owned outside this test.</summary>
        /// <param name="disposing">Whether disposal was requested explicitly.</param>
        protected override void Dispose(bool disposing) { }
    }
}
