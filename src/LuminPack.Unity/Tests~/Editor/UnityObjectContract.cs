#if UNITY_EDITOR
#nullable enable
using System;
using System.Linq;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Utility;
using LuminPack.Unity;
using UnityEngine;

/// <summary>Runs focused native-owner codec checks inside a real Unity Editor, without a generated application model.</summary>
public static unsafe class UnityObjectContract
{
    /// <summary>Checks actual native objects, reference reuse, all short windows, nulls, sizes and forged counts.</summary>
    public static void RunAll()
    {
        var curve = new AnimationCurve(new Keyframe(0, 1, -2, 3, 0.125f, 0.875f), new Keyframe(1, 4, 5, -6, 0.375f, 0.625f))
        { preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.PingPong };
        var curveTarget = new AnimationCurve(new Keyframe(0, 9));
        Check<AnimationCurve>(curve, curveTarget, &LuminUnity.Write, &LuminUnity.Read);
        for (int sample = -8; sample <= 24; sample++)
            Require(Mathf.Abs(curve.Evaluate(sample / 16f) - curveTarget.Evaluate(sample / 16f)) < 0.00001f, "Curve evaluation changed.");

        var gradient = new Gradient { mode = GradientMode.Fixed, colorSpace = ColorSpace.Linear };
        gradient.SetKeys(new[] { new GradientColorKey(Color.red, 0), new GradientColorKey(Color.blue, 1) },
            new[] { new GradientAlphaKey(0.125f, 0), new GradientAlphaKey(0.875f, 1) });
        var gradientTarget = new Gradient();
        Check<Gradient>(gradient, gradientTarget, &LuminUnity.Write, &LuminUnity.Read);
        Require(gradientTarget.mode == gradient.mode && gradientTarget.colorSpace == gradient.colorSpace, "Gradient metadata changed.");
        Check<RectOffset>(new RectOffset(-1, 2, 3, -4), new RectOffset(10, 11, 12, 13), &LuminUnity.Write, &LuminUnity.Read);
        LuminUnity.Register(); LuminUnity.Register();
        Debug.Log("LUMINPACK_UNITY_OBJECTS_OK: curve/gradient/offset native round trips, reuse, all truncations, nulls, exact-length writes and count validation.");
    }

    /// <summary>Runs an independent malformed/truncated input matrix before checking complete publication.</summary>
    /// <typeparam name="T">Native Unity owner type.</typeparam>
    /// <param name="source">Source owner.</param><param name="destination">Reusable owner.</param><param name="write">Writer.</param><param name="read">Reader.</param>
    private static void Check<T>(T source, T destination, delegate*<ref LuminPackWriter, in T?, void> write,
        delegate*<ref LuminPackReader, ref T?, void> read) where T : class
    {
        byte[] record = Encode(source, write), before = Encode(destination, write);
        T? current = destination;
        fixed (byte* input = record)
        {
            for (int length = 0; length < record.Length; length++)
            {
                ReadOnlySpan<byte> window = new ReadOnlySpan<byte>(input, length);
                var reader = new LuminPackReader(ref window);
                bool rejected = false;
                try { read(ref reader, ref current); } catch (Exception) { rejected = true; }
                Require(rejected && reader.Position == 0 && ReferenceEquals(destination, current), "Short record published a native owner or cursor.");
                Require(before.SequenceEqual(Encode(current, write, size)), "Short record mutated a native owner.");
            }
            ReadOnlySpan<byte> complete = new ReadOnlySpan<byte>(input, record.Length);
            var full = new LuminPackReader(ref complete);
            read(ref full, ref current);
            Require(full.Position == record.Length && ReferenceEquals(current, destination), "Native owner reuse or cursor completion failed.");
            Require(record.SequenceEqual(Encode(current, write, size)), "Native object record was not stable.");
            T? fresh = null;
            var freshReader = new LuminPackReader(ref complete);
            read(ref freshReader, ref fresh);
            Require(fresh is not null && !ReferenceEquals(source, fresh) && record.SequenceEqual(Encode(fresh, write, size)), "Fresh native owner construction failed.");
        }
        if (typeof(T) != typeof(RectOffset))
        {
            foreach (int count in new[] { -2, -1, int.MaxValue })
            {
                byte[] malformed = (byte[])record.Clone();
                BitConverter.GetBytes(count).CopyTo(malformed, 9);
                fixed (byte* input = malformed)
                {
                    ReadOnlySpan<byte> window = new ReadOnlySpan<byte>(input, malformed.Length);
                    var reader = new LuminPackReader(ref window);
                    bool rejected = false;
                    try { read(ref reader, ref current); } catch (Exception) { rejected = true; }
                    Require(rejected && reader.Position == 0, "Forged key count was accepted or advanced the caller.");
                    Require(record.SequenceEqual(Encode(current, write, size)), "Forged count changed native state.");
                }
            }
        }
        byte[] nullRecord = Encode<T>(null, write, size);
        Require(nullRecord.Length == 1, "Null object framing changed.");
        fixed (byte* input = nullRecord)
        {
            ReadOnlySpan<byte> window = new ReadOnlySpan<byte>(input, nullRecord.Length);
            var reader = new LuminPackReader(ref window);
            read(ref reader, ref current);
            Require(current is null && reader.Position == 1, "Null record failed.");
        }
    }

    /// <summary>Checks a repeatable record length and bounded output through a pinned netstandard cursor.</summary>
    /// <typeparam name="T">Native owner type.</typeparam>
    /// <param name="value">Value or null.</param><param name="write">Writer.</param><returns>Complete record bytes.</returns>
    private static byte[] Encode<T>(T? value, delegate*<ref LuminPackWriter, in T?, void> write) where T : class
    {
        int length;
        var buffer = LuminBufferWriterPool.Rent();
        try
        {
            var probe = new LuminPackWriter(buffer);
            write(ref probe, value);
            length = probe.CurrentIndex;
        }
        finally { LuminBufferWriterPool.Return(buffer); }
        byte[] output = Enumerable.Repeat((byte)0xA5, length + 8).ToArray();
        fixed (byte* data = output)
        {
            Span<byte> span = new Span<byte>(data + 3, length);
            var writer = new LuminPackWriter(ref span);
            write(ref writer, value);
            Require(writer.Position == length, "Native object record length changed between writes.");
        }
        Require(output.Take(3).Concat(output.Skip(3 + length)).All(b => b == 0xA5), "Native object output crossed its window.");
        return output.Skip(3).Take(length).ToArray();
    }

    /// <summary>Reports a focused validation failure.</summary><param name="condition">Required condition.</param><param name="message">Detail.</param>
    private static void Require(bool condition, string message)
    { if (!condition) LuminPackExceptionHelper.ThrowInvalidOperationException(message); }
}
#endif
