#if !UNITY_5_3_OR_NEWER
using LuminPack.Burst;

unsafe
{
    KernelContract.Verify(BurstKernels.Copy, BurstKernels.CountMatch,
        BurstKernels.Xor, BurstKernels.CopyMatch);
    LuminPack.Burst.Tests.TextKernelContract.Verify(BurstKernels.Utf8Encode, BurstKernels.Utf8Decode,
        BurstKernels.Utf8Equal, BurstKernels.AsciiEncode);
    LuminPack.Burst.Tests.TextKernelContract.VerifyBytes(BurstKernels.Equal);
    LuminPack.Burst.Tests.Latin1KernelContract.Verify(BurstKernels.IsLatin1, BurstKernels.NarrowLatin1, BurstKernels.WidenLatin1, BurstKernels.Latin1Equal);
    LuminPack.Burst.Tests.LayoutCryptoContract.Verify(BurstKernels.Layout, BurstKernels.ChaCha20);
    LuminPack.Burst.Tests.LayoutCryptoContract.VerifyPlanned(BurstKernels.Layout, BurstKernels.LayoutPlanned);
    LuminPack.Burst.Tests.PortableLayoutContract.Verify(false);
    if (BurstKernels.Capabilities() != 0)
        LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Managed execution must not report native Burst hardware coverage.");
    Console.WriteLine("Passed managed portable kernel contracts; native Burst/ISA execution is not established by this run.");
}
#endif
