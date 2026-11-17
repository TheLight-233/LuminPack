using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that fixed header blocks are validated before their unchecked loads.</summary>
public sealed partial class TruncatedFixedHeaderTest
{
    /// <summary>Reads every truncated prefix from the start of a larger buffer whose remaining bytes are garbage.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="bytes">The complete payload.</param>
    /// <param name="read">Reads the value from a span.</param>
    private static void CheckPrefixes<T>(byte[] bytes, ReadSpan<T> read)
    {
        byte[] storage = new byte[bytes.Length + 64];
        for (int prefix = 0; prefix < bytes.Length; prefix++)
        {
            Array.Fill(storage, (byte)0x7F);
            bytes.AsSpan(0, prefix).CopyTo(storage);
            Assert.ThrowsAny<Exception>(() => read(storage.AsSpan(0, prefix)));
        }

        read(bytes);
    }

    /// <summary>Reads a value from a span.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="input">The input.</param>
    /// <returns>The value.</returns>
    private delegate T ReadSpan<T>(ReadOnlySpan<byte> input);

    /// <summary>Rejects every truncated multidimensional array header before loading its dimensions.</summary>
    [Fact]
    public void MultidimensionalDimensionsAreCheckedFirst()
    {
        var matrix = new int[3, 4];
        matrix[2, 3] = 137;
        CheckPrefixes(LuminPackSerializer.Serialize(matrix), input => { int[,]? value = null; LuminPackSerializer.Deserialize(input, ref value); return value; });
        var cube = new long[2, 2, 2];
        cube[1, 1, 1] = 271;
        CheckPrefixes(LuminPackSerializer.Serialize(cube), input => { long[,,]? value = null; LuminPackSerializer.Deserialize(input, ref value); return value; });
    }

    /// <summary>Rejects every truncated version record before loading its components.</summary>
    [Fact]
    public void VersionComponentsAreCheckedFirst()
    {
        CheckPrefixes(LuminPackSerializer.Serialize(new Version(1, 2, 3, 4)), input => { Version? value = null; LuminPackSerializer.Deserialize(input, ref value); return value; });
        CheckPrefixes(LuminPackSerializer.Serialize(new Version(5, 6)), input => { Version? value = null; LuminPackSerializer.Deserialize(input, ref value); return value; });
    }

    /// <summary>Checks the marker and full fixed payload of a nested concrete Union case before reusing its fields.</summary>
    [Fact]
    public void NestedFixedCaseChecksMarkerAndPayload()
    {
        var source = new FixedCaseContainer { Child = new FixedCase { First = 19, Second = 23 } };
        byte[] record = LuminPackSerializer.Serialize(source);
        var child = new FixedCase { First = -1, Second = -2 };
        var target = new FixedCaseContainer { Child = child };
        for (int length = 1; length < record.Length; length++)
        {
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(record.AsSpan(0, length), ref target));
            Assert.Same(child, target.Child);
            Assert.Equal(-1, child.First);
            Assert.Equal(-2, child.Second);
        }

        byte[] invalidMarker = (byte[])record.Clone();
        invalidMarker[1] = 0;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(invalidMarker, ref target));
        Assert.Same(child, target.Child);
        Assert.Equal(record.Length, LuminPackSerializer.Deserialize(record, ref target));
        Assert.Same(child, target.Child);
        Assert.Equal(19, child.First);
        Assert.Equal(23, child.Second);

        byte[] nullRecord = LuminPackSerializer.Serialize(new FixedCaseContainer());
        Assert.Equal(nullRecord.Length, LuminPackSerializer.Deserialize(nullRecord, ref target));
        Assert.Null(target.Child);
    }

    /// <summary>Checks string identity, partial reads, and fallback after a matching dictionary key.</summary>
    [Fact]
    public void MixedDictionaryKeepsMatchedValuesBeforeFallback()
    {
        string original = new string('a', 10);
        var target = new Dictionary<int, string?> { [1] = original, [2] = "old" };
        var matching = new Dictionary<int, string?> { [1] = new string('a', 10), [2] = null };
        byte[] bytes = LuminPackSerializer.Serialize(matching);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(original, target[1]);
        Assert.Null(target[2]);

        matching[1] = "changed";
        matching[2] = "truncated";
        bytes = LuminPackSerializer.Serialize(matching);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        Assert.Equal("changed", target[1]);
        Assert.Null(target[2]);

        var reordered = new Dictionary<int, string?> { [1] = "retained", [3] = "inserted" };
        bytes = LuminPackSerializer.Serialize(reordered);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Equal(reordered.ToArray(), target.ToArray());
    }

    /// <summary>Checks string-key identity and staged publication when the value is fully replaced.</summary>
    [Fact]
    public void StringKeyDictionaryStagesCompleteRawValues()
    {
        string original = new string('a', 10);
        var target = new Dictionary<string, int> { [original] = 11, ["old"] = 25 };
        var source = new Dictionary<string, int> { [new string('a', 10)] = 31, ["new"] = 45 };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        Assert.Equal(11, target[original]);
        Assert.Equal(25, target["old"]);

        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Equal(source.ToArray(), target.ToArray());
        Assert.Same(original, target.Keys.First());
    }

    /// <summary>Checks dictionary interface wire bytes for dense, sparse and derived runtime implementations.</summary>
    [Fact]
    public void RawDictionaryInterfacesPreserveEnumerationWireOrder()
    {
        var dense = Enumerable.Range(0, 37).ToDictionary(index => -5 * index, index => index * 11 + 17);
        Check(dense);
        var bounded = Enumerable.Repeat((byte)0xC3, 36).ToArray();
        Span<byte> output = bounded;
        var writer = new LuminPack.Core.LuminPackWriter(ref output);
        writer.WriteCollectionHeader(dense.Count);
        Assert.False(LuminPack.Code.LuminPackMarshal.TryWriteBoundedDenseRawPairs(ref writer, dense, out _));
        Assert.Equal(sizeof(int), writer.Position);
        Assert.All(bounded[sizeof(int)..], value => Assert.Equal((byte)0xC3, value));

        if (LuminPack.Code.LuminPackMarshal.TryGetDictionaryView(dense, out var view))
        {
            Span<byte> aliased = System.Runtime.InteropServices.MemoryMarshal.AsBytes(view._entries.AsSpan());
            byte[] original = aliased.ToArray();
            var aliasedWriter = new LuminPack.Core.LuminPackWriter(ref aliased);
            Assert.False(LuminPack.Code.LuminPackMarshal.TryWriteBoundedDenseRawPairs(ref aliasedWriter, dense, out _));
            Assert.Equal(0, aliasedWriter.Position);
            Assert.Equal(original, aliased.ToArray());
        }

        dense.Remove(-10);
        Check(dense);
        Check(new DerivedRawDictionary(dense));

        static void Check(Dictionary<int, int> source)
        {
            byte[] expected = LuminPackSerializer.Serialize(source);
            Assert.Equal(expected, LuminPackSerializer.Serialize<IDictionary<int, int>>(source));
            Assert.Equal(expected, LuminPackSerializer.Serialize<IReadOnlyDictionary<int, int>>(source));
        }
    }

    /// <summary>Exercises the enumeration fallback for a derived dictionary.</summary>
    private sealed class DerivedRawDictionary(IDictionary<int, int> source) : Dictionary<int, int>(source) { }

    /// <summary>Provides a concrete field whose type is also a fixed Union case.</summary>
    [LuminPackable]
    public sealed class FixedCaseContainer
    {
        public FixedCase? Child;
    }

    /// <summary>Declares the Union that shares the fixed case reader.</summary>
    [LuminPackable]
    [LuminPackUnion(0, typeof(FixedCase))]
    public abstract partial class FixedCaseBase
    {
    }

    /// <summary>Provides adjacent fixed fields and the scalar fallback.</summary>
    [LuminPackable]
    public sealed partial class FixedCase : FixedCaseBase
    {
        public int First;
        public int Second;
    }
}
