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

    /// <summary>Checks the measured copy ranges of every processor class and the retained default policy.</summary>
    [Fact]
    public void CopyRangesFollowTheMeasuredBounds()
    {
        uint[] counts = { 1, 63, 64, 79, 80, 127, 128, 255, 256, 511, 512, 24 * 1024, 24 * 1024 + 1, 32 * 1024, 32 * 1024 + 1, uint.MaxValue };
        foreach (var processor in Enum.GetValues<LuminKernelPolicy.ProcessorClass>())
        foreach (bool bit64 in new[] { false, true })
        foreach (bool has256 in new[] { false, true })
        foreach (bool has512 in new[] { false, true })
        {
            var ranges = LuminKernelPolicy.SelectCopyRanges(bit64, has256, has512, processor);
            foreach (uint count in counts)
            {
                bool enabled = bit64 && has256;
                bool zen5 = processor == LuminKernelPolicy.ProcessorClass.AmdZen5 && has512;
                bool amd = processor is LuminKernelPolicy.ProcessorClass.AmdZen5 or LuminKernelPolicy.ProcessorClass.Amd;
                bool intelWide = processor == LuminKernelPolicy.ProcessorClass.Intel && has512;
                bool wide = enabled && (zen5 ? count is >= 512 and <= 24 * 1024 : intelWide && count is >= 128 and <= 32 * 1024);
                bool medium = enabled && !intelWide && (zen5 ? count is >= 80 and <= 511 :
                    amd ? count is >= 80 and <= 32 * 1024 : count is >= 256 and <= 32 * 1024);
                Assert.Equal(wide, ranges.Wide(count));
                Assert.Equal(medium, ranges.Medium(count));
            }
        }

        foreach (bool enabled in new[] { false, true })
        foreach (bool bit64 in new[] { false, true })
        foreach (uint count in counts)
            Assert.Equal(bit64 && enabled && count is >= 256 and <= 32 * 1024, LuminKernelPolicy.UseMediumCopy(bit64, enabled, count));
    }

    /// <summary>Checks the zeroed-array thresholds and that every result is usable and fully sized.</summary>
    [Fact]
    public void ArraysBelowTheMeasuredThresholdAreZeroed()
    {
        Assert.Equal(16UL * 1024, LuminKernelPolicy.SelectZeroedArrayLimit(LuminKernelPolicy.ProcessorClass.Intel));
        foreach (var processor in new[] { LuminKernelPolicy.ProcessorClass.AmdZen5, LuminKernelPolicy.ProcessorClass.Amd, LuminKernelPolicy.ProcessorClass.Other })
            Assert.Equal(5UL * 1024, LuminKernelPolicy.SelectZeroedArrayLimit(processor));
        // Every class zeroes below 5 KiB.
        foreach (int length in new[] { 0, 1, 2047, 2048, 5119 })
            Assert.All(LuminPackMarshal.AllocateUninitializedArray<byte>(length), item => Assert.Equal(0, item));
        Assert.Equal(16384, LuminPackMarshal.AllocateUninitializedArray<byte>(16384).Length);
        Assert.Equal(2048, LuminPackMarshal.AllocateUninitializedArray<int>(2048).Length);
        Assert.Throws<OverflowException>(() => LuminPackMarshal.AllocateUninitializedArray<byte>(-1));
    }
}
