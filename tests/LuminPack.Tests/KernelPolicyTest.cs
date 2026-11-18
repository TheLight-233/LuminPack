using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects the one shared copy eligibility policy across .NET and Burst primitive backends.</summary>
public sealed class KernelPolicyTest
{
    /// <summary>Checks every boundary against the established Core short-copy schedule.</summary>
    [Fact]
    public void ShortCopyRangeRetainsCoreFallbacks()
    {
        foreach (bool wide in new[] { false, true })
        foreach (bool medium in new[] { false, true })
        foreach (bool narrow in new[] { false, true })
        foreach (bool bit64 in new[] { false, true })
        foreach (uint count in new uint[] { 0, 1, 8, 15, 16, 17, 31, 32, 33, 63, 64, 127, 128, 129, 160, 192, 224, 255, 256, 257, 1024, uint.MaxValue })
        {
            bool expected = bit64 && narrow && count <= LuminKernelPolicy.ShortCopyLimit;
            Assert.Equal(expected, LuminKernelPolicy.UseShortCopy(bit64, narrow, medium, wide, count));
        }
    }

    /// <summary>Checks the measured 256-bit (256 B) and 512-bit (128 B) medium-copy ranges through 32 KiB.</summary>
    [Fact]
    public void MediumCopyRangesFollowTheMeasuredBounds()
    {
        foreach (bool enabled in new[] { false, true })
        foreach (bool bit64 in new[] { false, true })
        foreach (uint count in new uint[] { 0, 127, 128, 255, 256, 257, 32 * 1024, 32 * 1024 + 1, uint.MaxValue })
        {
            Assert.Equal(bit64 && enabled && count is >= 256 and <= 32 * 1024, LuminKernelPolicy.UseMediumCopy(bit64, enabled, count));
            Assert.Equal(bit64 && enabled && count is >= 128 and <= 32 * 1024, LuminKernelPolicy.UseWideCopy(bit64, enabled, count));
        }
    }

    /// <summary>Checks the zeroed-array threshold and that every result is usable and fully sized.</summary>
    [Fact]
    public void ArraysBelowEightKibibytesAreZeroed()
    {
        foreach (int length in new[] { 0, 1, 2047, 2048, 8191 })
            Assert.All(LuminPackMarshal.AllocateUninitializedArray<byte>(length), item => Assert.Equal(0, item));
        Assert.Equal(8192, LuminPackMarshal.AllocateUninitializedArray<byte>(8192).Length);
        Assert.Equal(2048, LuminPackMarshal.AllocateUninitializedArray<int>(2048).Length);
        Assert.Throws<OverflowException>(() => LuminPackMarshal.AllocateUninitializedArray<byte>(-1));
    }
}
