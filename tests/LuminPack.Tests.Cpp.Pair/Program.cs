using System.Text;
using System.Text.Json.Nodes;
using LuminPack;
using LuminPack.Attribute;

#if LUMINPACK_CPP_PAIR_PACKED
[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
#endif

namespace LuminPackCppPairFixtures;

/// <summary>Exchanges managed KeyValuePair fixtures with generated C++ codecs.</summary>
internal static class Program
{
    /// <summary>Creates or reads Binary and JSON documents containing pairs.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero for success, or two when the arguments are invalid.</returns>
    private static int Main(string[] args)
    {
        var expected = new PairPacket
        {
            Uneven = new(0x51, 0x11223344),
            Wide = new(-17, long.MinValue),
            Text = new(null, -3),
            DenseArray = [new(-47, 0x12345678), new(281, -9)],
            DenseList = [new(-73, uint.MaxValue), new(26, 0x89abcdef)],
            TextArray = [new("value", null), new("\u503c", 0x34)],
            ByKey = new() { [76] = new(0x27, -100) },
#if LUMINPACK_CPP_PAIR_PACKED
            PaddedArray = [new(0x99, -11), new(0x32, 0x01020304)],
            TaggedList = [new(-42, 0x1122334455667788)],
#endif
        };

        if (args is ["write", var output])
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "pair.bin"), LuminPackSerializer.Serialize(expected));
            File.WriteAllBytes(Path.Combine(output, "pair.json"), Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson(expected)));
            return 0;
        }

        if (args is ["read", var input])
        {
            Verify(LuminPackSerializer.Deserialize<PairPacket>(File.ReadAllBytes(Path.Combine(input, "pair.bin"))), expected);
            Verify(LuminPackSerializer.DeserializeJson<PairPacket>(File.ReadAllBytes(Path.Combine(input, "pair.json"))), expected);
            Console.WriteLine(".NET verified the C++ KeyValuePair Binary and JSON documents.");
            return 0;
        }

        Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Pair <read|write> <directory>");
        return 2;
    }

    /// <summary>Compares all modeled fields of a decoded fixture.</summary>
    /// <param name="actual">The deserialized document.</param>
    /// <param name="expected">The original fixture.</param>
    private static void Verify(PairPacket? actual, PairPacket expected)
    {
        if (actual == null || !JsonNode.DeepEquals(JsonNode.Parse(LuminPackSerializer.SerializeJson(actual)),
                JsonNode.Parse(LuminPackSerializer.SerializeJson(expected))))
            throw new InvalidDataException("The decoded pair document differs from the original fixture.");
    }
}

/// <summary>Tests standalone pairs, dense and managed collections, and dictionary values.</summary>
[LuminPackable]
public partial class PairPacket
{
    public KeyValuePair<byte, int> Uneven { get; set; }
    public KeyValuePair<int, long> Wide { get; set; }
    public KeyValuePair<string?, int?> Text { get; set; }
    public KeyValuePair<int, int>[] DenseArray { get; set; } = [];
    public List<KeyValuePair<int, uint>> DenseList { get; set; } = [];
    public KeyValuePair<string?, int?>[] TextArray { get; set; } = [];
    public Dictionary<int, KeyValuePair<byte, int>> ByKey { get; set; } = [];
#if LUMINPACK_CPP_PAIR_PACKED
    public KeyValuePair<byte, int>[] PaddedArray { get; set; } = [];
    public List<KeyValuePair<int, long>> TaggedList { get; set; } = [];
#endif
}

/// <summary>Contains single-model padding in the default CLR collection layout.</summary>
[LuminPackable]
public partial class PaddedPairBlock
{
    public KeyValuePair<byte, int>[] Values { get; set; } = [];
}

/// <summary>Contains a multi-model CLR collection with a layout tag under the default options.</summary>
[LuminPackable]
public partial class TaggedPairBlock
{
    public List<KeyValuePair<int, long>> Values { get; set; } = [];
}
