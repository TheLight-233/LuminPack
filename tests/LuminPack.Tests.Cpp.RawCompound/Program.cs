using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using LuminPack;
using LuminPack.Attribute;

#if LUMINPACK_CPP_RAW_COMPOUND_NATIVE
[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
#endif

namespace LuminPackCppRawCompoundFixtures;

/// <summary>Exchanges framework values with independently distinguishable CLR padding.</summary>
internal static class Program
{
    /// <summary>Writes managed fixtures or accepts the C++ round-trip documents.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero if the operation succeeds, or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        var expected = new RawCompoundPacket
        {
            Tuple = (0x12345678, 0x67),
            Tuples = [(26, 0x89), (-42, 0x32)],
            TupleList = [(123, 0x54)],
            Pair = new(0x98, -75),
            Pairs = [new(0x01, 0x11223344), new(0x12, -73)],
            PairList = [new(0x13, 0x55667788)],
            NestedTuple = new((0x11223344, 0x55)),
            NestedPairs = [new(new(0x6A, 0x33445566), 0x70)],
        };
        FillPadding(MemoryMarshal.CreateSpan(ref expected.Tuple, 1), 5, 0xA1);
        FillPadding(expected.Tuples.AsSpan(), 5, 0xA2);
        FillPadding(CollectionsMarshal.AsSpan(expected.TupleList), 5, 0xA4);
        FillPadding(expected.Pairs.AsSpan(), 1, 0xB1);
        FillPadding(CollectionsMarshal.AsSpan(expected.PairList), 1, 0xB3);
        FillPadding(MemoryMarshal.CreateSpan(ref expected.NestedTuple, 1), 5, 0xC1);
        FillPadding(expected.NestedPairs.AsSpan(), 1, 0xC2, stride: 12);
        FillPadding(expected.NestedPairs.AsSpan(), 9, 0xC3, stride: 12);

        if (args is ["write", var output])
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "raw-compound.bin"), LuminPackSerializer.Serialize(expected));
            File.WriteAllBytes(Path.Combine(output, "raw-compound.json"), Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson(expected)));
            return 0;
        }
        if (args is ["read", var input])
        {
            Verify(LuminPackSerializer.Deserialize<RawCompoundPacket>(File.ReadAllBytes(Path.Combine(input, "raw-compound.bin"))), expected);
            Verify(LuminPackSerializer.Deserialize<RawCompoundPacket>(File.ReadAllBytes(Path.Combine(input, "raw-compound-pad.bin"))), expected);
            Verify(LuminPackSerializer.DeserializeJson<RawCompoundPacket>(File.ReadAllBytes(Path.Combine(input, "raw-compound.json"))), expected);
            Console.WriteLine(".NET verified the C++ raw framework Binary and JSON documents.");
            return 0;
        }

        Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.RawCompound <read|write> <directory>");
        return 2;
    }

    /// <summary>Marks each expected three-byte padding range with a distinct value.</summary>
    /// <typeparam name="T">The native fixture value type.</typeparam>
    /// <param name="values">The initialized storage to mark.</param>
    /// <param name="offset">The expected padding start.</param>
    /// <param name="marker">The first record's padding byte.</param>
    /// <param name="stride">The expected byte width of each record.</param>
    private static void FillPadding<T>(Span<T> values, int offset, byte marker, int stride = 8) where T : unmanaged
    {
        Span<byte> bytes = MemoryMarshal.AsBytes(values);
        if (bytes.Length != values.Length * stride)
            throw new InvalidDataException("The CLR fixture has an unexpected native stride.");
        for (int index = 0; index < values.Length; index++)
            bytes.Slice(index * stride + offset, 3).Fill((byte)(marker + index));
    }

    /// <summary>Verifies all logical fields without inspecting JSON-irrelevant padding.</summary>
    /// <param name="actual">The deserialized C++ document.</param>
    /// <param name="expected">The original managed value.</param>
    private static void Verify(RawCompoundPacket? actual, RawCompoundPacket expected)
    {
        if (actual == null || !JsonNode.DeepEquals(JsonNode.Parse(LuminPackSerializer.SerializeJson(actual)),
                JsonNode.Parse(LuminPackSerializer.SerializeJson(expected))))
            throw new InvalidDataException("The decoded raw framework document differs from the original fixture.");
    }
}

/// <summary>Contains flat and nested padded pair collections and tuple values.</summary>
[LuminPackable]
public partial class RawCompoundPacket
{
    public (int, byte) Tuple;
    public (int, byte)[] Tuples { get; set; } = [];
    public List<(int, byte)> TupleList { get; set; } = [];
    public KeyValuePair<byte, int> Pair { get; set; }
    public KeyValuePair<byte, int>[] Pairs { get; set; } = [];
    public List<KeyValuePair<byte, int>> PairList { get; set; } = [];
    public ValueTuple<(int, byte)> NestedTuple;
    public KeyValuePair<KeyValuePair<byte, int>, byte>[] NestedPairs { get; set; } = [];
}

#if LUMINPACK_CPP_RAW_COMPOUND_NATIVE
/// <summary>Contains untagged CLR automatic storage without one verified layout.</summary>
[LuminPackable]
public partial class AmbiguousTuplePacket
{
    public (int, long) Value { get; set; }
}

/// <summary>Contains untagged platform-dependent pair storage without a portable model.</summary>
[LuminPackable]
public partial class AmbiguousPairPacket
{
    public KeyValuePair<byte, long>[] Values { get; set; } = [];
}
#endif
