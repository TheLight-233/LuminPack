using System.Buffers.Binary;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Option;
using Xunit;

/// <summary>Checks equivalent direct framework entries without weakening Reader validation.</summary>
public class FrameworkEntryTest
{
    /// <summary>Checks complete storage, allocation entries and failed publication for every enum backing width.</summary>
    [Fact]
    public void EnumWidthsKeepRawBytesAndRejectTruncation()
    {
        CheckEnum<EByte>((EByte)255, static (EByte value) => LuminPackSerializer.Serialize(value), static (ReadOnlySpan<byte> bytes, ref EByte value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckEnum<ESByte>((ESByte)(-127), static (ESByte value) => LuminPackSerializer.Serialize(value), static (ReadOnlySpan<byte> bytes, ref ESByte value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckEnum<EShort>((EShort)short.MinValue, static (EShort value) => LuminPackSerializer.Serialize(value), static (ReadOnlySpan<byte> bytes, ref EShort value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckEnum<EUShort>((EUShort)ushort.MaxValue, static (EUShort value) => LuminPackSerializer.Serialize(value), static (ReadOnlySpan<byte> bytes, ref EUShort value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckEnum<EInt>((EInt)int.MinValue, static (EInt value) => LuminPackSerializer.Serialize(value), static (ReadOnlySpan<byte> bytes, ref EInt value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckEnum<EUInt>((EUInt)uint.MaxValue, static (EUInt value) => LuminPackSerializer.Serialize(value), static (ReadOnlySpan<byte> bytes, ref EUInt value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckEnum<ELong>((ELong)long.MinValue, static (ELong value) => LuminPackSerializer.Serialize(value), static (ReadOnlySpan<byte> bytes, ref ELong value) => LuminPackSerializer.Deserialize(bytes, ref value));
        CheckEnum<EULong>((EULong)ulong.MaxValue, static (EULong value) => LuminPackSerializer.Serialize(value), static (ReadOnlySpan<byte> bytes, ref EULong value) => LuminPackSerializer.Deserialize(bytes, ref value));
    }

    /// <summary>Reads through the concrete enum overload.</summary>
    /// <typeparam name="T">The enum storage type.</typeparam>
    /// <param name="bytes">The complete or truncated input.</param>
    /// <param name="value">The destination.</param>
    /// <returns>The consumed width.</returns>
    private delegate int ReadEnum<T>(ReadOnlySpan<byte> bytes, ref T value);

    /// <summary>Compares against independent native bytes and checks all rejected prefixes.</summary>
    /// <typeparam name="T">The closed enum type.</typeparam>
    /// <param name="source">The complete raw bits.</param>
    /// <param name="write">The concrete allocating writer.</param>
    /// <param name="read">The concrete reader.</param>
    private static void CheckEnum<T>(T source, Func<T, byte[]> write, ReadEnum<T> read) where T : unmanaged, Enum
    {
        byte[] expected = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref source, 1)).ToArray();
        Assert.Equal(expected, write(source));
        T target = default;
        Assert.Equal(Unsafe.SizeOf<T>(), read(expected, ref target));
        Assert.Equal(source, target);
        for (int length = 0; length < expected.Length; length++)
        {
            target = source;
            Assert.ThrowsAny<Exception>(() => read(expected.AsSpan(0, length), ref target));
            Assert.Equal(source, target);
        }
    }

    /// <summary>Checks word-rounded BitArray framing and failed input across every binary profile.</summary>
    [Fact]
    public void BitArrayProfilesKeepTheirOriginalWireAndReuse()
    {
        CheckBits<LuminPackUtf8LengthOptions>();
        CheckBits<LuminPackUtf8TokenOptions>();
        CheckBits<LuminPackUtf16LengthOptions>();
        CheckBits<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks exact independent bits, null/empty markers and every short prefix.</summary>
    /// <typeparam name="TProfile">The compile-time profile.</typeparam>
    private static void CheckBits<TProfile>() where TProfile : struct, ILuminPackOptions
    {
        foreach (int length in new[] { 0, 1, 7, 8, 9, 31, 32, 33, 63, 64, 65, 127, 128, 129, 257 })
        {
            var source = new BitArray(length);
            byte[] expected = new byte[4 + (length + 31) / 32 * 4];
            BinaryPrimitives.WriteInt32LittleEndian(expected, length);
            for (int i = 0; i < length; i++) if (source[i] = i % 3 == 0) expected[4 + i / 8] |= (byte)(1 << (i & 7));
            Assert.Equal(expected, LuminPackSerializer.Serialize<TProfile>(source));
            var target = new BitArray(length, true);
            var original = target;
            Assert.Equal(expected.Length, LuminPackSerializer.Deserialize<TProfile>(expected, ref target));
            Assert.Same(original, target);
            for (int i = 0; i < length; i++) Assert.Equal(source[i], target[i]);
            for (int prefix = 0; prefix < expected.Length; prefix++)
            {
                target = new BitArray(length, true);
                original = target;
                Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<TProfile>(expected.AsSpan(0, prefix), ref target));
                Assert.Same(original, target);
                for (int i = 0; i < length; i++) Assert.True(target[i]);
            }
        }
        BitArray? absent = null;
        byte[] marker = LuminPackSerializer.Serialize<TProfile>(absent);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, marker);
        LuminPackSerializer.Deserialize<TProfile>(marker, ref absent);
        Assert.Null(absent);
    }
}

public enum EByte : byte { Zero }
public enum ESByte : sbyte { Zero }
public enum EShort : short { Zero }
public enum EUShort : ushort { Zero }
public enum EInt : int { Zero }
public enum EUInt : uint { Zero }
public enum ELong : long { Zero }
public enum EULong : ulong { Zero }
