#if NETSTANDARD2_1
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.Code;

namespace LuminPack.Core;

/// <summary>Publishes netstandard2.1-only native acceleration hooks without depending on Unity or Burst assemblies.</summary>
/// <remarks>
/// Only trusted initialization code may register kernels. Native code owns ISA selection and must obey the documented
/// complete-range, validation, overlap and lifetime contracts. Registration is atomic; clearing a table does not wait
/// for calls already using its snapshot. Keep native code alive until those calls finish. No backend is installed by default.
/// </remarks>
public static unsafe partial class LuminPackBurst
{
    /// <summary>The supported native kernel table ABI.</summary>
    public const uint AbiVersion = 3;

    /// <summary>A copied table of optional Cdecl kernels; a null field leaves that operation on its existing path.</summary>
    /// <remarks>Pointers receive pinned, complete caller-owned ranges and must never retain them or throw across the ABI.</remarks>
    public struct KernelTable
    {
        /// <summary>Must equal <see cref="AbiVersion"/> when registering a table.</summary>
        public uint Version;

        /// <summary>Copies destination, source and byte count; returns one on completion, or zero without writes to retain Core's native copy.</summary>
        public delegate* unmanaged[Cdecl]<byte*, byte*, uint, int> Copy;

        /// <summary>Compares two complete byte ranges; returns one for equality and zero otherwise.</summary>
        public delegate* unmanaged[Cdecl]<byte*, byte*, uint, int> Equal;

        /// <summary>Encodes UTF-16 to UTF-8: source/count, destination/capacity, consumed, written, flags; returns OperationStatus or -1 without writes when unsupported.</summary>
        public delegate* unmanaged[Cdecl]<char*, int, byte*, int, int*, int*, uint, int> Utf8Encode;

        /// <summary>Decodes UTF-8 to UTF-16 with the same count/status/flag contract as <see cref="Utf8Encode"/>.</summary>
        public delegate* unmanaged[Cdecl]<byte*, int, char*, int, int*, int*, uint, int> Utf8Decode;

        /// <summary>Compares complete UTF-8 and UTF-16 ranges; returns one only for valid exactly equal text, zero for mismatch or invalid input.</summary>
        public delegate* unmanaged[Cdecl]<byte*, int, char*, int, int> Utf8Equal;

        /// <summary>Encodes complete unquoted ASCII text; flag bit zero excludes JSON escapes. Returns one on success, or zero without writes.</summary>
        public delegate* unmanaged[Cdecl]<char*, int, byte*, uint, int> AsciiEncode;

        /// <summary>Converts leading complete layout elements: input/output, count, strides, source/destination byte-offset arrays and their length; returns completed elements.</summary>
        public delegate* unmanaged[Cdecl]<byte*, byte*, int, int, int, int*, int*, int, int> Layout;

        /// <summary>Counts equal leading bytes within two complete ranges; returns at most the supplied byte count.</summary>
        public delegate* unmanaged[Cdecl]<byte*, byte*, uint, uint> CountMatch;

        /// <summary>Expands a periodic LZ match: destination, count and positive backward distance; supports overlap and bounded tails.</summary>
        public delegate* unmanaged[Cdecl]<byte*, uint, uint, void> CopyMatch;

        /// <summary>Writes one, two or four consecutive 64-byte ChaCha20 blocks from sixteen words without modifying the state.</summary>
        public delegate* unmanaged[Cdecl]<uint*, byte*, int, void> ChaCha20;

        /// <summary>XORs complete input and keystream ranges into output; exact in-place input/output aliasing is supported.</summary>
        public delegate* unmanaged[Cdecl]<byte*, byte*, byte*, uint, void> Xor;

        /// <summary>Checks complete UTF-16 code units for Latin-1 representability without writes.</summary>
        public delegate* unmanaged[Cdecl]<char*, int, int> IsLatin1;

        /// <summary>Narrows complete UTF-16 to Latin-1; zero means unrepresentable and may leave a narrowed prefix, matching Core's existing contract.</summary>
        public delegate* unmanaged[Cdecl]<char*, byte*, int, int> NarrowLatin1;

        /// <summary>Widens a complete Latin-1 payload into unpublished UTF-16 storage.</summary>
        public delegate* unmanaged[Cdecl]<byte*, char*, int, void> WidenLatin1;

        /// <summary>Compares a complete Latin-1 payload with the same number of UTF-16 code units.</summary>
        public delegate* unmanaged[Cdecl]<byte*, char*, int, int> Latin1Equal;

        /// <summary>Converts a complete element prefix using Core's immutable precomputed layout plan.</summary>
        public delegate* unmanaged[Cdecl]<byte*, byte*, int, global::LuminPack.Utility.LuminLayoutPlan*, int> LayoutPlanned;

    }

    private sealed class Registration
    {
        internal readonly KernelTable Table;

        /// <summary>Captures an immutable copy of a caller's kernel configuration.</summary>
        /// <param name="table">The ABI-validated kernel table.</param>
        internal Registration(KernelTable table) => Table = table;
    }

    private static Registration? _registration;

    /// <summary>Gets whether a table has been registered; individual slots can still be null.</summary>
    public static bool IsRegistered => Volatile.Read(ref _registration) is not null;

    /// <summary>Atomically installs a copied table whose kernels and native code remain owned by the caller.</summary>
    /// <param name="kernels">The table with <see cref="KernelTable.Version"/> set to <see cref="AbiVersion"/>.</param>
    public static void Register(KernelTable kernels)
    {
        if (kernels.Version != 2 && kernels.Version != AbiVersion)
            LuminPackExceptionHelper.ThrowArgumentException("Unsupported LuminPackBurst kernel table ABI.", nameof(kernels));
        if (kernels.Version == 2)
        {
            // Existing managed version-two callers keep their original table semantics.
            kernels.IsLatin1 = null;
            kernels.NarrowLatin1 = null;
            kernels.WidenLatin1 = null;
            kernels.Latin1Equal = null;
            kernels.LayoutPlanned = null;
        }
        Volatile.Write(ref _registration, new Registration(kernels));
    }

    /// <summary>Stops publishing native kernels to subsequent calls without unloading native code or draining active calls.</summary>
    public static void Clear() => Volatile.Write(ref _registration, null);

    /// <summary>Gets whether complete UTF-8 decoding can be attempted.</summary>
    internal static bool HasUtf8Decode
    {
        get
        {
            var registration = Volatile.Read(ref _registration);
            return registration is not null && registration.Table.Utf8Decode != null;
        }
    }

    /// <summary>Gets whether complete ASCII encoding can be attempted.</summary>
    internal static bool HasAsciiEncode
    {
        get
        {
            var registration = Volatile.Read(ref _registration);
            return registration is not null && registration.Table.AsciiEncode != null;
        }
    }

    /// <summary>Gets whether a layout conversion hook is currently available.</summary>
    internal static bool HasLayout
    {
        get
        {
            var registration = Volatile.Read(ref _registration);
            return registration is not null && (registration.Table.Layout != null || registration.Table.LayoutPlanned != null);
        }
    }

    /// <summary>Checks native layout eligibility before creating a converter's lazy descriptors.</summary>
    /// <param name="count">Complete elements.</param><returns>Whether the selected backend can receive this batch.</returns>
    internal static bool UseLayout(int count)
    {
        var registration = Volatile.Read(ref _registration);
        if (registration is null) return false;
        if (registration.Table.LayoutPlanned != null) return LuminKernelPolicy.UseNativeLayout(count);
        return registration.Table.Layout != null;
    }

    /// <summary>Attempts the optional complete non-overlapping raw copy.</summary>
    /// <param name="destination">The first byte of the complete writable range.</param>
    /// <param name="source">The first byte of the complete readable range.</param>
    /// <param name="count">The byte count.</param>
    /// <returns>Whether a registered kernel completed the copy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryCopy(ref byte destination, ref byte source, uint count)
    {
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.Copy == null || count == 0 || count > LuminKernelPolicy.ShortCopyLimit) return false;
        fixed (byte* output = &destination)
        fixed (byte* input = &source)
            return registration.Table.Copy(output, input, count) != 0;
    }

    /// <summary>Attempts a complete byte comparison while keeping both owners pinned.</summary>
    /// <param name="left">The first readable range.</param>
    /// <param name="right">The second readable range.</param>
    /// <param name="equal">Receives the comparison result.</param>
    /// <returns>Whether a registered kernel supplied the result.</returns>
    internal static bool TryEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, out bool equal)
    {
        equal = false;
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.Equal == null) return false;
        if (left.Length != right.Length) return true;
        if (left.IsEmpty) { equal = true; return true; }
        fixed (byte* a = left)
        fixed (byte* b = right)
            equal = registration.Table.Equal(a, b, (uint)left.Length) != 0;
        return true;
    }

    /// <summary>Rejects a native result that would advance a caller outside its validated range.</summary>
    /// <param name="completed">The native completed count.</param>
    /// <param name="available">The complete validated count.</param>
    internal static void ValidateCompleted(int completed, int available)
    {
        if ((uint)completed > (uint)available)
            LuminPackExceptionHelper.ThrowInvalidOperationException("A LuminPackBurst kernel returned an out-of-range count.");
    }
}

#endif
