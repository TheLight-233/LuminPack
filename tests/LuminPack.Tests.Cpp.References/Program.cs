using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackCppReferenceFixtures;

/// <summary>Exchanges recursive and shared reference graphs with the generated C++ models.</summary>
internal static partial class Program
{
    /// <summary>Writes the managed documents or verifies the native documents.</summary>
    /// <param name="args">The requested operation and document directory.</param>
    /// <returns>Zero for success or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["read" or "write", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.References <read|write> <directory>");
            return 2;
        }

        var first = new Node { Value = 17, Label = "alpha" };
        var second = new Node { Value = 29, Label = "café" };
        first.Next = second;
        second.Next = first;
        first.Peer = new Peer { Home = first, Rank = -7 };
        second.Peer = first.Peer;
        var version = new VersionNode { Count = 37 };
        version.Next = version;
        version.Items = [version, null, version];
        var document = new Envelope
        {
            Alternate = first,
            Original = first,
            Items = Enumerable.Range(0, 130).Select(index => new Node { Value = index + 1000 }).ToList<Node?>(),
            ByName = new Dictionary<string, Node?> { ["root"] = first },
            Version = version,
            Pick = first
        };
        document.Items.Add(document.Items[127]);
        document.Items.Add(second);

        bool write = args[0] == "write";
        if (write)
            Directory.CreateDirectory(directory);
        Exchange(directory, write, "node", first, VerifyNode);
        Exchange<Node?>(directory, write, "null-node", null, value => Require(value == null));
        Exchange(directory, write, "version-node", version, VerifyVersion);
        Exchange(directory, write, "envelope", document, VerifyEnvelope);
        Exchange<IWrapper>(directory, write, "rtti-wrapper", new Wrapper { Value = first }, VerifyWrapper);
        ExchangeTrackedUnions(directory, write);
        Console.WriteLine(write ? ".NET wrote the reference graph fixtures." : ".NET verified the C++ reference graph documents.");
        return 0;
    }

    /// <summary>Exchanges one model in binary and both JSON encodings.</summary>
    /// <typeparam name="T">The fixture payload type.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="write">Whether to write the managed files.</param>
    /// <param name="name">The common document basename.</param>
    /// <param name="original">The fixture to write.</param>
    /// <param name="verify">The graph identity assertions.</param>
    private static void Exchange<T>(string directory, bool write, string name, T original, Action<T> verify)
    {
        string binary = Path.Combine(directory, name + ".bin");
        string utf8 = Path.Combine(directory, name + ".utf8.json");
        string utf16 = Path.Combine(directory, name + ".utf16.json");
        if (write)
        {
            File.WriteAllBytes(binary, LuminPackSerializer.Serialize(original));
            var buffer = new ArrayBufferWriter<byte>();
            graphUsesUtf8 = true;
            LuminPackSerializer.SerializeJson<T, LuminPackUtf8LengthOptions>(original, buffer);
            File.WriteAllBytes(utf8, buffer.WrittenSpan.ToArray());
            buffer.Clear();
            graphUsesUtf8 = false;
            LuminPackSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(original, buffer);
            File.WriteAllBytes(utf16, buffer.WrittenSpan.ToArray());
            return;
        }

        verify(LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(binary)));
        T value = default!;
        graphUsesUtf8 = true;
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf8LengthOptions>(File.ReadAllBytes(utf8), ref value);
        verify(value);
        value = default!;
        graphUsesUtf8 = false;
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf16LengthOptions>(File.ReadAllBytes(utf16), ref value);
        verify(value);
    }

    /// <summary>Checks a mutual cycle and a shared instance with another concrete type.</summary>
    /// <param name="value">The decoded reference root.</param>
    private static void VerifyNode(Node value)
    {
        Require(value is { Value: 17, Label: "alpha", Next: { Value: 29, Label: "café" }, Peer.Rank: -7 });
        Require(ReferenceEquals(value, value.Next!.Next) && ReferenceEquals(value, value.Peer!.Home));
        Require(ReferenceEquals(value.Peer, value.Next.Peer));
    }

    /// <summary>Checks a Version cycle through both simple and collection members.</summary>
    /// <param name="value">The decoded versioned root.</param>
    private static void VerifyVersion(VersionNode value)
    {
        Require(value is { Count: 37, Items.Count: 3 } && ReferenceEquals(value, value.Next));
        Require(ReferenceEquals(value, value.Items![0]) && value.Items[1] == null && ReferenceEquals(value, value.Items[2]));
    }

    /// <summary>Checks sharing across profiles, collections and multiple reference-id widths.</summary>
    /// <param name="value">The decoded ordinary envelope.</param>
    private static void VerifyEnvelope(Envelope value)
    {
        Require(value is { Alternate: not null, Items.Count: 132, ByName: not null, Version: not null });
        VerifyNode(value.Alternate!);
        VerifyVersion(value.Version!);
        Require(ReferenceEquals(value.Alternate, value.Original) && ReferenceEquals(value.Alternate, value.ByName!["root"]));
        Require(value.Items![127]?.Value == 1127 && ReferenceEquals(value.Items[127], value.Items[130]));
        Require(ReferenceEquals(value.Alternate!.Next, value.Items[131]));
        Require(ReferenceEquals(value.Alternate, value.Pick));
    }

    /// <summary>Checks that a generated polymorphic case uses the enclosing reference operation.</summary>
    /// <param name="value">The decoded polymorphic root.</param>
    private static void VerifyWrapper(IWrapper value)
    {
        Require(value is Wrapper { Value: not null });
        VerifyNode(((Wrapper)value).Value!);
    }

    /// <summary>Throws when a graph does not preserve the managed fixture's values and identity.</summary>
    /// <param name="condition">The assertion result.</param>
    private static void Require(bool condition)
    {
        if (!condition)
            throw new InvalidDataException("The C++ reference graph differs from the managed fixture.");
    }
}

/// <summary>A positional reference-tracked record with one mutually recursive component.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true)]
public sealed class Node : IChoice
{
    public int Value;
    public string? Label;
    public Node? Next;
    public Peer? Peer;
}

/// <summary>A statically dispatched Union sharing its tracked case with ordinary member slots.</summary>
[LuminPackable, LuminPackUnion(11, typeof(Node)), LuminPackUnion(12, typeof(OtherChoice))]
public interface IChoice
{
}

/// <summary>An ordinary alternative uses its existing positional Union payload.</summary>
[LuminPackable]
public sealed class OtherChoice : IChoice
{
    public int Value;
}

/// <summary>A second type in the same reference table.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true)]
public sealed class Peer
{
    public Node? Home;
    public int Rank;
}

/// <summary>A tracked record that participates in existing Version scope and alias handling.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)]
public sealed class VersionNode
{
    [LuminPackOldName("PreviousCount")] public int Count;
    public VersionNode? Next;
    public List<VersionNode?>? Items;
}

/// <summary>An ordinary root that carries tracked values through several statically bound codecs.</summary>
[LuminPackable]
public sealed class Envelope
{
    [LuminPackOption(StringRecording = LuminPackStringRecording.VarInt)] public Node? Alternate;
    public Node? Original;
    public List<Node?>? Items;
    public Dictionary<string, Node?>? ByName;
    public VersionNode? Version;
    public IChoice? Pick;
}

/// <summary>A polymorphic root with an ordinary case that refers to a tracked graph.</summary>
[LuminPackable, LuminPackUnion(21, typeof(Wrapper))]
public interface IWrapper
{
}

/// <summary>Places a tracked graph inside the existing positional Union payload.</summary>
[LuminPackable]
public sealed class Wrapper : IWrapper
{
    public Node? Value;
}
