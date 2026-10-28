using System.Runtime.InteropServices;
using System.Collections.Frozen;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the vectorized portable transfer against independently packed bytes around every block boundary.</summary>
public class PortableTransferTest
{
    private static readonly int[] Lengths = Enumerable.Range(0, 72).Concat(new[] { 127, 128, 129, 1000, 1001 }).ToArray();

    /// <summary>Checks count-only packed records across contiguous and scalar collection families.</summary>
    [Fact]
    public void EmptyPackedElementsRetainTheirZeroByteWire()
    {
        Assert.Empty(LuminPackSerializer.Serialize(default(PortableEmpty)));
        var items = new PortableEmpty[3];
        CheckEmpty(items, (PortableEmpty[] v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<PortableEmpty[]>(b), v => v.Length);
        CheckEmpty(items.ToList(), (List<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<List<PortableEmpty>>(b), v => v.Count);
        CheckEmpty(items.AsMemory(), (Memory<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<Memory<PortableEmpty>>(b), v => v.Length);
        CheckEmpty((ReadOnlyMemory<PortableEmpty>)items, (ReadOnlyMemory<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<ReadOnlyMemory<PortableEmpty>>(b), v => v.Length);
        CheckEmpty(new ArraySegment<PortableEmpty>(items), (ArraySegment<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<ArraySegment<PortableEmpty>>(b), v => v.Count);
        var queueBytes = LuminPackSerializer.Serialize(new Queue<PortableEmpty>(items));
        Assert.Equal(BitConverter.GetBytes(3).Concat(BitConverter.GetBytes(0)).Concat(BitConverter.GetBytes(0)).Concat(BitConverter.GetBytes(3)), queueBytes);
        Assert.Equal(3, LuminPackSerializer.Deserialize<Queue<PortableEmpty>>(queueBytes).Count);
        CheckEmpty(new Stack<PortableEmpty>(items), (Stack<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<Stack<PortableEmpty>>(b), v => v.Count);
        CheckEmpty(new LinkedList<PortableEmpty>(items), (LinkedList<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<LinkedList<PortableEmpty>>(b), v => v.Count);
        CheckEmpty(new HashSet<PortableEmpty>(items), (HashSet<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<HashSet<PortableEmpty>>(b), v => v.Count);
        CheckEmpty(new SortedSet<PortableEmpty>(items), (SortedSet<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<SortedSet<PortableEmpty>>(b), v => v.Count);
        CheckEmpty(System.Collections.Immutable.ImmutableArray.CreateRange(items), (System.Collections.Immutable.ImmutableArray<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<System.Collections.Immutable.ImmutableArray<PortableEmpty>>(b), v => v.Length);
        CheckEmpty(System.Collections.Immutable.ImmutableList.CreateRange(items), (System.Collections.Immutable.ImmutableList<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<System.Collections.Immutable.ImmutableList<PortableEmpty>>(b), v => v.Count);
        CheckEmpty(System.Collections.Immutable.ImmutableHashSet.CreateRange(items), (System.Collections.Immutable.ImmutableHashSet<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<System.Collections.Immutable.ImmutableHashSet<PortableEmpty>>(b), v => v.Count);
        CheckEmpty(new Dictionary<PortableEmpty, PortableEmpty> { [default] = default }, (Dictionary<PortableEmpty, PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<Dictionary<PortableEmpty, PortableEmpty>>(b), v => v.Count);
        CheckEmpty(new SortedDictionary<PortableEmpty, PortableEmpty> { [default] = default }, (SortedDictionary<PortableEmpty, PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<SortedDictionary<PortableEmpty, PortableEmpty>>(b), v => v.Count);
        CheckEmpty(new SortedList<PortableEmpty, PortableEmpty> { [default] = default }, (SortedList<PortableEmpty, PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<SortedList<PortableEmpty, PortableEmpty>>(b), v => v.Count);
        CheckEmpty((IList<PortableEmpty>)items.ToList(), (IList<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<IList<PortableEmpty>>(b), v => v.Count);
        CheckEmpty(new System.Collections.Concurrent.ConcurrentDictionary<PortableEmpty, PortableEmpty>(new Dictionary<PortableEmpty, PortableEmpty> { [default] = default }), (System.Collections.Concurrent.ConcurrentDictionary<PortableEmpty, PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<System.Collections.Concurrent.ConcurrentDictionary<PortableEmpty, PortableEmpty>>(b), v => v.Count);
        CheckEmpty(items.ToFrozenSet(), (FrozenSet<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<FrozenSet<PortableEmpty>>(b), v => v.Count);
        CheckEmpty(new Dictionary<PortableEmpty, PortableEmpty> { [default] = default }.ToFrozenDictionary(), (FrozenDictionary<PortableEmpty, PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<FrozenDictionary<PortableEmpty, PortableEmpty>>(b), v => v.Count);
        var immutable = System.Collections.Immutable.ImmutableDictionary<PortableEmpty, PortableEmpty>.Empty.Add(default, default);
        CheckEmpty(immutable, (System.Collections.Immutable.ImmutableDictionary<PortableEmpty, PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<System.Collections.Immutable.ImmutableDictionary<PortableEmpty, PortableEmpty>>(b), v => v.Count);
        CheckEmpty((System.Collections.Immutable.IImmutableDictionary<PortableEmpty, PortableEmpty>)immutable, (System.Collections.Immutable.IImmutableDictionary<PortableEmpty, PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<System.Collections.Immutable.IImmutableDictionary<PortableEmpty, PortableEmpty>>(b), v => v.Count);
        CheckEmpty(System.Collections.Immutable.ImmutableSortedDictionary<PortableEmpty, PortableEmpty>.Empty.Add(default, default), (System.Collections.Immutable.ImmutableSortedDictionary<PortableEmpty, PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<System.Collections.Immutable.ImmutableSortedDictionary<PortableEmpty, PortableEmpty>>(b), v => v.Count);
        CheckEmpty((System.Collections.Immutable.IImmutableSet<PortableEmpty>)System.Collections.Immutable.ImmutableHashSet.CreateRange(items), (System.Collections.Immutable.IImmutableSet<PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<System.Collections.Immutable.IImmutableSet<PortableEmpty>>(b), v => v.Count);
        var priority = new PriorityQueue<PortableEmpty, PortableEmpty>();
        priority.Enqueue(default, default);
        CheckEmpty(priority, (PriorityQueue<PortableEmpty, PortableEmpty> v) => LuminPackSerializer.Serialize(v), b => LuminPackSerializer.Deserialize<PriorityQueue<PortableEmpty, PortableEmpty>>(b), v => v.Count);
        var matrix = new PortableEmpty[2, 3];
        byte[] matrixBytes = LuminPackSerializer.Serialize(matrix);
        var decoded = LuminPackSerializer.Deserialize<PortableEmpty[,]>(matrixBytes);
        Assert.Equal(2, decoded.GetLength(0));
        Assert.Equal(3, decoded.GetLength(1));
        var reused = items;
        LuminPackSerializer.Deserialize(BitConverter.GetBytes(3).AsSpan(), ref reused);
        Assert.Same(items, reused);
        for (int prefix = 0; prefix < 4; prefix++)
        {
            byte[] shortHeader = new byte[prefix];
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<PortableEmpty[]>(shortHeader));
        }
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<PortableEmpty[]>(BitConverter.GetBytes(-2)));
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<int[]>(BitConverter.GetBytes(3)));
    }

    /// <summary>Verifies that a nonempty collection occupies only its count and preserves that count.</summary>
    /// <typeparam name="T">The concrete collection contract.</typeparam>
    /// <param name="value">The input collection.</param>
    /// <param name="write">Its generated writer.</param>
    /// <param name="read">Its generated reader.</param>
    /// <param name="count">Gets the logical count.</param>
    private static void CheckEmpty<T>(T value, Func<T, byte[]> write, Func<byte[], T> read, Func<T, int> count)
    {
        byte[] bytes = write(value);
        Assert.Equal(BitConverter.GetBytes(count(value)), bytes);
        Assert.Equal(count(value), count(read(bytes)));
    }

    /// <summary>Checks several elements per vector, one element per vector, two-vector elements and the field-wise fallback.</summary>
    [Fact]
    public void EveryKernelModeMatchesTheFieldOrderedWire()
    {
        Check(index => new PortableSmall { Tag = (byte)index, Value = index * 7919 - 3 },
            value => new[] { value.Tag }.Concat(BitConverter.GetBytes(value.Value)));
        Check(index => new PortableWide { Value = index * 31, Large = index * 0x10000000001L },
            value => BitConverter.GetBytes(value.Value).Concat(BitConverter.GetBytes(value.Large)));
        Check(index => new PortableRuns { A = (byte)index, B = (short)(index * 3), C = (byte)(index ^ 0x5A), D = index * 101, E = (byte)(index + 1), F = index * -77L },
            value => new[] { value.A }.Concat(BitConverter.GetBytes(value.B)).Concat(new[] { value.C }).Concat(BitConverter.GetBytes(value.D))
                .Concat(new[] { value.E }).Concat(BitConverter.GetBytes(value.F)));
        Check(index => new PortableLarge { A = index, B = (byte)index, C = -index, D = (byte)(index * 5), E = index * 3L, F = (byte)(255 - index) },
            value => BitConverter.GetBytes(value.A).Concat(new[] { value.B }).Concat(BitConverter.GetBytes(value.C)).Concat(new[] { value.D })
                .Concat(BitConverter.GetBytes(value.E)).Concat(new[] { value.F }));
        Check(index => new PortableVolatile { Tag = (byte)index, Value = index * 13 },
            value => new[] { value.Tag }.Concat(BitConverter.GetBytes(value.Value)));
    }

    /// <summary>Checks overlapping explicit fields write each field and read back with the later field winning.</summary>
    [Fact]
    public void OverlappingExplicitFieldsKeepFieldWiseSemantics()
    {
        foreach (int length in Lengths)
        {
            var values = Enumerable.Range(0, length).Select(index => new PortableOverlap { Whole = 0x0102030405060708L * (index + 1) }).ToArray();
            byte[] expected = BitConverter.GetBytes(length).Concat(values.SelectMany(value =>
                BitConverter.GetBytes(value.Whole).Concat(BitConverter.GetBytes(value.High)))).ToArray();
            Assert.Equal(expected, LuminPackSerializer.Serialize(values));
            Assert.Equal(values, LuminPackSerializer.Deserialize<PortableOverlap[]>(expected));
        }
    }

    /// <summary>Checks arrays, lists and reused storage for one shape at every length.</summary>
    /// <typeparam name="T">The portable struct.</typeparam>
    /// <param name="create">Creates the element at an index.</param>
    /// <param name="pack">Packs one element in declaration order.</param>
    private static void Check<T>(Func<int, T> create, Func<T, IEnumerable<byte>> pack) where T : unmanaged
    {
        foreach (int length in Lengths)
        {
            var values = Enumerable.Range(0, length).Select(create).ToArray();
            byte[] expected = BitConverter.GetBytes(length).Concat(values.SelectMany(pack)).ToArray();
            Assert.Equal(expected, Serialize(values));
            Assert.Equal(expected, Serialize(values.ToList()));
            Assert.Equal(values, Deserialize(expected, (T[]?)null));
            Assert.Equal(values, Deserialize(expected, new T[length]));
            Assert.Equal(values, DeserializeList(expected, new List<T>()));
        }
    }

    /// <summary>Selects the closed array writer for each tested shape.</summary>
    /// <typeparam name="T">The portable struct.</typeparam>
    /// <param name="values">The elements.</param>
    /// <returns>The serialized payload.</returns>
    private static byte[] Serialize<T>(T[] values) => values switch
    {
        PortableSmall[] small => LuminPackSerializer.Serialize(small),
        PortableWide[] wide => LuminPackSerializer.Serialize(wide),
        PortableRuns[] runs => LuminPackSerializer.Serialize(runs),
        PortableLarge[] large => LuminPackSerializer.Serialize(large),
        PortableVolatile[] other => LuminPackSerializer.Serialize(other),
        _ => throw new NotSupportedException()
    };

    /// <summary>Selects the closed list writer for each tested shape.</summary>
    /// <typeparam name="T">The portable struct.</typeparam>
    /// <param name="values">The elements.</param>
    /// <returns>The serialized payload.</returns>
    private static byte[] Serialize<T>(List<T> values) => values switch
    {
        List<PortableSmall> small => LuminPackSerializer.Serialize(small),
        List<PortableWide> wide => LuminPackSerializer.Serialize(wide),
        List<PortableRuns> runs => LuminPackSerializer.Serialize(runs),
        List<PortableLarge> large => LuminPackSerializer.Serialize(large),
        List<PortableVolatile> other => LuminPackSerializer.Serialize(other),
        _ => throw new NotSupportedException()
    };

    /// <summary>Reads into a fresh or reused array through the closed reader for each tested shape.</summary>
    /// <typeparam name="T">The portable struct.</typeparam>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="existing">The array to reuse, or null for a fresh read.</param>
    /// <returns>The read array.</returns>
    private static T[] Deserialize<T>(byte[] payload, T[]? existing)
    {
        object? result = existing;
        switch (existing ?? Array.Empty<T>())
        {
            case PortableSmall[]: { var value = (PortableSmall[]?)result; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            case PortableWide[]: { var value = (PortableWide[]?)result; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            case PortableRuns[]: { var value = (PortableRuns[]?)result; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            case PortableLarge[]: { var value = (PortableLarge[]?)result; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            case PortableVolatile[]: { var value = (PortableVolatile[]?)result; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            default: throw new NotSupportedException();
        }
        return (T[])result!;
    }

    /// <summary>Reads into a reused list through the closed reader for each tested shape.</summary>
    /// <typeparam name="T">The portable struct.</typeparam>
    /// <param name="payload">The serialized payload.</param>
    /// <param name="existing">The list to reuse.</param>
    /// <returns>The read list.</returns>
    private static List<T> DeserializeList<T>(byte[] payload, List<T> existing)
    {
        object result = existing;
        switch (existing)
        {
            case List<PortableSmall> list: { var value = list; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            case List<PortableWide> list: { var value = list; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            case List<PortableRuns> list: { var value = list; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            case List<PortableLarge> list: { var value = list; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            case List<PortableVolatile> list: { var value = list; LuminPackSerializer.Deserialize(payload.AsSpan(), ref value); result = value; break; }
            default: throw new NotSupportedException();
        }
        return (List<T>)result;
    }
}

/// <summary>An empty packed element has no serialized fields or padding bytes.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct PortableEmpty : IComparable<PortableEmpty>
{
    /// <summary>Compares the identical empty values.</summary>
    /// <param name="other">The other empty value.</param>
    /// <returns>Zero.</returns>
    public readonly int CompareTo(PortableEmpty other) => 0;
}

/// <summary>Eight storage bytes and five wire bytes: several elements per vector.</summary>
[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableSmall
{
    /// <summary>The leading byte.</summary>
    public byte Tag;
    /// <summary>The aligned integer.</summary>
    public int Value;
}

/// <summary>Twelve wire bytes in sixteen storage bytes: one element per vector.</summary>
[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableWide
{
    /// <summary>The leading integer.</summary>
    public int Value;
    /// <summary>The aligned long.</summary>
    public long Large;
}

/// <summary>Seventeen wire bytes split into six storage runs: two vectors per element.</summary>
[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableRuns
{
    /// <summary>A byte.</summary>
    public byte A;
    /// <summary>A short.</summary>
    public short B;
    /// <summary>A byte.</summary>
    public byte C;
    /// <summary>An int.</summary>
    public int D;
    /// <summary>A byte.</summary>
    public byte E;
    /// <summary>A long.</summary>
    public long F;
}

/// <summary>More than 32 storage bytes: the field-wise path only.</summary>
[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableLarge
{
    /// <summary>A long.</summary>
    public long A;
    /// <summary>A byte.</summary>
    public byte B;
    /// <summary>A long.</summary>
    public long C;
    /// <summary>A byte.</summary>
    public byte D;
    /// <summary>A long.</summary>
    public long E;
    /// <summary>A byte.</summary>
    public byte F;
}

/// <summary>Volatile storage keeps its field-wise accesses.</summary>
[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableVolatile
{
    /// <summary>A byte.</summary>
    public byte Tag;
    /// <summary>A volatile integer.</summary>
    public volatile int Value;

    /// <summary>Compares both fields.</summary>
    /// <param name="obj">The other value.</param>
    /// <returns>Whether both fields are equal.</returns>
    public override readonly bool Equals(object? obj) => obj is PortableVolatile other && other.Tag == Tag && other.Value == Value;

    /// <summary>Combines both fields.</summary>
    /// <returns>The hash.</returns>
    public override readonly int GetHashCode() => HashCode.Combine(Tag, Value);
}

/// <summary>Overlapping explicit fields: twelve wire bytes in eight storage bytes.</summary>
[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed), StructLayout(LayoutKind.Explicit, Size = 12)]
public partial struct PortableOverlap
{
    /// <summary>The complete value.</summary>
    [FieldOffset(0)] public long Whole;
    /// <summary>The high half, aliasing <see cref="Whole"/>.</summary>
    [FieldOffset(4)] public int High;
}
