using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackPortableFixtures;

/// <summary>Exercises ordered UTF-8 JSON property heads, integer ranges, and parser fallback.</summary>
internal static class OrderedJsonFixtures
{
    private const string StartField = "\"FirstInt\":-2147483648";
    private const string LastField = "\"LastInt\":2147483647";
    private static readonly string[] OrderedCases =
        ["ordered", "from-binary", "whitespace", "reordered", "escaped", "unknown", "duplicate", "negative-zero"];

    /// <summary>Writes the canonical managed documents and their alternate syntax test inputs.</summary>
    /// <param name="directory">The output directory.</param>
    internal static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var value = Create();
        string canonical = Serialize(value);
        WriteCase<OrderedJsonDocument>(directory, "ordered", canonical, canonical);
        File.WriteAllBytes(Path.Combine(directory, "ordered.bin"), LuminPackSerializer.Serialize(value));
        WriteCase<OrderedJsonDocument>(directory, "whitespace", canonical,
            Change(canonical, ",\"Value08\":0", ", \n\t\"Value08\"\r : 0"));
        WriteCase<OrderedJsonDocument>(directory, "reordered", canonical,
            Change(canonical, ",\"Value15\":999,\"Value16\":1000", ",\"Value16\":1000,\"Value15\":999"));
        WriteCase<OrderedJsonDocument>(directory, "escaped", canonical,
            Change(canonical, "\"Value08\":0", "\"\\u0056alue08\":0"));
        WriteCase<OrderedJsonDocument>(directory, "unknown", canonical,
            Change(canonical, ",\"Value08\":0", ",\"Un\\u006bnown\":{\"x\":[true,{\"y\":\"a\\\"b\"}]},\"Value08\":0"));
        WriteCase<OrderedJsonDocument>(directory, "duplicate", canonical,
            Change(canonical, StartField, "\"FirstInt\":19," + StartField));
        WriteCase<OrderedJsonDocument>(directory, "negative-zero", canonical,
            Change(canonical, "\"Value08\":0", "\"Value08\":-0"));

        string nested = Serialize(CreateContainer(value));
        WriteCase<OrderedJsonContainer>(directory, "nested", nested, nested);
        WriteCase<OrderedJsonContainer>(directory, "alias", nested,
            Change(nested, "\"CurrentNumber\":70", "\"Prev\\u0069ousNumber\":70"));

        string limited = Serialize(new OrderedJsonLimitedDocument());
        string atLimit = limited.PadRight(OrderedJsonLimitedDocument.InputLimit);
        WriteCase<OrderedJsonLimitedDocument>(directory, "limited", limited, limited);
        WriteCase<OrderedJsonLimitedDocument>(directory, "at-limit", limited, atLimit);
        WriteInput(directory, "over-limit", atLimit + " ");
        WriteInput(directory, "depth-limit", Change(limited, ",\"LastCount\":", ",\"Extra\":[[[[[[0]]]]]],\"LastCount\":"));
        WriteInput(directory, "overflow", Change(canonical, LastField, "\"LastInt\":2147483648"));
        WriteInput(directory, "underflow", Change(canonical, StartField, "\"FirstInt\":-2147483649"));
        WriteInput(directory, "missing-comma", Change(canonical, ",\"Value08\":0", "\"Value08\":0"));
        WriteInput(directory, "missing-colon", Change(canonical, "\"Value16\":1000", "\"Value16\"1000"));
        WriteInput(directory, "leading-zero", Change(canonical, "\"Value08\":0", "\"Value08\":00"));
        WriteInput(directory, "truncated-value", canonical[..canonical.IndexOf(LastField, StringComparison.Ordinal)] + "\"LastInt\":-");
    }

    /// <summary>Checks native outputs using the original managed reader.</summary>
    /// <param name="directory">The native output directory.</param>
    internal static void Verify(string directory)
    {
        var value = Create();
        string canonical = Serialize(value);
        foreach (string name in OrderedCases)
            VerifyFile<OrderedJsonDocument>(directory, name, canonical);

        string nested = Serialize(CreateContainer(value));
        VerifyFile<OrderedJsonContainer>(directory, "nested", nested);
        VerifyFile<OrderedJsonContainer>(directory, "alias", nested);
        string limited = Serialize(new OrderedJsonLimitedDocument());
        VerifyFile<OrderedJsonLimitedDocument>(directory, "limited", limited);
        VerifyFile<OrderedJsonLimitedDocument>(directory, "at-limit", limited);
    }

    /// <summary>Creates signed and unsigned integer values around digit, chunk, and storage-width boundaries.</summary>
    /// <returns>The fixture's ordinary record.</returns>
    private static OrderedJsonDocument Create() => new()
    {
        FirstInt = int.MinValue, Value01 = -2147483647, Value02 = -10000, Value03 = -1000,
        Value04 = -999, Value05 = -100, Value06 = -10, Value07 = -1, Value08 = 0,
        Value09 = 1, Value10 = 9, Value11 = 10, Value12 = 99, Value13 = 100,
        Value14 = 998, Value15 = 999, Value16 = 1000, Value17 = 2147483646, LastInt = int.MaxValue,
        Unsigned32 = uint.MaxValue, Signed64 = long.MinValue, Unsigned64 = ulong.MaxValue,
        Largest64 = long.MaxValue, FinalText = "done",
    };

    /// <summary>Pairs the ordered run with a Version value whose former name must set a nondefault field.</summary>
    /// <param name="value">The ordinary ordered value.</param>
    /// <returns>The nested fixture.</returns>
    private static OrderedJsonContainer CreateContainer(OrderedJsonDocument value) => new()
    {
        Ordered = value,
        History = new() { CurrentNumber = 70, OtherNumber = -89 },
    };

    /// <summary>Captures the managed UTF-8 writer's canonical representation.</summary>
    /// <typeparam name="T">The managed model.</typeparam>
    /// <param name="value">The model to serialize.</param>
    /// <returns>The UTF-8 JSON text.</returns>
    private static string Serialize<T>(T value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson<T, LuminPackUtf8LengthOptions>(value, buffer);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Writes a valid alternate input and checks the managed reader's interpretation.</summary>
    /// <typeparam name="T">The managed model.</typeparam>
    /// <param name="directory">The fixture output directory.</param>
    /// <param name="name">The fixture basename.</param>
    /// <param name="expected">The expected canonical representation.</param>
    /// <param name="text">The input under test.</param>
    private static void WriteCase<T>(string directory, string name, string expected, string text)
    {
        WriteInput(directory, name, text);
        VerifyFile<T>(directory, name, expected);
    }

    /// <summary>Writes UTF-8 input without a byte-order mark.</summary>
    /// <param name="directory">The fixture output directory.</param>
    /// <param name="name">The fixture basename.</param>
    /// <param name="text">The input under test.</param>
    private static void WriteInput(string directory, string name, string text) =>
        File.WriteAllBytes(Path.Combine(directory, name + ".json"), Encoding.UTF8.GetBytes(text));

    /// <summary>Checks that a fixture reconstructs the expected model using the managed codec.</summary>
    /// <typeparam name="T">The managed model.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="name">The fixture basename.</param>
    /// <param name="expected">The expected canonical representation.</param>
    private static void VerifyFile<T>(string directory, string name, string expected)
    {
        string path = Path.Combine(directory, name + ".json");
        T value = default!;
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf8LengthOptions>(File.ReadAllBytes(path), ref value);
        if (Serialize(value) != expected)
            throw new InvalidDataException("Ordered JSON changed the managed fixture: " + path);
    }

    /// <summary>Changes a known part of the managed document and fails if the shape changes.</summary>
    /// <param name="text">The managed document.</param>
    /// <param name="before">The part to change.</param>
    /// <param name="after">The replacement.</param>
    /// <returns>The edited JSON text.</returns>
    private static string Change(string text, string before, string after) => text.Contains(before, StringComparison.Ordinal)
        ? text.Replace(before, after, StringComparison.Ordinal)
        : throw new InvalidDataException("Missing fixture text: " + before);
}

/// <summary>Provides a run longer than one 16-field cursor publication.</summary>
[LuminPackable]
public sealed class OrderedJsonDocument
{
    public int FirstInt;
    public int Value01;
    public int Value02;
    public int Value03;
    public int Value04;
    public int Value05;
    public int Value06;
    public int Value07;
    public int Value08;
    public int Value09;
    public int Value10;
    public int Value11;
    public int Value12;
    public int Value13;
    public int Value14;
    public int Value15;
    public int Value16;
    public int Value17;
    public int LastInt;
    public uint Unsigned32;
    public long Signed64;
    public ulong Unsigned64;
    public long Largest64;
    public string? FinalText;
}

/// <summary>Combines the ordinary run with collection parsing and Version aliases.</summary>
[LuminPackable]
public sealed class OrderedJsonContainer
{
    public int FirstCount = 8;
    public int LastCount = -3;
    public OrderedJsonDocument Ordered = new();
    public int[] Samples = [3, -5, 17];
    public Dictionary<int, int> Counts = new() { [4] = -7, [10] = 30 };
    public OrderedJsonVersion History = new();
}

/// <summary>Maps a former JSON property name to its current member.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class OrderedJsonVersion
{
    [LuminPackOldName("PreviousNumber")]
    public int CurrentNumber;
    public int OtherNumber;
}

/// <summary>Sets document byte and nesting limits for the strict reader.</summary>
[LuminPackable, LuminPackOption(MaxInputBytes = InputLimit, MaxDepth = 4)]
public sealed class OrderedJsonLimitedDocument
{
    internal const int InputLimit = 128;
    public int FirstCount = 26;
    public int LastCount = -80;
}
