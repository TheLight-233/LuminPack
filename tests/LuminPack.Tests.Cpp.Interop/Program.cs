using System.Text;
using System.Text.Json.Nodes;
using LuminPack;
using LuminPack.Option;
using LuminPackBenchmark;

if (args is not ["read" or "write", var directory])
{
    Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Interop <read|write> <directory>");
    return 2;
}

bool writing = args[0] == "write";
if (writing)
    Directory.CreateDirectory(directory);
BenchmarkFixture.Exchange(directory, writing, "simple", SimpleClass.Create(), "Map1");
BenchmarkFixture.Exchange(directory, writing, "serialize", CharacterSaveData.Create(), "SkillLevels");
Console.WriteLine(writing ? ".NET wrote both benchmark models." : ".NET verified both C++ benchmark models.");
return 0;

/// <summary>Exercises the original managed benchmark factories against generated C++ codecs.</summary>
internal static class BenchmarkFixture
{
    /// <summary>Writes an original .NET document or checks a C++ document against the factory model.</summary>
    /// <typeparam name="T">The original benchmark model.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="writing">Whether to emit fixture inputs.</param>
    /// <param name="name">The fixture basename.</param>
    /// <param name="model">The value returned by the benchmark's own factory.</param>
    /// <param name="pairs">The unordered non-string dictionary property to compare by key.</param>
    internal static void Exchange<T>(string directory, bool writing, string name, T model, string pairs)
    {
        string path = Path.Combine(directory, name);
        string original = LuminPackSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(model);
        if (writing)
        {
            File.WriteAllBytes(path + ".bin", LuminPackSerializer.Serialize<T, LuminPackUtf16LengthOptions>(model));
            File.WriteAllBytes(path + ".json", Encoding.Unicode.GetBytes(original));
            File.WriteAllBytes(path + ".utf8.json", Encoding.UTF8.GetBytes(original));
            return;
        }

        T binary = LuminPackSerializer.Deserialize<T, LuminPackUtf16LengthOptions>(File.ReadAllBytes(path + ".bin"));
        T json = default!;
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf16LengthOptions>(File.ReadAllBytes(path + ".json"), ref json);
        JsonNode expected = Canonical(original, pairs);
        if (!JsonNode.DeepEquals(expected, Canonical(LuminPackSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(binary), pairs)))
            throw new InvalidDataException("The C++ binary differs from the benchmark model " + name + ".");
        if (!JsonNode.DeepEquals(expected, Canonical(LuminPackSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(json), pairs)))
            throw new InvalidDataException("The C++ JSON differs from the benchmark model " + name + ".");
    }

    /// <summary>Orders the benchmark's integer-key dictionary pairs for semantic comparison across native maps.</summary>
    /// <param name="json">The complete managed document.</param>
    /// <param name="pairs">The integer-key dictionary property.</param>
    /// <returns>The test comparison value.</returns>
    private static JsonNode Canonical(string json, string pairs)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidDataException("Missing benchmark object.");
        if (node[pairs] is JsonArray unordered)
        {
            JsonNode?[] sorted = unordered.OrderBy(item => item![0]!.GetValue<int>())
                .Select(item => item?.DeepClone()).ToArray();
            node[pairs] = new JsonArray(sorted);
        }
        return node;
    }
}
