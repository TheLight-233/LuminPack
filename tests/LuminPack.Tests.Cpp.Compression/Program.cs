using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackCppCompressionFixtures;

/// <summary>Exchanges managed compressed fields and their unchanged JSON forms.</summary>
internal static class Program
{
    /// <summary>Writes or reads the fixture documents.</summary>
    /// <param name="args">The operation and target directory.</param>
    /// <returns>Zero after exchange or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["write" or "read", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Compression <write|read> <directory>");
            return 2;
        }

        bool write = args[0] == "write";
        if (write)
            Directory.CreateDirectory(directory);
        var payload = new CompressedPacket
        {
            Bytes = Enumerable.Range(0, 384).Select(i => (byte)(i % 13)).ToArray(),
            Numbers = Enumerable.Range(0, 127).Select(i => i % 5 - 2).ToList(),
            Bits = [true, false, false, true, true, false],
            Empty = [],
            Tail = -127
        };
        var version = new VersionCompressedPacket { Values = [17, 0, -3, 17, -3, 0], Tail = 89 };
        var shapes = new CompressedShapePacket
        {
            Codes = [SampleCode.First, SampleCode.Second, (SampleCode)0xabcd],
            Raw = [new() { Low = -13, High = 17 }, new() { Low = 19, High = -23 }],
            NativeIntegers = [-1, nint.MaxValue, 113],
            Jagged = [[1, 2, 3], [], null],
            JaggedList = [[[17, -19], null], []]
        };
        Exchange(directory, write, "compressed-record", payload, static bytes => LuminPackSerializer.Deserialize<CompressedPacket>(bytes));
        Exchange(directory, write, "compressed-version", version, static bytes => LuminPackSerializer.Deserialize<VersionCompressedPacket>(bytes));
        Exchange(directory, write, "compressed-shapes", shapes, static bytes => LuminPackSerializer.Deserialize<CompressedShapePacket>(bytes));
        if (!write)
            VerifyListReuse(directory, payload);
        Console.WriteLine(write ? ".NET wrote the compressed field fixtures." : ".NET verified the C++ compressed fields.");
        return 0;
    }

    /// <summary>Exchanges Binary and both JSON encodings for one record.</summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="write">Whether to prepare the managed fixtures.</param>
    /// <param name="name">The shared input and output basename.</param>
    /// <param name="expected">The source record.</param>
    /// <param name="readBinary">The closed generated Binary operation for the fixture.</param>
    private static void Exchange<T>(string directory, bool write, string name, T expected, Func<byte[], T> readBinary)
    {
        string path = Path.Combine(directory, name);
        if (write)
        {
            File.WriteAllBytes(path + ".bin", LuminPackSerializer.Serialize(expected));
            var output = new ArrayBufferWriter<byte>();
            LuminPackSerializer.SerializeJson<T, LuminPackUtf8LengthOptions>(expected, output);
            File.WriteAllBytes(path + ".utf8.json", output.WrittenSpan.ToArray());
            output.Clear();
            LuminPackSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(expected, output);
            File.WriteAllBytes(path + ".utf16.json", output.WrittenSpan.ToArray());
            return;
        }

        T fromBinary = readBinary(File.ReadAllBytes(path + ".bin"));
        T fromUtf8 = default!;
        T fromUtf16 = default!;
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf8LengthOptions>(File.ReadAllBytes(path + ".utf8.json"), ref fromUtf8);
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf16LengthOptions>(File.ReadAllBytes(path + ".utf16.json"), ref fromUtf16);
        string json = LuminPackSerializer.SerializeJson(expected);
        if (LuminPackSerializer.SerializeJson(fromBinary) != json || LuminPackSerializer.SerializeJson(fromUtf8) != json ||
            LuminPackSerializer.SerializeJson(fromUtf16) != json)
            throw new InvalidDataException("C++ changed the compressed record " + name + ".");
    }

    /// <summary>Decodes a valid compressed field whose element count exceeds its input bytes.</summary>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="expected">The original Default record.</param>
    private static void VerifyListReuse(string directory, CompressedPacket expected)
    {
        var numbers = new List<int> { 1, 2, 3 };
        CompressedPacket? value = new() { Numbers = numbers };
        byte[] source = File.ReadAllBytes(Path.Combine(directory, "compressed-record.bin"));
        LuminPackSerializer.Deserialize(source, ref value);
        if (!ReferenceEquals(numbers, value?.Numbers) || !numbers.SequenceEqual(expected.Numbers!))
            throw new InvalidDataException("The managed compressed read did not reuse the existing list.");
    }
}

/// <summary>Default record with filled, empty and absent compressed slots.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxCollectionLength = 512)]
public sealed class CompressedPacket
{
    [LuminPackCompress] public byte[]? Bytes;
    [LuminPackCompress] public List<int>? Numbers;
    [LuminPackCompress] public bool[]? Bits;
    [LuminPackCompress] public short[]? Empty;
    [LuminPackCompress] public uint[]? Missing;
    public int Tail;
}

/// <summary>A Version record containing the managed compressed-field descriptor.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class VersionCompressedPacket
{
    [LuminPackCompress] public int[]? Values;
    public int Tail;
}

/// <summary>Raw enum, unmanaged, native integer and forwarded vector compression.</summary>
[LuminPackable]
public sealed class CompressedShapePacket
{
    [LuminPackCompress] public SampleCode[]? Codes;
    [LuminPackCompress] public List<NativeSample>? Raw;
    [LuminPackCompress] public nint[]? NativeIntegers;
    [LuminPackableObject, LuminPackCompress] public int[]?[]? Jagged;
    [LuminPackableObject, LuminPackCompress] public List<int[]?[]>? JaggedList;
}

/// <summary>A sample with a proven headerless CLR representation.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
public struct NativeSample
{
    public int Low;
    public int High;
}

/// <summary>The numeric values of the compressed enum.</summary>
public enum SampleCode : ushort { First = 7, Second = 257 }
