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
        /// <summary>Checks complete code units for Latin-1 representability.</summary><param name="source">Input.</param><param name="length">Characters.</param><returns>One when representable.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int IsLatin1Function(ushort* source, int length);
        /// <summary>Narrows a complete range with Core's existing partial-prefix-on-false contract.</summary><param name="source">Input.</param><param name="destination">Output.</param><param name="length">Characters.</param><returns>One on complete success.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int NarrowLatin1Function(ushort* source, byte* destination, int length);
        /// <summary>Widens complete Latin-1 into unpublished character storage.</summary><param name="source">Input.</param><param name="destination">Output.</param><param name="length">Characters.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void WidenLatin1Function(byte* source, ushort* destination, int length);
        /// <summary>Compares complete Latin-1 and UTF-16 ranges.</summary><param name="source">Payload.</param><param name="candidate">Candidate.</param><param name="length">Characters.</param><returns>One on equality.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int Latin1EqualFunction(byte* source, ushort* candidate, int length);
        private static readonly IsLatin1Function Latin1CheckRoot = BurstKernels.IsLatin1;
        private static readonly NarrowLatin1Function Latin1NarrowRoot = BurstKernels.NarrowLatin1;
        private static readonly WidenLatin1Function Latin1WidenRoot = BurstKernels.WidenLatin1;
        private static readonly Latin1EqualFunction Latin1EqualRoot = BurstKernels.Latin1Equal;
        /// <summary>Compiles compact-record kernels before publishing the complete table.</summary><param name="table">Unpublished table.</param><returns>Whether all pointers exist.</returns>
        private static bool CompileLatin1Kernels(ref LuminPackBurst.KernelTable table)
        {
            var check = BurstCompiler.CompileFunctionPointer<IsLatin1Function>(Latin1CheckRoot);
            var narrow = BurstCompiler.CompileFunctionPointer<NarrowLatin1Function>(Latin1NarrowRoot);
            var widen = BurstCompiler.CompileFunctionPointer<WidenLatin1Function>(Latin1WidenRoot);
            var equal = BurstCompiler.CompileFunctionPointer<Latin1EqualFunction>(Latin1EqualRoot);
            if (!check.IsCreated || !narrow.IsCreated || !widen.IsCreated || !equal.IsCreated) return false;
            table.IsLatin1 = (delegate* unmanaged[Cdecl]<char*, int, int>)(void*)check.Value;
            table.NarrowLatin1 = (delegate* unmanaged[Cdecl]<char*, byte*, int, int>)(void*)narrow.Value;
            table.WidenLatin1 = (delegate* unmanaged[Cdecl]<byte*, char*, int, void>)(void*)widen.Value;
            table.Latin1Equal = (delegate* unmanaged[Cdecl]<byte*, char*, int, int>)(void*)equal.Value;
            return true;
        }
    }
    public static unsafe partial class BurstKernels
    {
        /// <summary>Runs Core's complete Latin-1 classification.</summary><param name="source">Input.</param><param name="length">Characters.</param><returns>One when representable.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.IsLatin1Function))]
        public static int IsLatin1(ushort* source, int length) => CoreLatin1Records.IsLatin1(new ReadOnlySpan<char>((char*)source, length)) ? 1 : 0;
        /// <summary>Runs Core's same validation, narrowing and bounded stores.</summary><param name="source">Input.</param><param name="destination">Output.</param><param name="length">Characters.</param><returns>One on complete success.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.NarrowLatin1Function))]
        public static int NarrowLatin1(ushort* source, byte* destination, int length) => CoreLatin1Records.TryNarrowLatin1(ref *(char*)source, ref *destination, length) ? 1 : 0;
        /// <summary>Runs Core's Latin-1 widening with complete input and output ranges.</summary><param name="source">Input.</param><param name="destination">Unpublished output.</param><param name="length">Characters.</param>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.WidenLatin1Function))]
        public static void WidenLatin1(byte* source, ushort* destination, int length) => CoreLatin1Records.WidenLatin1(ref *source, ref *destination, length);
        /// <summary>Runs Core's same-length compact-record reuse comparison.</summary><param name="source">Payload.</param><param name="candidate">Candidate.</param><param name="length">Characters.</param><returns>One on equality.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.Latin1EqualFunction))]
        public static int Latin1Equal(byte* source, ushort* candidate, int length) => CoreLatin1Records.MatchesLatin1(ref *source, new ReadOnlySpan<char>((char*)candidate, length)) ? 1 : 0;
    }
}
#endif

