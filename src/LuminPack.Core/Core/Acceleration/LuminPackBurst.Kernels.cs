#if NETSTANDARD2_1
using System;
using System.Threading;

namespace LuminPack.Core;

public static unsafe partial class LuminPackBurst
{
    /// <summary>Attempts leading layout conversion while pinning data and immutable byte-offset descriptions.</summary>
    /// <param name="source">The first byte of the complete input elements.</param>
    /// <param name="destination">The first byte of the complete output elements.</param>
    /// <param name="count">The validated element count.</param>
    /// <param name="sourceStride">The complete source storage width.</param>
    /// <param name="destinationStride">The complete destination storage width.</param>
    /// <param name="sourceOffsets">Source offsets for every semantic byte.</param>
    /// <param name="destinationOffsets">Corresponding destination offsets.</param>
    /// <param name="plan">The converter-owned immutable precomputed plan, or the default descriptor for legacy callers.</param>
    /// <returns>The completed element prefix, or zero when unavailable.</returns>
    internal static int TryLayout(ref byte source, ref byte destination, int count, int sourceStride, int destinationStride,
        ReadOnlySpan<int> sourceOffsets, ReadOnlySpan<int> destinationOffsets, in global::LuminPack.Utility.LuminLayoutPlan plan = default)
    {
        var registration = Volatile.Read(ref _registration);
        if (registration is null || count <= 0) return 0;
        if (registration.Table.LayoutPlanned != null)
        {
            if (plan.Mode == 0 || !global::LuminPack.Code.LuminKernelPolicy.UseNativeLayout(count)) return 0;
            int prefix;
            fixed (byte* input = &source)
            fixed (byte* output = &destination)
            fixed (global::LuminPack.Utility.LuminLayoutPlan* descriptor = &plan)
                prefix = registration.Table.LayoutPlanned(input, output, count, descriptor);
            ValidateCompleted(prefix, count);
            return prefix;
        }
        if (registration.Table.Layout == null) return 0;
        if (sourceStride <= 0 || destinationStride <= 0 || sourceOffsets.Length != destinationOffsets.Length)
            return 0;
        for (int index = 0; index < sourceOffsets.Length; index++)
            if ((uint)sourceOffsets[index] >= (uint)sourceStride || (uint)destinationOffsets[index] >= (uint)destinationStride)
                return 0;
        int completed;
        fixed (byte* input = &source)
        fixed (byte* output = &destination)
        fixed (int* from = sourceOffsets)
        fixed (int* to = destinationOffsets)
            completed = registration.Table.Layout(input, output, count, sourceStride, destinationStride, from, to, sourceOffsets.Length);
        ValidateCompleted(completed, count);
        return completed;
    }

    /// <summary>Attempts a bounded compression match comparison.</summary>
    /// <param name="left">The first readable window.</param>
    /// <param name="right">The second readable window.</param>
    /// <param name="count">The complete maximum comparison width.</param>
    /// <param name="matched">Receives the equal prefix width.</param>
    /// <returns>Whether the backend supplied the match length.</returns>
    internal static bool TryCountMatch(ref byte left, ref byte right, int count, out int matched)
    {
        matched = 0;
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.CountMatch == null || count <= 0) return false;
        uint completed;
        fixed (byte* a = &left)
        fixed (byte* b = &right)
            completed = registration.Table.CountMatch(a, b, (uint)count);
        ValidateCompleted(unchecked((int)completed), count);
        matched = (int)completed;
        return true;
    }

    /// <summary>Attempts an overlapping periodic LZ expansion after the caller validates the backward reference.</summary>
    /// <param name="destination">The complete output range, with its prior distance bytes still readable.</param>
    /// <param name="count">The complete output count.</param>
    /// <param name="distance">The validated positive backward distance.</param>
    /// <returns>Whether the backend completed the expansion.</returns>
    internal static bool TryCopyMatch(ref byte destination, int count, int distance)
    {
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.CopyMatch == null || count <= 0 || distance <= 0) return false;
        fixed (byte* output = &destination)
            registration.Table.CopyMatch(output, (uint)count, (uint)distance);
        return true;
    }

    /// <summary>Attempts whole ChaCha20 blocks without changing the input state or counter policy.</summary>
    /// <param name="state">The caller-owned sixteen words.</param>
    /// <param name="output">The complete output for the requested blocks.</param>
    /// <param name="blocks">One, two or four blocks.</param>
    /// <returns>Whether the backend wrote all requested blocks.</returns>
    internal static bool TryChaCha20(uint* state, byte* output, int blocks)
    {
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.ChaCha20 == null) return false;
        registration.Table.ChaCha20(state, output, blocks);
        return true;
    }

    /// <summary>Attempts the complete XOR after the caller validates all three ranges.</summary>
    /// <param name="output">The complete writable range.</param>
    /// <param name="input">The complete input, which may equal output.</param>
    /// <param name="keyStream">The complete keystream range.</param>
    /// <param name="count">The byte count.</param>
    /// <returns>Whether the backend completed the operation.</returns>
    internal static bool TryXor(byte* output, byte* input, byte* keyStream, int count)
    {
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.Xor == null || count <= 0) return false;
        registration.Table.Xor(output, input, keyStream, (uint)count);
        return true;
    }

}

#endif
