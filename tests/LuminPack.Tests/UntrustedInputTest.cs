using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks untrusted reads: ordinary wire, read-site checks, declared limits and fail-closed input-selected shapes.</summary>
public class UntrustedInputTest
{
    /// <summary>Untrusted records use the ordinary wire format in both directions.</summary>
    [Fact]
    public void UntrustedRecordsUseOrdinaryWire()
    {
        var untrusted = new UntrustedRecord { Id = 7, Flag = true, Name = "name", Values = [1, 2, 3], Bits = [true, false, true], List = [false, true] };
        var trusted = new TrustedRecord { Id = 7, Flag = true, Name = "name", Values = [1, 2, 3], Bits = [true, false, true], List = [false, true] };
        byte[] bytes = LuminPackSerializer.Serialize(untrusted);
        Assert.Equal(LuminPackSerializer.Serialize(trusted), bytes);
        TrustedRecord? fromUntrusted = null;
        LuminPackSerializer.Deserialize(bytes, ref fromUntrusted);
        Assert.Equal(untrusted.Bits, fromUntrusted!.Bits);
        UntrustedRecord? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(trusted), ref copy));
        Assert.Equal(7, copy!.Id);
        Assert.True(copy.Flag);
        Assert.Equal("name", copy.Name);
        Assert.Equal(new[] { 1, 2, 3 }, copy.Values);
        Assert.Equal(new[] { true, false, true }, copy.Bits);
        Assert.Equal(new List<bool> { false, true }, copy.List);
    }

    /// <summary>Reading into an existing graph keeps the ordinary reuse rules.</summary>
    [Fact]
    public void UntrustedReadsReuseExistingStorage()
    {
        var value = new UntrustedRecord { Id = 1, Name = "same", Values = [4, 5], Bits = [true], List = [true] };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        var target = new UntrustedRecord { Name = "same", Values = new int[2], Bits = new bool[1], List = new List<bool>() };
        var (values, bits, list) = (target.Values, target.Bits, target.List);
        UntrustedRecord? reused = target;
        LuminPackSerializer.Deserialize(bytes, ref reused);
        Assert.Same(target, reused);
        Assert.Same(values, reused!.Values);
        Assert.Same(bits, reused.Bits);
        Assert.Same(list, reused.List);
        Assert.Equal(new[] { 4, 5 }, reused.Values);
    }

    /// <summary>Every truncated record fails with a catchable exception.</summary>
    [Fact]
    public void UntrustedReadsRejectEveryTruncation()
    {
        var value = new UntrustedRecord { Id = 9, Flag = true, Name = "truncate", Values = [1, 2], Bits = [true, false], List = [true],
            Child = new UntrustedChild { Number = 3, Flag = true, Text = "child" } };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] prefix = bytes.AsSpan(0, length).ToArray();
            UntrustedRecord? target = null;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(prefix, ref target));
        }
    }

    /// <summary>Object markers must equal the declared member count or the null marker.</summary>
    [Fact]
    public void UntrustedMarkersRequireMemberCount()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new UntrustedRecord());
        foreach (byte marker in new byte[] { 0, 6, 8, 250, 254 })
        {
            bytes[0] = marker;
            UntrustedRecord? target = null;
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        }
        UntrustedRecord? empty = null;
        Assert.Equal(1, LuminPackSerializer.Deserialize(new byte[] { 255 }, ref empty));
        Assert.Null(empty);
    }

    /// <summary>Boolean fields reject every noncanonical storage value, through batches and nested records.</summary>
    [Fact]
    public void UntrustedBooleanFieldsRejectNoncanonicalStorage()
    {
        var value = new UntrustedScalars { Number = 5, Flag = true, Stamp = 11, Last = false };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        // Marker, Number (4), Flag, Stamp (8), Last.
        foreach (int offset in new[] { 5, 14 })
            foreach (byte invalid in new byte[] { 2, 3, 128, 255 })
            {
                byte[] forged = (byte[])bytes.Clone();
                forged[offset] = invalid;
                UntrustedScalars? target = null;
                Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(forged, ref target));
            }
        var parent = new UntrustedRecord { Child = new UntrustedChild { Flag = true } };
        byte[] nested = LuminPackSerializer.Serialize(parent);
        // The child's Flag precedes its empty Text header.
        int flag = nested.Length - 4 - 1;
        Assert.Equal(1, nested[flag]);
        nested[flag] = 2;
        UntrustedRecord? graph = null;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(nested, ref graph));
    }

    /// <summary>Boolean arrays and lists reject a noncanonical element at every position and vector lane.</summary>
    [Fact]
    public void UntrustedBooleanSequencesRejectEveryLane()
    {
        foreach (int count in new[] { 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63, 64, 65, 127, 128, 130 })
        {
            var value = new UntrustedBooleans { Bits = Enumerable.Range(0, count).Select(index => index % 3 == 0).ToArray(), List = Enumerable.Repeat(true, count).ToList() };
            byte[] bytes = LuminPackSerializer.Serialize(value);
            UntrustedBooleans? valid = null;
            LuminPackSerializer.Deserialize(bytes, ref valid);
            Assert.Equal(value.Bits, valid!.Bits);
            int arrayStart = 1 + 4, listStart = arrayStart + count + 4;
            for (int index = 0; index < count; index++)
                foreach (int start in new[] { arrayStart, listStart })
                {
                    byte[] forged = (byte[])bytes.Clone();
                    forged[start + index] = (byte)(index % 2 == 0 ? 2 : 0x81);
                    UntrustedBooleans? target = null;
                    Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(forged, ref target));
                }
        }
    }

    /// <summary>Collection and string limits apply before a read can publish an oversized value.</summary>
    [Fact]
    public void UntrustedLimitsRejectOversizedValues()
    {
        var within = new UntrustedLimited { Values = [1, 2, 3, 4], Items = [new UntrustedChild(), new UntrustedChild()], Text = new string('x', 8) };
        byte[] bytes = LuminPackSerializer.Serialize(within);
        UntrustedLimited? copy = null;
        LuminPackSerializer.Deserialize(bytes, ref copy);
        Assert.Equal(4, copy!.Values.Length);
        foreach (var oversized in new[]
                 {
                     new UntrustedLimited { Values = [1, 2, 3, 4, 5] },
                     new UntrustedLimited { Items = Enumerable.Range(0, 5).Select(_ => new UntrustedChild()).ToList() },
                     new UntrustedLimited { Text = new string('x', 9) },
                     new UntrustedLimited { Map = Enumerable.Range(0, 5).ToDictionary(index => index, index => "v") }
                 })
        {
            byte[] forged = LuminPackSerializer.Serialize(oversized);
            UntrustedLimited? target = null;
            Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(forged, ref target));
        }
    }

    /// <summary>A forged count larger than the remaining input fails before allocation.</summary>
    [Fact]
    public void UntrustedCountsCannotExceedTheInput()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new UntrustedLimited());
        // Marker, then the Values count.
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 1);
        UntrustedLimited? target = null;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes, ref target));
    }

    /// <summary>Recursive values count their nesting against the frozen depth limit.</summary>
    [Fact]
    public void UntrustedRecursionHonorsTheDepthLimit()
    {
        UntrustedNode Chain(int length)
        {
            var head = new UntrustedNode { Value = 0 };
            var current = head;
            for (int i = 1; i < length; i++) current = current.Next = new UntrustedNode { Value = i };
            return head;
        }
        UntrustedNode? copy = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(Chain(4)), ref copy);
        Assert.Equal(3, copy!.Next!.Next!.Next!.Value);
        UntrustedNode? deep = null;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(Chain(5)), ref deep));
        // Sequential reads start from zero again, and a nested recursive type uses the root's limit.
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(Chain(4)), ref copy);
        var graph = new UntrustedGraph { Node = new TrustedNode { Next = new TrustedNode { Next = new TrustedNode() } } };
        UntrustedGraph? graphCopy = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(graph), ref graphCopy);
        graph.Node.Next.Next.Next = new TrustedNode();
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(graph), ref graphCopy));
        // The nested type's own trusted entry is unchanged.
        TrustedNode? trusted = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(graph.Node), ref trusted);
        Assert.NotNull(trusted!.Next!.Next!.Next);
    }

    /// <summary>A trusted parent keeps the checks of an untrusted child.</summary>
    [Fact]
    public void TrustedParentKeepsUntrustedChildChecks()
    {
        var parent = new TrustedParent { Before = 1, Child = new UntrustedScalars { Flag = true }, After = 2 };
        byte[] bytes = LuminPackSerializer.Serialize(parent);
        TrustedParent? copy = null;
        LuminPackSerializer.Deserialize(bytes, ref copy);
        Assert.Equal(2, copy!.After);
        // Parent marker, Before (4), child marker, Number (4), Flag.
        bytes[1 + 4 + 1 + 4] = 7;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref copy));
    }

    /// <summary>Unreviewed shapes fail closed in both directions.</summary>
    [Fact]
    public void UntrustedUnsupportedShapesFailClosed()
    {
        Assert.Throws<NotSupportedException>(() => LuminPackSerializer.Serialize(new UntrustedDangerous()));
        UntrustedDangerous? target = null;
        Assert.Throws<NotSupportedException>(() => LuminPackSerializer.Deserialize(new byte[] { 1, 255 }, ref target));
    }

    /// <summary>The encoded input cap applies before decoding or staging.</summary>
    /// <returns>The asynchronous ingestion verification.</returns>
    [Fact]
    public async Task UntrustedInputCapBoundsIngestion()
    {
        UntrustedLimited? target = null;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(new byte[2048], ref target));
        using var stream = new MemoryStream(new byte[64 * 1024]);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await LuminPackSerializer.DeserializeAsync(stream, (UntrustedLimited)null!));
        Assert.InRange(stream.Position, 1, 1024 + 8192);
    }

    /// <summary>Reads without any declaration are untrusted: they check Boolean domains and markers.</summary>
    [Fact]
    public void DefaultReadsAreUntrusted()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new DefaultScalars { Number = 3, Flag = true });
        DefaultScalars? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.True(copy!.Flag);
        bytes[^1] = 2;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref copy));
        bytes[0] = 9;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref copy));
    }

    /// <summary>Version-tolerant records check their members like ordinary records, including when nested in an object.</summary>
    [Fact]
    public void VersionTolerantReadsAreChecked()
    {
        var holder = new VersionHolder { Before = 5, Versioned = new SafeVersioned { Id = 1, Flag = true, Name = "v" }, After = 6 };
        byte[] bytes = LuminPackSerializer.Serialize(holder);
        // A version-tolerant member keeps its own record format inside an ordinary object.
        Assert.Equal(251, bytes[1 + 4]);
        VersionHolder? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.Equal("v", copy!.Versioned!.Name);
        Assert.Equal(6, copy.After);
        byte[] record = LuminPackSerializer.Serialize(new SafeVersioned { Flag = true });
        record[Difference(record, LuminPackSerializer.Serialize(new SafeVersioned()))] = 3;
        SafeVersioned? versioned = null;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(record, ref versioned));
    }

    /// <summary>Reference-tracked records reject back references of another type and raw slots of another width.</summary>
    [Fact]
    public void ReferenceTrackedReadsAreChecked()
    {
        var owner = new SafeOwner { Leaf = new SafeLeaf { Value = 4 } };
        byte[] bytes = LuminPackSerializer.Serialize(owner);
        SafeOwner? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.Equal(4, copy!.Leaf!.Value);
        // The last slot is a null leaf behind its four-byte length; point it at the owner itself, reference id zero.
        Assert.Equal(new byte[] { 1, 0, 0, 0, 255 }, bytes[^5..]);
        byte[] forged = [.. bytes[..^5], 2, 0, 0, 0, 250, 0];
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(forged, ref copy));
        // A raw slot records its exact width.
        byte[] leaf = LuminPackSerializer.Serialize(new SafeLeaf { Value = 9 });
        Assert.Equal(4, leaf[1]);
        leaf[1] = 2;
        SafeLeaf? leafCopy = null;
        Assert.Throws<FormatException>(() => LuminPackSerializer.Deserialize(leaf, ref leafCopy));
    }

    /// <summary>Union cases are read with the same checks, and undeclared tags are rejected.</summary>
    [Fact]
    public void UnionReadsAreChecked()
    {
        var shapes = new SafeShapes { Items = [new SafeCircle { Filled = true, Radius = 2 }, new SafeSquare { Side = 3 }] };
        byte[] bytes = LuminPackSerializer.Serialize(shapes);
        SafeShapes? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.True(((SafeCircle)copy!.Items[0]).Filled);
        byte[] forged = (byte[])bytes.Clone();
        forged[Difference(bytes, LuminPackSerializer.Serialize(new SafeShapes { Items = [new SafeCircle { Radius = 2 }, new SafeSquare { Side = 3 }] }))] = 5;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(forged, ref copy));
        SafeShape? shape = null;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(new byte[] { 9, 1 }, ref shape));
    }

    /// <summary>A compressed collection's count must be producible by its payload before storage is allocated.</summary>
    [Fact]
    public void CompressedCountsAreBoundedByTheirPayload()
    {
        var value = new SafeCompressed { Values = Enumerable.Repeat(7, 512).ToArray() };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        SafeCompressed? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref copy));
        Assert.Equal(value.Values, copy!.Values);
        // The record starts after the object marker with its element count.
        Assert.Equal(512, BitConverter.ToInt32(bytes, 1));
        byte[] forged = (byte[])bytes.Clone();
        BitConverter.GetBytes(int.MaxValue / 4).CopyTo(forged, 1);
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(forged, ref copy));
    }

    /// <summary>Finds the single byte where two encodings differ.</summary>
    /// <param name="left">The first encoding.</param><param name="right">The second encoding of equal length.</param>
    /// <returns>The differing index.</returns>
    private static int Difference(byte[] left, byte[] right)
    {
        Assert.Equal(left.Length, right.Length);
        return Enumerable.Range(0, left.Length).Single(i => left[i] != right[i]);
    }

    /// <summary>Untrusted JSON documents use the strict grammar and the untrusted depth bound.</summary>
    [Fact]
    public void UntrustedJsonIsStrict()
    {
        var value = new UntrustedLimited { Values = [1, 2], Text = "json" };
        string json = LuminPackSerializer.SerializeJson(value);
        UntrustedLimited? copy = null;
        LuminPackSerializer.DeserializeJson(json, ref copy);
        Assert.Equal(new[] { 1, 2 }, copy!.Values);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson(json.Replace("[1,2]", "[1,2,]"), ref copy));
        UntrustedNode? node = null;
        string deep = "{\"Value\":0,\"Next\":{\"Value\":1,\"Next\":{\"Value\":2,\"Next\":{\"Value\":3,\"Next\":{\"Value\":4,\"Next\":null}}}}}";
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson(deep, ref node));
    }
}

/// <summary>An untrusted record covering scalars, strings, arrays, lists and a nested record.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true)]
public partial class UntrustedRecord
{
    public int Id;
    public bool Flag;
    public string Name = "";
    public int[] Values = [];
    public bool[] Bits = [];
    public List<bool> List = new();
    public UntrustedChild? Child;
}

/// <summary>The trusted twin of <see cref="UntrustedRecord"/>.</summary>
[LuminPackable, LuminPackOption(UntrustedData = false)]
public partial class TrustedRecord
{
    public int Id;
    public bool Flag;
    public string Name = "";
    public int[] Values = [];
    public bool[] Bits = [];
    public List<bool> List = new();
    public UntrustedChild? Child;
}

/// <summary>A nested record without its own option, checked through its untrusted root.</summary>
[LuminPackable]
public partial class UntrustedChild
{
    public int Number;
    public bool Flag;
    public string Text = "";
}

/// <summary>A fixed scalar record with Boolean fields at the start and the end of its batch.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true)]
public partial class UntrustedScalars
{
    public int Number;
    public bool Flag;
    public long Stamp;
    public bool Last;
}

/// <summary>Boolean sequences.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true)]
public partial class UntrustedBooleans
{
    public bool[] Bits = [];
    public List<bool> List = new();
}

/// <summary>Tight collection, string and input limits.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxCollectionLength = 4, MaxStringLength = 8, MaxInputBytes = 1024)]
public partial class UntrustedLimited
{
    public int[] Values = [];
    public List<UntrustedChild> Items = new();
    public string Text = "";
    public Dictionary<int, string> Map = new();
}

/// <summary>A recursive untrusted type.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxDepth = 4)]
public partial class UntrustedNode
{
    public int Value;
    public UntrustedNode? Next;
}

/// <summary>An untrusted root reaching a recursive trusted type.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxDepth = 3)]
public partial class UntrustedGraph
{
    public TrustedNode? Node;
}

/// <summary>A recursive trusted type.</summary>
[LuminPackable, LuminPackOption(UntrustedData = false)]
public partial class TrustedNode
{
    public int Value;
    public TrustedNode? Next;
}

/// <summary>A trusted parent with an untrusted child.</summary>
[LuminPackable, LuminPackOption(UntrustedData = false)]
public partial class TrustedParent
{
    public int Before;
    public UntrustedScalars Child = new();
    public int After;
}

/// <summary>A shape the option rejects.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true)]
public partial class UntrustedDangerous
{
    public Type? Type;
}

/// <summary>A record without any option, untrusted by default.</summary>
[LuminPackable]
public partial class DefaultScalars
{
    public int Number;
    public bool Flag;
}

/// <summary>A version-tolerant record with Boolean storage.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public partial class SafeVersioned
{
    public int Id;
    public bool Flag;
    public string Name = "";
}

/// <summary>An ordinary object with a version-tolerant member.</summary>
[LuminPackable]
public partial class VersionHolder
{
    public int Before;
    public SafeVersioned? Versioned;
    public int After;
}

/// <summary>A reference-tracked record with a raw slot.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public partial class SafeLeaf
{
    [LuminPackOrder(0)] public int Value;
}

/// <summary>A reference-tracked owner whose slots expect <see cref="SafeLeaf"/> values.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public partial class SafeOwner
{
    [LuminPackOrder(0)] public SafeLeaf? Leaf;
    [LuminPackOrder(1)] public SafeLeaf? Other;
}

/// <summary>A Union root.</summary>
[LuminPackable]
[LuminPackUnion(0, typeof(SafeCircle))]
[LuminPackUnion(1, typeof(SafeSquare))]
public abstract partial class SafeShape;

/// <summary>A Union case with Boolean storage.</summary>
[LuminPackable]
public sealed partial class SafeCircle : SafeShape
{
    public bool Filled;
    public double Radius;
}

/// <summary>A second Union case.</summary>
[LuminPackable]
public sealed partial class SafeSquare : SafeShape
{
    public int Side;
}

/// <summary>A list of Union values.</summary>
[LuminPackable]
public partial class SafeShapes
{
    public List<SafeShape> Items = new();
}

/// <summary>A compressed primitive array.</summary>
[LuminPackable]
public partial class SafeCompressed
{
    [LuminPackCompress] public int[] Values = [];
}
