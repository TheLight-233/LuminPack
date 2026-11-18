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

        // Copy-size evidence (LuminPack's own CI 57 round-4 probe, run 44205342061, .NET 10 FullOpts, reused destination,
        // one cache-resident target, source offset 4; values are kernel time / runtime block copy time):
        //
        //   host                       128 B   256 B   512 B   1 KiB   4 KiB   16 KiB
        //   Xeon 6975P-C   256-bit      1.08    0.67    0.59    0.74    1.09    0.99
        //                  512-bit      0.81    0.65    0.48    0.57    0.86    0.93
        //   EPYC 9V74      256-bit      0.82    0.73    0.59    0.79    0.77    0.78
        //                  512-bit      1.00    0.88    0.70    0.87    0.89    0.89
        //   EPYC 7763      256-bit      0.78    0.62    0.59    0.59    0.83    0.64
        //   i7-8700B       256-bit      0.60    0.73    0.69    0.70    0.60    0.63
        //   i7-13650HX     256-bit      1.05    0.66    0.65    0.78    0.92    0.98   (local Windows run, same probe)
        //
        // 128 bytes is mixed for the 256-bit schedule (Intel 1.05 to 1.08), and 256 bytes and above win on every x64 host, so the
        // 256-bit schedule starts at 256 bytes. Arm64 hosts (Neoverse N2, Cobalt 100, Apple M1) have no accelerated 256-bit
        // vectors; a 128-bit schedule took 0.87 to 1.34 of the runtime copy from 128 bytes through 4 KiB with no consistent win,
        // so they keep the runtime copy.
        // End to end, the matrix raw-collection reuse rows with the runtime copy in place of these kernels took 1.16 to 1.37
        // times as long at 1 KiB, 0.93 to 1.52 at 4 KiB and 0.99 to 1.41 at 16 KiB across the four x64 hosts, so x64 keeps them.

        /// <summary>Gets the smallest width handled by destination-aligned 256-bit medium stores.</summary>
        private const uint MediumCopyMinimum = 256;

        /// <summary>Gets the smallest width handled by destination-aligned 512-bit medium stores.</summary>
        private const uint WideCopyMinimum = 128;

        /// <summary>Gets the upper bound of medium copies with destination-aligned vector stores.</summary>
        public const uint MediumCopyLimit = 32 * 1024;

        /// <summary>Selects the destination-aligned 256-bit medium copy; false retains the native block copy.</summary>
        /// <param name="is64Bit">Whether fixed wide typed storage is permitted.</param><param name="has256">256-bit support.</param><param name="count">Complete byte width.</param>
        /// <returns>Whether a block from 256 bytes through 32 KiB uses aligned vector stores.</returns>
        /// <remarks>The size range comes from the measurements above. Targets without accelerated 256-bit vectors, including
        /// ARM64, keep the native copy.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseMediumCopy(bool is64Bit, bool has256, uint count)
            => is64Bit && has256 && count - MediumCopyMinimum <= MediumCopyLimit - MediumCopyMinimum;

        /// <summary>Selects the destination-aligned 512-bit medium copy ahead of the 256-bit schedule.</summary>
        /// <param name="is64Bit">Whether fixed wide typed storage is permitted.</param>
        /// <param name="preferWide">Whether the processor runs full-width 512-bit stores faster than two 256-bit stores.</param>
        /// <param name="count">Complete byte width.</param>
        /// <returns>Whether a block from 128 bytes through 32 KiB uses aligned 512-bit stores.</returns>
        /// <remarks>
        /// Evidence (same CI run, matrix raw-collection rows, 512-bit build / 256-bit build, fastest round, median of three
        /// processes): on the Xeon 6975P-C, reuse took 0.92 at 1 KiB, 0.87 at 4 KiB and 0.91 at 16 KiB, and writes 0.98 at
        /// 1 KiB, 1.02 at 4 KiB and 1.04 at 16 KiB. On the EPYC 9V74 (Zen 4, 512-bit operations split into two 256-bit halves)
        /// reuse took 1.04 at 1 KiB and 4 KiB, so AMD processors keep the 256-bit schedule. Zen 5 was not sampled.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool UseWideCopy(bool is64Bit, bool preferWide, uint count)
            => is64Bit && preferWide && count - WideCopyMinimum <= MediumCopyLimit - WideCopyMinimum;
    }
}
