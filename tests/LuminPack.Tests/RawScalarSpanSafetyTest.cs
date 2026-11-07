using System.Numerics;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Option;
using LuminPack.Core;
using Xunit;

/// <summary>Protects bounded scalar facades independently of unchecked address-only storage kernels.</summary>
public sealed class RawScalarSpanSafetyTest
{
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
