using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackCppClosedGenericFixtures;

/// <summary>Exchanges closed generic payloads reached only through managed serializer calls.</summary>
internal static class Program
{
    /// <summary>Writes the managed fixtures or verifies the C++ documents.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero for success, or two when the arguments are invalid.</returns>
    private static int Main(string[] args)
    {
        var direct = new Box<string>
        {
            Value = "caf\u00e9 \u03a9",
            Items = [null, "\u503c"],
            ByKey = new() { [71] = "item" }
        };
        var nested = new List<Cell<int>?>
        {
            new() { Value = -71, Index = 13 },
            null,
            new() { Value = 97, Index = -31 }
        };
        if (args is ["write", var output])
        {
            Directory.CreateDirectory(output);
            var utf8 = new ArrayBufferWriter<byte>();
            LuminPackSerializer.SerializeJson<Box<string>, LuminPackUtf8LengthOptions>(direct, utf8);
            Store(output, "direct", LuminPackSerializer.Serialize(direct), LuminPackSerializer.SerializeJson(direct), utf8.WrittenSpan);
            utf8.Clear();
            LuminPackSerializer.SerializeJson<List<Cell<int>?>, LuminPackUtf8LengthOptions>(nested, utf8);
            Store(output, "nested", LuminPackSerializer.Serialize(nested), LuminPackSerializer.SerializeJson(nested), utf8.WrittenSpan);
            return 0;
        }

        if (args is ["read", var input])
        {
            var directBinary = LuminPackSerializer.Deserialize<Box<string>>(File.ReadAllBytes(Path.Combine(input, "direct.bin")));
            var directJson = LuminPackSerializer.DeserializeJson<Box<string>>(File.ReadAllBytes(Path.Combine(input, "direct.json")));
            Box<string>? directUtf8 = null;
            LuminPackSerializer.DeserializeJson<Box<string>?, LuminPackUtf8LengthOptions>(
                File.ReadAllBytes(Path.Combine(input, "direct.utf8.json")), ref directUtf8);
            Verify(LuminPackSerializer.SerializeJson(direct), LuminPackSerializer.SerializeJson(directBinary),
                LuminPackSerializer.SerializeJson(directJson), LuminPackSerializer.SerializeJson(directUtf8));

            var nestedBinary = LuminPackSerializer.Deserialize<List<Cell<int>?>>(File.ReadAllBytes(Path.Combine(input, "nested.bin")));
            var nestedJson = LuminPackSerializer.DeserializeJson<List<Cell<int>?>>(File.ReadAllBytes(Path.Combine(input, "nested.json")));
            List<Cell<int>?>? nestedUtf8 = null;
            LuminPackSerializer.DeserializeJson<List<Cell<int>?>?, LuminPackUtf8LengthOptions>(
                File.ReadAllBytes(Path.Combine(input, "nested.utf8.json")), ref nestedUtf8);
            Verify(LuminPackSerializer.SerializeJson(nested), LuminPackSerializer.SerializeJson(nestedBinary),
                LuminPackSerializer.SerializeJson(nestedJson), LuminPackSerializer.SerializeJson(nestedUtf8));
            Console.WriteLine(".NET verified the C++ closed generic Binary and JSON documents.");
            return 0;
        }

        Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.ClosedGeneric <read|write> <directory>");
        return 2;
    }

    /// <summary>Stores the binary and both JSON encodings of one fixture.</summary>
    /// <param name="directory">The destination directory.</param>
    /// <param name="name">The common basename.</param>
    /// <param name="binary">The managed Binary document.</param>
    /// <param name="json">The managed JSON document.</param>
    /// <param name="utf8">The managed UTF-8 JSON document.</param>
    private static void Store(string directory, string name, byte[] binary, string json, ReadOnlySpan<byte> utf8)
    {
        File.WriteAllBytes(Path.Combine(directory, name + ".bin"), binary);
        File.WriteAllBytes(Path.Combine(directory, name + ".json"), Encoding.Unicode.GetBytes(json));
        File.WriteAllBytes(Path.Combine(directory, name + ".utf8.json"), utf8);
    }

    /// <summary>Checks the decoded members against the original fixture.</summary>
    /// <param name="expected">The original JSON representation.</param>
    /// <param name="binary">The representation decoded from the C++ Binary document.</param>
    /// <param name="json">The representation decoded from the C++ UTF-16 JSON document.</param>
    /// <param name="utf8">The representation decoded from the C++ UTF-8 JSON document.</param>
    private static void Verify(string expected, string binary, string json, string utf8)
    {
        if (binary != expected || json != expected || utf8 != expected)
            throw new InvalidDataException("The decoded closed generic document differs from the original fixture.");
    }
}

/// <summary>Uses a type parameter directly and in standard collection slots.</summary>
[LuminPackable]
public sealed partial class Box<T>
{
    public T? Value;
    public List<T?>? Items;
    public Dictionary<int, T?>? ByKey;
}

/// <summary>Uses a closed Version descriptor through a managed list root.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed partial class Cell<T>
{
    public T? Value;
    public int Index;
}
