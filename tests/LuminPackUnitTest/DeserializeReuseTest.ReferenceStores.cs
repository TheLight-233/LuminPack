using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

public partial class DeserializeReuseTest
{
    /// <summary>Checks a callback that replaces public and private members during parsing keeps its replacement.</summary>
    [Fact]
    public void CallbackMemberReplacementPersistsWithoutLateRootCommit()
    {
        var target = new StoreCommitGraph { Items = [new()], VolatileChild = new() };
        var items = target.Items;
        var child = target.VolatileChild;
        byte[] bytes = LuminPackSerializer.Serialize(new StoreCommitGraph
        {
            Items = [new() { Number = 17 }], VolatileChild = new() { Number = 19 }
        });
        StoreCommitCallback.Reading = () =>
        {
            target.Items = [];
            target.VolatileChild = new();
        };
        try { LuminPackSerializer.Deserialize<StoreCommitGraph>(bytes, ref target); }
        finally { StoreCommitCallback.Reading = null; }
        // Ordinary reuse reads directly into existing storage, so the callback replacement is not restored.
        Assert.NotSame(items, target.Items);
        Assert.Empty(target.Items);
        Assert.NotSame(child, target.VolatileChild);
        // The original instances were populated before the callback replaced the member slots.
        Assert.Equal(17, items[0].Number);
        Assert.Equal(19, child.Number);
    }

    /// <summary>Checks skipped equal stores still publish changed and null strings under every binary profile.</summary>
    [Fact]
    public void ReusedElementsPublishChangedStringsAndNulls()
    {
        CheckChangedElementStrings<LuminPackDefaultOptions>();
        CheckChangedElementStrings<LuminPackUtf8TokenOptions>();
        CheckChangedElementStrings<LuminPackUtf16LengthOptions>();
        CheckChangedElementStrings<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks existing element identities and every text transition with one compiled representation.</summary>
    /// <typeparam name="TOptions">The compiled binary string profile.</typeparam>
    private static void CheckChangedElementStrings<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var item = new ReuseNode { Text = "before" };
        var element = new ReuseNode { Text = "before" };
        var target = new ReuseGraph { Items = [item], Array = [element] };
        foreach (string? text in new[] { "changed", "\u4E2D\uD83D\uDE00", "", null, "after null" })
        {
            byte[] bytes = LuminPackSerializer.Serialize<ReuseGraph, TOptions>(new()
            {
                Items = [new() { Text = text! }], Array = [new() { Text = text! }]
            });
            int consumed = LuminPackSerializer.Deserialize<ReuseGraph, TOptions>(bytes, ref target);
            var fresh = LuminPackSerializer.Deserialize<ReuseGraph, TOptions>(bytes);
            Assert.Equal(bytes.Length, consumed);
            Assert.Same(item, target.Items[0]);
            Assert.Same(element, target.Array[0]);
            Assert.Equal(fresh.Items[0].Text, item.Text);
            Assert.Equal(fresh.Array[0].Text, element.Text);
        }
    }

    /// <summary>Provides private property storage and a public volatile reference for callback-replacement checks.</summary>
    [LuminPackable]
    public sealed class StoreCommitGraph
    {
        public List<ReuseNode> Items { get; set; } = [];
        public volatile ReuseNode VolatileChild = new();
        [LuminPackableObject] public StoreCommitCallback Trigger = new();
    }

    /// <summary>Provides a formatter boundary that mutates its containing target during parsing.</summary>
    [LuminPackable]
    public sealed class StoreCommitCallback
    {
        public static Action? Reading;

        /// <summary>Invokes the test's member replacement after the outer root members were read.</summary>
        [LuminPackOnDeserializing]
        public static void BeforeRead() => Reading?.Invoke();
    }
}
