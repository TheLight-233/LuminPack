using System.Buffers.Binary;
using System.Collections;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects exact scalar interface writes and the original ownership of unknown implementations.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class ScalarInterfaceWriteTest
{
    /// <summary>Preserves count framing and native payloads across every existing binary profile.</summary>
    /// <param name="count">The number of source values before removing sparse set entries.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(1000)]
    public void ExactImplementationsKeepTheirBytes(int count)
    {
        var items = Enumerable.Range(-count / 2, count).ToArray();
        var list = items.ToList();
        var set = items.ToHashSet();
        set.RemoveWhere(item => item % 7 == 0);
        CheckProfiles<LuminPackUtf8LengthOptions>(set, list);
        CheckProfiles<LuminPackUtf8TokenOptions>(set, list);
        CheckProfiles<LuminPackUtf16LengthOptions>(set, list);
        CheckProfiles<LuminPackUtf16TokenOptions>(set, list);
        var colliding = items.ToHashSet(new CollisionComparer());
        colliding.RemoveWhere(item => item % 5 == 0);
        CheckProfiles<LuminPackUtf16LengthOptions>(colliding, list);
        IList<int> array = items;
        AssertWire(LuminPackSerializer.Serialize(in array), items);
    }

    /// <summary>Keeps null distinct from empty without requesting an implementation's count.</summary>
    [Fact]
    public void NullContractsKeepTheirOriginalHeader()
    {
        ISet<int> set = null!;
        IReadOnlySet<int> readOnly = null!;
        IList<int> list = null!;
        foreach (byte[] bytes in new[] { LuminPackSerializer.Serialize(in set), LuminPackSerializer.Serialize(in readOnly), LuminPackSerializer.Serialize(in list) })
        {
            Assert.Equal(4, bytes.Length);
            Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        }
    }

    /// <summary>Retains one traversal for derived implementations with an opaque count property.</summary>
    [Fact]
    public void UnknownImplementationsStillEnumerateOnce()
    {
        var set = new OpaqueSet([7, 3, 9]);
        ISet<int> setContract = set;
        AssertWire(LuminPackSerializer.Serialize(in setContract), [3, 7, 9]);
        Assert.Equal(1, set.Enumerations);
        Assert.Equal(1, set.Disposals);
        var list = new OpaqueList([7, 3, 9]);
        IList<int> listContract = list;
        AssertWire(LuminPackSerializer.Serialize(in listContract), [7, 3, 9]);
        Assert.Equal(1, list.Enumerations);
        Assert.Equal(1, list.Disposals);
    }

    /// <summary>Retains exception identity and releases a failed unknown traversal's pooled output.</summary>
    [Fact]
    public void FailedUnknownTraversalDisposesAndLeavesTheNextOperationClean()
    {
        var failure = new InvalidOperationException("Caller failure.");
        var source = new OpaqueSet([3, 7, 9]) { Failure = failure };
        ISet<int> value = source;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Serialize(in value)));
        Assert.Equal(1, source.Disposals);
        ISet<int> next = new HashSet<int> { 13, 19 };
        AssertWire(LuminPackSerializer.Serialize(in next), [13, 19]);
    }

    /// <summary>Uses one selected reference for the implementation proof, count and raw kernel.</summary>
    /// <returns>Completion of the independently changing source reference.</returns>
    [Fact]
    public async Task ChangingTheCallerReferenceCannotInvalidateTheImplementationProof()
    {
        ISet<int> first = new HashSet<int> { 1, 3, 5 };
        ISet<int> second = new SortedSet<int> { 10, 12, 14 };
        var holder = new MutableHolder { Value = first };
        using var ready = new ManualResetEventSlim();
        int done = 0;
        var setter = Task.Run(() =>
        {
            ready.Set();
            while (Volatile.Read(ref done) == 0)
            {
                Volatile.Write(ref holder.Value, first);
                Volatile.Write(ref holder.Value, second);
            }
        });
        try
        {
            ready.Wait();
            for (int index = 0; index < 5000; index++)
            {
                byte[] bytes = LuminPackSerializer.Serialize(in holder.Value);
                Assert.Equal(16, bytes.Length);
                int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
                Assert.True(start is 1 or 10);
                AssertWire(bytes, start == 1 ? [1, 3, 5] : [10, 12, 14]);
            }
        }
        finally
        {
            Volatile.Write(ref done, 1);
            await setter;
        }
    }

    /// <summary>Checks a complete native integer record through all three statically bound interfaces.</summary>
    /// <typeparam name="TProfile">The existing compile-time binary profile.</typeparam>
    /// <param name="set">The exact set implementation.</param>
    /// <param name="list">The exact list implementation.</param>
    private static void CheckProfiles<TProfile>(HashSet<int> set, List<int> list) where TProfile : struct, ILuminPackOptions
    {
        ISet<int> mutable = set;
        IReadOnlySet<int> readOnly = set;
        IList<int> sequence = list;
        AssertWire(LuminPackSerializer.Serialize<TProfile>(in mutable), set.ToArray());
        AssertWire(LuminPackSerializer.Serialize<TProfile>(in readOnly), set.ToArray());
        AssertWire(LuminPackSerializer.Serialize<TProfile>(in sequence), list.ToArray());
    }

    /// <summary>Compares the original little-endian count and each native integer.</summary>
    /// <param name="actual">The complete serializer output.</param>
    /// <param name="values">The collection's original enumeration order.</param>
    private static void AssertWire(byte[] actual, int[] values)
    {
        var expected = new byte[4 + values.Length * 4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, values.Length);
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4 + index * 4), values[index]);
        Assert.Equal(expected, actual);
    }

    /// <summary>Exercises sparse entries without using their comparer during a raw write.</summary>
    private sealed class CollisionComparer : IEqualityComparer<int>
    {
        /// <summary>Compares the original integer identities.</summary>
        /// <param name="left">The first identity.</param>
        /// <param name="right">The second identity.</param>
        /// <returns>Whether the identities match.</returns>
        public bool Equals(int left, int right) => left == right;

        /// <summary>Places all items in one collision bucket.</summary>
        /// <param name="value">The original identity.</param>
        /// <returns>The common bucket hash.</returns>
        public int GetHashCode(int value) => 0;
    }

    /// <summary>Provides a non-HashSet implementation whose traversal owns observable cleanup.</summary>
    private sealed class OpaqueSet : SortedSet<int>, ISet<int>, IEnumerable<int>
    {
        internal int Enumerations, Disposals;
        internal Exception? Failure;
        int ICollection<int>.Count => throw new InvalidOperationException("Opaque Count must not be queried.");

        /// <summary>Initializes the opaque sorted implementation.</summary>
        /// <param name="items">The original values.</param>
        internal OpaqueSet(IEnumerable<int> items) : base(items) { }

        /// <summary>Traverses once and optionally propagates the exact caller exception.</summary>
        /// <returns>The caller-owned traversal.</returns>
        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            if (++Enumerations != 1) throw new InvalidOperationException("Repeated enumeration.");
            try
            {
                int index = 0;
                foreach (int item in (SortedSet<int>)this)
                {
                    if (index++ == 1 && Failure is not null) throw Failure;
                    yield return item;
                }
            }
            finally { Disposals++; }
        }
    }

    /// <summary>Provides a derived List whose interface traversal must not be replaced by its backing array.</summary>
    private sealed class OpaqueList : List<int>, IList<int>, IEnumerable<int>
    {
        internal int Enumerations, Disposals;
        int ICollection<int>.Count => throw new InvalidOperationException("Opaque Count must not be queried.");

        /// <summary>Initializes the derived implementation.</summary>
        /// <param name="items">The original values.</param>
        internal OpaqueList(IEnumerable<int> items) : base(items) { }

        /// <summary>Records one complete interface traversal.</summary>
        /// <returns>The original values in list order.</returns>
        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            if (++Enumerations != 1) throw new InvalidOperationException("Repeated enumeration.");
            try { foreach (int item in (List<int>)this) yield return item; }
            finally { Disposals++; }
        }
    }

    /// <summary>Provides an explicitly passed interface field whose reference can change independently.</summary>
    private sealed class MutableHolder
    {
        internal ISet<int> Value = null!;
    }
}
