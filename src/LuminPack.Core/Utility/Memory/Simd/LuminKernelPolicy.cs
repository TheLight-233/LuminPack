using System.Runtime.CompilerServices;

namespace LuminPack.Code
{
    /// <summary>Shares established short-copy eligibility across native .NET and optional Burst instruction backends.</summary>
    public static class LuminKernelPolicy
    {
        /// <summary>Gets the minimum element batch that amortizes the optional native layout boundary.</summary>
        public const int NativeLayoutMinimumCount = 512;

        /// <summary>Selects native layout batching while retaining the original Core field-wise path for small ranges.</summary>
        /// <param name="count">Complete elements.</param><returns>Whether the native boundary is eligible.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseNativeLayout(int count) => count >= NativeLayoutMinimumCount;

        /// <summary>Gets the original upper bound of cached short copies.</summary>
        public const uint ShortCopyLimit = 256;
        /// <summary>Gets the original upper bound with only 128-bit lanes.</summary>
        public const uint NarrowCopyLimit = 128;

        /// <summary>Selects the established bounded short-copy path; false retains the caller's native block copy.</summary>
        /// <param name="is64Bit">Whether fixed wide typed storage is permitted.</param><param name="has128">128-bit support.</param><param name="has256">256-bit support.</param><param name="has512">Preferred native 512-bit support.</param><param name="count">Complete byte width.</param>
        /// <returns>Whether the original short-copy path completes without its cpblk fallback.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseShortCopy(bool is64Bit, bool has128, bool has256, bool has512, uint count)
            => is64Bit && has128 && count <= ShortCopyLimit && (count <= NarrowCopyLimit || has256 || has512);

        /// <summary>Gets the upper bound of medium copies with destination-aligned 256-bit stores.</summary>
        public const uint MediumCopyLimit = 32 * 1024;

        /// <summary>Selects the destination-aligned 256-bit medium copy; false retains the native block copy.</summary>
        /// <param name="is64Bit">Whether fixed wide typed storage is permitted.</param><param name="has256">256-bit support.</param><param name="count">Complete byte width.</param>
        /// <returns>Whether a block above the short range and within the medium range uses aligned vector stores.</returns>
        /// <remarks>Measured against the native block copy on AVX2 x64, destination-aligned 256-bit stores take 0.56 to 0.97 of its
        /// time from 257 bytes through 32 KiB. Targets with 512-bit vectors keep the same width: no wider schedule has local evidence.
        /// Targets without accelerated 256-bit vectors, including ARM64, keep the native copy.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseMediumCopy(bool is64Bit, bool has256, uint count)
            => is64Bit && has256 && count - (ShortCopyLimit + 1) <= MediumCopyLimit - (ShortCopyLimit + 1);
    }
}
