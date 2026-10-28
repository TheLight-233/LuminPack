using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects reserved collection regions, trusted record bounds and checked arithmetic fallbacks.</summary>
public partial class CapacityTierTest
{
    /// <summary>Checks byte parity and fresh-buffer growth for complete guaranteed records.</summary>
    [Fact]
    public void GuaranteedRootPreservesOrdinaryWireAndNestedRecords()
    {
        var value = new LargeGuaranteed { Text = new string('x', 20000), Values = Enumerable.Range(0, 6000).ToArray(), Builder = new StringBuilder(new string('y', 17000)) };
        var ordinary = new LargeOrdinary { Text = value.Text, Values = value.Values, Builder = value.Builder };
        foreach (var option in Options())
        {
            byte[] bytes = LuminPackSerializer.Serialize(value, option);
            Assert.Equal(LuminPackSerializer.Serialize(ordinary, option), bytes);
            var copy = LuminPackSerializer.Deserialize<LargeGuaranteed>(bytes, option);
            Assert.Equal(value.Text, copy.Text);
            Assert.Equal(value.Values, copy.Values);
            Assert.Equal(value.Builder.ToString(), copy.Builder.ToString());
        }
    }

    /// <summary>Crosses many initial segments using arrays, lists, sets and sparse dictionary guarantees.</summary>
    [Fact]
    public void GuaranteedElementsReserveTheirCompleteContainerPayload()
    {
        var list = Enumerable.Range(0, 12000).Select(index => new SmallGuaranteed { Id = index, Text = "value" }).ToList();
        byte[] bytes = LuminPackSerializer.Serialize(list);
        var copy = LuminPackSerializer.Deserialize<List<SmallGuaranteed>>(bytes);
        Assert.Equal(list.Select(item => item.Id), copy.Select(item => item.Id));
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
        var array = list.ToArray();
        Assert.Equal(bytes, LuminPackSerializer.Serialize(array));
        var dictionary = list.ToDictionary(item => item.Id, item => item);
        foreach (int key in dictionary.Keys.Where(key => key % 11 == 0).ToArray()) dictionary.Remove(key);
        byte[] map = LuminPackSerializer.Serialize(dictionary);
        var mapCopy = LuminPackSerializer.Deserialize<Dictionary<int, SmallGuaranteed>>(map);
        Assert.Equal(dictionary.Select(pair => pair.Value.Id), mapCopy.Select(pair => pair.Value.Id));
        var set = list.ToHashSet();
        var setCopy = LuminPackSerializer.Deserialize<HashSet<SmallGuaranteed>>(LuminPackSerializer.Serialize(set));
        Assert.Equal(set.Select(item => item.Id).Order(), setCopy.Select(item => item.Id).Order());
    }

    /// <summary>Checks length prescans, null/empty normalization and exact wire bytes in all runtime option modes.</summary>
    [Fact]
    public void StringUpperBoundsKeepExactRecordsAndFixedTails()
    {
        var strings = Enumerable.Range(0, 9000).Select(index => index % 11 == 0 ? null! : index % 13 == 0 ? "" : "\u4e2d\ud83d\ude42-" + index).ToArray();
        foreach (var option in Options())
        {
            var graph = new TextGraph { Values = strings, List = strings.ToList(), Tail = 971 };
            byte[] bytes = LuminPackSerializer.Serialize(graph, option);
            var copy = LuminPackSerializer.Deserialize<TextGraph>(bytes, option);
            Assert.Equal(strings.Select(value => value ?? ""), copy.Values.Select(value => value ?? ""));
            Assert.Equal(strings.Select(value => value ?? ""), copy.List.Select(value => value ?? ""));
            Assert.Equal(graph.Tail, copy.Tail);
            Assert.Equal(bytes, LuminPackSerializer.Serialize(copy, option));
            Assert.Equal(LuminPackSerializer.Serialize(strings, option), LuminPackSerializer.Serialize(strings.ToList(), option));
            var dictionary = strings.Select((value, index) => (value, index)).ToDictionary(pair => "key-" + pair.index, pair => pair.value);
            foreach (string key in dictionary.Keys.Where((_, index) => index % 7 == 0).ToArray()) dictionary.Remove(key);
            byte[] map = LuminPackSerializer.Serialize(dictionary, option);
            var mapCopy = LuminPackSerializer.Deserialize<Dictionary<string, string>>(map, option);
            Assert.Equal(dictionary.Keys, mapCopy.Keys);
            Assert.Equal(dictionary.Values.Select(value => value ?? ""), mapCopy.Values.Select(value => value ?? ""));
            Assert.Equal(map, LuminPackSerializer.Serialize(mapCopy, option));
        }
    }

    /// <summary>Rejects unrepresentable reservations without allocation, cursor publication or canary writes.</summary>
    [Fact]
    public void UnrepresentableReservationsRejectWithoutPublishing()
    {
        foreach (var (position, count) in new[] { (0, int.MaxValue), (int.MaxValue, 1), (-1, 1), (0, -1), (7, 58) })
        {
            Span<byte> span = new byte[64];
            var writer = new LuminPackWriter(ref span);
            bool failed = false;
            try { writer.Reserve(position, count); }
            catch (Exception) { failed = true; }
            Assert.True(failed);
            Assert.Equal(0, writer.Position);
            Assert.All(span.ToArray(), value => Assert.Equal((byte)0, value));
        }
        Span<byte> exact = new byte[64];
        var bounded = new LuminPackWriter(ref exact);
        Assert.Equal(57, bounded.Reserve(7, 57).Length);
        Assert.Equal(0, bounded.Position);
    }

    /// <summary>Compares reserved and original strings, including raw surrogates, terminators and encoding failures.</summary>
    [Fact]
    public void ReservedStringsPreserveCodeUnitsHeadersAndEncodingFailures()
    {
        foreach (var option in Options())
        {
            foreach (string? text in new[] { null, "", "a", "a\0b", "\u4e2d\ud83d\ude42" })
            {
                var bounded = new LargeGuaranteed { Text = text! };
                var ordinary = new LargeOrdinary { Text = text! };
                Assert.Equal(LuminPackSerializer.Serialize(ordinary, option), LuminPackSerializer.Serialize(bounded, option));
            }
            foreach (string text in new[] { "\ud800", "x\udfffy" })
            {
                var bounded = new LargeGuaranteed { Text = text };
                var ordinary = new LargeOrdinary { Text = text };
                if (option.StringEncoding == LuminPackStringEncoding.UTF16)
                    Assert.Equal(LuminPackSerializer.Serialize(ordinary, option), LuminPackSerializer.Serialize(bounded, option));
                else
                {
                    Exception expected = Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Serialize(ordinary, option));
                    Exception actual = Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Serialize(bounded, option));
                    Assert.Equal(expected.GetType(), actual.GetType());
                    Assert.Equal(expected.Message, actual.Message);
                }
            }
        }
    }

    /// <summary>Reserves independent staging writers while preserving tolerant length tables and cyclic identity.</summary>
    [Fact]
    public void GuaranteedSpecializedRecordsKeepTheirIndependentStagingCapacity()
    {
        var values = Enumerable.Range(0, 4000).Select(index => new SmallGuaranteed { Id = index, Text = "text" }).ToList();
        var versioned = new VersionedGuaranteed { Values = values, Text = new string('v', 18000) };
        var ordinary = new VersionedOrdinary { Values = values, Text = versioned.Text };
        foreach (var option in Options())
        {
            byte[] bytes = LuminPackSerializer.Serialize(versioned, option);
            Assert.Equal(LuminPackSerializer.Serialize(ordinary, option), bytes);
            var copy = LuminPackSerializer.Deserialize<VersionedGuaranteed>(bytes, option);
            Assert.Equal(values.Select(value => value.Id), copy.Values.Select(value => value.Id));
            Assert.Equal(versioned.Text, copy.Text);
        }
        var circle = new CircleGuaranteed { Values = values, Text = new string('c', 18000) };
        circle.Self = circle;
        byte[] cyclic = LuminPackSerializer.Serialize(circle);
        var cyclicCopy = LuminPackSerializer.Deserialize<CircleGuaranteed>(cyclic);
        Assert.Same(cyclicCopy, cyclicCopy.Self);
        Assert.Equal(values.Select(value => value.Id), cyclicCopy.Values.Select(value => value.Id));
        Assert.Equal(circle.Text, cyclicCopy.Text);
    }

    /// <summary>Retains the original caller-span result for a fixed class when opting into a complete bound.</summary>
    [Fact]
    public void GuaranteedFixedClassPreservesCallerSpanBehavior()
    {
        var ordinary = WriteFixedClassToSpan(false);
        var guaranteed = WriteFixedClassToSpan(true);
        Assert.Equal(ordinary.Error, guaranteed.Error);
        Assert.Equal(ordinary.Bytes, guaranteed.Bytes);
    }

    /// <summary>Keeps late mutation failures safe when a callback replaces a prescannable string key.</summary>
    [Fact]
    public void StringDictionaryCallbacksRetainCheckedMutationFailure()
    {
        var values = new Dictionary<string, MutatingRecord> { ["first"] = new(), ["last"] = new() };
        MutatingRecord.Before = () =>
        {
            MutatingRecord.Before = null;
            values.Remove("last");
            values.Add(new string('k', 90000), new MutatingRecord());
        };
        try { Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Serialize(values)); }
        finally { MutatingRecord.Before = null; }
    }

    /// <summary>Executes an aggregate-overflow fallback without allocating the conservative multi-gigabyte bound.</summary>
    [Fact]
    public void AggregateBoundOverflowUsesCheckedElementsAndActualLength()
    {
        const int count = 70000;
        var values = Enumerable.Range(0, count).Select(index => new LooseGuaranteed { Id = index }).ToList();
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(values, buffer);
        Assert.Equal(sizeof(int) + count * (sizeof(int) + 1), buffer.CurrentIndex);
        Assert.InRange(buffer.TotalLength, buffer.CurrentIndex, 2_000_000);
        var ordinary = Enumerable.Range(0, count).Select(index => new FixedOrdinary { Id = index }).ToList();
        Assert.Equal(LuminPackSerializer.Serialize(ordinary), buffer.GetSpan().ToArray());
        var copy = LuminPackSerializer.Deserialize<List<LooseGuaranteed>>(buffer.GetSpan());
        Assert.Equal(values.Select(value => value.Id), copy.Select(value => value.Id));
    }

    /// <summary>Captures bytes and the original unowned-writer completion behavior.</summary>
    /// <param name="guaranteed">Whether the otherwise identical fixed record declares a bound.</param>
    /// <returns>The written bytes and any completion failure type.</returns>
    private static (byte[] Bytes, Type? Error) WriteFixedClassToSpan(bool guaranteed)
    {
        Span<byte> span = new byte[64];
        var writer = new LuminPackWriter(ref span);
        Type? error = null;
        try
        {
            if (guaranteed)
            {
                var value = new FixedGuaranteed { Id = 97 };
                LuminPackSerializer.Serialize(ref writer, in value);
            }
            else
            {
                var value = new FixedOrdinary { Id = 97 };
                LuminPackSerializer.Serialize(ref writer, in value);
            }
        }
        catch (Exception exception) { error = exception.GetType(); }
        return (span.ToArray(), error);
    }

    /// <summary>Creates every existing binary string representation without changing its option ownership.</summary>
    /// <returns>The four encoding and recording combinations.</returns>
    private static IEnumerable<TestSerializationProfile> Options()
    {
        foreach (var encoding in new[] { LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16 })
            foreach (var recording in new[] { LuminPackStringRecording.Length, LuminPackStringRecording.Token })
                yield return new TestSerializationProfile { StringEncoding = encoding, StringRecording = recording };
    }

    [LuminPackable, LuminPackBounded(128)]
    public sealed class SmallGuaranteed { public int Id; public string Text = ""; public int __lumin_capacity0_count = 13; public int writer = 17; }

    [LuminPackable, LuminPackBounded(160000)]
    public sealed class LargeGuaranteed { public string Text = ""; public int[] Values = []; public StringBuilder Builder = new(); }

    [LuminPackable]
    public sealed class LargeOrdinary { public string Text = ""; public int[] Values = []; public StringBuilder Builder = new(); }

    [LuminPackable]
    public sealed class TextGraph { public string[] Values = []; public List<string> List = []; public int Tail; }

    [LuminPackable, LuminPackBounded(8)]
    public sealed class FixedGuaranteed { public int Id; }

    [LuminPackable]
    public sealed class FixedOrdinary { public int Id; }

    [LuminPackable, LuminPackBounded(65536)]
    public sealed class LooseGuaranteed { public int Id; }

    [LuminPackable, LuminPackBounded(64)]
    public sealed class MutatingRecord
    {
        public int Id = 31;
        public static Action? Before;

        /// <summary>Executes the optional synchronous mutation callback.</summary>
        [LuminPackOnSerializing]
        public void OnWriting() => Before?.Invoke();
    }

    [LuminPackable(GeneratorType.VersionTolerant), LuminPackBounded(300000)]
    public sealed partial class VersionedGuaranteed
    {
        [LuminPackOrder(0)] public List<SmallGuaranteed> Values = [];
        [LuminPackOrder(1)] public string Text = "";
    }

    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed partial class VersionedOrdinary
    {
        [LuminPackOrder(0)] public List<SmallGuaranteed> Values = [];
        [LuminPackOrder(1)] public string Text = "";
    }

    [LuminPackable(GeneratorType.CircleReference), LuminPackBounded(300000)]
    public sealed partial class CircleGuaranteed
    {
        [LuminPackOrder(0)] public CircleGuaranteed? Self;
        [LuminPackOrder(1)] public List<SmallGuaranteed> Values = [];
        [LuminPackOrder(2)] public string Text = "";
    }
}
