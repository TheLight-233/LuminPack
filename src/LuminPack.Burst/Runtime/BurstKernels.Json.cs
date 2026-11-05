#if LUMINPACK_BURST
using System;
using Unity.Burst;
using AOT;
namespace LuminPack.Burst
{
    public static unsafe partial class BurstKernels
    {
        /// <summary>Runs Core's read-only structural sizing algorithm over complete UTF-8 input.</summary>
        /// <param name="input">Input after ArrayStart.</param><param name="length">Complete byte count.</param>
        /// <param name="flags">Zero for general arrays, one for flat scalar/string pairs.</param><returns>The count or minus one.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.JsonUtf8Function))]
        public static int JsonArrayHintUtf8(byte* input, int length, uint flags) =>
            length < 0 || flags > 1 ? -1 : CoreJsonCapacity.ScanHintByte(new ReadOnlySpan<byte>(input, length), flags != 0);

        /// <summary>Runs Core's read-only structural sizing algorithm over complete UTF-16 code units.</summary>
        /// <param name="input">Input after ArrayStart.</param><param name="length">Complete code-unit count.</param>
        /// <param name="flags">Zero for general arrays, one for flat scalar/string pairs.</param><returns>The count or minus one.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.JsonUtf16Function))]
        public static int JsonArrayHintUtf16(ushort* input, int length, uint flags) =>
            length < 0 || flags > 1 ? -1 : CoreJsonCapacity.ScanHintChar(new ReadOnlySpan<char>(input, length), flags != 0);
    }
}
#endif
