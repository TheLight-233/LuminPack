extern alias Benchmark;

using System.Buffers;
using System.Text.Json.Nodes;
using Benchmark::LuminPackBenchmark;
using LuminPack.Option;
using LuminPackPortableFixtures;
using BenchmarkSerializer = Benchmark::LuminPack.LuminPackSerializer;

if (args is ["write-strings" or "read-strings", var stringsDirectory])
{
    if (args[0] == "write-strings")
        StringBoundaryFixtures.Write(stringsDirectory);
    else
        StringBoundaryFixtures.Verify(stringsDirectory);
    Console.WriteLine(args[0] == "write-strings" ? ".NET emitted string boundary documents." : ".NET verified string boundary documents.");
    return 0;
}

if (args is ["write-ordered-json" or "read-ordered-json", var orderedJsonDirectory])
{
    if (args[0] == "write-ordered-json")
        OrderedJsonFixtures.Write(orderedJsonDirectory);
    else
        OrderedJsonFixtures.Verify(orderedJsonDirectory);
    Console.WriteLine(args[0] == "write-ordered-json" ? ".NET emitted ordered JSON documents." : ".NET verified ordered JSON documents.");
    return 0;
}

if (args is ["write-json-numbers" or "read-json-numbers", var numberDirectory])
{
    if (args[0] == "write-json-numbers")
        JsonNumberFixtures.Write(numberDirectory);
    else
        JsonNumberFixtures.Verify(numberDirectory);
    Console.WriteLine(args[0] == "write-json-numbers" ? ".NET emitted JSON number documents and vectors." : ".NET verified JSON number documents.");
    return 0;
}

if (args is not ["read" or "write", var directory])
{
    Console.Error.WriteLine("Usage: LuminPack.Tests.Portable <read|write|read-strings|write-strings|read-ordered-json|write-ordered-json|read-json-numbers|write-json-numbers> <directory>");
    return 2;
}

var simple = SimpleClass.Create();
var save = CharacterSaveData.Create();
if (args[0] == "write")
{
    Directory.CreateDirectory(directory);
    BenchmarkFixtures.Write(Path.Combine(directory, "simple"), simple);
    BenchmarkFixtures.Write(Path.Combine(directory, "serialize"), save);
}
else
{
    BenchmarkFixtures.Verify(Path.Combine(directory, "simple"), simple, "Map1");
    BenchmarkFixtures.Verify(Path.Combine(directory, "serialize"), save, "SkillLevels");
}

Console.WriteLine(args[0] == "write" ? ".NET emitted both benchmark models." : ".NET verified both benchmark models.");
return 0;

/// <summary>Exchanges benchmark models across the three portable wire encodings.</summary>
internal static class BenchmarkFixtures
{
    private static readonly string[] Suffixes = [".bin", ".utf8.json", ".utf16.json"];

    /// <summary>Writes one original managed benchmark model in Binary and both JSON encodings.</summary>
    /// <typeparam name="T">The original benchmark model type.</typeparam>
    /// <param name="prefix">The output path before the format suffix.</param>
    /// <param name="original">The model produced by its benchmark factory.</param>
    internal static void Write<T>(string prefix, T original)
    {
        File.WriteAllBytes(prefix + Suffixes[0], BenchmarkSerializer.Serialize(original));
        var output = new ArrayBufferWriter<byte>();
        BenchmarkSerializer.SerializeJson<T, LuminPackUtf8LengthOptions>(original, output);
        File.WriteAllBytes(prefix + Suffixes[1], output.WrittenSpan.ToArray());
        output.Clear();
        BenchmarkSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(original, output);
        File.WriteAllBytes(prefix + Suffixes[2], output.WrittenSpan.ToArray());
    }

    /// <summary>Checks same-encoding and cross-encoding outputs with the managed decoders.</summary>
    /// <typeparam name="T">The original benchmark model type.</typeparam>
    /// <param name="prefix">The common output pathname before the fixture route and format.</param>
    /// <param name="original">The model produced by its benchmark factory.</param>
    /// <param name="pairs">The integer-key dictionary property whose order is unspecified.</param>
    internal static void Verify<T>(string prefix, T original, string pairs)
    {
        JsonNode expected = Canonical(original, pairs);
        for (int format = 0; format < Suffixes.Length; format++)
        {
            Check(prefix + Suffixes[format], format, expected, pairs);
            Check(prefix + ".from-" + ((format + Suffixes.Length - 1) % Suffixes.Length).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + Suffixes[format], format, expected, pairs);
        }

        void Check(string path, int format, JsonNode model, string unorderedPairs)
        {
            byte[] bytes = File.ReadAllBytes(path);
            T value = default!;
            switch (format)
            {
                case 0:
                    BenchmarkSerializer.Deserialize(bytes, ref value);
                    break;
                case 1:
                    BenchmarkSerializer.DeserializeJson<T, LuminPackUtf8LengthOptions>(bytes, ref value);
                    break;
                default:
                    BenchmarkSerializer.DeserializeJson<T, LuminPackUtf16LengthOptions>(bytes, ref value);
                    break;
            }

            if (!JsonNode.DeepEquals(model, Canonical(value, unorderedPairs)))
                throw new InvalidDataException("Portable output changed the benchmark model: " + path);
        }
    }

    /// <summary>Sorts the one integer-key pair list without changing array or list order elsewhere.</summary>
    /// <typeparam name="T">The original benchmark model type.</typeparam>
    /// <param name="value">The model to compare.</param>
    /// <param name="pairs">The dictionary property to order by key.</param>
    /// <returns>The managed semantic comparison node.</returns>
    private static JsonNode Canonical<T>(T value, string pairs)
    {
        JsonNode node = JsonNode.Parse(BenchmarkSerializer.SerializeJson(value))
            ?? throw new InvalidDataException("Missing benchmark object.");
        if (node[pairs] is JsonArray unordered)
            node[pairs] = new JsonArray(unordered.OrderBy(item => item![0]!.GetValue<int>())
                .Select(item => item?.DeepClone()).ToArray());
        return node;
    }
}
