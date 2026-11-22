using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPackCppCustomFixtures;

/// <summary>Exchanges application-owned Custom representations with generated C++ contracts.</summary>
internal static class Program
{
    /// <summary>Writes managed fixtures or verifies the native outputs.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero for success or two when the arguments are invalid.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["read" or "write", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Custom <read|write> <directory>");
            return 2;
        }

        bool write = args[0] == "write";
        if (write)
            Directory.CreateDirectory(directory);
        var data = new Shifted { Number = 42 };
        Exchange(directory, write, "custom", data);
        Exchange<Shifted?>(directory, write, "custom-null", null);
        Exchange(directory, write, "custom-ordinary", new Ordinary { Data = data, Values = [null, data], Marker = 13 });
        Exchange(directory, write, "custom-version", new Versioned { Data = data, Marker = 7 });

        string binaryOnly = Path.Combine(directory, "custom-binary-only.bin");
        var value = new BinaryOnly { Number = 29 };
        if (write)
            File.WriteAllBytes(binaryOnly, LuminPackSerializer.Serialize(value));
        else if (LuminPackSerializer.Deserialize<BinaryOnly>(File.ReadAllBytes(binaryOnly)).Number != 29)
            throw new InvalidDataException("The Binary-only Custom value differs from the fixture.");

        Reject(() => LuminPackSerializer.SerializeJson(value));
        Reject(() => LuminPackSerializer.SerializeJson<Shifted, LuminPackUtf8LengthOptions>(data));
        Reject(() => LuminPackSerializer.Serialize<IClosedCustomUnion>(new ClosedCustomCase { Payload = data }));
        if (!write)
        {
            string alias = Encoding.Unicode.GetString(File.ReadAllBytes(Path.Combine(directory, "custom-version.json")))
                .Replace("\"Data\"", "\"Previous\"", StringComparison.Ordinal);
            Versioned? restored = null;
            LuminPackSerializer.DeserializeJson(alias, ref restored);
            if (restored?.Data?.Number != 42 || restored.Marker != 7)
                throw new InvalidDataException("The Version Custom former name was not selected.");
        }
        Console.WriteLine(write ? ".NET wrote the Custom fixtures." : ".NET verified the C++ Custom Binary and JSON documents.");
        return 0;
    }

    /// <summary>Exchanges both encodings of one fixture and checks its exact Custom representation.</summary>
    /// <typeparam name="T">The managed payload type.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="write">Whether to produce fixtures.</param>
    /// <param name="name">The fixture basename.</param>
    /// <param name="value">The expected payload.</param>
    private static void Exchange<T>(string directory, bool write, string name, T value)
    {
        string file = Path.Combine(directory, name);
        var binary = LuminPackSerializer.Serialize(value);
        string json = LuminPackSerializer.SerializeJson(value);
        if (write)
        {
            File.WriteAllBytes(file + ".bin", binary);
            File.WriteAllBytes(file + ".json", Encoding.Unicode.GetBytes(json));
            return;
        }

        var decodedBinary = LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(file + ".bin"));
        T decodedJson = default!;
        var jsonBytes = File.ReadAllBytes(file + ".json");
        LuminPackSerializer.DeserializeJson(jsonBytes, ref decodedJson);
        if (!LuminPackSerializer.Serialize(decodedBinary).AsSpan().SequenceEqual(binary) ||
            !LuminPackSerializer.Serialize(decodedJson).AsSpan().SequenceEqual(binary) ||
            Encoding.Unicode.GetString(jsonBytes) != json)
            throw new InvalidDataException("The C++ Custom representation differs from the fixture " + name + ".");
    }

    /// <summary>Checks that an unregistered Custom operation fails.</summary>
    /// <param name="operation">The unsupported serialization call.</param>
    private static void Reject(Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception error) when (error.Message.Contains("formatter", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        throw new InvalidDataException("A missing Custom formatter was selected.");
    }
}

/// <summary>Uses its own null sentinel and formats Binary and JSON differently.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Custom)]
public sealed class Shifted
{
    public int Number;

    /// <summary>Writes the transformed value or the reserved Binary null sentinel.</summary>
    /// <param name="writer">The Binary output cursor.</param>
    /// <param name="value">The source value.</param>
    public static void Serialize(ref LuminPackWriter writer, in Shifted value)
        => writer.Write(value == null ? long.MinValue : value.Number ^ 85);

    /// <summary>Reads the transformed Binary value and restores nulls.</summary>
    /// <param name="reader">The Binary input cursor.</param>
    /// <param name="value">The decoded value.</param>
    public static void Deserialize(ref LuminPackReader reader, ref Shifted value)
    {
        long encoded = 0;
        reader.Read(ref encoded);
        value = encoded == long.MinValue ? null! : new Shifted { Number = checked((int)encoded) ^ 85 };
    }

    /// <summary>Writes the transformed JSON number or the reserved null sentinel.</summary>
    /// <param name="writer">The JSON output cursor.</param>
    /// <param name="value">The source value.</param>
    public static void SerializeJson(ref LuminPackJsonWriter writer, in Shifted value)
        => writer.Write(value == null ? long.MinValue : value.Number ^ 137);

    /// <summary>Reads the transformed JSON number and restores nulls.</summary>
    /// <param name="reader">The JSON input cursor.</param>
    /// <param name="value">The decoded value.</param>
    public static void DeserializeJson(ref LuminPackJsonReader reader, ref Shifted value)
    {
        long encoded = reader.Read<long, LuminPackDefaultOptions>();
        value = encoded == long.MinValue ? null! : new Shifted { Number = checked((int)encoded) ^ 137 };
    }
}

/// <summary>Uses a one-byte Binary representation and has no JSON registration.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Custom)]
public struct BinaryOnly
{
    public int Number;

    /// <summary>Writes one transformed byte.</summary>
    /// <param name="writer">The Binary output cursor.</param>
    /// <param name="value">The source value.</param>
    public static void Serialize(ref LuminPackWriter writer, in BinaryOnly value)
        => writer.Write(checked((byte)(value.Number ^ 85)));

    /// <summary>Reads the application-owned one-byte payload.</summary>
    /// <param name="reader">The Binary input cursor.</param>
    /// <param name="value">The decoded value.</param>
    public static void Deserialize(ref LuminPackReader reader, ref BinaryOnly value)
    {
        byte encoded = 0;
        reader.Read(ref encoded);
        value.Number = encoded ^ 85;
    }
}

/// <summary>Includes nullable Custom values directly and as list elements.</summary>
[LuminPackable]
public sealed partial class Ordinary
{
    public Shifted? Data;
    public Shifted? Absent;
    public List<Shifted?> Values = [];
    public int Marker;
}

/// <summary>Contains a bounded Custom member that retains its former wire name.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed partial class Versioned
{
    [LuminPackableObject, LuminPackOldName("Previous")]
    public Shifted? Data;
    public int Marker;
}

/// <summary>A declared Union cannot derive a hash case body from an opaque Custom registration.</summary>
[LuminPackable, LuminPackUnion(10, typeof(ClosedCustomCase))]
public partial interface IClosedCustomUnion
{
}

/// <summary>Provides both Custom operation pairs without a generated Union payload.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Custom)]
public sealed class ClosedCustomCase : IClosedCustomUnion
{
    public Shifted Payload = new();

    /// <summary>Writes the complete Custom Binary payload.</summary>
    /// <param name="writer">The Binary output cursor.</param>
    /// <param name="value">The source case.</param>
    public static void Serialize(ref LuminPackWriter writer, in ClosedCustomCase value)
        => Shifted.Serialize(ref writer, in value.Payload);

    /// <summary>Reads the complete Custom Binary payload.</summary>
    /// <param name="reader">The Binary input cursor.</param>
    /// <param name="value">The decoded case.</param>
    public static void Deserialize(ref LuminPackReader reader, ref ClosedCustomCase value)
    {
        value = new ClosedCustomCase();
        Shifted.Deserialize(ref reader, ref value.Payload);
    }

    /// <summary>Writes the complete Custom JSON payload.</summary>
    /// <param name="writer">The JSON output cursor.</param>
    /// <param name="value">The source case.</param>
    public static void SerializeJson(ref LuminPackJsonWriter writer, in ClosedCustomCase value)
        => Shifted.SerializeJson(ref writer, in value.Payload);

    /// <summary>Reads the complete Custom JSON payload.</summary>
    /// <param name="reader">The JSON input cursor.</param>
    /// <param name="value">The decoded case.</param>
    public static void DeserializeJson(ref LuminPackJsonReader reader, ref ClosedCustomCase value)
    {
        value = new ClosedCustomCase();
        Shifted.DeserializeJson(ref reader, ref value.Payload);
    }
}
