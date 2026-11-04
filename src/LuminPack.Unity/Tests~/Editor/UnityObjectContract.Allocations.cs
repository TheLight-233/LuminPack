#if UNITY_EDITOR
#nullable enable
using System;
using LuminPack.Core;
using LuminPack.Unity;
using UnityEngine;
using Unity.Profiling;

public static unsafe partial class UnityObjectContract
{
    /// <summary>Checks allocation-free stack paths and observes pooled paths across their capacity boundary.</summary>
    private static void CheckAllocationProfiles()
    {
        var control = StartAllocation();
        GC.KeepAlive(new byte[64]);
        Require(FinishAllocation(ref control) > 0, "The allocation recorder failed its positive control.");
        foreach (int count in new[] { 0, 1, 2, 16, 17, 257 })
        {
            var keys = new Keyframe[count];
            for (int index = 0; index < count; index++) keys[index] = new Keyframe(index, index * 0.25f, -2, 3, 0.125f, 0.875f);
            CheckAllocation<AnimationCurve>(new AnimationCurve(keys), new AnimationCurve(),
                &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size, &CreateCurve, count, true);
        }
        foreach (int count in new[] { 2, 4, 8 })
        {
            var colors = new GradientColorKey[count];
            var alphas = new GradientAlphaKey[count];
            for (int index = 0; index < count; index++)
            {
                float time = (float)index / (count - 1);
                colors[index] = new GradientColorKey(new Color(time, 0.25f, 0.75f, 1), time);
                alphas[index] = new GradientAlphaKey(time, time);
            }
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            CheckAllocation<Gradient>(gradient, new Gradient(), &LuminUnity.Write, &LuminUnity.Read,
                &LuminUnity.Size, &CreateGradient, count, false);
        }
        CheckAllocation<RectOffset>(new RectOffset(-1, 2, 3, -4), new RectOffset(),
            &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size, &CreateOffset, 4, true);
    }

    /// <summary>Measures native-owner reads against necessary wrapper construction after warming the selected scratch path.</summary>
    /// <typeparam name="T">The native owner class.</typeparam>
    /// <param name="source">The complete input owner.</param>
    /// <param name="target">The reusable output owner.</param>
    /// <param name="write">The binary encoder.</param>
    /// <param name="read">The complete binary reader.</param>
    /// <param name="size">The exact size evaluator.</param>
    /// <param name="create">The required result constructor.</param>
    /// <param name="count">The fixture key count for diagnostics.</param>
    /// <param name="sizeWithoutScratch">Whether older Unity also offers a direct count query.</param>
    private static void CheckAllocation<T>(T source, T target,
        delegate*<ref LuminPackWriter, in T?, void> write,
        delegate*<ref LuminPackReader, ref T?, void> read,
        delegate*<ref LuminPackEvaluator, in T?, void> size,
        delegate*<T> create, int count, bool sizeWithoutScratch) where T : class
    {
        byte[] record = Encode(source, write);
        byte[] output = new byte[record.Length];
        T? reused = target;
        fixed (byte* data = record)
        fixed (byte* destination = output)
        {
            ReadOnlySpan<byte> input = new ReadOnlySpan<byte>(data, record.Length);
            Span<byte> targetBytes = new Span<byte>(destination, output.Length);
            for (int iteration = 0; iteration < 64; iteration++)
            {
                var reader = new LuminPackReader(ref input);
                read(ref reader, ref reused);
                var evaluator = new LuminPackEvaluator();
                size(ref evaluator, source);
                var writer = new LuminPackWriter(ref targetBytes);
                write(ref writer, source);
                Require(evaluator.Value == record.Length && reader.Position == record.Length && ReferenceEquals(reused, target), "Scratch boundary changed native owner publication.");
                T? fresh = null;
                reader = new LuminPackReader(ref input);
                read(ref reader, ref fresh);
                GC.KeepAlive(fresh);
                GC.KeepAlive(create());
            }
            Require(Encode(reused, write).AsSpan().SequenceEqual(record), "Scratch boundary changed native key values.");
            var recording = StartAllocation();
            for (int iteration = 0; iteration < 128; iteration++)
            {
                var reader = new LuminPackReader(ref input);
                read(ref reader, ref reused);
            }
            long reuseAllocations = FinishAllocation(ref recording);
            // Older read APIs can trigger collection and trim shared scratch; warm the write path independently.
            for (int iteration = 0; iteration < 64; iteration++)
            {
                var writer = new LuminPackWriter(ref targetBytes);
                write(ref writer, source);
            }
            recording = StartAllocation();
            for (int iteration = 0; iteration < 128; iteration++)
            {
                var writer = new LuminPackWriter(ref targetBytes);
                write(ref writer, source);
            }
            long writeAllocations = FinishAllocation(ref recording);
            recording = StartAllocation();
            for (int iteration = 0; iteration < 128; iteration++)
            {
                var evaluator = new LuminPackEvaluator();
                size(ref evaluator, source);
            }
            long sizeAllocations = FinishAllocation(ref recording);
            recording = StartAllocation();
            for (int iteration = 0; iteration < 128; iteration++)
            {
                T? fresh = null;
                var reader = new LuminPackReader(ref input);
                read(ref reader, ref fresh);
                GC.KeepAlive(fresh);
            }
            long freshAllocations = FinishAllocation(ref recording);
            recording = StartAllocation();
            for (int iteration = 0; iteration < 128; iteration++) GC.KeepAlive(create());
            long constructionAllocations = FinishAllocation(ref recording);
            Debug.Log("LUMINPACK_UNITY_ALLOCATION_EVENTS: " + typeof(T).Name + "," + count + ",write=" + writeAllocations / 128.0 + ",reuse=" + reuseAllocations / 128.0 + ",fresh=" + freshAllocations / 128.0 + ",construction=" + constructionAllocations / 128.0 + ",size=" + sizeAllocations / 128.0);
#if UNITY_6000_2_OR_NEWER
            // Shared-pool trimming can occur during result construction; only stack paths promise independent zero scratch allocation.
            if (count <= 16) Require(writeAllocations == 0 && reuseAllocations == 0 && freshAllocations == constructionAllocations, "Stack-backed native-owner operations allocated discarded scratch.");
            Require(sizeAllocations == 0, "Native-owner sizing allocated key snapshots.");
#else
            if (sizeWithoutScratch) Require(sizeAllocations == 0 && (count > 16 || writeAllocations == 0), "Stack-backed native-owner writing or sizing allocated a discardable snapshot.");
#endif
        }
    }

    /// <summary>Records allocation events on the test thread without relying on Mono's zero-valued GC byte counter.</summary>
    /// <returns>A running bounded allocation recorder.</returns>
    private static ProfilerRecorder StartAllocation()
    {
        var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 8192, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        Require(recorder.Valid, "The allocation recorder is unavailable.");
        return recorder;
    }

    /// <summary>Stops the allocation window and counts complete allocation events before releasing native profiler storage.</summary>
    /// <param name="recorder">The running allocation recorder.</param>
    /// <returns>The managed allocation count in the window.</returns>
    private static long FinishAllocation(ref ProfilerRecorder recorder)
    {
        recorder.Stop();
        try
        {
            Require(!recorder.WrappedAround, "The allocation recorder overflowed.");
            return recorder.Count;
        }
        finally { recorder.Dispose(); }
    }

    /// <summary>Creates the required managed curve wrapper.</summary><returns>A fresh native curve owner.</returns>
    private static AnimationCurve CreateCurve() => new AnimationCurve();

    /// <summary>Creates the required managed gradient wrapper.</summary><returns>A fresh native gradient owner.</returns>
    private static Gradient CreateGradient() => new Gradient();

    /// <summary>Creates the required managed offset wrapper.</summary><returns>A fresh native offset owner.</returns>
    private static RectOffset CreateOffset() => new RectOffset();
}
#endif
