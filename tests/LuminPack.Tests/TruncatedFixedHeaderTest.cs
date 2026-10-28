using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that fixed header blocks are validated before their unchecked loads.</summary>
public sealed class TruncatedFixedHeaderTest
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
}
