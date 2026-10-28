#if UNITY_EDITOR && LUMINPACK_BURST
using LuminPack.Burst;
using NUnit.Framework;
using Unity.Burst;

/// <summary>Executes the same contract through real Burst function pointers in an explicitly configured Unity project.</summary>
[NonParallelizable]
public unsafe class BurstNativeTests
{
    /// <summary>Rejects native-sized optional constants that Unity 2022.3 IL2CPP cannot encode in parameter metadata.</summary>
    [Test]
    public void NativeSizedOffsetsUseOverloadsInsteadOfOptionalConstants()
    {
        foreach (var type in typeof(BurstBackend).Assembly.GetTypes())
        foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                                              System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance |
                                              System.Reflection.BindingFlags.DeclaredOnly))
        foreach (var parameter in method.GetParameters())
        {
            if (parameter.ParameterType == typeof(System.UIntPtr) || parameter.ParameterType == typeof(System.IntPtr))
                Assert.That(parameter.HasDefaultValue, Is.False, type.FullName + "." + method.Name + ": use an overload for a zero offset.");
        }
    }

    /// <summary>Requires actual compiled ISA code, then runs complete differential/tail/overlap checks.</summary>
    [Test]
    public void CompiledKernelsPreserveCompleteRanges()
    {
        if (!BurstCompiler.IsEnabled) Assert.Ignore("Burst compilation is disabled; no native coverage is established.");
        var probe = BurstCompiler.CompileFunctionPointer<BurstBackend.CapabilityFunction>(BurstKernels.Capabilities);
        Assert.That(probe.Invoke(), Is.Not.Zero, "A native SIMD-capable Burst runner is required.");
        LuminPack.Core.LuminPackBurst.Clear();
        Assert.That(BurstBackend.TryRegister(), Is.True);
        try
        {
            LuminPack.Burst.Tests.PortableLayoutContract.Verify((probe.Invoke() & 6) != 0);
            KernelContract.Verify(
                BurstCompiler.CompileFunctionPointer<BurstBackend.CopyFunction>(BurstKernels.Copy).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.CountMatchFunction>(BurstKernels.CountMatch).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.XorFunction>(BurstKernels.Xor).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.CopyMatchFunction>(BurstKernels.CopyMatch).Invoke);
            LuminPack.Burst.Tests.TextKernelContract.Verify(
                BurstCompiler.CompileFunctionPointer<BurstBackend.Utf8EncodeFunction>(BurstKernels.Utf8Encode).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.Utf8DecodeFunction>(BurstKernels.Utf8Decode).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.Utf8EqualFunction>(BurstKernels.Utf8Equal).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.AsciiEncodeFunction>(BurstKernels.AsciiEncode).Invoke);
            LuminPack.Burst.Tests.TextKernelContract.VerifyBytes(
                BurstCompiler.CompileFunctionPointer<BurstBackend.EqualFunction>(BurstKernels.Equal).Invoke);
            LuminPack.Burst.Tests.Latin1KernelContract.Verify(
                BurstCompiler.CompileFunctionPointer<BurstBackend.IsLatin1Function>(BurstKernels.IsLatin1).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.NarrowLatin1Function>(BurstKernels.NarrowLatin1).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.WidenLatin1Function>(BurstKernels.WidenLatin1).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.Latin1EqualFunction>(BurstKernels.Latin1Equal).Invoke);
            LuminPack.Burst.Tests.LayoutCryptoContract.Verify(
                BurstCompiler.CompileFunctionPointer<BurstBackend.LayoutFunction>(BurstKernels.Layout).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.ChaCha20Function>(BurstKernels.ChaCha20).Invoke);
            LuminPack.Burst.Tests.LayoutCryptoContract.VerifyPlanned(
                BurstCompiler.CompileFunctionPointer<BurstBackend.LayoutFunction>(BurstKernels.Layout).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.LayoutPlannedFunction>(BurstKernels.LayoutPlanned).Invoke);
        }
        finally { LuminPack.Core.LuminPackBurst.Clear(); }
    }
}
#endif
