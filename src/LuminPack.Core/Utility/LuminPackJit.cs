using System.Runtime.CompilerServices;

namespace LuminPack.Utility
{
    /// <summary>Just-in-time compilation hints shared by the runtime's hot kernels.</summary>
    internal static class LuminPackJit
    {
        /// <summary>Compiles a loop or vector kernel with full optimization on its first call.</summary>
        /// <remarks>Tier-0 code neither inlines the kernel's helpers nor expands its vector operations, and a long loop can
        /// finish before on-stack replacement applies, so a kernel would run unoptimized until call counting promotes it.
        /// Combined with <see cref="MethodImplOptions.AggressiveInlining"/> the hint does not prevent inlining: an
        /// optimized caller still inlines the kernel, while a tier-0 caller calls its optimized body. The flag does not
        /// exist in the netstandard2.1 reference assemblies and is ignored by Mono, IL2CPP and NativeAOT.</remarks>
#if NET8_0_OR_GREATER
        internal const MethodImplOptions Optimized = MethodImplOptions.AggressiveOptimization;
#else
        internal const MethodImplOptions Optimized = 0;
#endif
    }
}
