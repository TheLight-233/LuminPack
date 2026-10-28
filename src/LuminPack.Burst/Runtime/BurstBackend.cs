#if LUMINPACK_BURST
using System;
using System.Runtime.InteropServices;
using LuminPack.Core;
using Unity.Burst;

namespace LuminPack.Burst
{
    /// <summary>Publishes Burst-compiled kernels to the existing netstandard2.1 Core hooks before scene loading or through explicit registration.</summary>
    /// <remarks>Core ABI version two provides copy refusal. Unity type formatters are independent of this backend.</remarks>
    [global::Unity.IL2CPP.CompilerServices.Il2CppEagerStaticClassConstruction]
    public static unsafe partial class BurstBackend
    {
        /// <summary>Returns a compiled CPU capability mask.</summary>
        /// <returns>The available SIMD families.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int CapabilityFunction();
        /// <summary>Copies exact non-overlapping bytes.</summary>
        /// <param name="destination">Output.</param><param name="source">Input.</param><param name="count">Bytes.</param>
        /// <returns>One on completion, or zero without writes for Core's native-copy fallback.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int CopyFunction(byte* destination, byte* source, uint count);
        /// <summary>Counts equal leading bytes.</summary>
        /// <param name="left">First input.</param><param name="right">Second input.</param><param name="count">Maximum bytes.</param>
        /// <returns>The matching prefix.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate uint CountMatchFunction(byte* left, byte* right, uint count);
        /// <summary>XORs complete input and keystream ranges.</summary>
        /// <param name="output">Output.</param><param name="input">Input.</param><param name="stream">Keystream.</param><param name="count">Bytes.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void XorFunction(byte* output, byte* input, byte* stream, uint count);
        /// <summary>Expands a validated periodic match.</summary>
        /// <param name="output">Output.</param><param name="count">Bytes.</param><param name="distance">Backward distance.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CopyMatchFunction(byte* output, uint count, uint distance);

        private static readonly object Sync = new object();
        // Root the original delegates for as long as any captured Core registration might invoke native code.
        private static readonly CapabilityFunction CapabilityRoot = BurstKernels.Capabilities;
        private static readonly CopyFunction CopyRoot = BurstKernels.Copy;
        private static readonly CountMatchFunction MatchRoot = BurstKernels.CountMatch;
        private static readonly XorFunction XorRoot = BurstKernels.Xor;
        private static readonly CopyMatchFunction ExpandRoot = BurstKernels.CopyMatch;
        private static LuminPackBurst.KernelTable _table;
        private static bool _compiled;

        /// <summary>Gets the SIMD capability mask from the compiled native probe.</summary>
        public static int Capabilities { get; private set; }

        /// <summary>Registers available Burst kernels before scene loading, retaining Core when compilation is disabled or unsupported.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register() => TryRegister();

        /// <summary>Compiles kernels synchronously and installs a complete table only when actual SIMD code is available.</summary>
        /// <returns>False without registration when Burst is disabled, unsupported, or another backend is already published.</returns>
        /// <remarks>Call at a controlled initialization boundary. Compilation errors propagate before any Core registration changes.</remarks>
        public static bool TryRegister()
        {
            lock (Sync)
            {
#if UNITY_WEBGL
                return false;
#else
                if (sizeof(void*) != 8 || !BurstCompiler.IsEnabled || LuminPackBurst.IsRegistered) return false;
                if (!_compiled)
                {
                    var probe = BurstCompiler.CompileFunctionPointer<CapabilityFunction>(CapabilityRoot);
                    if (!probe.IsCreated) return false;
                    int available = probe.Invoke();
                    if (available == 0) return false;
                    var copy = BurstCompiler.CompileFunctionPointer<CopyFunction>(CopyRoot);
                    var match = BurstCompiler.CompileFunctionPointer<CountMatchFunction>(MatchRoot);
                    var xor = BurstCompiler.CompileFunctionPointer<XorFunction>(XorRoot);
                    var expand = BurstCompiler.CompileFunctionPointer<CopyMatchFunction>(ExpandRoot);
                    if (!copy.IsCreated || !match.IsCreated || !xor.IsCreated || !expand.IsCreated) return false;
                    _table = new LuminPackBurst.KernelTable
                    {
                        Version = LuminPackBurst.AbiVersion,
                        Copy = (delegate* unmanaged[Cdecl]<byte*, byte*, uint, int>)(void*)copy.Value,
                        CountMatch = (delegate* unmanaged[Cdecl]<byte*, byte*, uint, uint>)(void*)match.Value,
                        Xor = (delegate* unmanaged[Cdecl]<byte*, byte*, byte*, uint, void>)(void*)xor.Value,
                        CopyMatch = (delegate* unmanaged[Cdecl]<byte*, uint, uint, void>)(void*)expand.Value
                    };
                    if (!CompileTextKernels(ref _table)) return false;
                    if (!CompileLayoutAndCrypto(ref _table)) return false;
                    if (!CompileLatin1Kernels(ref _table)) return false;
                    Capabilities = available;
                    _compiled = true;
                }
                LuminPackBurst.Register(_table);
                return true;
#endif
            }
        }
    }
}
#endif
