using System.Buffers;
using LuminPack.Attribute;
using LuminPackBenchmark;
using MemoryPack;

[LuminPackable, MemoryPackable]
public sealed partial class Element : IComparable<Element>
{
    public int Id;
    public string Name = "";
    public List<int> Values = new();

    /// <summary>Compares to.</summary>
    /// <param name="other">The value to compare with this instance.</param>
    /// <returns>The resulting <c>int</c>.</returns>
    public int CompareTo(Element? other) => other is null ? 1 : Id.CompareTo(other.Id);
}

[LuminPackable, MemoryPackable]
public sealed partial class Record : IComparable<Record>
{
    public int Id;
    public string Name = "";
    public List<int> Values = new();

    /// <summary>Compares to.</summary>
    /// <param name="other">The value to compare with this instance.</param>
    /// <returns>The resulting <c>int</c>.</returns>
    public int CompareTo(Record? other) => other is null ? 1 : Id.CompareTo(other.Id);
}

public enum SampleEnum : short
{
    First = 1,
    Second = 17
}

internal static class Config
{
    internal static int Count { get; set; }
}

internal static partial class Fixtures
{
    /// <summary>Handles make matrix for <c>Fixtures</c>.</summary>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    /// <param name="values">The values supplied to <c>MakeMatrix</c>.</param>
    /// <returns>The resulting <c>T[,]</c>.</returns>
    private static T[,] MakeMatrix<T>(T[] values)
    {
        var matrix = new T[1, values.Length];
        for (int i = 0; i < values.Length; i++)
            matrix[0, i] = values[i];
        return matrix;
    }

    /// <summary>Handles make sequence for <c>Fixtures</c>.</summary>
    /// <typeparam name="T">The value type processed by the operation.</typeparam>
    /// <param name="values">The values supplied to <c>MakeSequence</c>.</param>
    /// <returns>The resulting <c>ReadOnlySequence&lt;T&gt;</c>.</returns>
    private static ReadOnlySequence<T> MakeSequence<T>(T[] values)
    {
        int split = values.Length / 2;
        var start = new Segment<T>(values.AsMemory(0, split));
        var end = start.Append(values.AsMemory(split));
        return new(start, 0, end, end.Memory.Length);
    }

    private sealed class Segment<T> : ReadOnlySequenceSegment<T>
    {
        /// <summary>Initializes a <c>Segment</c> from the supplied data.</summary>
        /// <param name="memory">The memory supplied to <c>Segment</c>.</param>
        internal Segment(ReadOnlyMemory<T> memory) => Memory = memory;

        /// <summary>Handles append for <c>Segment</c>.</summary>
        /// <param name="memory">The memory supplied to <c>Append</c>.</param>
        /// <returns>The resulting <c>Segment&lt;T&gt;</c>.</returns>
        internal Segment<T> Append(ReadOnlyMemory<T> memory)
        {
            var segment = new Segment<T>(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = segment;
            return segment;
        }
    }

    /// <summary>Creates polymorphic fixture.</summary>
    /// <returns>The polymorphic fixture.</returns>
    private static PolymorphismClassBase CreatePolymorphicFixture()
    {
        Func<IFoo>[] variants =
        [
            FooA.Create, FooB.Create, FooC.Create, FooD.Create, FooE.Create,
            FooF.Create, FooG.Create, FooH.Create, FooI.Create, FooJ.Create,
            FooK.Create, FooL.Create, FooM.Create, FooN.Create, FooO.Create,
            FooP.Create, FooQ.Create, FooR.Create, FooS.Create, FooZ.Create
        ];

        var value = PolymorphismClass.Create();
        value.Numbers2.Clear();
        for (int i = 0; i < 1000; i++)
            value.Numbers2.Add(variants[i % variants.Length]());
        return value;
    }
}
