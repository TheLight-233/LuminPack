using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Exercises repeated variable-element growth, including unpublished Union cursors.</summary>
public partial class VariableCollectionCapacityTest
{
    /// <summary>Crosses several native buffer boundaries using fixed virtual Union payloads.</summary>
    /// <param name="sparse">Whether dictionary enumeration uses its sparse fallback.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnionElementsPreserveActualCursorAcrossGrowth(bool sparse)
    {
        var values = new Dictionary<int, Root>();
        var list = new List<Root>();
        for (int index = 0; index < 30000; index++)
        {
            Root value = index % 23 == 0 ? null! : new Fixed { Number = index, First = index * 17L, Second = -index * 31L };
            values.Add(index, value);
            list.Add(value);
        }
        if (sparse)
            for (int index = 1; index < 30000; index += 7) values.Remove(index);
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(values, buffer);
        var dictionaryCopy = LuminPackSerializer.Deserialize<Dictionary<int, Root>>(buffer.GetSpan());
        Assert.Equal(values.Keys, dictionaryCopy.Keys);
        foreach (var pair in values)
            Assert.Equal((pair.Value as Fixed)?.Second, (dictionaryCopy[pair.Key] as Fixed)?.Second);
        var strings = values.ToDictionary(pair => "key-" + pair.Key, pair => pair.Value);
        if (sparse)
            foreach (string key in strings.Keys.Where((_, index) => index % 11 == 0).ToArray()) strings.Remove(key);
        using var stringBuffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(strings, stringBuffer);
        var stringCopy = LuminPackSerializer.Deserialize<Dictionary<string, Root>>(stringBuffer.GetSpan());
        Assert.Equal(strings.Keys, stringCopy.Keys);
        foreach (var pair in strings)
            Assert.Equal((pair.Value as Fixed)?.Second, (stringCopy[pair.Key] as Fixed)?.Second);
        byte[] bytes = LuminPackSerializer.Serialize(list);
        var listCopy = LuminPackSerializer.Deserialize<List<Root>>(bytes);
        Assert.Equal(list.Select(value => (value as Fixed)?.Number), listCopy.Select(value => (value as Fixed)?.Number));
        Assert.Equal(bytes, LuminPackSerializer.Serialize(listCopy));
    }

    /// <summary>Checks inline string collections whose null elements never enter the string conversion core.</summary>
    /// <param name="encoding">The string payload encoding.</param>
    /// <param name="recording">The null/empty framing contract.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Token)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Token)]
    public void InlineNullCollectionsAndNestedPayloadsKeepFollowingFields(LuminPackStringEncoding encoding, LuminPackStringRecording recording)
    {
        var value = new Graph
        {
            Items = Enumerable.Repeat<string>(null!, 100000).ToList(),
            Array = Enumerable.Range(0, 10000).Select(index => index % 3 == 0 ? null! : new string('x', index % 97)).ToArray(),
            Tail = 917
        };
        var option = new LuminPackSerializerOption { StringEncoding = encoding, StringRecording = recording };
        using var buffer = new LuminBufferWriter(true);
        buffer.Option.StringEncoding = encoding;
        buffer.Option.StringRecording = recording;
        LuminPackSerializer.Serialize(value, buffer);
        byte[] bytes = buffer.GetSpan().ToArray();
        var copy = LuminPackSerializer.Deserialize<Graph>(bytes, option);
        Assert.Equal(value.Items.Select(text => text ?? ""), copy.Items.Select(text => text ?? ""));
        Assert.Equal(value.Array.Select(text => text ?? ""), copy.Array.Select(text => text ?? ""));
        Assert.Equal(value.Tail, copy.Tail);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy, option));
    }

    /// <summary>Checks the completed variable payload rather than its stale starting cursor before a fixed tail.</summary>
    /// <param name="shape">A flat string, inline List, or inline Array boundary.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CompletedPayloadNearSegmentEndPreservesFixedTail(int shape)
    {
        var option = new LuminPackSerializerOption { StringEncoding = LuminPackStringEncoding.UTF16, StringRecording = LuminPackStringRecording.Length };
        using var buffer = new LuminBufferWriter(true);
        buffer.Option.StringEncoding = option.StringEncoding;
        buffer.Option.StringRecording = option.StringRecording;
        if (shape == 0)
        {
            var value = new TextEdge { Text = new string('x', 16381), Tail = 1917 };
            LuminPackSerializer.Serialize(value, buffer);
            Assert.Equal(value.Tail, LuminPackSerializer.Deserialize<TextEdge>(buffer.GetSpan(), option).Tail);
        }
        else
        {
            var value = new CollectionEdge { Tail = 1917 };
            if (shape == 1) value.List = [new string('x', 16379)];
            else value.Array = [new string('x', 16377)];
            LuminPackSerializer.Serialize(value, buffer);
            var copy = LuminPackSerializer.Deserialize<CollectionEdge>(buffer.GetSpan(), option);
            Assert.Equal(value.Tail, copy.Tail);
            Assert.Equal(value.List, copy.List);
            Assert.Equal(value.Array, copy.Array);
        }
        Assert.True(buffer.TotalLength > 32 * 1024);
    }

    /// <summary>Uses the existing returning-cursor virtual dispatch for eligible cases.</summary>
    [LuminPackable, LuminPackUnion(1, typeof(Fixed))]
    public abstract partial class Root { }

    /// <summary>Keeps its complete payload fixed and callback-free.</summary>
    [LuminPackable]
    public sealed partial class Fixed : Root
    {
        public int Number;
        public long First;
        public long Second;
    }

    /// <summary>Retains inline list/array operation templates and trailing fixed state.</summary>
    [LuminPackable]
    public sealed class Graph
    {
        public List<string> Items = null!;
        public string[] Array = null!;
        public int Tail;
    }

    /// <summary>Places an unchecked fixed tail after a dynamic text field.</summary>
    [LuminPackable]
    public sealed class TextEdge
    {
        public string Text = null!;
        public int Tail;
    }

    /// <summary>Places fixed state after both inline collection alternatives.</summary>
    [LuminPackable]
    public sealed class CollectionEdge
    {
        public List<string>? List;
        public string[]? Array;
        public int Tail;
    }
}
