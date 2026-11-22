using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using LuminPack;
using LuminPack.Attribute;

#if LUMINPACK_CPP_TUPLE_PACKED
[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
#endif

namespace LuminPackCppTupleFixtures;

/// <summary>Exchanges CLR ValueTuple fixtures with the generated C++ codecs.</summary>
internal static class Program
{
    /// <summary>Creates or verifies one binary and JSON document and the alternate tagged blocks.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero if the operation succeeds, or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        var expected = new TuplePacket
        {
            TaggedArray = [(39, 0x0102030405060708), (-500, long.MaxValue)],
            TaggedList = [((0x61, 0x11223344), long.MinValue), ((0x12, -42), 0x1122334455667788)],
            Managed = (0x11223344, "\u503c"),
            One = new("one"),
            Dense = (-17, 0x11223344),
            Packed = (0x51, 0x22446688),
            Nested = ((0x33, 0x11223344), 0x43),
            Inline = [(1, "yes"), (2, null)],
            DenseArray = [(3, -4), (-5, 9)],
            ByKey = new() { [-11] = (12, 0x102030405060708) },
            LongRest = (1, "a", 2, "b", 3, "c", 4, "\u03b4"),
#if LUMINPACK_CPP_TUPLE_PACKED
            PackedReverse = (0x22334455, 0x61),
#endif
        };

        if (args is ["write", var output])
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "tuple.bin"), LuminPackSerializer.Serialize(expected));
            File.WriteAllBytes(Path.Combine(output, "tuple.json"), Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson(expected)));
            return 0;
        }

        if (args is ["read", var input])
        {
            Verify(LuminPackSerializer.Deserialize<TuplePacket>(File.ReadAllBytes(Path.Combine(input, "tuple.bin"))), expected);
            Verify(LuminPackSerializer.DeserializeJson<TuplePacket>(File.ReadAllBytes(Path.Combine(input, "tuple.json"))), expected);
#if !LUMINPACK_CPP_TUPLE_PACKED
            for (int tag = 1; tag <= 4; tag++)
            {
                string file = "tuple-layout-" + tag.ToString(CultureInfo.InvariantCulture) + ".bin";
                Verify(LuminPackSerializer.Deserialize<TuplePacket>(File.ReadAllBytes(Path.Combine(input, file))), expected);
            }
#endif
            Console.WriteLine(".NET verified C++ ValueTuple Binary and JSON documents.");
            return 0;
        }

        Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Tuple <read|write> <directory>");
        return 2;
    }

    /// <summary>Compares all modeled fields of a decoded fixture.</summary>
    /// <param name="actual">The deserialized document.</param>
    /// <param name="expected">The original fixture.</param>
    private static void Verify(TuplePacket? actual, TuplePacket expected)
    {
        if (actual == null || !JsonNode.DeepEquals(JsonNode.Parse(LuminPackSerializer.SerializeJson(actual)),
                JsonNode.Parse(LuminPackSerializer.SerializeJson(expected))))
            throw new InvalidDataException("The decoded tuple document differs from the original fixture.");
    }
}

/// <summary>Exercises managed, dense raw, Packed, and multi-model tuple fields and collections.</summary>
[LuminPackable]
public partial class TuplePacket
{
    public (int, long)[] TaggedArray { get; set; } = [];
    public List<((byte, int), long)> TaggedList { get; set; } = [];
    public (int Id, string? Text) Managed { get; set; }
    public ValueTuple<string?> One { get; set; }
    public (int, int) Dense { get; set; }
    public (byte, int) Packed { get; set; }
    public ((byte, int), byte) Nested { get; set; }
    public List<(int, string?)> Inline { get; set; } = [];
    public (int, int)[] DenseArray { get; set; } = [];
    public Dictionary<int, (int, long)> ByKey { get; set; } = [];
    public (int, string?, int, string?, int, string?, int, string?) LongRest { get; set; }
#if LUMINPACK_CPP_TUPLE_PACKED
    public (int, byte) PackedReverse { get; set; }
#endif
}

/// <summary>Contains a raw CLR scalar tuple with headerless padding.</summary>
[LuminPackable]
public partial class PaddedTuplePacket
{
    public (int, byte) Value { get; set; }
}
