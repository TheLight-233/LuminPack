using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackPortableFixtures;

/// <summary>Produces an exact managed JSON oracle across IEEE 754 exponents and integer boundaries.</summary>
internal static class JsonNumberFixtures
{
    private static readonly (string Name, uint F32, ulong F64)[] Nonfinite =
    [
        ("f32-nan", 0x7fc00000, 0), ("f32-positive-infinity", 0x7f800000, 0), ("f32-negative-infinity", 0xff800000, 0),
        ("f64-nan", 0, 0x7ff8000000000000), ("f64-positive-infinity", 0, 0x7ff0000000000000), ("f64-negative-infinity", 0, 0xfff0000000000000),
    ];

    /// <summary>Writes typed Binary and canonical JSON fixtures plus the ASCII floating-point oracle.</summary>
    /// <param name="directory">The fixture output directory.</param>
    internal static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var document = Create();
        File.WriteAllBytes(Path.Combine(directory, "numbers.bin"), LuminPackSerializer.Serialize(document));
        byte[] utf8 = SerializeJson<JsonNumberDocument, LuminPackUtf8LengthOptions>(document);
        byte[] utf16 = SerializeJson<JsonNumberDocument, LuminPackUtf16LengthOptions>(document);
        if (Encoding.UTF8.GetString(utf8) != Encoding.Unicode.GetString(utf16))
            throw new InvalidDataException("The managed encodings produced different JSON number text.");
        File.WriteAllBytes(Path.Combine(directory, "numbers.utf8.json"), utf8);
        File.WriteAllBytes(Path.Combine(directory, "numbers.utf16.json"), utf16);
        WriteOracle(directory, utf8, document);

        foreach (var (name, f32, f64) in Nonfinite)
        {
            var invalid = new JsonNumberRejectDocument
            {
                Float32 = BitConverter.UInt32BitsToSingle(f32),
                Float64 = BitConverter.UInt64BitsToDouble(f64),
            };
            RequireNonfiniteRejection<LuminPackUtf8LengthOptions>(invalid);
            RequireNonfiniteRejection<LuminPackUtf16LengthOptions>(invalid);
            File.WriteAllBytes(Path.Combine(directory, name + ".bin"), LuminPackSerializer.Serialize(invalid));
        }
    }

    /// <summary>Verifies the native outputs through the typed managed readers, including signed zero.</summary>
    /// <param name="directory">The native output directory.</param>
    internal static void Verify(string directory)
    {
        byte[] expected = LuminPackSerializer.Serialize(Create());
        foreach (string source in new[] { "utf8", "utf16" })
        {
            VerifyJson<LuminPackUtf8LengthOptions>(directory, $"numbers.from-{source}.utf8.json", expected);
            VerifyJson<LuminPackUtf16LengthOptions>(directory, $"numbers.from-{source}.utf16.json", expected);
        }
        VerifyJson<LuminPackUtf8LengthOptions>(directory, "numbers.from-binary.utf8.json", expected);
        VerifyJson<LuminPackUtf16LengthOptions>(directory, "numbers.from-binary.utf16.json", expected);
        foreach (var (name, f32, f64) in Nonfinite)
        {
            var document = LuminPackSerializer.Deserialize<JsonNumberRejectDocument>(File.ReadAllBytes(Path.Combine(directory, name + ".bin")));
            if (document == null || BitConverter.SingleToUInt32Bits(document.Float32) != f32 || BitConverter.DoubleToUInt64Bits(document.Float64) != f64)
                throw new InvalidDataException("The native Binary writer changed a nonfinite value: " + name);
        }
    }

    /// <summary>Builds the numeric fixture in a stable order.</summary>
    /// <returns>A document containing floating-point patterns and integer boundaries.</returns>
    private static JsonNumberDocument Create() => new()
    {
        FloatField = BitConverter.UInt32BitsToSingle(0x80000000),
        DoubleField = BitConverter.UInt64BitsToDouble(0x0010000000000000),
        SignedField = long.MinValue,
        UnsignedField = ulong.MaxValue,
        Float32 = FloatingPatterns(8, 23, -45, 38).Select(bits => BitConverter.UInt32BitsToSingle((uint)bits)).ToArray(),
        Float64 = FloatingPatterns(11, 52, -324, 308).Select(BitConverter.UInt64BitsToDouble).ToArray(),
        Int8 = [sbyte.MinValue, sbyte.MinValue + 1, -100, -99, -10, -9, -1, 0, 1, 9, 10, 99, 100, sbyte.MaxValue - 1, sbyte.MaxValue],
        UInt8 = [0, 1, 9, 10, 99, 100, byte.MaxValue - 1, byte.MaxValue],
        Int16 = [short.MinValue, short.MinValue + 1, -10000, -9999, -1000, -999, -1, 0, 1, 999, 1000, 9999, 10000, short.MaxValue - 1, short.MaxValue],
        UInt16 = [0, 1, 255, 256, 9999, 10000, (ushort)short.MaxValue, short.MaxValue + 1, ushort.MaxValue - 1, ushort.MaxValue],
        Int32 = [int.MinValue, int.MinValue + 1, -1000000000, -999999999, -10000, -1, 0, 1, 10000, 999999999, 1000000000, int.MaxValue - 1, int.MaxValue],
        UInt32 = [0, 1, 999999999, 1000000000, int.MaxValue, (uint)int.MaxValue + 1, uint.MaxValue - 1, uint.MaxValue],
        Int64 = [long.MinValue, long.MinValue + 1, -9007199254740993, -9007199254740992, -9007199254740991,
            -999999999999999, -1, 0, 1, 999999999999999, 9007199254740991, 9007199254740992, 9007199254740993,
            999999999999999999, 1000000000000000000, long.MaxValue - 1, long.MaxValue],
        UInt64 = [0, 1, 4294967295, 4294967296, 9007199254740991, 9007199254740992, 9007199254740993,
            long.MaxValue, (ulong)long.MaxValue + 1, 9999999999999999999, 10000000000000000000, ulong.MaxValue - 1, ulong.MaxValue],
    };

    /// <summary>Builds a deterministic corpus across all finite exponents, adjacent decimal powers, and seeded raw bits.</summary>
    /// <param name="exponentWidth">The IEEE 754 exponent width.</param>
    /// <param name="significandWidth">The encoded significand width.</param>
    /// <param name="minimumDecimalPower">The first decimal power to parse at the selected width.</param>
    /// <param name="maximumDecimalPower">The final decimal power to parse at the selected width.</param>
    /// <returns>The deduplicated finite bit patterns.</returns>
    private static ulong[] FloatingPatterns(int exponentWidth, int significandWidth, int minimumDecimalPower, int maximumDecimalPower)
    {
        ulong exponentMask = (1UL << exponentWidth) - 1;
        ulong significandMask = (1UL << significandWidth) - 1;
        ulong sign = 1UL << (exponentWidth + significandWidth);
        ulong widthMask = sign | (sign - 1);
        ulong[] fractions = [0, 1, 2, 3, significandMask / 3, significandMask / 2, significandMask / 2 + 1,
            significandMask * 2 / 3, significandMask - 1, significandMask];
        var result = new SortedSet<ulong>();
        for (ulong exponent = 0; exponent < exponentMask; exponent++)
            foreach (ulong fraction in fractions)
            {
                ulong bits = (exponent << significandWidth) | fraction;
                result.Add(bits);
                result.Add(bits | sign);
            }

        for (int exponent = minimumDecimalPower; exponent <= maximumDecimalPower; exponent++)
        {
            string token = "1e" + exponent.ToString(CultureInfo.InvariantCulture);
            IncludeNeighbors(result, DecimalBits(token, significandWidth), sign, significandWidth, exponentMask);
        }

        string[] sensitiveTokens = significandWidth == 23
            ? ["0.1", "0.3", "0.33333334", "0.000123", "123456.75", "16777215", "16777218", "3.1415927", "2.3283064e-10"]
            : ["0.1", "0.30000000000000004", "1.83749023360500067", "9007199254740991", "9007199254740994",
                "4.1045368012983762e-289", "2.2250738585072011e-308", "0.9999999999999999", "1.0000000000000002"];
        foreach (string token in sensitiveTokens)
            IncludeNeighbors(result, DecimalBits(token, significandWidth), sign, significandWidth, exponentMask);

        ulong state = 0x1ceb00da61f5a43d;
        for (int index = 0; index < 16_384; index++)
        {
            state = unchecked(state + 0x9e3779b97f4a7c15UL);
            ulong bits = unchecked((state ^ (state >> 30)) * 0xbf58476d1ce4e5b9UL);
            bits = unchecked((bits ^ (bits >> 27)) * 0x94d049bb133111ebUL);
            bits = (bits ^ (bits >> 31)) & widthMask;
            if (((bits >> significandWidth) & exponentMask) != exponentMask)
                result.Add(bits);
        }

        return result.ToArray();
    }

    /// <summary>Converts an invariant decimal into the corresponding raw bits at the requested width.</summary>
    /// <param name="token">The decimal boundary or regression input.</param>
    /// <param name="significandWidth">The encoded significand width.</param>
    /// <returns>The raw IEEE 754 bits.</returns>
    private static ulong DecimalBits(string token, int significandWidth) => significandWidth == 23
        ? BitConverter.SingleToUInt32Bits(float.Parse(token, CultureInfo.InvariantCulture))
        : BitConverter.DoubleToUInt64Bits(double.Parse(token, CultureInfo.InvariantCulture));

    /// <summary>Includes a decimal boundary and its adjacent finite values with each sign.</summary>
    /// <param name="result">The output patterns.</param>
    /// <param name="bits">The nonnegative boundary.</param>
    /// <param name="sign">The sign-bit mask.</param>
    /// <param name="significandWidth">The encoded significand width.</param>
    /// <param name="exponentMask">The unshifted exponent mask.</param>
    private static void IncludeNeighbors(SortedSet<ulong> result, ulong bits, ulong sign, int significandWidth, ulong exponentMask)
    {
        ulong first = bits == 0 ? 0 : bits - 1;
        for (ulong neighbor = first; neighbor <= bits + 1; neighbor++)
            if (((neighbor >> significandWidth) & exponentMask) != exponentMask)
            {
                result.Add(neighbor);
                result.Add(neighbor | sign);
            }
    }

    /// <summary>Extracts the writer's exact numeric token text for independent runtime tests.</summary>
    /// <param name="directory">The fixture output directory.</param>
    /// <param name="utf8">The unmodified managed JSON output.</param>
    /// <param name="document">The values whose raw IEEE 754 bits identify the tokens.</param>
    private static void WriteOracle(string directory, byte[] utf8, JsonNumberDocument document)
    {
        using var json = JsonDocument.Parse(utf8);
        using var output = new StreamWriter(Path.Combine(directory, "number-vectors.tsv"), append: false, new UTF8Encoding(false)) { NewLine = "\n" };
        output.WriteLine("type\tbits\tjson");
        int index = 0;
        foreach (JsonElement token in json.RootElement.GetProperty(nameof(JsonNumberDocument.Float32)).EnumerateArray())
        {
            uint bits = BitConverter.SingleToUInt32Bits(document.Float32[index++]);
            output.WriteLine($"f32\t{bits:x8}\t{token.GetRawText()}");
        }

        index = 0;
        foreach (JsonElement token in json.RootElement.GetProperty(nameof(JsonNumberDocument.Float64)).EnumerateArray())
        {
            ulong bits = BitConverter.DoubleToUInt64Bits(document.Float64[index++]);
            output.WriteLine($"f64\t{bits:x16}\t{token.GetRawText()}");
        }
    }

    /// <summary>Uses the managed serializer for the selected JSON encoding.</summary>
    /// <typeparam name="T">The managed fixture model.</typeparam>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="value">The value to encode.</param>
    /// <returns>The encoded JSON document.</returns>
    private static byte[] SerializeJson<T, TOptions>(T value) where TOptions : struct, ILuminPackOptions
    {
        var buffer = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson<T, TOptions>(value, buffer);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Checks that the managed writer rejects nonfinite inputs for the chosen encoding.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="document">The document containing a nonfinite number.</param>
    private static void RequireNonfiniteRejection<TOptions>(JsonNumberRejectDocument document) where TOptions : struct, ILuminPackOptions
    {
        try
        {
            SerializeJson<JsonNumberRejectDocument, TOptions>(document);
        }
        catch (FormatException)
        {
            return;
        }

        throw new InvalidDataException("The managed JSON writer accepted a nonfinite number.");
    }

    /// <summary>Reads a native JSON document and verifies the exact numeric values against Binary storage.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="directory">The native output directory.</param>
    /// <param name="name">The output file name.</param>
    /// <param name="expected">The Binary encoding of the original managed values.</param>
    private static void VerifyJson<TOptions>(string directory, string name, byte[] expected) where TOptions : struct, ILuminPackOptions
    {
        JsonNumberDocument? document = null;
        LuminPackSerializer.DeserializeJson<JsonNumberDocument?, TOptions>(File.ReadAllBytes(Path.Combine(directory, name)), ref document);
        if (document == null || !LuminPackSerializer.Serialize(document).AsSpan().SequenceEqual(expected))
            throw new InvalidDataException("The numeric JSON round trip changed the input bits: " + name);
    }
}

/// <summary>Covers scalar member formatting and contiguous sequences at every available primitive integer width.</summary>
[LuminPackable]
public sealed class JsonNumberDocument
{
    public float FloatField;
    public double DoubleField;
    public long SignedField;
    public ulong UnsignedField;
    public float[] Float32 = [];
    public double[] Float64 = [];
    public sbyte[] Int8 = [];
    public byte[] UInt8 = [];
    public short[] Int16 = [];
    public ushort[] UInt16 = [];
    public int[] Int32 = [];
    public uint[] UInt32 = [];
    public long[] Int64 = [];
    public ulong[] UInt64 = [];
}

/// <summary>Supplies Binary numbers outside the JSON domain.</summary>
[LuminPackable]
public sealed class JsonNumberRejectDocument
{
    public float Float32;
    public double Float64;
}
