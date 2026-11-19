#if LUMINPACK_BURST
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;

namespace LuminPack.Burst
{
    public static unsafe partial class BurstBackend
    {
        /// <summary>Copies and validates complete Boolean storage.</summary>
        /// <param name="input">The complete readable range.</param>
        /// <param name="output">The complete destination, which may equal the input.</param>
        /// <param name="length">The byte count of both ranges.</param>
        /// <returns>One for canonical storage, zero for invalid storage, or minus one when the kernel declines without writing.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BooleanFunction(byte* input, byte* output, int length);
        private static readonly BooleanFunction BooleanRoot = BurstKernels.CopyBooleans;
    }

    public static unsafe partial class BurstKernels
    {
        /// <summary>Executes Core's fused Boolean transfer on the supported SIMD width.</summary>
        /// <param name="input">The first byte of the validated input.</param>
        /// <param name="output">The start of the complete writable range.</param>
        /// <param name="length">The number of bytes to transfer.</param>
        /// <returns>One for canonical storage, zero for invalid storage, or minus one without writes if unavailable.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.BooleanFunction))]
        public static int CopyBooleans(byte* input, byte* output, int length)
        {
            if (length < 16 || !Vector128.IsHardwareAccelerated && !Vector256.IsHardwareAccelerated)
                return -1;
            return CoreAlgorithms.CopyBooleans(ref *input, ref *output, length) ? 1 : 0;
        }
    }
}
#endif
