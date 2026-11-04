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

        /// <summary>Gets the upper bound of the inline short-copy path.</summary>
        public const uint ShortCopyLimit = 32;

        /// <summary>Gets the historical narrow-lane upper bound retained for external callers.</summary>
        /// <remarks>The short path no longer extends past <see cref="ShortCopyLimit"/>.</remarks>
        public const uint NarrowCopyLimit = 128;

        /// <summary>Selects the established bounded short-copy path; false retains the caller's native block copy.</summary>
        /// <param name="is64Bit">Whether fixed wide typed storage is permitted.</param>
        /// <param name="has128">128-bit support.</param>
        /// <param name="has256">256-bit support; retained for source compatibility, no longer changes the selection.</param>
        /// <param name="has512">Preferred native 512-bit support; retained for source compatibility, no longer changes the selection.</param>
        /// <param name="count">Complete byte width.</param>
        /// <returns>Whether the bounded short-copy path handles the range.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseShortCopy(bool is64Bit, bool has128, bool has256, bool has512, uint count)
            => is64Bit && has128 && count <= ShortCopyLimit;

        /// <summary>Gets the smallest width handled by destination-aligned 256-bit medium stores.</summary>
        private const uint MediumCopyMinimum = 257;

        /// <summary>Gets the upper bound of medium copies with destination-aligned 256-bit stores.</summary>
        public const uint MediumCopyLimit = 32 * 1024;

        /// <summary>Selects the destination-aligned 256-bit medium copy; false retains the native block copy.</summary>
        /// <param name="is64Bit">Whether fixed wide typed storage is permitted.</param><param name="has256">256-bit support.</param><param name="count">Complete byte width.</param>
        /// <returns>Whether a block from 257 bytes through 32 KiB uses aligned vector stores.</returns>
        /// <remarks>Measured against the native block copy on AVX2 x64, destination-aligned 256-bit stores take 0.56 to 0.97 of its
        /// time from 257 bytes through 32 KiB. Targets with 512-bit vectors keep the same width: no wider schedule has local evidence.
        /// Targets without accelerated 256-bit vectors, including ARM64, keep the native copy.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseMediumCopy(bool is64Bit, bool has256, uint count)
            => is64Bit && has256 && count - MediumCopyMinimum <= MediumCopyLimit - MediumCopyMinimum;
    }
}
