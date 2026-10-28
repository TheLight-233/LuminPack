using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Utility;
using Xunit;
namespace LuminPackUnitTest;
/// <summary>Checks compact Latin-1 record kernels with normal and runtime-disabled hardware intrinsics.</summary>
public sealed class Latin1KernelFallbackTest
{
    /// <summary>Checks that empty validation and comparison never access either reference.</summary>
    [Fact]
    public void EmptyKernelsDoNotAccessReferences()
    {
        Assert.True(StringSerializer.IsLatin1(string.Empty));
        Assert.True(StringSerializer.TryNarrowLatin1(ref Unsafe.NullRef<char>(), ref Unsafe.NullRef<byte>(), 0));
        Assert.True(StringSerializer.MatchesLatin1(ref Unsafe.NullRef<byte>(), string.Empty));
    }

    /// <summary>Checks all byte values, unaligned payloads, exact source ranges and untouched destination guards.</summary>
    [Fact]
    public void CompactPayloadsRespectExactUnalignedRanges()
    {
        for (int length = 1; length <= 257; length++)
        {
            for (int offset = 0; offset < 8; offset++)
            {
                char[] chars = Enumerable.Repeat('\uFFFF', length + offset + 1).ToArray();
                byte[] expected = new byte[length];
                for (int i = 0; i < length; i++)
                    chars[offset + i] = (char)(expected[i] = (byte)(i * 73 + length));
                byte[] bytes = Enumerable.Repeat((byte)0xA5, length + offset + 8).ToArray();
                string text = new string(chars, offset, length);
                Assert.True(StringSerializer.IsLatin1(text));
                Assert.True(StringSerializer.TryNarrowLatin1(ref chars[offset], ref bytes[offset], length));
                Assert.Equal(expected, bytes.AsSpan(offset, length).ToArray());
                Assert.All(bytes.AsSpan(0, offset).ToArray(), b => Assert.Equal((byte)0xA5, b));
                Assert.All(bytes.AsSpan(offset + length).ToArray(), b => Assert.Equal((byte)0xA5, b));
                Assert.Equal(text, StringSerializer.CreateLatin1String(ref bytes[offset], length));
                Assert.True(StringSerializer.MatchesLatin1(ref bytes[offset], text));
                Assert.Same(text, StringSerializer.ReadLatin1PayloadCore(ref bytes[offset], length, text));
            }
        }
    }

    /// <summary>Rejects non-Latin-1 high bytes in every word lane and scalar tail.</summary>
    /// <param name="invalid">The code unit whose high byte must not be discarded.</param>
    [Theory]
    [InlineData(0x0100)]
    [InlineData(0x8000)]
    [InlineData(0xD800)]
    [InlineData(0xFFFF)]
    public void InvalidCodeUnitsAreRejectedAtEveryPosition(int invalid)
    {
        for (int length = 1; length <= 65; length++)
        {
            char[] chars = Enumerable.Repeat('\u00A5', length).ToArray();
            byte[] payload = Enumerable.Repeat((byte)0xA5, length).ToArray();
            byte[] destination = new byte[length + 2];
            for (int index = 0; index < length; index++)
            {
                chars[index] = (char)invalid;
                string text = new string(chars);
                Assert.False(StringSerializer.IsLatin1(text));
                Assert.False(StringSerializer.TryNarrowLatin1(ref chars[0], ref destination[1], length));
                Assert.False(StringSerializer.MatchesLatin1(ref payload[0], text));
                Assert.Equal(0, destination[0]);
                Assert.Equal(0, destination[length + 1]);
                chars[index] = '\u00A5';
            }
        }
    }

    /// <summary>Checks every character, every mismatch and the complete widened output, including scalar-only long ranges.</summary>
    [Fact]
    public void CompactPayloadsCoverEveryCharacterAndRejectEveryMismatch()
    {
        var random = new Random(137);
        for (int length = 1; length <= 257; length++)
        {
            string text = new string(Enumerable.Range(0, length).Select(_ => (char)random.Next(256)).ToArray());
            byte[] bytes = new byte[length + 6];
            Assert.True(StringSerializer.TryNarrowLatin1(ref MemoryMarshal.GetReference(text.AsSpan()), ref bytes[3], length));
            Assert.Equal(text, StringSerializer.CreateLatin1String(ref bytes[3], length));
            Assert.True(StringSerializer.MatchesLatin1(ref bytes[3], text));
            for (int mismatch = 0; mismatch < length; mismatch++)
            {
                bytes[3 + mismatch] ^= 1;
                Assert.False(StringSerializer.MatchesLatin1(ref bytes[3], text));
                bytes[3 + mismatch] ^= 1;
            }
            Assert.Equal(0, bytes[2]);
            Assert.Equal(0, bytes[length + 3]);
        }
    }
}

