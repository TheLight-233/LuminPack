#if LUMINPACK_BURST
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using LuminPack.Core;
namespace LuminPack.Burst
{
    public static unsafe partial class BurstBackend
    {
        /// <summary>Counts structural UTF-8 array elements without changing the input.</summary>
        /// <param name="input">Complete input after the opening bracket.</param><param name="length">Bytes.</param>
        /// <param name="flags">Zero for general arrays, one for flat scalar/string pairs.</param><returns>The count or minus one.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int JsonUtf8Function(byte* input, int length, uint flags);

        /// <summary>Counts structural UTF-16 array elements without changing the input.</summary>
        /// <param name="input">Complete code units after the opening bracket.</param><param name="length">Code units.</param>
        /// <param name="flags">Zero for general arrays, one for flat scalar/string pairs.</param><returns>The count or minus one.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int JsonUtf16Function(ushort* input, int length, uint flags);

        private static readonly JsonUtf8Function Json8Root = BurstKernels.JsonArrayHintUtf8;
        private static readonly JsonUtf16Function Json16Root = BurstKernels.JsonArrayHintUtf16;

        /// <summary>Compiles shared Core structural scans and appends their optional ABI slots.</summary>
        /// <param name="table">The unpublished registration.</param><returns>Whether both native functions were created.</returns>
        private static bool CompileJsonKernels(ref LuminPackBurst.KernelTable table)
        {
            var utf8 = BurstCompiler.CompileFunctionPointer<JsonUtf8Function>(Json8Root);
            var utf16 = BurstCompiler.CompileFunctionPointer<JsonUtf16Function>(Json16Root);
            if (!utf8.IsCreated || !utf16.IsCreated)
                return false;
            table.JsonArrayHintUtf8 = (delegate* unmanaged[Cdecl]<byte*, int, uint, int>)(void*)utf8.Value;
            table.JsonArrayHintUtf16 = (delegate* unmanaged[Cdecl]<char*, int, uint, int>)(void*)utf16.Value;
            return true;
        }
    }
}
#endif
