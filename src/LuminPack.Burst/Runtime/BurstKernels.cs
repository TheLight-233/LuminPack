#if LUMINPACK_BURST
using AOT;
using Unity.Burst;
using Unity.Burst.Intrinsics;

namespace LuminPack.Burst
{
    /// <summary>Adapts the Core algorithm projection to the pointer ABI; it owns no alternative traversal algorithms.</summary>
    [BurstCompile(CompileSynchronously = true)]
    public static unsafe partial class BurstKernels
    {
        /// <summary>Identifies native compiled instruction families.</summary>
        /// <returns>SSE2 bit 1, AVX2 bit 2, ARM64 Neon bit 4, or zero in portable managed execution.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.CapabilityFunction))]
        public static int Capabilities()
        {
            int value = 0;
            if (X86.Sse2.IsSse2Supported) value |= 1;
            if (X86.Avx2.IsAvx2Supported) value |= 2;
            if (Arm.Neon.IsNeonSupported) value |= 4;
            return value;
        }

        /// <summary>Executes Core's eligible short-copy algorithm, declining into Core's native copy otherwise.</summary>
        /// <param name="destination">Complete output.</param><param name="source">Complete input.</param><param name="count">Bytes.</param>
        /// <returns>One on completion; zero without writes when Core must perform its existing cpblk fallback.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.CopyFunction))]
        public static int Copy(byte* destination, byte* source, uint count)
        {
            if (!global::LuminPack.Code.LuminKernelPolicy.UseShortCopy(KernelMemory.Is64Bit, Vector128.IsHardwareAccelerated,
                Vector256.IsHardwareAccelerated, Vector512.IsHardwareAccelerated, count)) return 0;
            CoreAlgorithms.CopyShortBlock(ref *destination, ref *source, count);
            return 1;
        }

        /// <summary>Runs the original Core match-extension algorithm with mapped vector primitives.</summary>
        /// <param name="left">First range.</param><param name="right">Second range.</param><param name="maximum">Complete comparison length.</param>
        /// <returns>The equal prefix length.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.CountMatchFunction))]
        public static uint CountMatch(byte* left, byte* right, uint maximum)
        {
            ref byte limit = ref *(left + maximum);
            return (uint)CoreAlgorithms.ExtendMatch(ref *left, ref *right, ref limit);
        }

        /// <summary>Runs Core's existing XOR width sequence and four-byte/scalar tail.</summary>
        /// <param name="output">Output.</param><param name="input">Input, with exact in-place alias allowed.</param><param name="stream">Keystream.</param><param name="count">Bytes.</param>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.XorFunction))]
        public static void Xor(byte* output, byte* input, byte* stream, uint count)
            => CoreAlgorithms.XorKeystream(output, input, stream, (int)count);

        /// <summary>Runs Core's overlap, fill, vector-threshold and eight-byte-tail algorithm.</summary>
        /// <param name="destination">Validated output.</param><param name="count">Bytes.</param><param name="distance">Validated backward distance.</param>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.CopyMatchFunction))]
        public static void CopyMatch(byte* destination, uint count, uint distance)
        {
            if (count == 0) return;
            CoreAlgorithms.CopyMatch(ref *destination, ref *(destination - distance), (int)count, (int)distance);
        }
    }
}
#endif
