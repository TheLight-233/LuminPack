#if LUMINPACK_NEXT_GENERATOR
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

// A class whose payload is only a supported list base: the empty record shape used to drop every element on read.
/// <summary>Provides a recursive list-derived payload for the Next sequence base path.</summary>
[LuminPackable]
public sealed partial class NextShapeTree : List<NextShapeTree>
{
    /// <summary>Builds a tree with the requested depth and fan-out.</summary>
    /// <param name="depth">The remaining levels below the new node.</param>
    /// <param name="width">The number of children per node.</param>
    /// <returns>The root node.</returns>
    public static NextShapeTree Create(int depth, int width)
    {
        var tree = new NextShapeTree();
        for (int index = 0; depth > 0 && index < width; index++)
            tree.Add(Create(depth - 1, width));
        return tree;
    }
}

// A generic type as a closed case of a non-generic root: the open arity used to reject the closed construction.
/// <summary>Provides a non-generic Union root whose case closes a generic type.</summary>
[LuminPackable]
[LuminPackUnion(0, typeof(NextRpcOk<List<NextRpcItem>>))]
[LuminPackUnion(1, typeof(NextRpcNotFound))]
public partial interface INextRpcResult
{
}

/// <summary>Provides the generic success case.</summary>
/// <typeparam name="T">The payload carried by the case.</typeparam>
[LuminPackable]
public sealed partial class NextRpcOk<T> : INextRpcResult
{
    /// <summary>Gets or sets the payload.</summary>
    public T Value { get; set; }
}

/// <summary>Provides the non-generic failure case.</summary>
[LuminPackable]
public sealed partial class NextRpcNotFound : INextRpcResult
{
    /// <summary>Gets or sets the resource name.</summary>
    public string Resource { get; set; } = string.Empty;
}

/// <summary>Provides the element carried by the generic success case.</summary>
[LuminPackable]
public sealed partial class NextRpcItem
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id { get; set; }
}

// A closed generic abstract base without discovered cases: forwarding to an unpublished generic entry used to emit CS0117.
/// <summary>Provides an abstract generic base closed over a value tuple.</summary>
/// <typeparam name="T">The stored value.</typeparam>
[LuminPackable]
public abstract partial class NextShapeBox<T>
{
    /// <summary>Gets or sets the stored value.</summary>
    public T Value { get; set; }
}

/// <summary>Provides an unannotated subclass of the closed generic base.</summary>
public sealed partial class NextShapeTup : NextShapeBox<(int, long)>
{
}

/// <summary>Holds the closed generic base that has no published cases.</summary>
[LuminPackable]
public sealed partial class NextShapeBoxBatch
{
    /// <summary>Gets or sets the closed base values.</summary>
    public NextShapeBox<(int, long)>[] Items { get; set; } = [];
}

/// <summary>Protects the Next generator shapes that previously failed to compile or silently dropped data.</summary>
public sealed class NextDerivedShapesTest
{
    /// <summary>Round-trips a list-derived payload in binary and JSON without losing elements.</summary>
    [Fact]
    public void ListDerivedPayloadRoundTrips()
    {
        var value = NextShapeTree.Create(3, 3);
        Assert.Equal(40, Count(value, 0));

        var bytes = LuminPackSerializer.Serialize(value);
        NextShapeTree binary = null!;
        LuminPackSerializer.Deserialize(bytes, ref binary);
        Assert.Equal(Count(value, 0), Count(binary, 0));

        string json = LuminPackSerializer.SerializeJson(value);
        NextShapeTree restored = null!;
        LuminPackSerializer.DeserializeJson(json, ref restored);
        Assert.Equal(Count(value, 0), Count(restored, 0));
    }

    /// <summary>Round-trips a closed generic case of a non-generic Union root.</summary>
    [Fact]
    public void ClosedGenericCaseRoundTrips()
    {
        INextRpcResult value = new NextRpcOk<List<NextRpcItem>>
        {
            Value = [new NextRpcItem { Id = 7 }],
        };

        INextRpcResult binary = null!;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(value), ref binary);
        var ok = Assert.IsType<NextRpcOk<List<NextRpcItem>>>(binary);
        Assert.Equal(7, Assert.Single(ok.Value).Id);

        INextRpcResult restored = null!;
        LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(value), ref restored);
        Assert.Equal(7, Assert.Single(Assert.IsType<NextRpcOk<List<NextRpcItem>>>(restored).Value).Id);
    }

    /// <summary>Keeps a closed generic base without published cases on the runtime registration path.</summary>
    [Fact]
    public void ClosedGenericBaseWithoutCasesFallsBackToRegistration()
    {
        var batch = new NextShapeBoxBatch { Items = [new NextShapeTup { Value = (1, 2L) }] };
        // The generated member must reference a registered formatter instead of a generic entry the definition never
        // published; the unavailable registration is reported at runtime, as the previous generation did.
        var error = Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Serialize(batch));
        Assert.Contains("No formatter is registered", error.Message);
    }

    /// <summary>Counts the nodes of a recursive list-derived value.</summary>
    /// <param name="tree">The node to count.</param>
    /// <param name="total">The count of already visited nodes.</param>
    /// <returns>The node count including every descendant.</returns>
    private static int Count(NextShapeTree tree, int total)
    {
        total++;
        foreach (NextShapeTree child in tree)
            total = Count(child, total);
        return total;
    }
}
#endif
