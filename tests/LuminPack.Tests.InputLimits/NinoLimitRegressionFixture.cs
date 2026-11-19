using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

[assembly: LuminPackOption(UntrustedData = true, MaxCollectionLength = 3, MaxStringLength = 4, MaxInputBytes = 256,
    FramedGroupings = true, RequireCompleteBinaryInput = true)]

namespace LuminPack.Tests.InputLimits;

/// <summary>Runs concrete reads in an assembly that declares finite binary input limits.</summary>
public static class NinoLimitRegressionFixture
{
    /// <summary>Reads a root collection whose encoded count exceeds the fixture policy.</summary>
    /// <param name="shape">The collection formatter to exercise.</param>
    /// <returns>The decoded value if the read succeeds.</returns>
    public static object? ReadOverlong(string shape)
    {
        int[] numbers = [1, 2, 3, 4];
        bool[] flags = [true, false, true, false];
        var pairs = new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3, [4] = 4 };

        return shape switch
        {
            "array" => LuminPackSerializer.Deserialize<int[]>(LuminPackSerializer.Serialize(numbers)),
            "boolean array" => LuminPackSerializer.Deserialize<bool[]>(LuminPackSerializer.Serialize(flags)),
            "list" => LuminPackSerializer.Deserialize<List<int>>(LuminPackSerializer.Serialize(new List<int>(numbers))),
            "boolean list" => LuminPackSerializer.Deserialize<List<bool>>(LuminPackSerializer.Serialize(new List<bool>(flags))),
            "dictionary" => LuminPackSerializer.Deserialize<Dictionary<int, int>>(LuminPackSerializer.Serialize(pairs)),
            "concurrent dictionary" => LuminPackSerializer.Deserialize<ConcurrentDictionary<int, int>>(
                LuminPackSerializer.Serialize(new ConcurrentDictionary<int, int>(pairs))),
            "sorted dictionary" => LuminPackSerializer.Deserialize<SortedDictionary<int, int>>(
                LuminPackSerializer.Serialize(new SortedDictionary<int, int>(pairs))),
            "immutable dictionary" => LuminPackSerializer.Deserialize<ImmutableDictionary<int, int>>(
                LuminPackSerializer.Serialize(pairs.ToImmutableDictionary())),
            "set" => LuminPackSerializer.Deserialize<HashSet<int>>(LuminPackSerializer.Serialize(new HashSet<int>(numbers))),
            "queue" => LuminPackSerializer.Deserialize<Queue<int>>(LuminPackSerializer.Serialize(new Queue<int>(numbers))),
            "linked list" => LuminPackSerializer.Deserialize<LinkedList<int>>(LuminPackSerializer.Serialize(new LinkedList<int>(numbers))),
            "read-only collection" => LuminPackSerializer.Deserialize<ReadOnlyCollection<int>>(
                LuminPackSerializer.Serialize(Array.AsReadOnly(numbers))),
            "immutable array" => LuminPackSerializer.Deserialize<ImmutableArray<int>>(
                LuminPackSerializer.Serialize(ImmutableArray.CreateRange(numbers))),
            "memory" => LuminPackSerializer.Deserialize<Memory<int>>(LuminPackSerializer.Serialize(new Memory<int>(numbers))),
            "array segment" => LuminPackSerializer.Deserialize<ArraySegment<int>>(
                LuminPackSerializer.Serialize(new ArraySegment<int>(numbers))),
            "string array" => LuminPackSerializer.Deserialize<string[]>(LuminPackSerializer.Serialize(new[] { "a", "b", "c", "d" })),
            "rectangular array" => LuminPackSerializer.Deserialize<int[,]>(LuminPackSerializer.Serialize(new int[2, 2])),
            "jagged array element" => LuminPackSerializer.Deserialize<int[][]>(LuminPackSerializer.Serialize(new[] { numbers })),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
    }

    /// <summary>Attempts to fill an existing array from an oversized record.</summary>
    public static void ReadOverlongArrayIntoExisting()
    {
        int[] target = [5, 6, 7, 8];
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(new[] { 1, 2, 3, 4 }), ref target);
    }

    /// <summary>Attempts to fill an existing rectangular array from an oversized record.</summary>
    public static void ReadOverlongRectangularArrayIntoExisting()
    {
        int[,] target = new int[2, 2];
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(new int[2, 2]), ref target);
    }

    /// <summary>Reads an array of zero-width packed elements from its count header alone.</summary>
    /// <param name="count">The encoded element count.</param>
    /// <returns>The decoded array if the count is within the fixture policy.</returns>
    public static EmptyPackedElement[] ReadForgedZeroWidthArray(int count)
    {
        Span<byte> input = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(input, count);
        return LuminPackSerializer.Deserialize<EmptyPackedElement[]>(input);
    }

    /// <summary>Round-trips a root string under the fixture policy.</summary>
    /// <param name="value">The string to write.</param>
    /// <returns>The decoded string if the read succeeds.</returns>
    public static string ReadString(string value) => LuminPackSerializer.Deserialize<string>(LuminPackSerializer.Serialize(value));

    /// <summary>Round-trips a string inside a root array under the fixture policy.</summary>
    /// <param name="value">The array's only string.</param>
    /// <returns>The decoded array if the read succeeds.</returns>
    public static string[] ReadStringArray(string value) =>
        LuminPackSerializer.Deserialize<string[]>(LuminPackSerializer.Serialize(new[] { value }));

    /// <summary>Round-trips a root array under the fixture policy.</summary>
    /// <param name="value">The array to write.</param>
    /// <returns>The decoded array if the read succeeds.</returns>
    public static int[] ReadArray(int[] value) => LuminPackSerializer.Deserialize<int[]>(LuminPackSerializer.Serialize(value));

    /// <summary>Reads a root array through the sequence overload.</summary>
    /// <param name="value">The array to write.</param>
    /// <returns>The decoded array if the read succeeds.</returns>
    public static int[] ReadSequence(int[] value)
    {
        var source = new ReadOnlySequence<byte>(LuminPackSerializer.Serialize(value));
        return LuminPackSerializer.Deserialize<int[]>(source);
    }

    /// <summary>Reads a root array through its generated typed registration.</summary>
    /// <param name="value">The array to write.</param>
    /// <returns>The decoded array if the read succeeds.</returns>
    public static int[] ReadRegisteredArray(int[] value) => ReadRegistered<int[]>(LuminPackSerializer.Serialize(value));

    /// <summary>Reads a root string through its typed registration.</summary>
    /// <param name="value">The string to write.</param>
    /// <returns>The decoded string if the read succeeds.</returns>
    public static string ReadRegisteredString(string value) => ReadRegistered<string>(LuminPackSerializer.Serialize(value));

    /// <summary>Exercises the generic fallback with an open payload type.</summary>
    /// <typeparam name="T">The registered payload type.</typeparam>
    /// <param name="source">The encoded record.</param>
    /// <returns>The decoded payload.</returns>
    private static T ReadRegistered<T>(ReadOnlySpan<byte> source) => LuminPackSerializer.Deserialize<T,
        LuminPackOptions<LuminPackUtf16LengthOptions, LuminPackGeneralFormat, RegisteredReadDepth, LuminPackTrustedJson>>(source);

    // Registry profiles share entries by constants. Give the fixture its own table without changing its binary format.
    private readonly struct RegisteredReadDepth : ILuminPackDepthOptions
    {
        public int Value => 31418;
    }

    /// <summary>Reads an array owned by a type that explicitly uses trusted input.</summary>
    /// <param name="value">The array to write.</param>
    /// <returns>The decoded array.</returns>
    public static int[] ReadTrustedArray(int[] value) =>
        LuminPackSerializer.Deserialize<TrustedArray>(LuminPackSerializer.Serialize(new TrustedArray { Items = value })).Items;

    /// <summary>Reads one scalar Boolean from its encoded byte.</summary>
    /// <param name="value">The serialized Boolean byte.</param>
    /// <returns>The decoder's result if the byte is canonical.</returns>
    public static bool ReadBoolean(byte value) => LuminPackSerializer.Deserialize<bool>(new[] { value });

    /// <summary>Reads one Boolean array element from its encoded byte.</summary>
    /// <param name="value">The serialized Boolean byte.</param>
    /// <returns>The one-element array if the byte is canonical.</returns>
    public static bool[] ReadBooleanArray(byte value) => LuminPackSerializer.Deserialize<bool[]>(new byte[] { 1, 0, 0, 0, value });

    /// <summary>Reads one Boolean list element from its encoded byte.</summary>
    /// <param name="value">The serialized Boolean byte.</param>
    /// <returns>The one-element list if the byte is canonical.</returns>
    public static List<bool> ReadBooleanList(byte value) =>
        LuminPackSerializer.Deserialize<List<bool>>(new byte[] { 1, 0, 0, 0, value });
}

/// <summary>A packed element whose wire representation has zero payload bytes.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct EmptyPackedElement
{
}

/// <summary>A record that opts out of the assembly's untrusted input policy.</summary>
[LuminPackable, LuminPackOption(UntrustedData = false)]
public partial class TrustedArray
{
    /// <summary>The array read under the record's trusted policy.</summary>
    public int[] Items = [];
}
