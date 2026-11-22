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
            Console.WriteLine(".NET verified the C++ recursive subtrees and tracked identities.");
            return 0;
        }

        Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.RecursiveComposition <read|write> <directory>");
        return 2;
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

/// <summary>Exercises the diagnostic for a tracked descendant of an owning recursive record.</summary>
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
