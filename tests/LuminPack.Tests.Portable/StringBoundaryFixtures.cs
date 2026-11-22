using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackPortableFixtures;

/// <summary>Exchanges strings that straddle short-record, vector, and length-prefix boundaries.</summary>
internal static class StringBoundaryFixtures
{
    private static readonly int[] Lengths =
    [
        .. Enumerable.Range(0, 18), 23, 24, 25, 31, 32, 33, 47, 48, 49, 63, 64, 65, 95, 96, 97,
        126, 127, 128, 129, 254, 255, 256, 257, 511, 512, 513, 1023, 1024, 1025, 4095, 4096, 4097,
    ];

    /// <summary>Emits the original managed serializations used by the portable fast path tests.</summary>
    /// <param name="directory">The output directory.</param>
    internal static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var fixture = Create();
        WriteBinary<LuminPackUtf8LengthOptions>(directory, "utf8-length", fixture);
        WriteBinary<LuminPackUtf8TokenOptions>(directory, "utf8-token", fixture);
        WriteBinary<LuminPackUtf16LengthOptions>(directory, "utf16-length", fixture);
        WriteBinary<LuminPackUtf16TokenOptions>(directory, "utf16-token", fixture);
        WriteBinary<LuminPackUtf8VarIntOptions>(directory, "utf8-varint", fixture);
        WriteBinary<LuminPackUtf16VarIntOptions>(directory, "utf16-varint", fixture);

        var output = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson<StringBoundaryDocument, LuminPackUtf8LengthOptions>(fixture, output);
        File.WriteAllBytes(Path.Combine(directory, "strings.utf8.json"), output.WrittenSpan.ToArray());
        output.Clear();
        LuminPackSerializer.SerializeJson<StringBoundaryDocument, LuminPackUtf16LengthOptions>(fixture, output);
        File.WriteAllBytes(Path.Combine(directory, "strings.utf16.json"), output.WrittenSpan.ToArray());
    }

    /// <summary>Reads every native output using the corresponding compiled managed profile.</summary>
    /// <param name="directory">The exchange output directory.</param>
    internal static void Verify(string directory)
    {
        string canonical = LuminPackSerializer.SerializeJson(Create());
        VerifyProfile<LuminPackUtf8LengthOptions>(directory, "utf8-length", canonical);
        VerifyProfile<LuminPackUtf8TokenOptions>(directory, "utf8-token", canonical);
        VerifyProfile<LuminPackUtf16LengthOptions>(directory, "utf16-length", canonical);
        VerifyProfile<LuminPackUtf16TokenOptions>(directory, "utf16-token", canonical);
        VerifyProfile<LuminPackUtf8VarIntOptions>(directory, "utf8-varint", canonical);
        VerifyProfile<LuminPackUtf16VarIntOptions>(directory, "utf16-varint", canonical);
        VerifyJson<LuminPackUtf8LengthOptions>(Path.Combine(directory, "strings.utf8.json"), canonical);
        VerifyJson<LuminPackUtf16LengthOptions>(Path.Combine(directory, "strings.utf16.json"), canonical);
    }

    /// <summary>Writes one Binary string profile.</summary>
    /// <typeparam name="TOptions">The statically selected profile.</typeparam>
    /// <param name="directory">The output directory.</param>
    /// <param name="profile">The fixture basename.</param>
    /// <param name="fixture">The original string model.</param>
    private static void WriteBinary<TOptions>(string directory, string profile, StringBoundaryDocument fixture)
        where TOptions : struct, ILuminPackOptions => File.WriteAllBytes(Path.Combine(directory, profile + ".bin"),
            LuminPackSerializer.Serialize<StringBoundaryDocument, TOptions>(fixture));

    /// <summary>Reads the native roundtrip and transcodes associated with one Binary profile.</summary>
    /// <typeparam name="TOptions">The statically selected profile.</typeparam>
    /// <param name="directory">The exchange output directory.</param>
    /// <param name="profile">The fixture basename.</param>
    /// <param name="canonical">The managed model used for comparison.</param>
    private static void VerifyProfile<TOptions>(string directory, string profile, string canonical)
        where TOptions : struct, ILuminPackOptions
    {
        string prefix = Path.Combine(directory, profile);
        foreach (string suffix in new[] { ".bin", ".from-utf8.bin", ".from-utf16.bin" })
        {
            string file = prefix + suffix;
            var decoded = LuminPackSerializer.Deserialize<StringBoundaryDocument, TOptions>(File.ReadAllBytes(file));
            VerifyModel(decoded, canonical, file);
        }

        VerifyJson<LuminPackUtf8LengthOptions>(prefix + ".utf8.json", canonical);
        VerifyJson<LuminPackUtf16LengthOptions>(prefix + ".utf16.json", canonical);
    }

    /// <summary>Reads one native JSON document under its corresponding managed text encoding.</summary>
    /// <typeparam name="TOptions">The statically selected text encoding.</typeparam>
    /// <param name="file">The native JSON file path.</param>
    /// <param name="canonical">The managed model used for comparison.</param>
    private static void VerifyJson<TOptions>(string file, string canonical) where TOptions : struct, ILuminPackOptions
    {
        StringBoundaryDocument? decoded = null;
        LuminPackSerializer.DeserializeJson<StringBoundaryDocument?, TOptions>(File.ReadAllBytes(file), ref decoded);
        VerifyModel(decoded, canonical, file);
    }

    /// <summary>Compares the recovered string records in the managed canonical representation.</summary>
    /// <param name="decoded">The native result decoded by the managed runtime.</param>
    /// <param name="canonical">The managed model used for comparison.</param>
    /// <param name="path">The fixture path used in error diagnostics.</param>
    private static void VerifyModel(StringBoundaryDocument? decoded, string canonical, string path)
    {
        if (decoded == null || LuminPackSerializer.SerializeJson(decoded) != canonical)
            throw new InvalidDataException("String transcode changed the managed fixture: " + path);
    }

    /// <summary>Creates deterministic string fixtures of several text widths and code-unit compositions.</summary>
    /// <returns>The original fixture document.</returns>
    private static StringBoundaryDocument Create()
    {
        var values = new List<string>();
        foreach (int length in Lengths)
        {
            values.Add(new string('a', length));
            values.Add(new string('\u00e9', length));
            values.Add(new string('\u6f22', length));
            if (length != 0)
            {
                values.Add(new string('x', length - 1) + '\u03a9');
                values.Add('\u0100' + new string('z', length - 1));
            }
            if (length >= 2)
                values.Add(new string('f', length - 2) + "\U0001F600");
        }

        return new StringBoundaryDocument
        {
            First = "x",
            AtShortLimit = "1234567890abcde\u00e9",
            AfterShortLimit = "1234567890abcde\U0001F600",
            Escapes = "\"\\/\r\n\t\b\f\u0001\u001f",
            Values = values.ToArray(),
            Nested = new() { Items = [.. values.AsEnumerable().Reverse()] },
            Tail = new string('x', 31) + "\U0001F600",
        };
    }
}

/// <summary>Combines direct string fields, arrays, and an ordinary nested record.</summary>
[LuminPackable]
public sealed class StringBoundaryDocument
{
    /// <summary>A one-unit ASCII field at the start of the record.</summary>
    public string First = "";
    /// <summary>A sixteen-unit field ending with a two-byte UTF-8 value.</summary>
    public string AtShortLimit = "";
    /// <summary>A seventeen-unit field whose final two units form a supplementary value.</summary>
    public string AfterShortLimit = "";
    /// <summary>JSON escapes adjacent to ordinary text.</summary>
    public string Escapes = "";
    /// <summary>Strings exercised with the selected operation profile.</summary>
    public string?[] Values = [];
    /// <summary>Strings exercised through a nested serializer.</summary>
    public StringBoundaryNested Nested = new();
    /// <summary>A supplementary scalar occupying the final units of the document.</summary>
    public string Tail = "";
}

/// <summary>Provides the same strings in reverse order through a list.</summary>
[LuminPackable]
public sealed class StringBoundaryNested
{
    /// <summary>Inherits the operation profile from the parent.</summary>
    public List<string?> Items = [];
}
