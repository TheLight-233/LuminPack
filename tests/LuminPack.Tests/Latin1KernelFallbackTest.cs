using System;
using System.Linq;
using System.Runtime.InteropServices;
using LuminPack.Utility;
using Xunit;
namespace LuminPackUnitTest;
/// <summary>Checks compact Latin-1 record kernels with normal and runtime-disabled hardware intrinsics.</summary>
public sealed class Latin1KernelFallbackTest
{
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

