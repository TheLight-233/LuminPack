using System.Buffers;
using System.Text;
using System.Text.Json.Nodes;
using LuminPack;
using LuminPack.Attribute;

#if LUMINPACK_CPP_SEQUENCE_PACKED
[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
#elif LUMINPACK_CPP_SEQUENCE_NATIVE
[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
#endif

namespace LuminPackCppSequenceFixtures;

/// <summary>Exchanges positional documents containing sliced multi-segment sequences.</summary>
internal static class Program
{
    /// <summary>Writes managed sequence fixtures or verifies the C++ documents.</summary>
    /// <param name="args">The operation and the fixture directory.</param>
    /// <returns>Zero for a completed operation or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["write" or "read", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Sequence <read|write> <directory>");
            return 2;
        }

        var expected = new SequencePacket
        {
            Tagged = MakeSequence(new TaggedItem { Code = 20, Amount = -123, Active = true },
                new TaggedItem { Code = 21, Amount = 0x1122334455667788 }),
            Packed = MakeSequence(new PackedItem { Code = 30, Amount = -124 },
                new PackedItem { Code = 31, Amount = 0x2233445566778899, Active = true }),
            Native = MakeSequence(new NativeItem { Code = 0x11223344, Amount = -123, Flag = 6 },
                new NativeItem { Code = 144, Amount = 42, Flag = 7 }),
            Pairs = MakeSequence(new KeyValuePair<int, byte>(-123, 0x41), new(922, 0x62)),
            Tuples = MakeSequence((0x11223344, (byte)1), (-933, (byte)2)),
            SmallNullable = MakeSequence((int?)null, (int?)712),
            BooleanNullable = MakeSequence((bool?)null, (bool?)true),
#if !LUMINPACK_CPP_SEQUENCE_NATIVE
            WideNullable = MakeSequence((long?)null, (long?)0x1122334455667788),
            WidePairs = MakeSequence(new KeyValuePair<byte, long>(0x41, -123), new(0x62, 0x1122334455667788)),
            WideTuples = MakeSequence((0x11223344, -25L), (-933, 0x1122334455667788L)),
            NestedTuples = MakeSequence(((true, 0x112233u), -25L), ((false, 0x11223344u), 0x1122334455667788L)),
#endif
        };
        string binary = Path.Combine(directory, "sequence.bin");
        string json = Path.Combine(directory, "sequence.json");
        string expectedJson = LuminPackSerializer.SerializeJson(expected);
        if (args[0] == "write")
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(binary, LuminPackSerializer.Serialize(expected));
            File.WriteAllBytes(json, Encoding.Unicode.GetBytes(expectedJson));
        }
        else
        {
            Verify(LuminPackSerializer.Deserialize<SequencePacket>(File.ReadAllBytes(binary)), expectedJson);
            Verify(LuminPackSerializer.DeserializeJson<SequencePacket>(File.ReadAllBytes(json)), expectedJson);
        }
        return 0;
    }

    /// <summary>Verifies the flattened values and their order against the managed document.</summary>
    /// <param name="actual">The value read from a C++ document.</param>
    /// <param name="expected">The managed JSON representation.</param>
    private static void Verify(SequencePacket? actual, string expected)
    {
        if (actual == null || !JsonNode.DeepEquals(JsonNode.Parse(LuminPackSerializer.SerializeJson(actual)), JsonNode.Parse(expected)))
            throw new InvalidDataException("The decoded sequence document differs from the original.");
    }

    /// <summary>Builds a slice covering two values with an empty segment between them.</summary>
    /// <typeparam name="T">The sequence element.</typeparam>
    /// <param name="first">The value in the first nonempty segment.</param>
    /// <param name="second">The value in the last segment.</param>
    /// <returns>The selected portion of the segment chain.</returns>
    private static ReadOnlySequence<T> MakeSequence<T>(T first, T second)
    {
        var head = new Segment<T>([default!, first]);
        var empty = head.Append([]);
        var tail = empty.Append([second, default!]);
        return new ReadOnlySequence<T>(head, 1, tail, 1);
    }

    private sealed class Segment<T> : ReadOnlySequenceSegment<T>
    {
        /// <summary>Wraps the specified backing array.</summary>
        /// <param name="items">The elements of this segment.</param>
        internal Segment(T[] items) => Memory = items;

        /// <summary>Links one additional segment to the current one.</summary>
        /// <param name="items">The elements of the additional segment.</param>
        /// <returns>The newly linked segment.</returns>
        internal Segment<T> Append(T[] items)
        {
            var tail = new Segment<T>(items) { RunningIndex = RunningIndex + Memory.Length };
            Next = tail;
            return tail;
        }
    }
}

/// <summary>Exercises standalone modeled elements, raw storage and both nullable slot encodings.</summary>
[LuminPackable]
public sealed partial class SequencePacket
{
    public ReadOnlySequence<TaggedItem> Tagged;
    public ReadOnlySequence<PackedItem> Packed;
    public ReadOnlySequence<NativeItem> Native;
    public ReadOnlySequence<KeyValuePair<int, byte>> Pairs;
    public ReadOnlySequence<(int, byte)> Tuples;
    public ReadOnlySequence<int?> SmallNullable;
    public ReadOnlySequence<bool?> BooleanNullable;
    public ReadOnlySequence<TaggedItem> Empty;
#if !LUMINPACK_CPP_SEQUENCE_NATIVE
    public ReadOnlySequence<long?> WideNullable;
    public ReadOnlySequence<KeyValuePair<byte, long>> WidePairs;
    public ReadOnlySequence<(int, long)> WideTuples;
    public ReadOnlySequence<((bool, uint), long)> NestedTuples;
#endif
}

#if LUMINPACK_CPP_SEQUENCE_NATIVE
/// <summary>Exercises the diagnostic for an untagged nullable without a unique storage layout.</summary>
[LuminPackable]
public sealed partial class AmbiguousNativeNullableSequence
{
    public ReadOnlySequence<long?> Values;
}
#endif

[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Tagged)]
public partial struct TaggedItem
{
    public byte Code;
    public long Amount;
    public bool Active;
}

[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct PackedItem
{
    public byte Code;
    public long Amount;
    public bool Active;
}

[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
public partial struct NativeItem
{
    public int Code;
    public int Amount;
    public byte Flag;
}
