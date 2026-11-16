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
