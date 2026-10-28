#if LUMINPACK_BURST
using System;
using System.Runtime.InteropServices;
using AOT;
using LuminPack.Core;
using Unity.Burst;
namespace LuminPack.Burst
{
    public static unsafe partial class BurstBackend
    {
        /// <summary>Converts an eligible complete layout prefix.</summary><param name="input">Source elements.</param><param name="output">Destination elements.</param><param name="count">Elements.</param><param name="sourceStride">Input stride.</param><param name="destinationStride">Output stride.</param><param name="sourceOffsets">Source byte offsets.</param><param name="destinationOffsets">Destination byte offsets.</param><param name="offsetCount">Descriptor length.</param><returns>Completed elements.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int LayoutFunction(byte* input, byte* output, int count, int sourceStride, int destinationStride, int* sourceOffsets, int* destinationOffsets, int offsetCount);
        /// <summary>Writes one, two or four consecutive ChaCha20 blocks.</summary><param name="state">Read-only sixteen-word state.</param><param name="output">Complete output.</param><param name="blocks">Block count.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void ChaCha20Function(uint* state, byte* output, int blocks);
        /// <summary>Converts complete leading elements using Core's pinned immutable plan.</summary><param name="input">Input.</param><param name="output">Output.</param><param name="count">Elements.</param><param name="plan">Complete descriptor.</param><returns>Completed elements.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int LayoutPlannedFunction(byte* input, byte* output, int count, global::LuminPack.Utility.LuminLayoutPlan* plan);
        private static readonly LayoutFunction LayoutRoot = BurstKernels.Layout;
        private static readonly ChaCha20Function ChaChaRoot = BurstKernels.ChaCha20;
        private static readonly LayoutPlannedFunction PlannedRoot = BurstKernels.LayoutPlanned;
        /// <summary>Compiles layout and cipher pointers before publishing the table.</summary><param name="table">Unpublished table.</param><returns>Whether both pointers exist.</returns>
        private static bool CompileLayoutAndCrypto(ref LuminPackBurst.KernelTable table)
        {
            var layout = BurstCompiler.CompileFunctionPointer<LayoutFunction>(LayoutRoot);
            var cipher = BurstCompiler.CompileFunctionPointer<ChaCha20Function>(ChaChaRoot);
            var planned = BurstCompiler.CompileFunctionPointer<LayoutPlannedFunction>(PlannedRoot);
            if (!layout.IsCreated || !cipher.IsCreated || !planned.IsCreated) return false;
            table.Layout = (delegate* unmanaged[Cdecl]<byte*, byte*, int, int, int, int*, int*, int, int>)(void*)layout.Value;
            table.ChaCha20 = (delegate* unmanaged[Cdecl]<uint*, byte*, int, void>)(void*)cipher.Value;
            table.LayoutPlanned = (delegate* unmanaged[Cdecl]<byte*, byte*, int, global::LuminPack.Utility.LuminLayoutPlan*, int>)(void*)planned.Value;
            return true;
        }
    }
    public static unsafe partial class BurstKernels
    {
        /// <summary>Runs Core's unchanged complete-vector prefix with its precomputed masks.</summary><param name="input">Complete input.</param><param name="output">Complete output.</param><param name="count">Elements.</param><param name="descriptor">Pinned immutable descriptor.</param><returns>Completed elements.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.LayoutPlannedFunction))]
        public static int LayoutPlanned(byte* input, byte* output, int count, global::LuminPack.Utility.LuminLayoutPlan* descriptor)
        {
            if (!global::LuminPack.Code.LuminKernelPolicy.UseNativeLayout(count) || !(Ssse3.IsSupported || AdvSimd.Arm64.IsSupported)) return 0;
            CoreLayout plan = default;
            plan.Initialize(in *descriptor);
            return plan.Prefix(ref *input, ref *output, count);
        }
        /// <summary>Builds Core's same shuffle plan and returns only its complete vector prefix for the existing Core byte-run tail.</summary><param name="input">Input.</param><param name="output">Output.</param><param name="count">Element count.</param><param name="sourceStride">Input stride.</param><param name="destinationStride">Output stride.</param><param name="sourceOffsets">Source offsets in declaration order.</param><param name="destinationOffsets">Destination offsets in declaration order.</param><param name="offsetCount">Descriptor count.</param><returns>Completed elements.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.LayoutFunction))]
        public static int Layout(byte* input, byte* output, int count, int sourceStride, int destinationStride, int* sourceOffsets, int* destinationOffsets, int offsetCount)
        {
            if (count <= 0 || offsetCount <= 0) return 0;
            CoreLayout plan = default;
            plan.Initialize(new ReadOnlySpan<int>(sourceOffsets, offsetCount), sourceStride, new ReadOnlySpan<int>(destinationOffsets, offsetCount), destinationStride, true);
            return plan.Prefix(ref *input, ref *output, count);
        }
        /// <summary>Runs Core's cipher block dispatch over a private state copy, preserving the ABI's read-only input contract.</summary><param name="state">Read-only state.</param><param name="output">Complete output.</param><param name="blocks">One, two or four blocks.</param>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.ChaCha20Function))]
        public static void ChaCha20(uint* state, byte* output, int blocks)
        {
            uint* copy = stackalloc uint[16];
            KernelMemory.CopyBlockUnaligned(ref *(byte*)copy, ref *(byte*)state, 64);
            if (blocks == 1) CoreChaCha20.Block(copy, output);
            else if (blocks == 2) CoreChaCha20.Block2(copy, output);
            else if (blocks == 4) CoreChaCha20.Block4(copy, output);
        }
    }
}
#endif

