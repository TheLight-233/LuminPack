using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackCppRectangularFixtures;

/// <summary>Exchanges multidimensional binary and JSON documents with generated C++ codecs.</summary>
internal static class Program
{
    /// <summary>Writes managed fixtures or verifies the native round-trip outputs.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero after a successful exchange or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["write" or "read", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Rectangular <write|read> <directory>");
            return 2;
        }

        var scalar = new ScalarPacket
        {
            Grid = new[,] { { 17, -29, 31 }, { 43, 61, -71 } },
            Text = new string?[,] { { null, "café" }, { "Ω", "" } },
            Empty = new int[2, 0, 1],
            Nested = [new[,] { { (short)7 }, { (short)-9 } }, null],
        };
        var modeled = new ModeledPacket
        {
            Numbers = new long?[1, 2, 2] { { { 17, null }, { 0x1122334455667788L, -42 } } },
            Points = new[,] { { new Point { Code = 71, Size = -9, Active = true }, new Point { Code = 81, Size = 27 } } },
        };
        var bounded = (int[,])Array.CreateInstance(typeof(int), [2, 2], [-2, 3]);
        bounded[-2, 3] = 17;
        bounded[-2, 4] = -29;
        bounded[-1, 3] = 31;
        bounded[-1, 4] = 43;
        bool write = args[0] == "write";
        if (write)
            Directory.CreateDirectory(directory);
        Exchange(directory, write, "scalar-array", scalar);
        Exchange(directory, write, "modeled-array", modeled);
        Exchange(directory, write, "bounds-array", new BoundsPacket { Grid = bounded }, jsonOnly: true);
        Console.WriteLine(write ? ".NET wrote the rectangular array fixtures." : ".NET verified all C++ rectangular arrays.");
        return 0;
    }

    /// <summary>Exchanges a fixture using both JSON encodings and Binary when its lower bounds are zero.</summary>
    /// <typeparam name="T">The managed payload type.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="write">Whether to produce the managed files.</param>
    /// <param name="name">The common fixture basename.</param>
    /// <param name="expected">The source managed object.</param>
    /// <param name="jsonOnly">Whether the source uses nonzero array lower bounds.</param>
    private static void Exchange<T>(string directory, bool write, string name, T expected, bool jsonOnly = false)
    {
        string path = Path.Combine(directory, name);
        if (!jsonOnly)
        {
            if (write)
                File.WriteAllBytes(path + ".bin", LuminPackSerializer.Serialize(expected));
            else
                RequireEqual(LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(path + ".bin")), expected, name);
        }

        if (write)
        {
            var output = new ArrayBufferWriter<byte>();
            LuminPackSerializer.SerializeJson<T, LuminPackUtf8LengthOptions>(expected, output);
            File.WriteAllBytes(path + ".utf8.json", output.WrittenSpan.ToArray());
            output.Clear();
            LuminPackSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(expected, output);
            File.WriteAllBytes(path + ".utf16.json", output.WrittenSpan.ToArray());
        }
        else
        {
            T fromUtf8 = default!;
            T fromUtf16 = default!;
            LuminPackSerializer.DeserializeJson<T, LuminPackUtf8LengthOptions>(File.ReadAllBytes(path + ".utf8.json"), ref fromUtf8);
            LuminPackSerializer.DeserializeJson<T, LuminPackUtf16LengthOptions>(File.ReadAllBytes(path + ".utf16.json"), ref fromUtf16);
            RequireEqual(fromUtf8, expected, name);
            RequireEqual(fromUtf16, expected, name);
        }
    }

    /// <summary>Checks the managed shape, lower bounds and row-major values after native round-trip.</summary>
    /// <typeparam name="T">The compared payload type.</typeparam>
    /// <param name="actual">The decoded native output.</param>
    /// <param name="expected">The original managed object.</param>
    /// <param name="name">The fixture name for an error message.</param>
    private static void RequireEqual<T>(T actual, T expected, string name)
    {
        if (LuminPackSerializer.SerializeJson(actual) != LuminPackSerializer.SerializeJson(expected))
            throw new InvalidDataException("C++ changed the rectangular fixture " + name + ".");
    }
}

/// <summary>Raw, general element and nested rectangular array examples.</summary>
[LuminPackable]
public sealed class ScalarPacket
{
    public int[,]? Grid;
    public string?[,]? Text;
    public int[,,]? Empty;
    public List<short[,]?>? Nested;
    public int[,]? Missing;
}

/// <summary>A tagged aggregate embedded in a rectangular collection.</summary>
[LuminPackable]
public struct Point
{
    public int Code;
    public long Size;
    public bool Active;
}

/// <summary>Rectangular nullable and layout-aware element blocks.</summary>
[LuminPackable]
public sealed class ModeledPacket
{
    public long?[,,]? Numbers;
    public Point[,]? Points;
}

/// <summary>A JSON array with nonzero bounds in each dimension.</summary>
[LuminPackable]
public sealed class BoundsPacket
{
    public int[,]? Grid;
}
