using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPackCppReferenceFixtures;

internal static partial class Program
{
    private const int GraphExternalTag = 600;
    private static bool graphUsesUtf8;

    /// <summary>Exchanges tracked, ordinary and runtime cases within the same Union graph.</summary>
    /// <param name="directory">The fixture document directory.</param>
    /// <param name="write">Whether to produce the managed documents.</param>
    private static void ExchangeTrackedUnions(string directory, bool write)
    {
        LuminPackSerializer.Register<IGraphChoice, GraphExternal>(
            GraphExternalTag, WriteGraphBinary, ReadGraphBinary, WriteGraphJson, ReadGraphJson);

        var node = new GraphNode { Id = 73 };
        var leaf = new GraphLeaf { Link = node, Value = -81 };
        node.Next = leaf;
        var version = new GraphVersion { Id = 79 };
        version.Next = version;
        Exchange<IGraphChoice>(directory, write, "tracked-case", node, VerifyGraphRoot);
        Exchange<IGraphChoice>(directory, write, "tracked-version-case", version, VerifyGraphVersion);
        Exchange<IGraphChoice?>(directory, write, "null-tracked-union", null, value => Require(value == null));
        var packet = new GraphPacket
        {
            Direct = node,
            ThroughUnion = node,
            Items = [node, leaf, null, version, new GraphExternal { Value = 97 }, node]
        };
        Exchange(directory, write, "tracked-union-packet", packet, VerifyGraphPacket);
    }

    /// <summary>Checks the tracked Union case and an ordinary case that points back to it.</summary>
    /// <param name="value">The decoded root case.</param>
    private static void VerifyGraphRoot(IGraphChoice value)
    {
        Require(value is GraphNode { Id: 73, Next: GraphLeaf { Value: -81 } });
        var node = (GraphNode)value;
        Require(ReferenceEquals(((GraphLeaf)node.Next!).Link, node));
    }

    /// <summary>Checks reference identity through a Version case in the same Union.</summary>
    /// <param name="value">The decoded versioned case.</param>
    private static void VerifyGraphVersion(IGraphChoice value)
        => Require(value is GraphVersion { Id: 79 } version && ReferenceEquals(version, version.Next));

    /// <summary>Checks shared identity, graph ownership and runtime dispatch through collection slots.</summary>
    /// <param name="value">The decoded containing record.</param>
    private static void VerifyGraphPacket(GraphPacket value)
    {
        Require(value is { Direct: not null, ThroughUnion: not null, Items.Count: 6 });
        VerifyGraphRoot(value.Direct!);
        var items = value.Items!;
        Require(ReferenceEquals(value.Direct, value.ThroughUnion) &&
            ReferenceEquals(value.Direct, items[0]) && ReferenceEquals(value.Direct, items[5]));
        Require(items[1] is GraphLeaf { Value: -81 } leaf && ReferenceEquals(leaf.Link, value.Direct));
        Require(!ReferenceEquals(value.Direct!.Next, items[1]) && items[2] == null);
        VerifyGraphVersion(items[3]!);
        Require(items[4] is GraphExternal { Value: 97 });
    }

    /// <summary>Writes the app-owned case tag and Binary payload.</summary>
    /// <param name="writer">The Binary writer after the escape hash.</param>
    /// <param name="value">The registered application case.</param>
    private static void WriteGraphBinary(ref LuminPackWriter writer, ref IGraphChoice value)
    {
        writer.WriteWideUnionHeader(GraphExternalTag);
        writer.Write(((GraphExternal)value).Value ^ 59);
    }

    /// <summary>Reads the replacement application case from a Binary payload.</summary>
    /// <param name="reader">The Binary reader after the numeric tag.</param>
    /// <param name="value">Receives the application case.</param>
    private static void ReadGraphBinary(ref LuminPackReader reader, ref IGraphChoice value)
    {
        int payload = 0;
        reader.Read(ref payload);
        value = new GraphExternal { Value = payload ^ 59 };
    }

    /// <summary>Writes an application case through the active fixture encoding.</summary>
    /// <param name="writer">The JSON writer at the Union value.</param>
    /// <param name="value">The registered application case.</param>
    private static void WriteGraphJson(ref LuminPackJsonWriter writer, ref IGraphChoice value)
    {
        int payload = ((GraphExternal)value).Value ^ 71;
        if (graphUsesUtf8)
            WriteGraphJson<LuminPackUtf8LengthOptions>(ref writer, payload);
        else
            WriteGraphJson<LuminPackUtf16LengthOptions>(ref writer, payload);
    }

    /// <summary>Writes the registered JSON envelope and scalar payload.</summary>
    /// <typeparam name="TProfile">The encoding used by the enclosing document.</typeparam>
    /// <param name="writer">The JSON writer at the Union value.</param>
    /// <param name="payload">The transformed application value.</param>
    private static void WriteGraphJson<TProfile>(ref LuminPackJsonWriter writer, int payload)
        where TProfile : struct, ILuminPackOptions
    {
        writer.WriteObjectStart<TProfile>();
        writer.WritePropertyName<TProfile>("$type");
        writer.Write<TProfile>(GraphExternalTag);
        writer.WritePropertyName<TProfile>("$value");
        writer.Write<TProfile>(payload);
        writer.WriteObjectEnd<TProfile>();
    }

    /// <summary>Reads the application case using the active fixture encoding.</summary>
    /// <param name="reader">The JSON reader positioned at the payload.</param>
    /// <param name="value">Receives the decoded application case.</param>
    private static void ReadGraphJson(ref LuminPackJsonReader reader, ref IGraphChoice value)
    {
        int payload = graphUsesUtf8 ? reader.Read<int, LuminPackUtf8LengthOptions>() :
            reader.Read<int, LuminPackUtf16LengthOptions>();
        value = new GraphExternal { Value = payload ^ 71 };
    }
}

/// <summary>A wide Union with both tracked and ordinary compiler-generated cases.</summary>
[LuminPackable, LuminPackWideTag, LuminPackUnion(31, typeof(GraphNode)), LuminPackUnion(32, typeof(GraphLeaf)),
    LuminPackUnion(33, typeof(GraphVersion))]
public interface IGraphChoice
{
}

/// <summary>A Default tracked case with an edge back to the Union that owns it.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true)]
public sealed class GraphNode : IGraphChoice
{
    public int Id;
    public IGraphChoice? Next;
}

/// <summary>An ordinary case borrows a tracked peer from the enclosing graph.</summary>
[LuminPackable]
public sealed class GraphLeaf : IGraphChoice
{
    public GraphNode? Link;
    public int Value;
}

/// <summary>A Version tracked case uses the full reference and Version record framing.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true, GeneratorType = GeneratorType.Version)]
public sealed class GraphVersion : IGraphChoice
{
    public int Id;
    public IGraphChoice? Next;
}

/// <summary>An application-owned derived case registered at runtime.</summary>
public sealed class GraphExternal : IGraphChoice
{
    public int Value;
}

/// <summary>A containing record sharing identities between direct and Union-typed slots.</summary>
[LuminPackable]
public sealed class GraphPacket
{
    public GraphNode? Direct;
    public IGraphChoice? ThroughUnion;
    public List<IGraphChoice?>? Items;
}
