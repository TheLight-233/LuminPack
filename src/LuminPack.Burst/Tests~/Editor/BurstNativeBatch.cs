#if UNITY_EDITOR && LUMINPACK_BURST
using System;
using LuminPack.Burst;
using LuminPack.Core;
using Unity.Burst;
using UnityEditor;
using UnityEngine;

/// <summary>Runs native Burst pointer tests in batch mode without the full project test suite.</summary>
public static unsafe class BurstNativeBatch
{
    /// <summary>Requires compiled SIMD, runs the Core-derived algorithms, then exits with a diagnostic status.</summary>
    public static void Run()
    {
        try
        {
            if (!BurstBackend.TryRegister())
                LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Native Burst registration was unavailable.");
            KernelContract.Verify(
                BurstCompiler.CompileFunctionPointer<BurstBackend.CopyFunction>(BurstKernels.Copy).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.CountMatchFunction>(BurstKernels.CountMatch).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.XorFunction>(BurstKernels.Xor).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.CopyMatchFunction>(BurstKernels.CopyMatch).Invoke);
            Debug.Log("LUMINPACK_BURST_NATIVE_OK: compiled capabilities=" + BurstBackend.Capabilities);
            LuminPackBurst.Clear();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
#endif
