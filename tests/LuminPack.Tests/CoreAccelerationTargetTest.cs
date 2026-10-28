using LuminPack.Core;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Verifies modern assets retain native SIMD dispatch and legacy generated entry names.</summary>
public sealed class CoreAccelerationTargetTest
{
    /// <summary>Checks that native hook publication is absent from modern .NET assets.</summary>
    [Fact]
    public void ModernAssetDoesNotCompileBurstHooks()
        => Assert.Null(typeof(LuminPackWriter).Assembly.GetType("LuminPack.Core.LuminPackBurst"));

    /// <summary>Checks the compatibility entry returns the canonical transfer result and identical bytes.</summary>
    [Fact]
    public void LegacyTransferEntryForwardsCanonicalImplementation()
    {
        int[] source = Enumerable.Range(0, 37).ToArray();
        byte[] canonical = new byte[148], legacy = new byte[148];
        int done = LuminPortable<int, IntLayout>.Pack(ref source[0], source.Length, ref canonical[0]);
        Assert.Equal(done, LuminPortableTransfer<int, IntLayout>.Pack(ref source[0], source.Length, ref legacy[0]));
        Assert.Equal(canonical, legacy);
        int[] first = new int[37], second = new int[37];
        Assert.Equal(LuminPortable<int, IntLayout>.Unpack(ref canonical[0], source.Length, ref first[0]),
            LuminPortableTransfer<int, IntLayout>.Unpack(ref canonical[0], source.Length, ref second[0]));
        Assert.Equal(first, second);
    }

    private readonly struct IntLayout : ILuminPortableLayout
    {
        public int WireSize => 4;
        /// <summary>Describes the contiguous four-byte integer storage.</summary>
        /// <param name="storageOffsets">The four byte offsets.</param>
        public void Describe(Span<int> storageOffsets)
        {
            for (int index = 0; index < 4; index++) storageOffsets[index] = index;
        }
    }
}
