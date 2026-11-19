#if LUMINPACK_NEXT_GENERATOR
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Verifies binary generation for shapes used by the Nino benchmark models.</summary>
public class NinoAdditionalGeneratorRegressionTest
{
    /// <summary>Disambiguates identical snapshot names in parent fields and expanded list elements.</summary>
    [Fact]
    public void ParentAndInlinedListElementsCanCaptureFieldsWithTheSameNames()
    {
        var source = new SnapshotPlayer
        {
            PlayerId = 17,
            Name = "player",
            Hero = 10,
            Deck = [new() { Id = 1, Name = "first", Cost = 1, Text = "deck" }],
            Hand = [new() { Id = 2, Name = "second", Cost = 2, Text = "hand" }],
            Number = 25,
            Text = "player details",
        };

        byte[] bytes = LuminPackSerializer.Serialize(source);
        SnapshotPlayer copy = LuminPackSerializer.Deserialize<SnapshotPlayer>(bytes);

        Assert.Equal(17, copy.PlayerId);
        Assert.Equal("player", copy.Name);
        Assert.Equal(10, copy.Hero);
        Assert.Equal("first", Assert.Single(copy.Deck).Name);
        Assert.Equal("deck", copy.Deck[0].Text);
        Assert.Equal("second", Assert.Single(copy.Hand).Name);
        Assert.Equal("hand", copy.Hand[0].Text);
        Assert.Equal(25, copy.Number);
        Assert.Equal("player details", copy.Text);
    }

    /// <summary>Reads concrete cases of distinct closed constructions without assigning them directly to an open type.</summary>
    [Fact]
    public void ClosedGenericUnionCasesRoundTrip()
    {
        SnapshotBox<int> scalar = new SnapshotIntBox { Value = 12 };
        SnapshotBox<(int, long)> tuple = new SnapshotTupleBox { Value = (23, 31) };

        Assert.Equal(12, Assert.IsType<SnapshotIntBox>(LuminPackSerializer.Deserialize<SnapshotBox<int>>(LuminPackSerializer.Serialize(scalar))).Value);
        Assert.Equal((23, 31L), Assert.IsType<SnapshotTupleBox>(
            LuminPackSerializer.Deserialize<SnapshotBox<(int, long)>>(LuminPackSerializer.Serialize(tuple))).Value);
    }

    [LuminPackable]
    public sealed partial class SnapshotPlayer
    {
        public long PlayerId;
        public string Name = "";
        public int Hero;
        public List<SnapshotCard> Deck = [];
        public List<SnapshotCard> Hand = [];
        public int Number;
        public string Text = "";
    }

    [LuminPackable]
    public sealed partial class SnapshotCard
    {
        public int Id;
        public string Name = "";
        public byte Cost;
        public string Text = "";
    }

    [LuminPackable]
    public abstract partial class SnapshotBox<T>
    {
        public T Value = default!;
    }

    [LuminPackable]
    public sealed partial class SnapshotIntBox : SnapshotBox<int> { }

    [LuminPackable]
    public sealed partial class SnapshotTupleBox : SnapshotBox<(int, long)> { }
}
#endif
