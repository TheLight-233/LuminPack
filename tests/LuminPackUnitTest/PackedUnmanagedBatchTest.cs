using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects the byte-concatenation contract of the 32-bit block-copy storage views.</summary>
public class PackedUnmanagedBatchTest
{
    /// <summary>Checks real constructed storage widths at every supported batch arity.</summary>
    [Fact]
    public void EveryBatchViewExcludesInterValueAndTrailingPadding()
    {
        MethodInfo sizeOf = typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!;
        for (int count = 1; count <= 15; count++)
        {
            Type definition = typeof(LuminPackWriter).Assembly.GetType("LuminPack.Utility.ViewModel.UnmanagedViewModel`" + count, true)!;
            Type[] arguments = Enumerable.Repeat(typeof(byte), count).ToArray();
            arguments[count / 2] = typeof(PaddedValue);
            Type view = definition.MakeGenericType(arguments);
            int size = (int)sizeOf.MakeGenericMethod(view).Invoke(null, null)!;
            Assert.Equal(16 + count - 1, size);
        }
    }

    /// <summary>Checks every cursor/constraint/return variant against independent canonical bytes and canaries.</summary>
    [Fact]
    public void AllCoreVariantsPreservePackedBytesInnerPaddingAndCursors()
    {
        PaddedValue value = default;
        Unsafe.InitBlockUnaligned(ref Unsafe.As<PaddedValue, byte>(ref value), 0xA5, 16);
        value.Tag = 0x39;
        value.Number = 0x123456789ABCDEF;
        byte[] expected = new byte[19];
        expected[0] = 0x17;
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1)).CopyTo(expected.AsSpan(1));
        BitConverter.TryWriteBytes(expected.AsSpan(17), (ushort)0x4351);

        foreach (int prefix in new[] { 1, 7 })
        for (int variant = 0; variant < 8; variant++)
        {
            byte[] storage = Enumerable.Repeat((byte)0xCC, prefix + expected.Length + 9).ToArray();
            Span<byte> span = storage;
            var writer = new LuminPackWriter(ref span);
            int operationIndex = variant % 2 == 0 ? prefix : prefix - 1;
            writer.Advance(operationIndex);
            int index = prefix;
            int size = Write(ref writer, ref index, value, variant);
            Assert.Equal(expected.Length, size);
            Assert.Equal(prefix, index);
            Assert.Equal(operationIndex, writer.CurrentIndex);
            Assert.Equal(expected, storage.AsSpan(prefix, expected.Length).ToArray());
            Assert.All(storage.Take(prefix).Concat(storage.Skip(prefix + expected.Length)), item => Assert.Equal((byte)0xCC, item));

            var reader = new LuminPackReader(ref span);
            reader.Advance(operationIndex);
            index = prefix;
            var result = Read(ref reader, ref index, variant, out size);
            Assert.Equal(expected.Length, size);
            Assert.Equal(prefix, index);
            Assert.Equal(operationIndex, reader.GetCurrentSpanIndex());
            Assert.Equal((byte)0x17, result.Head);
            Assert.Equal((ushort)0x4351, result.Tail);
            Assert.Equal(expected.AsSpan(1, 16).ToArray(), MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref result.Value, 1)).ToArray());
        }
    }

    /// <summary>Invokes one public fixed batch writer without advancing either caller cursor.</summary>
    /// <param name="writer">The fixed-span operation.</param>
    /// <param name="index">The separate explicit cursor.</param>
    /// <param name="value">A raw value containing deliberately populated padding.</param>
    /// <param name="variant">The constraint, cursor and return-value combination.</param>
    /// <returns>The reported or known canonical byte count.</returns>
    private static int Write(ref LuminPackWriter writer, ref int index, PaddedValue value, int variant)
    {
        switch (variant)
        {
            case 0: return writer.WriteUnmanaged((byte)0x17, value, (ushort)0x4351);
            case 1: return writer.WriteUnmanaged(ref index, (byte)0x17, value, (ushort)0x4351);
            case 2: return writer.DangerousWriteUnmanaged((byte)0x17, value, (ushort)0x4351);
            case 3: return writer.DangerousWriteUnmanaged(ref index, (byte)0x17, value, (ushort)0x4351);
            case 4: writer.WriteUnmanagedWithoutSizeReturn((byte)0x17, value, (ushort)0x4351); break;
            case 5: writer.WriteUnmanagedWithoutSizeReturn(ref index, (byte)0x17, value, (ushort)0x4351); break;
            case 6: writer.DangerousWriteUnmanagedWithoutSizeReturn((byte)0x17, value, (ushort)0x4351); break;
            case 7: writer.DangerousWriteUnmanagedWithoutSizeReturn(ref index, (byte)0x17, value, (ushort)0x4351); break;
            default: throw new ArgumentOutOfRangeException(nameof(variant));
        }
        return 19;
    }

    /// <summary>Invokes the corresponding public reader without advancing its caller-owned cursors.</summary>
    /// <param name="reader">The canonical input with nonzero prefix.</param>
    /// <param name="index">The separate explicit cursor.</param>
    /// <param name="variant">The constraint, cursor and return-value combination.</param>
    /// <param name="size">Receives the reported or known canonical byte count.</param>
    /// <returns>All three reconstructed raw values.</returns>
    private static (byte Head, PaddedValue Value, ushort Tail) Read(ref LuminPackReader reader, ref int index, int variant, out int size)
    {
        byte head;
        PaddedValue value;
        ushort tail;
        size = 19;
        switch (variant)
        {
            case 0: size = reader.ReadUnmanaged(out head, out value, out tail); break;
            case 1: size = reader.ReadUnmanaged(ref index, out head, out value, out tail); break;
            case 2: size = reader.DangerousReadUnmanaged(out head, out value, out tail); break;
            case 3: size = reader.DangerousReadUnmanaged(ref index, out head, out value, out tail); break;
            case 4: reader.ReadUnmanagedWithoutSizeReturn(out head, out value, out tail); break;
            case 5: reader.ReadUnmanagedWithoutSizeReturn(ref index, out head, out value, out tail); break;
            case 6: reader.DangerousReadUnmanagedWithoutSizeReturn(out head, out value, out tail); break;
            case 7: reader.DangerousReadUnmanagedWithoutSizeReturn(ref index, out head, out value, out tail); break;
            default: throw new ArgumentOutOfRangeException(nameof(variant));
        }
        return (head, value, tail);
    }

    /// <summary>Contains inner padding that belongs to the raw value and must remain in the wire payload.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PaddedValue
    {
        /// <summary>Stores the initial byte without consuming the following padding.</summary>
        [FieldOffset(0)] public byte Tag;
        /// <summary>Stores the eight-byte field after the deliberate internal gap.</summary>
        [FieldOffset(8)] public long Number;
    }
}
