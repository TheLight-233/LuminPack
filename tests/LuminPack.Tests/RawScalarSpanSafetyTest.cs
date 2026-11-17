using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Numerics;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Option;
using LuminPack.Core;
using LuminPack.Utility;
using Xunit;

/// <summary>Protects bounded scalar facades independently of unchecked address-only storage kernels.</summary>
public sealed class RawScalarSpanSafetyTest
{
    /// <summary>Compares generated frozen writers and unaligned reservations with explicit key/value wire order.</summary>
    [Fact]
    public void FrozenRawPairsKeepOrderAcrossVectorTails()
    {
        foreach (int count in new[] { 0, 1, 15, 16, 17, 31, 32, 33, 36, 39, 64, 65 })
        {
            var narrow = Enumerable.Range(0, count).ToFrozenDictionary(index => index * -7, index => index * 19 + 3);
            var wide = Enumerable.Range(0, count).ToFrozenDictionary(index => index * -5000000007L, index => index * 9000000011L + 5);
            Check(narrow, LuminPackSerializer.Serialize(narrow));
            Check(wide, LuminPackSerializer.Serialize(wide));
        }

        static void Check<T>(FrozenDictionary<T, T> frozen, byte[] serialized) where T : unmanaged
        {
            int width = Marshal.SizeOf<T>();
            byte[] expected = new byte[sizeof(int) + 2 * width * frozen.Count];
            BinaryPrimitives.WriteInt32LittleEndian(expected, frozen.Count);
            var pairs = MemoryMarshal.Cast<byte, T>(expected.AsSpan(sizeof(int)));
            int index = 0;
            foreach (var pair in frozen)
            {
                pairs[index++] = pair.Key;
                pairs[index++] = pair.Value;
            }
            Assert.Equal(expected, serialized);

            byte[] guarded = Enumerable.Repeat((byte)0xAC, expected.Length + 2).ToArray();
            Span<byte> output = guarded.AsSpan(1, expected.Length);
            var writer = new LuminPackWriter(ref output);
            LuminFrozenConstruction.WritePairs(ref writer, frozen.Keys.AsSpan(), frozen.Values.AsSpan());
            Assert.Equal(expected.Length, writer.Position);
            Assert.Equal(expected, output.ToArray());
            Assert.Equal((byte)0xAC, guarded[0]);
            Assert.Equal((byte)0xAC, guarded[^1]);
        }
    }

    /// <summary>Retains the scalar key-before-value load order for an overlapping caller-supplied span.</summary>
    /// <param name="overlapValues">Whether the values span overlaps the destination instead of the keys span.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrozenRawPairsKeepScalarOrderWhenInputOverlapsOutput(bool overlapValues)
    {
        const int count = 33;
        const int outputStart = 3;
        const int sourceStart = 29;
        var source = Enumerable.Range(0, count).Select(index => index + 1000).ToArray();
        var other = Enumerable.Range(0, count).Select(index => -index - 250).ToArray();
        byte[] storage = new byte[sizeof(int) + 2 * count * sizeof(int) + outputStart + 1];
        MemoryMarshal.AsBytes(source.AsSpan()).CopyTo(storage.AsSpan(sourceStart));
        byte[] expected = (byte[])storage.Clone();
        Span<int> prior = MemoryMarshal.Cast<byte, int>(expected.AsSpan(sourceStart, count * sizeof(int)));
        Span<int> packed = MemoryMarshal.Cast<byte, int>(expected.AsSpan(outputStart, (1 + 2 * count) * sizeof(int)));
        packed[0] = count;
        for (int index = 0; index < count; index++)
        {
            packed[1 + 2 * index] = overlapValues ? other[index] : prior[index];
            packed[2 + 2 * index] = overlapValues ? prior[index] : other[index];
        }

        Span<byte> output = storage.AsSpan(outputStart, (1 + 2 * count) * sizeof(int));
        Span<int> aliased = MemoryMarshal.Cast<byte, int>(storage.AsSpan(sourceStart, count * sizeof(int)));
        var writer = new LuminPackWriter(ref output);
        LuminFrozenConstruction.WritePairs<int, int>(ref writer, overlapValues ? other : aliased, overlapValues ? aliased : other);
        Assert.Equal(expected, storage);
    }

    /// <summary>Checks dense and sparse set wire order through the concrete and both set interface writers.</summary>
    [Fact]
    public void DenseRawHashSetKeepsWireOrderThroughInterfaces()
    {
        foreach (int count in new[] { 0, 15, 16, 17, 19, 23, 24, 25, 33, 100 })
        {
            var set = new HashSet<int>(count);
            for (int index = 0; index < count; index++)
                set.Add(unchecked(index * -1640531527));
            Check(set);
            if (count > 4)
            {
                set.Remove(0);
                set.Remove(unchecked(3 * -1640531527));
                Check(set);
            }
        }

        static void Check(HashSet<int> set)
        {
            byte[] expected = new byte[sizeof(int) * (set.Count + 1)];
            BinaryPrimitives.WriteInt32LittleEndian(expected, set.Count);
            int offset = sizeof(int);
            foreach (int item in set)
            {
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(offset), item);
                offset += sizeof(int);
            }

            Assert.Equal(expected, LuminPackSerializer.Serialize(set));
            Assert.Equal(expected, LuminPackSerializer.Serialize<ISet<int>>(set));
            Assert.Equal(expected, LuminPackSerializer.Serialize<IReadOnlySet<int>>(set));
            byte[] guarded = Enumerable.Repeat((byte)0xD7, expected.Length + 2).ToArray();
            Span<byte> output = guarded.AsSpan(1, expected.Length);
            var writer = new LuminPackWriter(ref output);
            LuminPack.Code.LuminPackMarshal.WriteHashSetRaw(ref writer, set);
            Assert.Equal(expected.Length, writer.Position);
            Assert.Equal(expected, output.ToArray());
            Assert.Equal((byte)0xD7, guarded[0]);
            Assert.Equal((byte)0xD7, guarded[^1]);
        }
    }

    /// <summary>Checks raw dictionary pairs across vector widths, tails, and unaligned output positions.</summary>
    [Fact]
    public void DenseRawDictionaryKeepsPackedKeyValueOrder()
    {
        for (int count = 0; count <= 19; count++)
        {
            var values = new Dictionary<int, int>(count);
            for (int index = 0; index < count; index++)
                values.Add(-index - 19, index * 787 + 41);

            byte[] expected = new byte[sizeof(int) + count * 2 * sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(expected, count);
            int offset = sizeof(int);
            foreach (var pair in values)
            {
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(offset), pair.Key);
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(offset + sizeof(int)), pair.Value);
                offset += 2 * sizeof(int);
            }

            Assert.Equal(expected, LuminPackSerializer.Serialize(values));
            byte[] storage = Enumerable.Repeat((byte)0xB9, expected.Length + 2).ToArray();
            Span<byte> buffer = storage.AsSpan(1, expected.Length);
            var writer = new LuminPackWriter(ref buffer);
            writer.WriteCollectionHeader(count);
            LuminPack.Code.LuminPackMarshal.WriteDenseRawPairs(ref writer, values, count);
            Assert.Equal(expected.Length, writer.Position);
            Assert.Equal(expected, buffer.ToArray());
            Assert.Equal((byte)0xB9, storage[0]);
            Assert.Equal((byte)0xB9, storage[^1]);
        }
    }

    /// <summary>Preserves optional framing, compressed bytes, raw storage and exact write boundaries.</summary>
    [Fact]
    public void RawCollectionWritesKeepCompressionAndHeaderContracts()
    {
        foreach (int count in new[] { 0, 1, 17, 1024 })
        foreach (bool header in new[] { false, true })
        foreach (bool compressed in new[] { false, true })
        {
            int[] values = Enumerable.Range(0, count).Select(index => index % 7 - 3).ToArray();
            int capacity = LuminPackWriter.Size<int>(count, header, compressed);
            byte[] storage = Enumerable.Repeat((byte)0xCD, capacity + 16).ToArray();
            int written = LuminPackWriter.Write<int>(storage.AsSpan(8, capacity), values, count, header, compressed);
            Assert.InRange(written, 0, capacity);
            Assert.All(storage[..8], value => Assert.Equal((byte)0xCD, value));
            Assert.All(storage[(8 + written)..], value => Assert.Equal((byte)0xCD, value));
            int[]? restored = null;
            int consumed = LuminPackReader.Read<int>(storage.AsSpan(8, written), ref restored, count, header, compressed);
            Assert.Equal(written, consumed);
            Assert.Equal(values, restored);
            if (!compressed)
            {
                int prefix = header ? sizeof(int) : 0;
                Assert.Equal(prefix + count * sizeof(int), written);
                Assert.True(MemoryMarshal.AsBytes(values.AsSpan()).SequenceEqual(storage.AsSpan(8 + prefix, count * sizeof(int))));
            }
        }
    }

    /// <summary>Rejects every shortened input window before mutating any supported scalar width.</summary>
    [Fact]
    public void ConcreteScalarEntriesValidateTheirCompleteWindow()
    {
        Check<byte>(0x77, 0x11, static (ReadOnlySpan<byte> bytes, ref byte value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<short>(0x7777, 0x1122, static (ReadOnlySpan<byte> bytes, ref short value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<int>(0x77777777, 0x11223344, static (ReadOnlySpan<byte> bytes, ref int value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<long>(long.MaxValue, long.MinValue + 17, static (ReadOnlySpan<byte> bytes, ref long value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<Guid>(Guid.Empty, new Guid("e50e71a5-4367-4ba1-98d5-a0d6e3e89a19"), static (ReadOnlySpan<byte> bytes, ref Guid value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<decimal>(decimal.MaxValue, decimal.MinValue, static (ReadOnlySpan<byte> bytes, ref decimal value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<Vector4>(Vector4.Zero, new Vector4(1, -2, 3, -4), static (ReadOnlySpan<byte> bytes, ref Vector4 value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<Matrix3x2>(Matrix3x2.Identity, new Matrix3x2(1, 2, 3, 4, 5, 6), static (ReadOnlySpan<byte> bytes, ref Matrix3x2 value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<Matrix4x4>(Matrix4x4.Identity, Matrix4x4.CreateScale(137), static (ReadOnlySpan<byte> bytes, ref Matrix4x4 value) => LuminPackSerializer.Deserialize(bytes, ref value));
    }

    /// <summary>Keeps explicit profile and generic entries bounded as well as the concrete default facade.</summary>
    [Fact]
    public void ProfileAndGenericEntriesValidateBeforeAssignment()
    {
        Check<Guid>(Guid.Empty, new Guid("7f3311a7-42b2-47c1-9569-a7c6a10ad5f7"), static (ReadOnlySpan<byte> bytes, ref Guid value) => LuminPackSerializer.Deserialize<Guid, LuminPackUtf8TokenOptions>(bytes, ref value));
        Check<int>(int.MaxValue, int.MinValue, static (ReadOnlySpan<byte> bytes, ref int value) => LuminPackSerializer.Deserialize(bytes, ref value));
    }

    /// <summary>Rejects managed-reference storage before a raw collection API can copy pointer bytes.</summary>
    [Fact]
    public void RawCollectionsRejectManagedReferences()
    {
        byte[] input = new byte[IntPtr.Size];
        string[] target = ["sentinel"];
        object original = target;
        Assert.ThrowsAny<Exception>(() => LuminPackReader.Read(input, ref target, 1, false, false));
        Assert.Same(original, target);
        Assert.Equal("sentinel", target[0]);
        byte[] output = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        Assert.ThrowsAny<Exception>(() => LuminPackWriter.Write<string>(output, target.AsSpan(), 1, false, false));
        Assert.All(output, value => Assert.Equal((byte)0xA5, value));
        WithReference[] nested = [new() { Value = original }];
        Assert.ThrowsAny<Exception>(() => LuminPackReader.Read(input, ref nested, 1, false, false));
        Assert.Same(original, nested[0].Value);
    }

    /// <summary>Represents a value type whose raw bytes include a managed object reference.</summary>
    private struct WithReference
    {
        internal object? Value;
    }

    /// <summary>Reads one concrete generated bounded entry.</summary>
    /// <typeparam name="T">The raw scalar storage type.</typeparam>
    /// <param name="bytes">The retained input window.</param>
    /// <param name="value">The destination storage.</param>
    /// <returns>The complete consumed record width.</returns>
    private delegate int ReadScalar<T>(ReadOnlySpan<byte> bytes, ref T value);

    /// <summary>Places complete backing storage behind truncated windows and checks exact destination bytes.</summary>
    /// <typeparam name="T">The reference-free scalar storage type.</typeparam>
    /// <param name="initial">The destination before the read.</param>
    /// <param name="expected">The independently formed complete wire value.</param>
    /// <param name="read">The concrete facade selected by the fixture.</param>
    private static void Check<T>(T initial, T expected, ReadScalar<T> read) where T : unmanaged
    {
        byte[] wire = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref expected, 1)).ToArray();
        byte[] original = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref initial, 1)).ToArray();
        byte[] backing = new byte[wire.Length + 17];
        wire.CopyTo(backing, 0);
        for (int length = 0; length < wire.Length; length++)
        {
            T target = initial;
            Assert.ThrowsAny<Exception>(() => read(backing.AsSpan(0, length), ref target));
            Assert.Equal(original, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref target, 1)).ToArray());
        }
        T complete = initial;
        Assert.Equal(wire.Length, read(backing, ref complete));
        Assert.Equal(wire, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref complete, 1)).ToArray());
    }
}
