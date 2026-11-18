#if LUMINPACK_NEXT_GENERATOR
using System.Runtime.CompilerServices;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

public partial class DeserializeReuseTest
{
    /// <summary>Validates the second group and the tail of the native-width decimal block before reuse.</summary>
    /// <param name="index">The element holding the malformed presence flag.</param>
    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8)]
    public void NullableDecimalArrayValidatesBeforeReuse(int index)
    {
        const int count = 9;
        const int tagOffset = sizeof(int);
        const int headerWidth = tagOffset + 1;
        int stride = Unsafe.SizeOf<decimal?>();
        var source = new decimal?[count];
        var alias = new decimal?[count];
        for (int i = 0; i < count; i++)
        {
            source[i] = i % 3 == 0 ? null : decimal.MaxValue - i;
            alias[i] = -19m;
        }
        byte[] wire = LuminPackSerializer.Serialize(source);
        Assert.True(stride is 20 or 24);
        Assert.Equal(headerWidth + count * stride, wire.Length);
        Assert.Equal(stride == 24 ? (byte)1 : (byte)2, wire[tagOffset]);

        int offset = headerWidth + index * stride;
        Assert.Equal((byte)1, wire[offset]);
        wire[offset] = 0x80;
        decimal?[]? target = alias;
        Assert.Throws<System.IO.InvalidDataException>(() => LuminPackSerializer.Deserialize(wire, ref target));
        Assert.Same(alias, target);
        Assert.All(alias, item => Assert.Equal<decimal?>(-19m, item));

        wire[offset] = 1;
        Assert.Equal(wire.Length, LuminPackSerializer.Deserialize(wire, ref target));
        Assert.Same(alias, target);
        Assert.Equal(source, alias);
    }
}
#endif
