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
        foreach (uint count in new uint[] { 0, 1, 8, 15, 16, 17, 31, 32, 63, 64, 127, 128, 129, 160, 192, 224, 255, 256, 257, 1024, uint.MaxValue })
        {
            bool expected = bit64 && narrow && count <= 256 && (count <= 128 || medium || wide);
            Assert.Equal(expected, LuminKernelPolicy.UseShortCopy(bit64, narrow, medium, wide, count));
        }
    }
}
