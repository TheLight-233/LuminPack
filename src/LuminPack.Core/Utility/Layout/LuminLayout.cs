using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if NETSTANDARD2_1
using System.Threading;
#endif
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif

namespace LuminPack.Utility;

/// <summary>Converts contiguous native elements between two layouts that carry the same data bytes at different offsets.</summary>
/// <remarks>
/// <para>
/// A converter is built once for a pair of offset lists. Every element is copied as a few byte runs; where the instruction set has a
/// byte shuffle and an element fits in at most two vectors, leading blocks of elements are moved with composed shuffles instead.
/// Supported 64-bit hardware can process unequal padded strides with a cached wider grouped permutation.
/// A shuffle writes zero to every byte of its output vector that carries no data, so destination padding in the converted prefix
/// becomes zero, while the byte-run tail leaves destination padding untouched. Neither side defines padding content.
/// </para>
/// <para>
/// Each shuffle block loads and stores whole vectors, so blocks stop while a complete vector still fits in both the source and the
/// destination; the byte-run loop finishes the remaining elements. Targets without a byte shuffle use the byte-run loop.
/// The netstandard2.1 target may additionally use an explicitly registered native prefix before finishing the same byte-run tail.
/// </para>
/// </remarks>
internal sealed class LuminLayout
{
    /// <summary>Copies one run of data bytes between two element layouts.</summary>
    private readonly struct Segment
    {
        /// <summary>The first source byte of the run within an element.</summary>
        internal readonly int Source;

        /// <summary>The first destination byte of the run within an element.</summary>
        internal readonly int Destination;

        /// <summary>The run length.</summary>
        internal readonly int Length;

        /// <summary>Creates a run.</summary>
        /// <param name="source">The first source byte.</param>
        /// <param name="destination">The first destination byte.</param>
        /// <param name="length">The run length.</param>
        internal Segment(int source, int destination, int length)
        {
            Source = source;
            Destination = destination;
            Length = length;
        }
    }

    private readonly Segment[] _segments;
    private readonly int _sourceStride;
    private readonly int _destinationStride;
#if NETSTANDARD2_1
    private readonly bool _allowAcceleration;
    private NativeLayout? _nativeLayout;

    /// <summary>Holds immutable native descriptors derived from the snapshotted byte runs.</summary>
    private sealed class NativeLayout
    {
        internal readonly int[] Source, Destination;
        internal readonly LuminLayoutPlan Plan;

        /// <summary>Expands the established runs in declaration order.</summary>
        /// <param name="segments">The converter-owned immutable runs.</param>
        /// <param name="sourceStride">The complete input stride.</param>
        /// <param name="destinationStride">The complete output stride.</param>
        internal NativeLayout(Segment[] segments, int sourceStride, int destinationStride)
        {
            int length = 0;
            foreach (var segment in segments) length = checked(length + segment.Length);
            Source = new int[length];
            Destination = new int[length];
            int index = 0;
            foreach (var segment in segments)
                for (int offset = 0; offset < segment.Length; offset++, index++)
                {
                    Source[index] = segment.Source + offset;
                    Destination[index] = segment.Destination + offset;
                }
            Plan = LuminLayoutPlan.Create(Source, sourceStride, Destination, destinationStride, segments.Length);
        }
    }

#endif
#if NET8_0_OR_GREATER
    /// <summary>Zero for byte runs only, one for several elements per vector, two for one element in up to two vectors.</summary>
    private readonly int _mode;

    /// <summary>The elements moved by one shuffle block.</summary>
    private readonly int _perBlock;

    /// <summary>The fewest remaining elements whose source and destination both cover every full-vector access of one block.</summary>
    private readonly int _reserve;

    /// <summary>The number of source and destination vectors a block touches.</summary>
    private readonly int _inputVectors, _outputVectors;

    /// <summary>Shuffle masks: output vector <c>o</c> takes input vector <c>i</c> through mask <c>o * 2 + i</c>; 0xFF selects zero.</summary>
    private readonly Vector128<byte> _mask0, _mask1, _mask2, _mask3;
    private readonly LuminGroupedLayout? _grouped;
#endif

    /// <summary>Builds the byte runs, and where possible the composed shuffle masks, for one direction of conversion.</summary>
    /// <param name="source">The source offset of each data byte, in the same order as <paramref name="destination"/>.</param>
    /// <param name="sourceStride">The source element width.</param>
    /// <param name="destination">The destination offset of each data byte.</param>
    /// <param name="destinationStride">The destination element width.</param>
    /// <param name="allowVectors">Whether vector shuffles may be used; tests turn them off to compare against the byte runs.</param>
    internal LuminLayout(int[] source, int sourceStride, int[] destination, int destinationStride, bool allowVectors = true)
    {
        _sourceStride = sourceStride;
        _destinationStride = destinationStride;
#if NETSTANDARD2_1
        _allowAcceleration = allowVectors;
#endif
        var runs = new List<Segment>();
        int start = 0;
        for (int index = 1; index <= source.Length; index++)
        {
            if (index < source.Length && source[index] == source[index - 1] + 1 && destination[index] == destination[index - 1] + 1)
                continue;
            runs.Add(new Segment(source[start], destination[start], index - start));
            start = index;
        }
        _segments = runs.ToArray();
#if NET8_0_OR_GREATER
        if (!allowVectors || !(Ssse3.IsSupported || AdvSimd.Arm64.IsSupported)) return;
        var plan = LuminLayoutPlan.Create(source, sourceStride, destination, destinationStride, _segments.Length);
        if (plan.Mode == 0) return;
        _mode = plan.Mode; _perBlock = plan.PerBlock; _reserve = plan.Reserve;
        _inputVectors = plan.InputVectors; _outputVectors = plan.OutputVectors;
        _mask0 = Vector128.Create(plan.Mask0Low, plan.Mask0High).AsByte();
        _mask1 = Vector128.Create(plan.Mask1Low, plan.Mask1High).AsByte();
        _mask2 = Vector128.Create(plan.Mask2Low, plan.Mask2High).AsByte();
        _mask3 = Vector128.Create(plan.Mask3Low, plan.Mask3High).AsByte();
        _grouped = LuminGroupedLayout.Create(source, sourceStride, destination, destinationStride);
#endif
    }

    /// <summary>Gets whether the vector kernel is active for this conversion.</summary>
    internal bool UsesVectors
#if NET8_0_OR_GREATER
        => _mode != 0;
#else
        => false;
#endif

    /// <summary>Converts every element between the two layouts.</summary>
    /// <param name="source">The first source byte of <c>count</c> source elements.</param>
    /// <param name="destination">The first destination byte of <c>count</c> destination elements.</param>
    /// <param name="count">The element count.</param>
    internal void Convert(ref byte source, ref byte destination, int count)
    {
        int done = 0;
#if NETSTANDARD2_1
        if (count > 0 && _allowAcceleration && global::LuminPack.Core.LuminPackBurst.UseLayout(count))
        {
            var layout = Volatile.Read(ref _nativeLayout);
            if (layout is null)
            {
                layout = new NativeLayout(_segments, _sourceStride, _destinationStride);
                layout = Interlocked.CompareExchange(ref _nativeLayout, layout, null) ?? layout;
            }
            done = global::LuminPack.Core.LuminPackBurst.TryLayout(ref source, ref destination, count,
                _sourceStride, _destinationStride, layout.Source, layout.Destination, in layout.Plan);
        }
#endif
#if NET8_0_OR_GREATER
        if (_grouped is { } grouped && count >= grouped.Reserve)
            done = grouped.ConvertPrefix(ref source, ref destination, count);
        else if (_mode != 0 && count >= _reserve)
            done = Blocks(ref source, ref destination, count);
#endif
        var segments = _segments;
        for (int element = done; element < count; element++)
        {
            ref byte from = ref Unsafe.Add(ref source, (nint)element * _sourceStride);
            ref byte to = ref Unsafe.Add(ref destination, (nint)element * _destinationStride);
            for (int index = 0; index < segments.Length; index++)
            {
                var segment = segments[index];
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref to, unchecked((nint)(uint)(segment.Destination))), ref Unsafe.Add(ref from, unchecked((nint)(uint)(segment.Source))), (uint)segment.Length);
            }
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>Selects bytes by index, producing zero for out-of-range indices on every supported instruction set.</summary>
    /// <param name="source">The source vector.</param>
    /// <param name="indices">The per-byte indices, 0xFF for zero.</param>
    /// <returns>The gathered bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Shuffle(Vector128<byte> source, Vector128<byte> indices)
        => Ssse3.IsSupported ? Ssse3.Shuffle(source, indices) : AdvSimd.Arm64.VectorTableLookup(source, indices);

    /// <summary>Moves leading blocks of elements with composed shuffles while whole vectors fit in both buffers.</summary>
    /// <param name="input">The first source byte.</param>
    /// <param name="output">The first destination byte.</param>
    /// <param name="count">The element count, at least the reserve.</param>
    /// <returns>The number of leading elements moved; the caller finishes the rest.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int Blocks(ref byte input, ref byte output, int count)
    {
        nint blocks = (count - _reserve) / _perBlock + 1;
        int done = (int)blocks * _perBlock;
        nint inputStep = (nint)_perBlock * _sourceStride, outputStep = (nint)_perBlock * _destinationStride;
        nint initial = blocks;
        LuminByteShuffle.CopyWide(ref input, ref output, ref blocks, inputStep, outputStep, _inputVectors == 2, _outputVectors == 2, _mask0, _mask1, _mask2, _mask3);
        input = ref Unsafe.Add(ref input, (initial - blocks) * inputStep);
        output = ref Unsafe.Add(ref output, (initial - blocks) * outputStep);
        var mask0 = _mask0;
        if (_mode == 1)
        {
            for (; blocks >= 2; blocks -= 2)
            {
                var first = Vector128.LoadUnsafe(ref input);
                var second = Vector128.LoadUnsafe(ref input, (nuint)inputStep);
                Shuffle(first, mask0).StoreUnsafe(ref output);
                Shuffle(second, mask0).StoreUnsafe(ref output, (nuint)outputStep);
                input = ref Unsafe.Add(ref input, inputStep * 2);
                output = ref Unsafe.Add(ref output, outputStep * 2);
            }
            if (blocks != 0)
                Shuffle(Vector128.LoadUnsafe(ref input), mask0).StoreUnsafe(ref output);
            return done;
        }

        var mask1 = _mask1;
        var mask2 = _mask2;
        var mask3 = _mask3;
        bool wideInput = _inputVectors == 2, wideOutput = _outputVectors == 2;
        for (; blocks != 0; blocks--)
        {
            var low = Vector128.LoadUnsafe(ref input);
            var high = wideInput ? Vector128.LoadUnsafe(ref input, 16) : default;
            var lowOut = Shuffle(low, mask0);
            if (wideInput)
                lowOut |= Shuffle(high, mask1);
            lowOut.StoreUnsafe(ref output);
            if (wideOutput)
            {
                var highOut = Shuffle(low, mask2);
                if (wideInput)
                    highOut |= Shuffle(high, mask3);
                highOut.StoreUnsafe(ref output, 16);
            }
            input = ref Unsafe.Add(ref input, inputStep);
            output = ref Unsafe.Add(ref output, outputStep);
        }
        return done;
    }
#endif
}

