using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackCppRecursiveCompositionFixtures;

/// <summary>Exchanges owning recursive subtrees and tracked identities in the same document.</summary>
internal static class Program
{
    /// <summary>Writes the managed document or verifies C++ output.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero on success, or two when the arguments are invalid.</returns>
    private static int Main(string[] args)
    {
        var tree = new Tree { Text = "caf\u00e9", Children = [null, new Tree { Text = "\u503c" }] };
        var identity = new Identity { Value = 71 };
        identity.Self = identity;
        var owner = new TrackedOwner { Tree = tree, Link = identity };
        var expected = new Document
        {
            Direct = tree,
            Owner = owner,
            Alias = owner,
            StaticCase = new OrdinaryCase { Tree = tree },
            TrackedCase = identity,
            Shared = identity
        };
        string json = LuminPackSerializer.SerializeJson(expected);
        if (args is ["write", var output])
        {
            Directory.CreateDirectory(output);
            var utf8 = new ArrayBufferWriter<byte>();
            LuminPackSerializer.SerializeJson<Document, LuminPackUtf8LengthOptions>(expected, utf8);
            File.WriteAllBytes(Path.Combine(output, "composition.bin"), LuminPackSerializer.Serialize(expected));
            File.WriteAllBytes(Path.Combine(output, "composition.json"), Encoding.Unicode.GetBytes(json));
            File.WriteAllBytes(Path.Combine(output, "composition.utf8.json"), utf8.WrittenSpan);
            ExchangeOutgoing(output, write: true);
            return 0;
        }

        if (args is ["read", var input])
        {
            Verify(LuminPackSerializer.Deserialize<Document>(File.ReadAllBytes(Path.Combine(input, "composition.bin"))), json);
            Verify(LuminPackSerializer.DeserializeJson<Document>(File.ReadAllBytes(Path.Combine(input, "composition.json"))), json);
            Document? decoded = null;
            LuminPackSerializer.DeserializeJson<Document?, LuminPackUtf8LengthOptions>(
                File.ReadAllBytes(Path.Combine(input, "composition.utf8.json")), ref decoded);
            Verify(decoded, json);
            ExchangeOutgoing(input, write: false);
            Console.WriteLine(".NET verified the C++ recursive subtrees and tracked identities.");
            return 0;
        }

        Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.RecursiveComposition <read|write> <directory>");
        return 2;
    }

    /// <summary>Exchanges Version recursion with tracked values defined inside and used outside its member windows.</summary>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="write">Whether to write the managed fixtures.</param>
    private static void ExchangeOutgoing(string directory, bool write)
    {
        var inner = new VersionLeaf { Value = 53 };
        var expected = new OutgoingDocument
        {
            Root = new OutgoingTree
            {
                Text = "root \u03a9",
                Leaf = inner,
                Children = [new OutgoingTree { Text = "child", Leaf = inner }, null],
                NestedOnly = new NestedLeaf { Number = -71 }
            },
            Shared = inner,
            Outside = new VersionLeaf { Value = -90 }
        };
        string canonical = LuminPackSerializer.SerializeJson(expected);
        if (write)
        {
            var utf8 = new ArrayBufferWriter<byte>();
            LuminPackSerializer.SerializeJson<OutgoingDocument, LuminPackUtf8LengthOptions>(expected, utf8);
            File.WriteAllBytes(Path.Combine(directory, "outgoing.bin"), LuminPackSerializer.Serialize(expected));
            File.WriteAllBytes(Path.Combine(directory, "outgoing.json"), Encoding.Unicode.GetBytes(canonical));
            File.WriteAllBytes(Path.Combine(directory, "outgoing.utf8.json"), utf8.WrittenSpan);
            return;
        }

        VerifyOutgoing(LuminPackSerializer.Deserialize<OutgoingDocument>(File.ReadAllBytes(Path.Combine(directory, "outgoing.bin"))), canonical);
        VerifyOutgoing(LuminPackSerializer.DeserializeJson<OutgoingDocument>(File.ReadAllBytes(Path.Combine(directory, "outgoing.json"))), canonical);
        OutgoingDocument? decoded = null;
        LuminPackSerializer.DeserializeJson<OutgoingDocument?, LuminPackUtf8LengthOptions>(
            File.ReadAllBytes(Path.Combine(directory, "outgoing.utf8.json")), ref decoded);
        VerifyOutgoing(decoded, canonical);
    }

    /// <summary>Checks references from the recursive members and the rest of the document.</summary>
    /// <param name="value">The decoded document.</param>
    /// <param name="canonical">The original JSON representation.</param>
    private static void VerifyOutgoing(OutgoingDocument? value, string canonical)
    {
        if (value is not { Root.Children: [{ Leaf: not null } child, null], Root.NestedOnly.Number: -71, Shared.Value: 53, Outside.Value: -90 } ||
            !ReferenceEquals(value.Shared, value.Root.Leaf) || !ReferenceEquals(value.Shared, child.Leaf) ||
            ReferenceEquals(value.Shared, value.Outside) || LuminPackSerializer.SerializeJson(value) != canonical)
            throw new InvalidDataException("The decoded recursive Version document changed a tracked identity.");
    }

    /// <summary>Checks the graph identity and independently decoded tree occurrences.</summary>
    /// <param name="value">The decoded document.</param>
    /// <param name="json">The original canonical JSON.</param>
    private static void Verify(Document? value, string json)
    {
        if (value is not { Direct: not null, Owner.Tree: not null, StaticCase: OrdinaryCase { Tree: not null } choice, Shared: not null } ||
            !ReferenceEquals(value.Owner, value.Alias) || !ReferenceEquals(value.Shared, value.TrackedCase) ||
            !ReferenceEquals(value.Shared, value.Owner.Link) || !ReferenceEquals(value.Shared, value.Shared.Self) ||
            ReferenceEquals(value.Direct, value.Owner.Tree) || ReferenceEquals(value.Direct, choice.Tree) ||
            LuminPackSerializer.SerializeJson(value) != json)
            throw new InvalidDataException("The decoded graph changed a tracked identity or an owning subtree.");
    }
}

/// <summary>Contains ordinary recursive records without reference tracking.</summary>
[LuminPackable]
public sealed partial class Tree
{
    public string? Text;
    public List<Tree?>? Children;
}

/// <summary>Owns a tree below one tracked record definition.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true)]
public sealed partial class TrackedOwner
{
    public Tree? Tree;
    public Identity? Link;
}

/// <summary>Tests an independent tracked cycle and a tracked Union case.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true)]
public sealed partial class Identity : IChoice
{
    public int Value;
    public Identity? Self;
}

/// <summary>Chooses between a tracked and an ordinary case.</summary>
[LuminPackable, LuminPackUnion(41, typeof(OrdinaryCase)), LuminPackUnion(42, typeof(Identity))]
public partial interface IChoice;

/// <summary>Owns a tree in a normal Union payload.</summary>
[LuminPackable]
public sealed partial class OrdinaryCase : IChoice
{
    public Tree? Tree;
}

/// <summary>Exposes owning copies and aliases through an ordinary document root.</summary>
[LuminPackable]
public sealed partial class Document
{
    public Tree? Direct;
    public TrackedOwner? Owner;
    public TrackedOwner? Alias;
    public IChoice? StaticCase;
    public IChoice? TrackedCase;
    public Identity? Shared;
}

/// <summary>Exercises the diagnostic for a recursively tracked descendant of an owning recursive record.</summary>
[LuminPackable]
public sealed partial class OutboundTree
{
    public OutboundTree? Child;
    public Identity? Link;
}

/// <summary>Exercises the diagnostic for a re-entered tracked ancestor.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true)]
public sealed partial class RecursiveTrackedOwner
{
    public RecursiveTrackedOwner? Next;
    public Tree? Tree;
}

/// <summary>Exercises the diagnostic for a Binary depth budget shared across the two ownership models.</summary>
[LuminPackable, LuminPackOption(MaxDepth = 8)]
public sealed partial class BoundedComposition
{
    public Tree? Tree;
    public Identity? Link;
}

/// <summary>Defines a tracked value inside an owning Version member and reuses its identity afterwards.</summary>
[LuminPackable]
public sealed partial class OutgoingDocument
{
    public OutgoingTree? Root;
    public VersionLeaf? Shared;
    public VersionLeaf? Outside;
}

/// <summary>Uses tracked leaves below an ordinary recursive Version record.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed partial class OutgoingTree
{
    public string? Text;
    public VersionLeaf? Leaf;
    public List<OutgoingTree?>? Children;
    public NestedLeaf? NestedOnly;
}

/// <summary>Participates in the Version registry while the references stay operation-wide.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)]
public sealed partial class VersionLeaf
{
    public int Value;
}

/// <summary>First becomes reachable while defining the ordinary recursive record.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true)]
public sealed partial class NestedLeaf
{
    public int Number;
}

/// <summary>Exercises the diagnostic for an independent limit below the recursive boundary.</summary>
[LuminPackable]
public sealed partial class BoundedLeafTree
{
    public BoundedLeafTree? Child;
    public BoundedLeaf? Leaf;
}

/// <summary>Declares a String limit that its unbounded root cannot project.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true, MaxStringLength = 4)]
public sealed partial class BoundedLeaf
{
    public string? Text;
}
