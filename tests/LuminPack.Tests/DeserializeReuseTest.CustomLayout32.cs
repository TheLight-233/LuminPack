#if LUMINPACK_NEXT_GENERATOR
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

public partial class DeserializeReuseTest
{
    /// <summary>Checks native custom arrays with a 32-byte payload and their nullable storage.</summary>
    [Fact]
    public void NativeCustom32ArrayRetainsStorage()
    {
        NativeCustomLayout32[] source = [new() { A = -1, B = 2, C = -3, D = 4 }];
        ref var first = ref source[0];
        CheckOffsets(ref first.A, ref first.B, ref first.C, ref first.D);
        byte[] wire = LuminPackSerializer.Serialize(source);
        NativeCustomLayout32[]? target = null;
        Assert.Equal(wire.Length, LuminPackSerializer.Deserialize(wire, ref target));
        CheckCustomBlock(source, target!, wire);
        CheckCustomNullableBlock(first, tagged: false,
            static values => LuminPackSerializer.Serialize(values),
            static (byte[] data, ref NativeCustomLayout32?[]? value) => LuminPackSerializer.Deserialize(data, ref value));
    }

    /// <summary>Checks tagged custom arrays when the runtime matches the stored layout model.</summary>
    [Fact]
    public void TaggedCustom32ArrayRetainsMatchingStorage()
    {
        TaggedCustomLayout32[] source = [new() { A = 5, B = -6, C = 7, D = -8 }];
        ref var first = ref source[0];
        CheckOffsets(ref first.A, ref first.B, ref first.C, ref first.D);
        byte[] wire = LuminPackSerializer.Serialize(source);
        TaggedCustomLayout32[]? target = null;
        Assert.Equal(wire.Length, LuminPackSerializer.Deserialize(wire, ref target));
        CheckCustomBlock(source, target!, wire);
        CheckCustomNullableBlock(first, tagged: true,
            static values => LuminPackSerializer.Serialize(values),
            static (byte[] data, ref TaggedCustomLayout32?[]? value) => LuminPackSerializer.Deserialize(data, ref value));
    }

    /// <summary>Verifies the four actual field addresses used by the storage model.</summary>
    private static void CheckOffsets(ref long first, ref long second, ref long third, ref long fourth)
    {
        Assert.Equal((nint)8, Unsafe.ByteOffset(ref first, ref second));
        Assert.Equal((nint)16, Unsafe.ByteOffset(ref first, ref third));
        Assert.Equal((nint)24, Unsafe.ByteOffset(ref first, ref fourth));
    }

    /// <summary>Compares the counted wire payload and the full memory of both nonnullable arrays.</summary>
    private static void CheckCustomBlock<T>(T[] source, T[] target, byte[] wire) where T : unmanaged
    {
        Assert.Equal(32, Unsafe.SizeOf<T>());
        Assert.Equal(source.Length, BinaryPrimitives.ReadInt32LittleEndian(wire));
        var bytes = MemoryMarshal.AsBytes(source.AsSpan());
        Assert.True(bytes.SequenceEqual(wire.AsSpan(sizeof(int))));
        Assert.True(bytes.SequenceEqual(MemoryMarshal.AsBytes(target.AsSpan())));
    }

    private delegate int ReadCustomNullableArray<T>(byte[] wire, ref T?[]? values) where T : unmanaged;

    /// <summary>Checks that matching layout transfers preserve padding and validate flags before publication.</summary>
    private static void CheckCustomNullableBlock<T>(T item, bool tagged, Func<T?[], byte[]> write, ReadCustomNullableArray<T> read)
        where T : unmanaged
    {
        const int count = 9;
        int stride = Unsafe.SizeOf<T?>();
        var source = new T?[count];
        for (int i = 1; i < count; i++)
            source[i] = item;
        ref T payload = ref Unsafe.AsRef(in Nullable.GetValueRefOrDefaultRef(in source[1]));
        ref byte origin = ref Unsafe.As<T?, byte>(ref source[1]);
        int payloadOffset = (int)Unsafe.ByteOffset(ref origin, ref Unsafe.As<T, byte>(ref payload));
        Assert.True(payloadOffset is 4 or 8);
        Assert.Equal(payloadOffset + 32, stride);

        var sourceBytes = CustomNullableBytes(source);
        for (int i = 0; i < count; i++)
            sourceBytes.Slice(i * stride + 1, payloadOffset - 1).Fill(0xA5);
        byte[] wire = write(source);
        int header = sizeof(int) + (tagged ? 1 : 0);
        Assert.Equal(count, BinaryPrimitives.ReadInt32LittleEndian(wire));
        Assert.Equal(header + count * stride, wire.Length);
        if (tagged)
            Assert.Equal(payloadOffset == 8 ? (byte)1 : (byte)2, wire[sizeof(int)]);
        Assert.True(sourceBytes.SequenceEqual(wire.AsSpan(header)));

        T?[] alias = Enumerable.Repeat<T?>(item, count).ToArray();
        byte[] previous = CustomNullableBytes(alias).ToArray();
        T?[]? target = alias;
        T?[]? fresh = null;
        int last = header + (count - 1) * stride;
        Assert.Equal((byte)1, wire[last]);
        wire[last] = 0x80;
        Assert.Throws<System.IO.InvalidDataException>(() => read(wire, ref target));
        Assert.Throws<System.IO.InvalidDataException>(() => read(wire, ref fresh));
        Assert.Null(fresh);
        Assert.Same(alias, target);
        Assert.True(previous.AsSpan().SequenceEqual(CustomNullableBytes(alias)));

        wire[last] = 1;
        Assert.Equal(wire.Length, read(wire, ref target));
        Assert.Same(alias, target);
        Assert.True(sourceBytes.SequenceEqual(CustomNullableBytes(alias)));
    }

    /// <summary>Views the entire array storage, including native padding outside the nullable value.</summary>
    private static Span<byte> CustomNullableBytes<T>(T?[] values) where T : unmanaged =>
        MemoryMarshal.CreateSpan(ref Unsafe.As<T?, byte>(ref MemoryMarshal.GetArrayDataReference(values)),
            checked(values.Length * Unsafe.SizeOf<T?>()));
}

/// <summary>A 32-byte user-defined value with native storage.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
public struct NativeCustomLayout32
{
    /// <summary>The first native field.</summary>
    public long A;
    /// <summary>The second native field.</summary>
    public long B;
    /// <summary>The third native field.</summary>
    public long C;
    /// <summary>The last native field.</summary>
    public long D;
}

/// <summary>A 32-byte user-defined value with tagged storage.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Tagged)]
public struct TaggedCustomLayout32
{
    /// <summary>The first tagged field.</summary>
    public long A;
    /// <summary>The second tagged field.</summary>
    public long B;
    /// <summary>The third tagged field.</summary>
    public long C;
    /// <summary>The last tagged field.</summary>
    public long D;
}
#endif
