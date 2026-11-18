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

        // Copy ranges for reused destinations, measured by LuminPack's own CI 57 probes (.NET 10 FullOpts, one
        // cache-resident target, source offset 4). Values are kernel time / runtime block copy time, median of nine rounds;
        // a range covers every host of that class.
        //
        // Round 5 (run 44216361325; 56 sizes from 32 B to 16 MiB):
        //   processor                    kernel    64 B      80 B      128 B     256 B     1 KiB     4 KiB     16 KiB     24 KiB     32 KiB     64 KiB
        //   Zen 5 (EPYC 9V45, 4 hosts)   256-bit   0.81-1.14 0.75-0.80 0.65-0.84 0.52-0.65 0.68-0.70 0.79-0.94 1.05-1.15  1.24-1.29  1.47-1.48  1.29-1.33
        //                                512-bit   0.99-1.33 0.88-1.00 0.69-1.04 0.49-1.10 0.50-0.56 0.69-0.77 0.87-0.96  0.97-0.99  1.47-1.55  1.20-1.36
        //   Zen 4 (EPYC 9V74)            256-bit   0.87      0.80      0.71      0.67      0.77      0.71      0.85       0.87       0.88       0.89
        //                                512-bit   0.93      0.97      0.78      0.73      0.78      0.81      1.11       0.88       1.04       0.92
        //   Zen 3 (EPYC 7763, 4 hosts)   256-bit   0.73-1.00 0.73-0.82 0.72-0.78 0.50-0.64 0.63-0.67 0.55-0.87 0.62-0.73  0.86-0.88  0.88-0.94  0.91-1.25
        //   Coffee Lake (i7-8700B)       256-bit   0.69      0.64      0.66      0.55      0.62      0.49      0.66       0.70       0.70       0.68
        //
        // On Zen 5 the 512-bit schedule overtakes the 256-bit one from about 512 bytes (512-bit / 256-bit: 0.86 to 0.97 at
        // 512 B, 0.70 to 0.84 from 640 B through 24 KiB, 1.10 to 1.30 below 160 B), and the runtime copy wins from 28 KiB
        // (both kernels 1.47 to 1.55). End to end (matrix raw-collection rows, 512-bit on any vendor / current policy), the
        // four Zen 5 hosts took 0.76 to 0.82 for 16 KiB reuse, 0.89 to 0.91 for 4 KiB reuse and 0.91 to 0.94 for 1 KiB reuse;
        // the Zen 4 host took 1.04, so Zen 4 keeps the 256-bit schedule. On AMD the 256-bit schedule wins from 80 B; one
        // Zen 3 host lost at 64 KiB, so the upper bound stays at 32 KiB. .NET 11 kept the large-size pattern; its faster
        // runtime copy narrowed the 80 to 255 B gain on Zen 5 to 0.86 to 1.03.
        //
        // Round 4 (run 44205342061), Intel with 512-bit vectors (Xeon 6975P-C): the 512-bit schedule took 0.81 at 128 B,
        // 0.48 at 512 B, 0.57 at 1 KiB, 0.86 at 4 KiB and 0.93 at 16 KiB, and matrix reuse 0.87 to 0.92. Intel's 256-bit
        // schedule keeps 256 B as its lower bound: at 128 B it took 1.08 on the Xeon and 1.14 on a local i7-13650HX.
        //
        // Arm64 hosts (Neoverse N2, Cobalt 100, Apple M1) have no accelerated 256-bit vectors and keep the runtime copy; a
        // 128-bit schedule had no consistent win. Streaming (non-temporal) stores were measured in round 5 and are not used:
        // into reused destinations the AMD hosts took 1.1 to 5 times as long below 4 MiB, and into freshly allocated
        // destinations they took 1.0 to 2.0 times as long below 84 KiB; only the Intel client processors gained.

        /// <summary>Processor classes whose copy and allocation thresholds were measured separately.</summary>
        internal enum ProcessorClass : byte
        {
            /// <summary>A processor without an x86 vendor match, including Arm64.</summary>
            Other,
            /// <summary>A GenuineIntel processor.</summary>
            Intel,
            /// <summary>An AuthenticAMD processor of family 1Ah (Zen 5).</summary>
            AmdZen5,
            /// <summary>Any other AuthenticAMD processor (Zen 3 and Zen 4 were measured).</summary>
            Amd,
        }

        /// <summary>The reused-destination size ranges of the 256-bit and 512-bit schedules.</summary>
        /// <remarks>An unused range is stored as (0, 0), which matches only zero bytes; the callers test for nonzero widths.</remarks>
        internal readonly struct CopyRanges
        {
            /// <summary>The smallest width of the 256-bit schedule.</summary>
            internal readonly uint MediumMinimum;

            /// <summary>The 256-bit schedule's largest width minus its smallest.</summary>
            internal readonly uint MediumSpan;

            /// <summary>The smallest width of the 512-bit schedule.</summary>
            internal readonly uint WideMinimum;

            /// <summary>The 512-bit schedule's largest width minus its smallest.</summary>
            internal readonly uint WideSpan;

            /// <summary>Creates the ranges from inclusive bounds; a zero maximum leaves a schedule unused.</summary>
            /// <param name="mediumMinimum">The smallest 256-bit width.</param>
            /// <param name="mediumMaximum">The largest 256-bit width, or zero.</param>
            /// <param name="wideMinimum">The smallest 512-bit width.</param>
            /// <param name="wideMaximum">The largest 512-bit width, or zero.</param>
            internal CopyRanges(uint mediumMinimum, uint mediumMaximum, uint wideMinimum, uint wideMaximum)
            {
                MediumMinimum = mediumMaximum == 0 ? 0 : mediumMinimum;
                MediumSpan = mediumMaximum == 0 ? 0 : mediumMaximum - mediumMinimum;
                WideMinimum = wideMaximum == 0 ? 0 : wideMinimum;
                WideSpan = wideMaximum == 0 ? 0 : wideMaximum - wideMinimum;
            }

            /// <summary>Whether the 256-bit schedule handles a nonzero width.</summary>
            /// <param name="count">A nonzero byte width.</param>
            /// <returns>Whether the width lies in the 256-bit range.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool Medium(uint count) => count - MediumMinimum <= MediumSpan;

            /// <summary>Whether the 512-bit schedule handles a nonzero width.</summary>
            /// <param name="count">A nonzero byte width.</param>
            /// <returns>Whether the width lies in the 512-bit range.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool Wide(uint count) => count - WideMinimum <= WideSpan;
        }

        /// <summary>Selects the measured copy ranges of a processor.</summary>
        /// <param name="is64Bit">Whether fixed wide typed storage is permitted.</param>
        /// <param name="has256">Accelerated 256-bit vectors.</param>
        /// <param name="has512">Accelerated 512-bit vectors.</param>
        /// <param name="processor">The processor class.</param>
        /// <returns>The 256-bit and 512-bit ranges; both unused without accelerated 256-bit vectors on 64-bit.</returns>
        internal static CopyRanges SelectCopyRanges(bool is64Bit, bool has256, bool has512, ProcessorClass processor)
        {
            if (!is64Bit || !has256)
                return default;
            return processor switch
            {
                ProcessorClass.Intel when has512 => new CopyRanges(0, 0, 128, MediumCopyLimit),
                ProcessorClass.AmdZen5 when has512 => new CopyRanges(80, 511, 512, 24 * 1024),
                ProcessorClass.AmdZen5 or ProcessorClass.Amd => new CopyRanges(80, MediumCopyLimit, 0, 0),
                _ => new CopyRanges(MediumCopyMinimum, MediumCopyLimit, 0, 0),
            };
        }

        /// <summary>Selects the smallest array, in bytes, that is allocated without zeroing.</summary>
        /// <param name="processor">The processor class.</param>
        /// <returns>16 KiB on Intel, otherwise 5 KiB.</returns>
        /// <remarks>The measurements are recorded beside <c>LuminPackMarshal.AllocateUninitializedArray</c>.</remarks>
        internal static ulong SelectZeroedArrayLimit(ProcessorClass processor)
            => processor == ProcessorClass.Intel ? 16UL * 1024 : 5UL * 1024;

        /// <summary>Gets the smallest width the default (Intel and unclassified) 256-bit schedule handles.</summary>
        private const uint MediumCopyMinimum = 256;

        /// <summary>Gets the upper bound of medium copies with destination-aligned vector stores.</summary>
        public const uint MediumCopyLimit = 32 * 1024;

        /// <summary>Selects the destination-aligned 256-bit medium copy of the default processor class; false retains the
        /// native block copy.</summary>
        /// <param name="is64Bit">Whether fixed wide typed storage is permitted.</param><param name="has256">256-bit support.</param><param name="count">Complete byte width.</param>
        /// <returns>Whether a block from 256 bytes through 32 KiB uses aligned vector stores.</returns>
        /// <remarks>Retained for external callers. Core itself selects per processor class through
        /// <see cref="SelectCopyRanges"/>. Targets without accelerated 256-bit vectors, including ARM64, keep the native copy.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UseMediumCopy(bool is64Bit, bool has256, uint count)
            => is64Bit && has256 && count - MediumCopyMinimum <= MediumCopyLimit - MediumCopyMinimum;
    }
}
