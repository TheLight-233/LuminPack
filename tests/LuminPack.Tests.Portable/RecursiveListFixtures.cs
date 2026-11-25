using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackPortableFixtures;

/// <summary>Managed documents for recursive list storage and terminal Union members in an owning tree.</summary>
internal static class RecursiveListFixtures
{
    /// <summary>Produces the oracle documents or verifies the returned Binary documents.</summary>
    /// <param name="directory">The exchange directory.</param>
    /// <param name="write">Whether to emit the inputs.</param>
    internal static void Exchange(string directory, bool write)
    {
        Directory.CreateDirectory(directory);
        var list = new SelfList { new SelfList(), null! };
        var tracked = new SharedList();
        tracked.Add(tracked);
        tracked.Add(null!);
        var wide = new SharedList();
        wide.AddRange(Enumerable.Repeat<SharedList>(null!, 255));
        var tree = new UnionTree { Child = new UnionTree { Choice = new Leaf { Value = 123 } } };
        var buffer = new ArrayBufferWriter<byte>();
        Check("list", LuminPackSerializer.Serialize(list));
        Check("tracked", LuminPackSerializer.Serialize(tracked));
        Check("wide", LuminPackSerializer.Serialize(wide));
        Check("tree", LuminPackSerializer.Serialize(tree));
        if (write)
        {
            LuminPackSerializer.SerializeJson<SelfList, LuminPackUtf8LengthOptions>(list, buffer);
            Save("list");
            LuminPackSerializer.SerializeJson<SharedList, LuminPackUtf8LengthOptions>(tracked, buffer);
            Save("tracked");
            LuminPackSerializer.SerializeJson<SharedList, LuminPackUtf8LengthOptions>(wide, buffer);
            Save("wide");
            File.WriteAllBytes(Path.Combine(directory, "invalid.bin"), [0, 0, 255, 255, 255, 255]);
            LuminPackSerializer.SerializeJson<UnionTree, LuminPackUtf8LengthOptions>(tree, buffer);
            Save("tree");
        }
        else
        {
            var decoded = LuminPackSerializer.Deserialize<SharedList>(File.ReadAllBytes(Path.Combine(directory, "tracked.bin")));
            if (decoded is not { Count: 2 } || !ReferenceEquals(decoded, decoded[0]) || decoded[1] != null)
                throw new InvalidDataException("Lost the recursive list identity.");
        }

        void Check(string name, byte[] bytes)
        {
            string path = Path.Combine(directory, name + ".bin");
            if (write) File.WriteAllBytes(path, bytes);
            else if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                throw new InvalidDataException("Unexpected recursive document for " + name);
        }

        void Save(string name)
        {
            File.WriteAllBytes(Path.Combine(directory, name + ".json"), buffer.WrittenSpan.ToArray());
            buffer.Clear();
        }
    }
}

[LuminPackable] public sealed partial class SelfList : List<SelfList>;
[LuminPackable, LuminPackOption(TrackReferences = true)] public sealed partial class SharedList : List<SharedList>;
[LuminPackable, LuminPackUnion(41, typeof(Leaf))] public partial interface ILeaf;
[LuminPackable] public sealed partial class Leaf : ILeaf { public int Value; }
[LuminPackable] public sealed partial class UnionTree
{
    public UnionTree? Child;
    public ILeaf? Choice;
}
