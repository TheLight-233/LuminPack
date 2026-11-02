using System.Buffers.Binary;
using System.Text;
using LuminPack.Fuzzing;

namespace Fuzzing;

/// <summary>Creates deterministic valid and malformed seeds for every encoding.</summary>
internal static class Corpus
{
    /// <summary>Builds representative graphs, including vector tails and non-ASCII strings.</summary>
    /// <returns>The named positive seed graphs.</returns>
    internal static IEnumerable<(string Name, FuzzRecord? Value)> Values()
    {
        yield return ("null", null);
        yield return ("empty", new FuzzRecord());
        yield return ("null-members", new FuzzRecord { Text = null, Numbers = null, Bits = null, Flags = null, Items = null, Map = null });
        yield return ("nested", new FuzzRecord
        {
            Number = int.MinValue, Flag = true, Stamp = long.MaxValue, Optional = 42,
            Text = "quotes\" slash\\ line\n \u00e9 \u4e2d \ud83d\ude80",
            Numbers = [int.MinValue, -1, 0, 1, int.MaxValue], Bits = [true, false, true], Flags = [false, true],
            Items = [new FuzzChild { Number = 3, Flag = true, Text = "child" }],
            Map = new() { [-1] = null, [0] = "", [42] = "value" },
            Child = new FuzzChild { Text = "first", Next = new FuzzChild { Number = 7, Text = "last" } }
        });
        foreach (int length in new[] { 15, 16, 17, 31, 32, 33, 63, 64, 65, 128 })
            yield return ($"boundary-{length}", new FuzzRecord
            {
                Text = new string('\u00e9', length), Numbers = Enumerable.Range(0, length).ToArray(),
                Bits = Enumerable.Range(0, length).Select(index => index % 3 == 0).ToArray(),
                Flags = Enumerable.Range(0, length).Select(index => index % 2 == 0).ToList()
            });
    }

    /// <summary>Writes target-specific seed files and verifies every positive seed through the real target.</summary>
    /// <param name="root">The corpus root directory.</param>
    internal static void Write(string root)
    {
        foreach (var target in FuzzTarget.All)
        {
            string directory = Path.Combine(root, target.Name);
            Directory.CreateDirectory(directory);
            foreach (var (name, value) in Values())
            {
                byte[] bytes = target.Seed(value);
                File.WriteAllBytes(Path.Combine(directory, name + ".bin"), bytes);
            }
            byte[] invalid = target.Json
                ? (target.Name == "json-utf16" ? Encoding.Unicode : Encoding.UTF8).GetBytes("{\"Bits\":[true,],\"Text\":\"\\uZZZZ\"}")
                : [250, 255, 255, 255, 127];
            FuzzTarget.Require(!target.Run(invalid), $"Malformed seed was accepted by {target.Name}.");
            File.WriteAllBytes(Path.Combine(directory, "invalid.bin"), invalid);
            if (target.Json)
            {
                byte[] wrongType = (target.Name == "json-utf16" ? Encoding.Unicode : Encoding.UTF8).GetBytes("{\"Text\":17}");
                FuzzTarget.Require(!target.Run(wrongType), $"A wrong-typed string was accepted by {target.Name}.");
                File.WriteAllBytes(Path.Combine(directory, "wrong-type.bin"), wrongType);
            }
        }
    }

    /// <summary>Produces bounded mutations of a seed without changing the stored corpus.</summary>
    /// <param name="source">The original seed bytes.</param>
    /// <param name="random">The deterministic random source.</param>
    /// <returns>A truncated, extended, overwritten or bit-flipped input.</returns>
    internal static byte[] Mutate(byte[] source, Random random)
    {
        byte[] bytes = (byte[])source.Clone();
        switch (random.Next(6))
        {
            case 0:
                Array.Resize(ref bytes, random.Next(bytes.Length + 1));
                break;
            case 1:
                int oldLength = bytes.Length;
                Array.Resize(ref bytes, Math.Min(FuzzTarget.MaxInputBytes, oldLength + random.Next(1, 17)));
                random.NextBytes(bytes.AsSpan(oldLength));
                break;
            case 2 when bytes.Length >= 4:
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(random.Next(bytes.Length - 3)),
                    new[] { -1, int.MinValue, int.MaxValue, 0, 128, 129, 513, 16385 }[random.Next(8)]);
                break;
            case 3:
                bytes = new byte[random.Next(257)];
                random.NextBytes(bytes);
                break;
            default:
                if (bytes.Length != 0)
                    for (int i = 0, count = random.Next(1, 5); i < count; i++)
                        bytes[random.Next(bytes.Length)] ^= (byte)(1 << random.Next(8));
                break;
        }
        return bytes;
    }
}
