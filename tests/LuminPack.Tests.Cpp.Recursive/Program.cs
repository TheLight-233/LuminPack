using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackCppRecursiveFixtures;

/// <summary>Exchanges untracked recursive records with generated C++ codecs.</summary>
internal static class Program
{
    /// <summary>Writes managed wire fixtures or verifies the C++ documents.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero on success, or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        var direct = new Node
        {
            Value = -19,
            Next = new Node { Value = 203 },
            Children = [null, new Node { Value = 71 }],
            ByKey = new() { [-31] = new Node { Value = 37 } }
        };
        var version = new VersionNode
        {
            Value = -89,
            Next = new VersionNode { Value = 97, Next = new VersionNode { Value = 101 } }
        };
        var mutual = new Envelope
        {
            Entry = new Left { Children = new() { [31] = new Right { Links = [new Left(), null] } } }
        };

        if (args is ["write", var output])
        {
            Directory.CreateDirectory(output);
            var utf8 = new ArrayBufferWriter<byte>();
            LuminPackSerializer.SerializeJson<Node, LuminPackUtf8LengthOptions>(direct, utf8);
            Store(output, "direct", LuminPackSerializer.Serialize(direct), LuminPackSerializer.SerializeJson(direct), utf8.WrittenSpan);
            utf8.Clear();
            LuminPackSerializer.SerializeJson<VersionNode, LuminPackUtf8LengthOptions>(version, utf8);
            Store(output, "version", LuminPackSerializer.Serialize(version), LuminPackSerializer.SerializeJson(version), utf8.WrittenSpan);
            utf8.Clear();
            LuminPackSerializer.SerializeJson<Envelope, LuminPackUtf8LengthOptions>(mutual, utf8);
            Store(output, "mutual", LuminPackSerializer.Serialize(mutual), LuminPackSerializer.SerializeJson(mutual), utf8.WrittenSpan);

            var exact = new Node { Next = new Node { Next = new Node { Next = new Node() } } };
            byte[] atLimit = LuminPackSerializer.Serialize(exact);
            _ = LuminPackSerializer.Deserialize<Node>(atLimit);
            File.WriteAllBytes(Path.Combine(output, "default-depth.bin"), atLimit);
            byte[] tooDeep = LuminPackSerializer.Serialize(new Node { Next = exact });
            Reject(() => _ = LuminPackSerializer.Deserialize<Node>(tooDeep));
            File.WriteAllBytes(Path.Combine(output, "default-deep.bin"), tooDeep);

            byte[] deepVersion = LuminPackSerializer.Serialize(new VersionNode { Next = new VersionNode { Next = version } });
            _ = LuminPackSerializer.Deserialize<VersionNode>(deepVersion);
            File.WriteAllBytes(Path.Combine(output, "version-deep.bin"), deepVersion);
            return 0;
        }

        if (args is ["read", var input])
        {
            var directBinary = LuminPackSerializer.Deserialize<Node>(File.ReadAllBytes(Path.Combine(input, "direct.bin")));
            var directJson = LuminPackSerializer.DeserializeJson<Node>(File.ReadAllBytes(Path.Combine(input, "direct.json")));
            Node? directUtf8 = null;
            LuminPackSerializer.DeserializeJson<Node?, LuminPackUtf8LengthOptions>(File.ReadAllBytes(Path.Combine(input, "direct.utf8.json")), ref directUtf8);
            Verify(LuminPackSerializer.SerializeJson(direct), LuminPackSerializer.SerializeJson(directBinary),
                LuminPackSerializer.SerializeJson(directJson), LuminPackSerializer.SerializeJson(directUtf8));

            var versionBinary = LuminPackSerializer.Deserialize<VersionNode>(File.ReadAllBytes(Path.Combine(input, "version.bin")));
            var versionJson = LuminPackSerializer.DeserializeJson<VersionNode>(File.ReadAllBytes(Path.Combine(input, "version.json")));
            VersionNode? versionUtf8 = null;
            LuminPackSerializer.DeserializeJson<VersionNode?, LuminPackUtf8LengthOptions>(File.ReadAllBytes(Path.Combine(input, "version.utf8.json")), ref versionUtf8);
            Verify(LuminPackSerializer.SerializeJson(version), LuminPackSerializer.SerializeJson(versionBinary),
                LuminPackSerializer.SerializeJson(versionJson), LuminPackSerializer.SerializeJson(versionUtf8));

            var mutualBinary = LuminPackSerializer.Deserialize<Envelope>(File.ReadAllBytes(Path.Combine(input, "mutual.bin")));
            var mutualJson = LuminPackSerializer.DeserializeJson<Envelope>(File.ReadAllBytes(Path.Combine(input, "mutual.json")));
            Envelope? mutualUtf8 = null;
            LuminPackSerializer.DeserializeJson<Envelope?, LuminPackUtf8LengthOptions>(File.ReadAllBytes(Path.Combine(input, "mutual.utf8.json")), ref mutualUtf8);
            Verify(LuminPackSerializer.SerializeJson(mutual), LuminPackSerializer.SerializeJson(mutualBinary),
                LuminPackSerializer.SerializeJson(mutualJson), LuminPackSerializer.SerializeJson(mutualUtf8));
            Console.WriteLine(".NET verified the C++ recursive Binary and JSON documents.");
            return 0;
        }

        Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Recursive <read|write> <directory>");
        return 2;
    }

    /// <summary>Stores managed Binary and both JSON encodings.</summary>
    /// <param name="directory">The destination directory.</param>
    /// <param name="name">The common basename.</param>
    /// <param name="binary">The binary document.</param>
    /// <param name="json">The managed JSON document.</param>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    private static void Store(string directory, string name, byte[] binary, string json, ReadOnlySpan<byte> utf8)
    {
        File.WriteAllBytes(Path.Combine(directory, name + ".bin"), binary);
        File.WriteAllBytes(Path.Combine(directory, name + ".json"), Encoding.Unicode.GetBytes(json));
        File.WriteAllBytes(Path.Combine(directory, name + ".utf8.json"), utf8);
    }

    /// <summary>Checks that the managed reader rejects nesting above its configured depth.</summary>
    /// <param name="read">The managed read to try.</param>
    private static void Reject(Action read)
    {
        try
        {
            read();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("The managed recursive read accepted excessive depth.");
    }

    /// <summary>Compares the decoded member trees.</summary>
    /// <param name="expected">The original fixture.</param>
    /// <param name="binary">The decoded binary tree.</param>
    /// <param name="json">The decoded JSON tree.</param>
    /// <param name="utf8">The tree decoded from UTF-8 JSON.</param>
    private static void Verify(string expected, string binary, string json, string utf8)
    {
        if (binary != expected || json != expected || utf8 != expected)
            throw new InvalidDataException("The decoded recursive document differs from the original fixture.");
    }
}

/// <summary>Combines direct recursion with list and dictionary elements.</summary>
[LuminPackable, LuminPackOption(MaxDepth = 4)]
public sealed partial class Node
{
    public int Value;
    public Node? Next;
    public List<Node?>? Children;
    public Dictionary<int, Node?>? ByKey;
}

/// <summary>Recurses into a Version member with an independent payload window.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed partial class VersionNode
{
    public int Value;
    public VersionNode? Next;
}

/// <summary>Owns an operation-wide depth limit for its recursive children.</summary>
[LuminPackable, LuminPackOption(MaxDepth = 8)]
public sealed partial class Envelope
{
    public Left? Entry;
}

/// <summary>Reaches its own type through a dictionary and a list.</summary>
[LuminPackable]
public sealed partial class Left
{
    public Dictionary<int, Right?>? Children;
}

/// <summary>Reaches its own type through a list and a dictionary.</summary>
[LuminPackable]
public sealed partial class Right
{
    public List<Left?>? Links;
}

/// <summary>Preserves its child's independent managed Binary depth policy.</summary>
[LuminPackable, LuminPackOption(UntrustedData = false)]
public sealed partial class TrustedEnvelope
{
    public Node? Entry;
}

/// <summary>Uses an independently scoped checked reader at each recursive Version member.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, MaxDepth = 4)]
public sealed partial class CheckedVersionNode
{
    public int Value;
    public CheckedVersionNode? Next;
}
