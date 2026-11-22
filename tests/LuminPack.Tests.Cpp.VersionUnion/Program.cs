using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackCppVersionUnionFixtures;

/// <summary>Exchanges ordinary Version Union cases with the C++ variant and RTTI bindings.</summary>
internal static class Program
{
    /// <summary>Writes the managed documents or reads back the native documents.</summary>
    /// <param name="args">The operation and the fixture directory.</param>
    /// <returns>Zero on success or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["write" or "read", var directory])
            return 2;
        var version = new VersionCase { Code = 19, Text = "one café" };
        var document = new Envelope
        {
            Cases = [version, new DefaultCase { Number = 97 }, null, new VersionCase { Code = 37, Text = "two Ω" }]
        };
        var tracked = new TrackedCase { Code = 57 };
        var graph = new GraphEnvelope { Cases = [version, tracked, tracked] };
        if (args[0] == "write")
        {
            Directory.CreateDirectory(directory);
            byte[] direct = LuminPackSerializer.Serialize<IChoice>(version);
            byte[] binary = LuminPackSerializer.Serialize(document);
            byte[] ordinal = [.. direct.AsSpan(0, 4), 251, 0x52, 0];
            if (direct[4] != 251 || direct[5] != 0x42 || binary.AsSpan().IndexOf(ordinal) < 0)
                throw new InvalidDataException("The managed Union did not share the Version registry.");
            WriteNulls(directory, direct.AsSpan(0, 4));
            Write(document, binary, directory, "envelope");
            Write(graph, LuminPackSerializer.Serialize(graph), directory, "graph");
        }
        else
        {
            Verify(directory, "envelope", LuminPackSerializer.SerializeJson(document),
                static bytes => LuminPackSerializer.Deserialize<Envelope>(bytes));
            if (File.Exists(Path.Combine(directory, "graph.bin")))
                Verify(directory, "graph", LuminPackSerializer.SerializeJson(graph),
                    static bytes => CheckGraph(LuminPackSerializer.Deserialize<GraphEnvelope>(bytes)));
            if (LuminPackSerializer.Deserialize<IChoice>(File.ReadAllBytes(Path.Combine(directory, "null.bin"))) != null ||
                LuminPackSerializer.DeserializeJson<IChoice>(File.ReadAllBytes(Path.Combine(directory, "null.utf16.json"))) != null)
                throw new InvalidDataException("The native writer did not publish a null Union.");
        }
        return 0;
    }

    /// <summary>Verifies and records how a known Version hash with a null body reads in C#.</summary>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="hash">The writer's canonical four-byte discriminator.</param>
    private static void WriteNulls(string directory, ReadOnlySpan<byte> hash)
    {
        byte[] nullBinary = [.. hash, 255];
        uint code = BinaryPrimitives.ReadUInt32LittleEndian(hash);
        string nullJson = FormattableString.Invariant($"{{\"$type\":{code},\"$value\":null}}");
        if (LuminPackSerializer.Deserialize<IChoice>(nullBinary) != null ||
            LuminPackSerializer.DeserializeJson<IChoice>(nullJson) != null)
            throw new InvalidDataException("The known Version case did not deserialize as null.");
        File.WriteAllBytes(Path.Combine(directory, "null.bin"), nullBinary);
        File.WriteAllBytes(Path.Combine(directory, "null.utf8.json"), Encoding.UTF8.GetBytes(nullJson));
        File.WriteAllBytes(Path.Combine(directory, "null.utf16.json"), Encoding.Unicode.GetBytes(nullJson));
    }

    /// <summary>Writes a Binary document and its native UTF-8 and UTF-16 JSON counterparts.</summary>
    /// <typeparam name="T">The closed managed fixture shape.</typeparam>
    /// <param name="value">The original fixture value.</param>
    /// <param name="binary">The actual managed Binary wire bytes.</param>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="name">The common fixture name.</param>
    private static void Write<T>(T value, byte[] binary, string directory, string name)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path + ".bin", binary);
        var output = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson<T, LuminPackUtf8LengthOptions>(value, output);
        File.WriteAllBytes(path + ".utf8.json", output.WrittenSpan.ToArray());
        output.Clear();
        LuminPackSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(value, output);
        File.WriteAllBytes(path + ".utf16.json", output.WrittenSpan.ToArray());
    }

    /// <summary>Reads native output and checks Binary and both JSON encodings against managed values.</summary>
    /// <typeparam name="T">The closed managed fixture shape.</typeparam>
    /// <param name="directory">The directory with the native documents.</param>
    /// <param name="name">The common fixture name.</param>
    /// <param name="expected">The managed canonical JSON representation.</param>
    /// <param name="readBinary">The statically bound managed Binary reader.</param>
    private static void Verify<T>(string directory, string name, string expected, Func<byte[], T> readBinary)
    {
        string path = Path.Combine(directory, name);
        T binary = readBinary(File.ReadAllBytes(path + ".bin"));
        T utf8 = default!;
        T utf16 = default!;
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf8LengthOptions>(File.ReadAllBytes(path + ".utf8.json"), ref utf8);
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf16LengthOptions>(File.ReadAllBytes(path + ".utf16.json"), ref utf16);
        if (expected != LuminPackSerializer.SerializeJson(binary) || expected != LuminPackSerializer.SerializeJson(utf8) ||
            expected != LuminPackSerializer.SerializeJson(utf16))
            throw new InvalidDataException("The native Version Union changed the managed values.");
    }

    /// <summary>Checks mixed graph cases preserve the tracked identity beside an ordinary Version case.</summary>
    /// <param name="value">The decoded graph envelope.</param>
    /// <returns>The verified graph for the common JSON comparison.</returns>
    private static GraphEnvelope CheckGraph(GraphEnvelope value)
    {
        if (value.Cases is not [VersionCase { Code: 19 }, TrackedCase { Code: 57 }, TrackedCase] ||
            !ReferenceEquals(value.Cases[1], value.Cases[2]))
            throw new InvalidDataException("The mixed Union changed a case or lost reference identity.");
        return value;
    }
}

/// <summary>Combines ordinary Default and Version cases.</summary>
[LuminPackable, LuminPackUnion(1, typeof(DefaultCase)), LuminPackUnion(2, typeof(VersionCase))]
public interface IChoice
{
}

/// <summary>Combines graph-borrowed tracked and ordinary Version cases.</summary>
[LuminPackable, LuminPackUnion(1, typeof(TrackedCase)), LuminPackUnion(2, typeof(VersionCase))]
public interface IGraphChoice
{
}

/// <summary>An ordinary payload-only case.</summary>
[LuminPackable]
public sealed class DefaultCase : IChoice
{
    public int Number;
}

/// <summary>A nontracked case with a complete Version record.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class VersionCase : IChoice, IGraphChoice
{
    public int Code;
    public string Text = "";
}

/// <summary>A tracked neighbor of the ordinary Version case.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true)]
public sealed class TrackedCase : IGraphChoice
{
    public int Code;
}

/// <summary>Shares one Version registry across ordinary Union members in a list.</summary>
[LuminPackable]
public sealed class Envelope
{
    public List<IChoice?> Cases = [];
}

/// <summary>Shares one reference graph across a Version case and repeated tracked cases.</summary>
[LuminPackable]
public sealed class GraphEnvelope
{
    public List<IGraphChoice?> Cases = [];
}
