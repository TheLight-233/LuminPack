using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPackCppRegisteredUnionFixtures;

/// <summary>A Union whose runtime registration uses a one-byte tag.</summary>
[LuminPackable, LuminPackUnion(10, typeof(GeneratedCase))]
public partial interface INarrow { }

/// <summary>A Union whose runtime registration can use a wide tag.</summary>
[LuminPackable, LuminPackWideTag, LuminPackUnion(10, typeof(GeneratedCase))]
public partial interface IWide { }

/// <summary>The statically generated case present in both Union contracts.</summary>
[LuminPackable]
public sealed partial class GeneratedCase : INarrow, IWide
{
    /// <summary>The ordinary generated payload.</summary>
    public int Value;
}

/// <summary>An app-owned case installed through the public Union registration API.</summary>
public sealed class RegisteredCase : INarrow, IWide
{
    /// <summary>The payload whose application representation is shared with C++.</summary>
    public OpaquePayload Payload = new();
}

/// <summary>A Custom payload with distinct Binary and JSON representations.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Custom)]
public sealed class OpaquePayload
{
    /// <summary>The decoded application value.</summary>
    public int Value;

    /// <summary>Writes the application-owned Binary representation.</summary>
    /// <param name="writer">The active Binary Union writer.</param>
    /// <param name="value">The decoded application payload.</param>
    public static void Serialize(ref LuminPackWriter writer, in OpaquePayload value) => writer.Write(value.Value ^ 85);

    /// <summary>Reads the application-owned Binary representation.</summary>
    /// <param name="reader">The reader at the Binary payload.</param>
    /// <param name="value">Receives the decoded application payload.</param>
    public static void Deserialize(ref LuminPackReader reader, ref OpaquePayload value)
    {
        int encoded = 0;
        reader.Read(ref encoded);
        value = new OpaquePayload { Value = encoded ^ 85 };
    }

    /// <summary>Writes the application-owned JSON representation.</summary>
    /// <param name="writer">The active JSON Union writer.</param>
    /// <param name="value">The decoded application payload.</param>
    public static void SerializeJson(ref LuminPackJsonWriter writer, in OpaquePayload value) => writer.Write(value.Value ^ 137);

    /// <summary>Reads the application-owned JSON representation.</summary>
    /// <param name="reader">The reader at the JSON payload.</param>
    /// <param name="value">Receives the decoded application payload.</param>
    public static void DeserializeJson(ref LuminPackJsonReader reader, ref OpaquePayload value)
        => value = new OpaquePayload { Value = reader.Read<int, LuminPackDefaultOptions>() ^ 137 };
}

internal static class Program
{
    private const int NarrowTag = 100;
    private const int WideTag = 600;

    /// <summary>Writes managed fixtures or verifies the native documents.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero after a successful exchange, or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["read" or "write", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.RegisteredUnion <read|write> <directory>");
            return 2;
        }

        LuminPackSerializer.Register<INarrow, RegisteredCase>(NarrowTag, WriteNarrow, ReadNarrow, WriteJsonNarrow, ReadJsonNarrow);
        LuminPackSerializer.Register<IWide, RegisteredCase>(WideTag, WriteWide, ReadWide, WriteJsonWide, ReadJsonWide);
        bool write = args[0] == "write";
        if (write)
            Directory.CreateDirectory(directory);
        var registered = new RegisteredCase { Payload = new OpaquePayload { Value = 42 } };
        var generated = new GeneratedCase { Value = 42 };
        Exchange<INarrow>(directory, write, "narrow", registered);
        Exchange<IWide>(directory, write, "wide", registered);
        Exchange<INarrow>(directory, write, "static-narrow", generated);
        Exchange<IWide>(directory, write, "static-wide", generated);
        return 0;
    }

    /// <summary>Exchanges one runtime-registered or generated case in all fixture encodings.</summary>
    /// <typeparam name="T">The interface that supplies the Union contract.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="write">Whether to write fixtures or read the native outputs.</param>
    /// <param name="name">The filename prefix.</param>
    /// <param name="value">The expected case and payload.</param>
    private static void Exchange<T>(string directory, bool write, string name, T value)
    {
        string path = Path.Combine(directory, name);
        byte[] binary = LuminPackSerializer.Serialize(value);
        string json = LuminPackSerializer.SerializeJson(value);
        if (write)
        {
            File.WriteAllBytes(path + ".bin", binary);
            File.WriteAllBytes(path + ".json", Encoding.Unicode.GetBytes(json));
            File.WriteAllBytes(path + ".utf8.json", Encoding.UTF8.GetBytes(json));
            return;
        }

        T fromBinary = LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(path + ".bin"));
        T fromJson = default!;
        LuminPackSerializer.DeserializeJson(File.ReadAllBytes(path + ".json"), ref fromJson);
        T fromUtf8Json = default!;
        LuminPackSerializer.DeserializeJson(Encoding.UTF8.GetString(File.ReadAllBytes(path + ".utf8.json")), ref fromUtf8Json);
        if (!Matches(fromBinary, value) || !Matches(fromJson, value) || !Matches(fromUtf8Json, value) ||
            !File.ReadAllBytes(path + ".bin").AsSpan().SequenceEqual(binary) ||
            Encoding.Unicode.GetString(File.ReadAllBytes(path + ".json")) != json ||
            Encoding.UTF8.GetString(File.ReadAllBytes(path + ".utf8.json")) != json)
            throw new InvalidDataException("Native Union output changed the case or its wire representation for " + name + ".");
    }

    /// <summary>Checks the decoded case type and its payload.</summary>
    /// <typeparam name="T">The Union contract.</typeparam>
    /// <param name="actual">The decoded native fixture.</param>
    /// <param name="expected">The expected case and payload.</param>
    /// <returns>Whether the fixtures represent the same case and value.</returns>
    private static bool Matches<T>(T actual, T expected) => (actual, expected) switch
    {
        (RegisteredCase left, RegisteredCase right) => left.Payload.Value == right.Payload.Value,
        (GeneratedCase left, GeneratedCase right) => left.Value == right.Value,
        _ => false
    };

    /// <summary>Writes the narrow registration's numeric tag and Binary payload.</summary>
    /// <param name="writer">The active Binary writer.</param>
    /// <param name="value">The concrete case passed through its narrow Union root.</param>
    private static void WriteNarrow(ref LuminPackWriter writer, ref INarrow value)
    {
        writer.WriteUnionHeader(NarrowTag);
        OpaquePayload.Serialize(ref writer, ((RegisteredCase)value).Payload);
    }

    /// <summary>Writes the wide registration's numeric tag and Binary payload.</summary>
    /// <param name="writer">The active Binary writer.</param>
    /// <param name="value">The concrete case passed through its wide Union root.</param>
    private static void WriteWide(ref LuminPackWriter writer, ref IWide value)
    {
        writer.WriteWideUnionHeader(WideTag);
        OpaquePayload.Serialize(ref writer, ((RegisteredCase)value).Payload);
    }

    /// <summary>Decodes the registered Binary payload into a concrete case.</summary>
    /// <param name="reader">The active reader after the numeric tag.</param>
    /// <returns>The registered case.</returns>
    private static RegisteredCase ReadBinary(ref LuminPackReader reader)
    {
        OpaquePayload payload = null!;
        OpaquePayload.Deserialize(ref reader, ref payload);
        return new RegisteredCase { Payload = payload };
    }

    /// <summary>Reads the registered Binary payload through the narrow Union root.</summary>
    /// <param name="reader">The active reader after the numeric tag.</param>
    /// <param name="value">Receives the registered case.</param>
    private static void ReadNarrow(ref LuminPackReader reader, ref INarrow value) => value = ReadBinary(ref reader);

    /// <summary>Reads the registered Binary payload through the wide Union root.</summary>
    /// <param name="reader">The active reader after the numeric tag.</param>
    /// <param name="value">Receives the registered case.</param>
    private static void ReadWide(ref LuminPackReader reader, ref IWide value) => value = ReadBinary(ref reader);

    /// <summary>Writes the narrow registration's JSON envelope and payload.</summary>
    /// <param name="writer">The active JSON writer.</param>
    /// <param name="value">The registered case passed through the narrow Union root.</param>
    private static void WriteJsonNarrow(ref LuminPackJsonWriter writer, ref INarrow value)
        => WriteJson(ref writer, (RegisteredCase)value, NarrowTag);

    /// <summary>Writes the wide registration's JSON envelope and payload.</summary>
    /// <param name="writer">The active JSON writer.</param>
    /// <param name="value">The registered case passed through the wide Union root.</param>
    private static void WriteJsonWide(ref LuminPackJsonWriter writer, ref IWide value)
        => WriteJson(ref writer, (RegisteredCase)value, WideTag);

    /// <summary>Writes the app-owned JSON envelope and payload.</summary>
    /// <param name="writer">The active JSON writer.</param>
    /// <param name="value">The concrete case to write.</param>
    /// <param name="tag">The registered numeric tag.</param>
    private static void WriteJson(ref LuminPackJsonWriter writer, RegisteredCase value, int tag)
    {
        writer.WriteObjectStart();
        writer.WritePropertyName("$type");
        writer.Write(tag);
        writer.WritePropertyName("$value");
        OpaquePayload.SerializeJson(ref writer, value.Payload);
        writer.WriteObjectEnd();
    }

    /// <summary>Decodes the app-owned JSON payload.</summary>
    /// <param name="reader">The active reader positioned at the payload.</param>
    /// <returns>The registered case.</returns>
    private static RegisteredCase ReadJson(ref LuminPackJsonReader reader)
    {
        OpaquePayload payload = null!;
        OpaquePayload.DeserializeJson(ref reader, ref payload);
        return new RegisteredCase { Payload = payload };
    }

    /// <summary>Reads the registered JSON payload through the narrow Union root.</summary>
    /// <param name="reader">The active reader positioned at the payload.</param>
    /// <param name="value">Receives the registered case.</param>
    private static void ReadJsonNarrow(ref LuminPackJsonReader reader, ref INarrow value) => value = ReadJson(ref reader);

    /// <summary>Reads the registered JSON payload through the wide Union root.</summary>
    /// <param name="reader">The active reader positioned at the payload.</param>
    /// <param name="value">Receives the registered case.</param>
    private static void ReadJsonWide(ref LuminPackJsonReader reader, ref IWide value) => value = ReadJson(ref reader);
}
