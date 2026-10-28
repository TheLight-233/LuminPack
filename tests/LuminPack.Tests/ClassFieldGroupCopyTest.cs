using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects packed class fields while copying physically contiguous raw storage.</summary>
public class ClassFieldGroupCopyTest
{
    /// <summary>Preserves primitive framing, struct padding, consumed lengths and merge identity.</summary>
    [Fact]
    public void ContiguousGroupsPreserveWirePaddingAndMergeIdentity()
    {
        var primitive = new PrimitiveGroup { A = 3, B = 5, C = 7, D = 11, E = 13, F = 17, G = 19, H = 23 };
        byte[] expected = new byte[33];
        expected[0] = 8;
        int offset = 1;
        foreach (int number in new[] { 3, 5, 7, 11, 13, 17, 19, 23 })
        {
            BitConverter.TryWriteBytes(expected.AsSpan(offset), number);
            offset += 4;
        }
        Assert.Equal(expected, LuminPackSerializer.Serialize(primitive));
        Assert.Equal(expected, LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<PrimitiveGroup>(expected)));

        RawStorage first = default, second = default;
        Unsafe.InitBlockUnaligned(ref Unsafe.As<RawStorage, byte>(ref first), 0xA5, 16);
        Unsafe.InitBlockUnaligned(ref Unsafe.As<RawStorage, byte>(ref second), 0x5A, 16);
        first.Tag = 29; first.Number = 31;
        second.Tag = 37; second.Number = 41;
        var source = new StructGroup { Head = 43, First = first, Second = second, Tail = -47 };
        expected = new byte[49];
        expected[0] = 4;
        BitConverter.TryWriteBytes(expected.AsSpan(1), source.Head);
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref first, 1)).CopyTo(expected.AsSpan(9));
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref second, 1)).CopyTo(expected.AsSpan(25));
        BitConverter.TryWriteBytes(expected.AsSpan(41), source.Tail);
        Assert.Equal(expected, LuminPackSerializer.Serialize(source));
        var restored = LuminPackSerializer.Deserialize<StructGroup>(expected);
        Assert.Equal(expected, LuminPackSerializer.Serialize(restored));
        var existing = new StructGroup();
        var identity = existing;
        Assert.Equal(expected.Length, LuminPackSerializer.Deserialize(expected, ref existing));
        Assert.Same(identity, existing);
        Assert.Equal(expected, LuminPackSerializer.Serialize(existing));
    }

    /// <summary>Excludes alignment gaps and ignored reference storage in both fresh and merge reads.</summary>
    [Fact]
    public void GappedGroupsPreserveIgnoredReferencesAndPackedWire()
    {
        var source = new GappedGroup { A = 53, B = -59, C = 61, D = -67 };
        byte[] expected = new byte[19];
        expected[0] = 4;
        expected[1] = source.A;
        BitConverter.TryWriteBytes(expected.AsSpan(2), source.B);
        expected[10] = source.C;
        BitConverter.TryWriteBytes(expected.AsSpan(11), source.D);
        Assert.Equal(expected, LuminPackSerializer.Serialize(source));
        var restored = LuminPackSerializer.Deserialize<GappedGroup>(expected);
        Assert.NotNull(restored.Guard);
        Assert.Equal(expected, LuminPackSerializer.Serialize(restored));
        var existing = new GappedGroup();
        object guard = existing.Guard;
        var identity = existing;
        Assert.Equal(expected.Length, LuminPackSerializer.Deserialize(expected, ref existing));
        Assert.Same(identity, existing);
        Assert.Same(guard, existing.Guard);
        Assert.Equal(expected, LuminPackSerializer.Serialize(existing));
    }

    /// <summary>Provides adjacent scalar storage without internal gaps.</summary>
    [LuminPackable, StructLayout(LayoutKind.Sequential)]
    public class PrimitiveGroup
    {
        /// <summary>Stores lane A.</summary>
        public int A;
        /// <summary>Stores lane B.</summary>
        public int B;
        /// <summary>Stores lane C.</summary>
        public int C;
        /// <summary>Stores lane D.</summary>
        public int D;
        /// <summary>Stores lane E.</summary>
        public int E;
        /// <summary>Stores lane F.</summary>
        public int F;
        /// <summary>Stores lane G.</summary>
        public int G;
        /// <summary>Stores lane H.</summary>
        public int H;
    }

    /// <summary>Retains complete raw storage padding inside a field.</summary>
    [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct RawStorage
    {
        /// <summary>Stores the leading byte.</summary>
        [FieldOffset(0)] public byte Tag;
        /// <summary>Stores the aligned payload.</summary>
        [FieldOffset(8)] public long Number;
    }

    /// <summary>Provides consecutive raw struct fields with preserved internal padding.</summary>
    [LuminPackable, StructLayout(LayoutKind.Sequential)]
    public class StructGroup
    {
        /// <summary>Stores the first scalar.</summary>
        public long Head;
        /// <summary>Stores the first raw field.</summary>
        public RawStorage First;
        /// <summary>Stores the second raw field.</summary>
        public RawStorage Second;
        /// <summary>Stores the final scalar.</summary>
        public long Tail;
    }

    /// <summary>Provides padding and an ignored GC reference between serialized fields.</summary>
    [LuminPackable, StructLayout(LayoutKind.Sequential)]
    public class GappedGroup
    {
        /// <summary>Stores the leading byte.</summary>
        public byte A;
        /// <summary>Stores an aligned scalar.</summary>
        public long B;
        /// <summary>Retains a constructor-initialized reference outside the wire contract.</summary>
        [LuminPackIgnore] public object Guard = new();
        /// <summary>Stores the second byte.</summary>
        public byte C;
        /// <summary>Stores the final aligned scalar.</summary>
        public long D;
    }
}
