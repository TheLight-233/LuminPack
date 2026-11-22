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
        File.WriteAllBytes(path + ".bin", LuminPackSerializer.Serialize(value));
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
        T binary = LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(path + ".bin"));
        T json = LuminPackSerializer.DeserializeJson<T>(File.ReadAllBytes(path + ".json"));
        JsonNode expected = Canonical(LuminPackSerializer.SerializeJson(value));
        if (!JsonNode.DeepEquals(expected, Canonical(LuminPackSerializer.SerializeJson(binary))) ||
            !JsonNode.DeepEquals(expected, Canonical(LuminPackSerializer.SerializeJson(json))))
            throw new InvalidDataException("The C++ nullable value differs from the fixture " + name + ".");
    }

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
