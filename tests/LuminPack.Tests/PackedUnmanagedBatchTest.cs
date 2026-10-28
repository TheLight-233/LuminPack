using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack;
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

    /// <summary>Checks the advancing scalar API against canonical bytes, populated inner padding and canaries.</summary>
    [Fact]
    public void ScalarOperationsPreservePackedBytesInnerPaddingAndCursors()
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
        {
            byte[] storage = Enumerable.Repeat((byte)0xCC, prefix + expected.Length + 9).ToArray();
            Span<byte> span = storage;
            var writer = new LuminPackWriter(ref span);
            writer.Advance(prefix);
            writer.Write((byte)0x17);
            writer.Write(value);
            writer.Write((ushort)0x4351);
            Assert.Equal(prefix + expected.Length, writer.Position);
            Assert.Equal(expected, storage.AsSpan(prefix, expected.Length).ToArray());
            Assert.All(storage.Take(prefix).Concat(storage.Skip(prefix + expected.Length)), item => Assert.Equal((byte)0xCC, item));
            var reader = new LuminPackReader(ref span);
            reader.Advance(prefix);
            Assert.Equal((byte)0x17, reader.Read<byte>());
            var result = reader.Read<PaddedValue>();
            Assert.Equal((ushort)0x4351, reader.Read<ushort>());
            Assert.Equal(prefix + expected.Length, reader.Position);
            Assert.Equal(expected.AsSpan(1, 16).ToArray(), MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref result, 1)).ToArray());
        }
    }

    /// <summary>Contains inner padding that belongs to the raw value and must remain in the wire payload.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PaddedValue
    {
        /// <summary>Stores the initial byte without consuming the following padding.</summary>
        [FieldOffset(0)]
        public byte Tag;
        /// <summary>Stores the eight-byte field after the deliberate internal gap.</summary>
        [FieldOffset(8)]
        public long Number;
    }
}
