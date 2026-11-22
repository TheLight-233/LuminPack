using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using LuminPack;
using LuminPackCppFixtures;

if (args is ["write", var output])
{
    Directory.CreateDirectory(output);
    NullableFixture.Write(output, "nullable", CppNullablePacket.Create());
    NullableFixture.Write(output, "nullable-version", CppNullableVersion.Create());
    return 0;
}
if (args is ["read", var input])
{
    NullableFixture.Verify(input, "nullable", CppNullablePacket.Create());
    NullableFixture.Verify(input, "nullable-version", CppNullableVersion.Create());
    Console.WriteLine(".NET verified both C++ nullable contracts.");
    return 0;
}
Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Nullable <read|write> <directory>");
return 2;

/// <summary>Exchanges nullable fixtures through the managed default serializer.</summary>
internal static class NullableFixture
{
    /// <summary>Writes both encodings of a managed fixture.</summary>
    /// <typeparam name="T">The annotated managed model.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="name">The fixture basename.</param>
    /// <param name="value">The original fixture value.</param>
    internal static void Write<T>(string directory, string name, T value)
    {
        string path = Path.Combine(directory, name);
        byte[] binary = LuminPackSerializer.Serialize(value);
#if LUMINPACK_CPP_TEST_NATIVE
        if (value is CppNullablePacket)
            CheckNativeArray(binary);
#endif
        File.WriteAllBytes(path + ".bin", binary);
        File.WriteAllBytes(path + ".json", Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson(value)));
    }

    /// <summary>Checks the values read from both C++ outputs against the original fixture.</summary>
    /// <typeparam name="T">The annotated managed model.</typeparam>
    /// <param name="directory">The C++ output directory.</param>
    /// <param name="name">The fixture basename.</param>
    /// <param name="value">The original fixture value.</param>
    internal static void Verify<T>(string directory, string name, T value)
    {
        string path = Path.Combine(directory, name);
        byte[] bytes = File.ReadAllBytes(path + ".bin");
#if LUMINPACK_CPP_TEST_NATIVE
        if (value is CppNullablePacket)
            CheckNativeArray(bytes);
#endif
        T binary = LuminPackSerializer.Deserialize<T>(bytes);
        T json = LuminPackSerializer.DeserializeJson<T>(File.ReadAllBytes(path + ".json"));
        JsonNode expected = Canonical(LuminPackSerializer.SerializeJson(value));
        if (!JsonNode.DeepEquals(expected, Canonical(LuminPackSerializer.SerializeJson(binary))) ||
            !JsonNode.DeepEquals(expected, Canonical(LuminPackSerializer.SerializeJson(json))))
            throw new InvalidDataException("The C++ nullable value differs from the fixture " + name + ".");
    }

#if LUMINPACK_CPP_TEST_NATIVE
    /// <summary>Checks the first Native array's complete slots against the actual CLR nullable width.</summary>
    /// <param name="document">The Binary fixture beginning with the wide nullable array.</param>
    private static void CheckNativeArray(ReadOnlySpan<byte> document)
    {
        int stride = Unsafe.SizeOf<long?>();
        int payloadOffset = stride switch
        {
            12 => 4,
            16 => 8,
            _ => throw new InvalidDataException("The CLR nullable stride is outside the fixture's storage models.")
        };
        ReadOnlySpan<byte> array = document[1..];
        if (BinaryPrimitives.ReadInt32LittleEndian(array) != 2)
            throw new InvalidDataException("The Native nullable array count differs from the fixture.");
        ReadOnlySpan<byte> slots = array.Slice(sizeof(int), 2 * stride);
        if (slots[0] != 1 || slots[stride] != 0 ||
            BinaryPrimitives.ReadInt64LittleEndian(slots.Slice(payloadOffset)) != 0x1122334455667788)
            throw new InvalidDataException("The nullable fixture differs from the CLR Native flag and payload layout.");
    }
#endif

    /// <summary>Normalizes the traversal order of the fixture's unordered dictionaries.</summary>
    /// <param name="json">The managed serialization of the fixture value.</param>
    /// <returns>The value used for semantic comparison.</returns>
    private static JsonNode Canonical(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidDataException("Expected a nullable fixture object.");
        string[] dictionaries = ["ValuesById", "WideById"];
        foreach (string name in dictionaries)
        {
            if (node[name] is JsonArray pairs)
                node[name] = new JsonArray(pairs.OrderBy(pair => pair![0]!.GetValue<int>())
                    .Select(pair => pair?.DeepClone()).ToArray());
        }
        return node;
    }
}
