#if LUMINPACK_NEXT_GENERATOR
using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;
using StrictUtf8 = LuminPack.Option.LuminPackOptions<LuminPack.Option.LuminPackUtf8LengthOptions, LuminPack.Option.LuminPackGeneralFormat, LuminPack.Option.LuminPackDefaultDepth, LuminPack.Option.LuminPackStrictJson>;

namespace LuminPackUnitTest;

/// <summary>Checks generated JSON reads into reused linked lists.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class LinkedListJsonReuseTest
{
    /// <summary>Keeps the overlapping nodes while invalidating the old enumerator, including at equal counts.</summary>
    /// <param name="count">The number of values in the new list.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void RefillsExistingNodesAndInvalidatesEnumerators(int count)
    {
        var target = new LinkedList<int>([-1, -2, -3]);
        var original = target;
        var head = target.First!;
        LinkedListNode<int>[] oldNodes = [head, head.Next!, target.Last!];
        var enumerator = target.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        int[] expected = Enumerable.Range(11, count).ToArray();
        string json = "[" + string.Join(",", expected) + "]";

        LuminPackSerializer.DeserializeJson(json, ref target);

        Assert.Same(original, target);
        Assert.Equal(expected, target);
        Assert.Equal(expected.Reverse(), target.Reverse());
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        var node = target.First;
        for (int index = 0; index < Math.Min(oldNodes.Length, count); index++, node = node!.Next)
        {
            Assert.Same(oldNodes[index], node);
            Assert.Same(target, node!.List);
        }
        for (int index = count; index < oldNodes.Length; index++)
        {
            Assert.Null(oldNodes[index].List);
            Assert.Null(oldNodes[index].Next);
            Assert.Null(oldNodes[index].Previous);
        }
    }

    /// <summary>Keeps the completed nodes and leaves the original suffix detached after malformed input.</summary>
    /// <param name="json">An array with an invalid value at the selected position.</param>
    /// <param name="completed">The number of valid values before the invalid value.</param>
    [Theory]
    [InlineData("[\"bad\"]", 0)]
    [InlineData("[1,2,\"bad\"]", 2)]
    public void InvalidElementLeavesTheCompletedPrefix(string json, int completed)
    {
        var target = new LinkedList<int>([-1, -2, -3]);
        var original = target;
        var first = target.First!;
        var last = target.Last!;
        var enumerator = target.GetEnumerator();
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        Assert.ThrowsAny<Exception>(() =>
            LuminPackSerializer.DeserializeJson<LinkedList<int>, StrictUtf8>(bytes, ref target));

        Assert.Same(original, target);
        Assert.Equal(Enumerable.Range(1, completed), target);
        if (completed > 0)
            Assert.Same(first, target.First);
        else
            Assert.Null(first.List);
        Assert.Null(last.List);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    /// <summary>Completes an already-selected inner array and continues through the following sibling value.</summary>
    [Fact]
    public void StringArrayPreservesTheEnclosingReaderPosition()
    {
        ReadOnlySpan<byte> bytes = """[["caf\u00e9",null],17]"""u8;
        using var state = new LuminPackReaderOptionalState();
        var reader = new LuminPackJsonReader(ref bytes, state);
        Assert.True(reader.Read<StrictUtf8>());
        Assert.True(reader.Read<StrictUtf8>());
        Assert.Equal(LuminPackJsonReader.JsonTokenType.ArrayStart, reader.CurrentTokenType);
        string kept = new string("caf\u00e9".AsSpan());
        var target = new LinkedList<string?>([kept, "old", "removed"]);
        var first = target.First!;
        var second = first.Next!;

        LuminPackSerializer.DeserializeJson<LinkedList<string?>, StrictUtf8>(ref reader, ref target);

        Assert.Equal(new string?[] { "caf\u00e9", null }, target);
        Assert.Same(kept, target.First!.Value);
        Assert.Same(first, target.First);
        Assert.Same(second, target.Last);
        Assert.True(reader.Read<StrictUtf8>());
        Assert.Equal(17, reader.Read<int, StrictUtf8>());
        reader.ConsumeArrayEnd<StrictUtf8>();
        reader.EnsureEndOfDocument<StrictUtf8>();
        Assert.Equal(bytes.Length, reader.Position);
    }

    /// <summary>A malformed string detaches the unread tail without publishing its temporary value.</summary>
    [Fact]
    public void InvalidStringKeepsThePreviousUnpublishedValue()
    {
        string unchanged = new string("keep".AsSpan());
        var target = new LinkedList<string>([unchanged, "old"]);
        var second = target.Last!;
        byte[] bytes = """["keep","bad\x"]"""u8.ToArray();

        Assert.Throws<FormatException>(() =>
            LuminPackSerializer.DeserializeJson<LinkedList<string>, StrictUtf8>(bytes, ref target));

        Assert.Same(unchanged, Assert.Single(target));
        Assert.Null(second.List);
        Assert.Equal("old", second.Value);
    }

    /// <summary>Keeps the old clear and append path for derived runtime lists.</summary>
    [Fact]
    public void DerivedListKeepsTheOrdinaryRead()
    {
        LinkedList<int> target = new DerivedList();
        var original = target;
        var old = target.AddLast(-1);
        LuminPackSerializer.DeserializeJson("[1]", ref target);
        Assert.Same(original, target);
        Assert.Equal(1, Assert.Single(target));
        Assert.Null(old.List);
    }

    /// <summary>Lets a custom element formatter observe exactly the completed prefix and propagate its exception.</summary>
    [Fact]
    public void CustomElementReadsStillObserveTheCompletedPrefix()
    {
        var target = new LinkedList<LinkedListJsonCallbackItem>([new() { Count = -1 }, new() { Count = -2 }]);
        var original = target;
        var old = target.First!;
        var observed = new List<int>();
        var failure = new InvalidOperationException("The second value failed.");
        LinkedListJsonCallbackItem.OnRead = item =>
        {
            observed.Add(target.Count);
            if (item.Count == 2)
                throw failure;
        };

        try
        {
            Exception? error = Record.Exception(() => LuminPackSerializer.DeserializeJson("[1,2]", ref target));
            Assert.Same(failure, error);
            Assert.Same(original, target);
            Assert.Equal([0, 1], observed);
            Assert.Equal(1, Assert.Single(target).Count);
            Assert.Null(old.List);
        }
        finally
        {
            LinkedListJsonCallbackItem.OnRead = null;
        }
    }

    /// <summary>Preserves mutations made by a user profile that matches the built-in encoding and syntax.</summary>
    [Fact]
    public void UserProfileCanAppendToTheCompletedPrefix()
    {
        string first = new string("keep".AsSpan());
        var target = new LinkedList<string>([first, "old"]);
        var original = target;
        var oldNode = target.First!;
        try
        {
            LinkedListJsonObserverProfile.Target = target;
            LuminPackSerializer.DeserializeJson<LinkedList<string>, LinkedListJsonObserverProfile>("[\"keep\",\"new\"]", ref target);
            Assert.Same(original, target);
            Assert.Null(LinkedListJsonObserverProfile.Target);
            Assert.Equal(new[] { "keep", "observed", "new" }, target);
            Assert.NotSame(first, target.First!.Value);
            Assert.Null(oldNode.List);
        }
        finally
        {
            LinkedListJsonObserverProfile.Target = null;
        }
    }

    /// <summary>Provides a derived destination for the ordinary framework path.</summary>
    private sealed class DerivedList : LinkedList<int>;
}

/// <summary>Lets a custom JSON element reader observe its enclosing destination after reading a value.</summary>
[LuminPackable(GeneratorType.Custom)]
public sealed class LinkedListJsonCallbackItem
{
    /// <summary>The value read by the element formatter.</summary>
    public int Count;

    /// <summary>The observer called before the collection publishes this item.</summary>
    public static Action<LinkedListJsonCallbackItem>? OnRead;

    /// <summary>Writes the element in binary form.</summary>
    /// <param name="writer">The active writer.</param>
    /// <param name="value">The element to write.</param>
    public static void Serialize(ref LuminPackWriter writer, in LinkedListJsonCallbackItem value) => writer.Write(value.Count);

    /// <summary>Reads the element in binary form.</summary>
    /// <param name="reader">The active reader.</param>
    /// <param name="value">The element destination.</param>
    public static void Deserialize(ref LuminPackReader reader, ref LinkedListJsonCallbackItem value)
    {
        value = new LinkedListJsonCallbackItem();
        reader.Read(ref value.Count);
    }

    /// <summary>Writes the element in JSON form.</summary>
    /// <param name="writer">The active writer.</param>
    /// <param name="value">The element to write.</param>
    public static void SerializeJson(ref LuminPackJsonWriter writer, in LinkedListJsonCallbackItem value) =>
        writer.Write(value.Count);

    /// <summary>Decodes one JSON integer and then invokes the observer on the new element.</summary>
    /// <param name="reader">The active reader.</param>
    /// <param name="value">The element destination.</param>
    public static void DeserializeJson(ref LuminPackJsonReader reader, ref LinkedListJsonCallbackItem value)
    {
        value = new LinkedListJsonCallbackItem { Count = reader.Read<int, LuminPackDefaultOptions>() };
        OnRead?.Invoke(value);
    }
}

/// <summary>Uses built-in UTF-16 length settings and observes publication from its encoding getter.</summary>
internal readonly struct LinkedListJsonObserverProfile : ILuminPackOptions
{
    internal static LinkedList<string>? Target;

    /// <inheritdoc/>
    public LuminPackStringEncoding StringEncoding
    {
        get
        {
            if (Target is { Count: 1 } list && list.First!.Value == "keep")
            {
                Target = null;
                list.AddLast("observed");
            }
            return default(LuminPackUtf16LengthOptions).StringEncoding;
        }
    }

    /// <inheritdoc/>
    public LuminPackStringRecording StringRecording => default(LuminPackUtf16LengthOptions).StringRecording;

    /// <inheritdoc/>
    public StandardFormat StandardFormat => default(LuminPackUtf16LengthOptions).StandardFormat;

    /// <inheritdoc/>
    public int MaxJsonDepth => default(LuminPackUtf16LengthOptions).MaxJsonDepth;

    /// <inheritdoc/>
    public bool ValidateJsonSyntax => default(LuminPackUtf16LengthOptions).ValidateJsonSyntax;
}
#endif
