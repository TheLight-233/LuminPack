using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Security;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the Boolean hook ABI against the netstandard2.1 Core asset.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed unsafe class BooleanBurstHookTest
{
    private static int calls;
    private static bool decline;
    private static bool exactAlias;

    /// <summary>Checks legacy tables, the size threshold, copied storage and fallback.</summary>
    [Fact]
    public void BooleanSlotChecksCompletePinnedStorage()
    {
        try
        {
            foreach (uint version in new uint[] { 2, 3, 4, LuminPackBurst.AbiVersion })
            {
                LuminPackBurst.Register(new() { Version = version, CopyBooleans = &Probe });
                calls = 0;
                decline = false;
                Assert.True(Read(127, invalid: false));
                Assert.Equal(0, calls);
                Assert.True(Read(128, invalid: false));
                Assert.False(Read(129, invalid: true));
                Assert.Equal(version == LuminPackBurst.AbiVersion ? 2 : 0, calls);
            }

            decline = true;
            Assert.True(Read(257, invalid: false));
            Assert.False(Read(257, invalid: true));
            Assert.Equal(4, calls);
        }
        finally
        {
            LuminPackBurst.Clear();
        }
    }

    /// <summary>Checks that an overlapping list payload reaches the native hook as an exact alias.</summary>
    [Fact]
    public void BooleanListReuseResolvesOverlapBeforeDispatch()
    {
        const int length = 257;
        bool[] expected = Enumerable.Range(0, length).Select(i => i % 3 == 1).ToArray();
        List<bool>? target = new(length + sizeof(int));
        var original = target;
        Span<byte> storage = MemoryMarshal.AsBytes(LuminPackMarshal.GetListSpan(target, length + sizeof(int)));
        BinaryPrimitives.WriteInt32LittleEndian(storage, length);
        MemoryMarshal.AsBytes(expected.AsSpan()).CopyTo(storage[sizeof(int)..]);
        ReadOnlySpan<byte> input = storage;
        var reader = new LuminPackReader(ref input);

        try
        {
            LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, CopyBooleans = &Probe });
            calls = 0;
            decline = false;
            exactAlias = false;
            LuminPackInputPolicy.ReadBooleans(ref reader, ref target);
            Assert.Equal(1, calls);
            Assert.True(exactAlias);
            Assert.Same(original, target);
            Assert.Equal(expected, target);
            Assert.Equal(length + sizeof(int), reader.Position);
        }
        finally
        {
            LuminPackBurst.Clear();
        }
    }

    /// <summary>Reads a Boolean array from an unaligned record and checks its copied bytes.</summary>
    /// <param name="length">The payload width in bytes.</param>
    /// <param name="invalid">Whether the last byte is noncanonical.</param>
    /// <returns>Whether the reader accepted all storage bytes.</returns>
    private static bool Read(int length, bool invalid)
    {
        byte[] storage = new byte[length + sizeof(int) + 2];
        Span<byte> input = storage.AsSpan(1, length + sizeof(int));
        BinaryPrimitives.WriteInt32LittleEndian(input, length);
        for (int index = 0; index < length; index++)
            input[sizeof(int) + index] = (byte)(index % 2);
        if (invalid)
            input[^1] = 128;
        ReadOnlySpan<byte> window = input;
        var reader = new LuminPackReader(ref window);
        bool[]? target = new bool[length];
        var original = target;
        bool result = reader.ReadCanonicalBooleans(ref target);
        Assert.Same(original, target);
        Assert.Equal(window.Length, reader.Position);
        Assert.True(input[sizeof(int)..].SequenceEqual(MemoryMarshal.AsBytes(target.AsSpan())));
        return result;
    }

    /// <summary>Exercises the pinned transfer through a scalar ABI stand-in.</summary>
    /// <param name="input">The complete input pointer.</param>
    /// <param name="output">The complete output pointer.</param>
    /// <param name="length">The element count.</param>
    /// <returns>Whether the bytes are canonical or the hook declines.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Probe(byte* input, byte* output, int length)
    {
        calls++;
        exactAlias = input == output;
        if (decline)
            return -1;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        int bits = 0;
        for (int index = 0; index < length; index++)
        {
            byte item = input[index];
            output[index] = item;
            bits |= item;
        }
        return (bits & 0xfe) == 0 ? 1 : 0;
    }
}
