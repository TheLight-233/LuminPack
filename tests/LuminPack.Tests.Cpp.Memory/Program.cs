using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackCppMemoryFixtures;

/// <summary>Exchanges CLR contiguous and segmented buffer fixtures with generated C++ codecs.</summary>
internal static class Program
{
    /// <summary>Writes managed fixtures or verifies native documents.</summary>
    /// <param name="arguments">The operation name and fixture directory.</param>
    /// <returns>Zero for a completed operation or two for invalid arguments.</returns>
    private static int Main(string[] arguments)
    {
        if (arguments is not ["write" or "read", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Memory <read|write> <directory>");
            return 2;
        }

        var packet = new MemoryPacket
        {
            Mutable = new[] { 99, -44, 108, 203 }.AsMemory(1, 2),
            Text = new string?[] { "outside", "unit\u03b4", null, string.Empty }.AsMemory(1),
            Segment = new ArraySegment<byte>([0xa5, 1, 2, 0xff, 0xa5], 1, 3),
            EmptySegment = new ArraySegment<byte>([0xa5], 0, 0),
            Scalars = new ReadOnlySequence<long>(new long[] { long.MinValue, 0, long.MaxValue }),
            Segmented = MakeSequence<string?>(["first", null], ["", "\u503c"]),
            Immutable = [17, -19, 103],
            EmptyImmutable = ImmutableArray<int>.Empty,
            Nullable = new int?[] { null, 107, 0, null },
        };
        var tagged = new TaggedMemory { Points = new (int, long)[] { (33, -1024), (-44, 0x1122334455667788) } };
        var version = new VersionedMemory { View = new int[] { -7, 512, 9 }, End = 13 };
        bool write = arguments[0] == "write";
        if (write)
            Directory.CreateDirectory(directory);
        Exchange(directory, write, "memory", packet);
        Exchange(directory, write, "memory-tagged", tagged);
        Exchange(directory, write, "memory-version", version);
        Console.WriteLine(write ? ".NET wrote the memory view fixtures." : ".NET verified the C++ memory view documents.");
        return 0;
    }

    /// <summary>Creates a two-segment sequence to verify the flattened representation.</summary>
    /// <typeparam name="T">The element type of both segments.</typeparam>
    /// <param name="first">The elements in the first segment.</param>
    /// <param name="second">The elements in the second segment.</param>
    /// <returns>A sequence with both segments in wire order.</returns>
    private static ReadOnlySequence<T> MakeSequence<T>(T[] first, T[] second)
    {
        var head = new Segment<T>(first);
        var tail = head.Append(second);
        return new ReadOnlySequence<T>(head, 0, tail, second.Length);
    }

    /// <summary>Exchanges the Binary and JSON documents for one model.</summary>
    /// <typeparam name="T">The closed fixture model.</typeparam>
    /// <param name="directory">The input or output directory.</param>
    /// <param name="write">Whether the managed process writes the fixture.</param>
    /// <param name="name">The base filename of the model.</param>
    /// <param name="value">The expected managed model.</param>
    private static void Exchange<T>(string directory, bool write, string name, T value)
    {
        string binary = Path.Combine(directory, name + ".bin");
        string json = Path.Combine(directory, name + ".json");
        string expected = LuminPackSerializer.SerializeJson(value);
        if (write)
        {
            File.WriteAllBytes(binary, LuminPackSerializer.Serialize(value));
            File.WriteAllBytes(json, Encoding.Unicode.GetBytes(expected));
            return;
        }

        Verify(LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(binary)), expected);
        Verify(LuminPackSerializer.DeserializeJson<T>(File.ReadAllBytes(json)), expected);
    }

    /// <summary>Checks all decoded fields against the known JSON representation.</summary>
    /// <typeparam name="T">The decoded model type.</typeparam>
    /// <param name="actual">The model decoded from a C++ document.</param>
    /// <param name="expected">The managed baseline JSON.</param>
    private static void Verify<T>(T actual, string expected)
    {
        if (!JsonNode.DeepEquals(JsonNode.Parse(LuminPackSerializer.SerializeJson(actual)), JsonNode.Parse(expected)))
            throw new InvalidDataException("The decoded memory view document differed from the original.");
    }

    /// <summary>Provides an owned backing array for one sequence segment.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class Segment<T> : ReadOnlySequenceSegment<T>
    {
        /// <summary>Creates one segment over the supplied array.</summary>
        /// <param name="array">The complete backing array.</param>
        internal Segment(T[] array) => Memory = array;

        /// <summary>Appends the last segment to this sequence.</summary>
        /// <param name="array">The final backing array.</param>
        /// <returns>The new final segment.</returns>
        internal Segment<T> Append(T[] array)
        {
            var next = new Segment<T>(array) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}

/// <summary>Exercises views, default values and slices in a positional document.</summary>
[LuminPackable]
public partial class MemoryPacket
{
    public Memory<int> Mutable { get; set; }
    public ReadOnlyMemory<string?> Text { get; set; }
    public ArraySegment<byte> Segment { get; set; }
    public ArraySegment<byte> EmptySegment { get; set; }
    public ArraySegment<byte> MissingSegment { get; set; }
    public ReadOnlyMemory<int> EmptyMemory { get; set; }
    public ReadOnlySequence<long> Scalars { get; set; }
    public ReadOnlySequence<string?> Segmented { get; set; }
    public ImmutableArray<int> Immutable { get; set; }
    public ImmutableArray<int> EmptyImmutable { get; set; }
    public ImmutableArray<int> MissingImmutable { get; set; }
    public Memory<int?> Nullable { get; set; }
}

/// <summary>Exercises modeled and canonical Tagged tuple storage within a memory block.</summary>
[LuminPackable]
public partial class TaggedMemory
{
    public ReadOnlyMemory<(int, long)> Points { get; set; }
}

/// <summary>Exercises the scoped Version member around a memory value.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public partial class VersionedMemory
{
    public ReadOnlyMemory<int> View { get; set; }
    public int End { get; set; }
}
