using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.JsonPerfBenchmarks;

/// <summary>
/// Reproducible comparison for the xjb-backed JSON float/double writer.
///
/// <para><b>New</b>: the shipped <see cref="LuminPackJsonWriter.WriteFloat(float)"/> /
/// <see cref="LuminPackJsonWriter.WriteDouble(double)"/>, which use the xjb shortest-decimal core
/// (Core/Json/Xjb).</para>
/// <para><b>Legacy</b>: the writer that shipped before the port, reproduced here verbatim (guarded
/// 1..9/17 fraction-digit loop with round-trip verification plus the Utf8Formatter fallback) so the
/// comparison stays reproducible after the production code changed.</para>
/// <para><b>DotNet</b>: <c>Utf8Formatter.TryFormat</c> with the default "G" format, i.e. what other
/// serializers do.</para>
///
/// Run: BenchmarkRunner.Run&lt;LuminPackXjbFloatBenchmark&gt;();
/// </summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 10)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class LuminPackXjbFloatBenchmark
{
    private const int Count = 4096;

    private byte[] _buffer = null!;
    private LuminPackWriterOptionalState _state = null!;
    private float[] _floatGame = null!, _floatRandom = null!, _floatAdversarial = null!;
    private double[] _doubleGame = null!, _doubleRandom = null!, _doubleAdversarial = null!;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        // One writer state for all invocations, so MemoryDiagnoser reports the formatting cost and
        // not the per-call state object (the generated serializers reuse a thread-static state).
        _state = new LuminPackWriterOptionalState();
        _floatGame = FloatingData.CreateFloats(FloatingDataset.GameLike, Count);
        _floatRandom = FloatingData.CreateFloats(FloatingDataset.RandomFinite, Count);
        _floatAdversarial = FloatingData.CreateFloats(FloatingDataset.Adversarial, Count);
        _doubleGame = FloatingData.CreateDoubles(FloatingDataset.GameLike, Count);
        _doubleRandom = FloatingData.CreateDoubles(FloatingDataset.RandomFinite, Count);
        _doubleAdversarial = FloatingData.CreateDoubles(FloatingDataset.Adversarial, Count);
        _buffer = new byte[Count * 32];

        PrintSizeDiagnostics();
    }

    /// <summary>Serialized size is part of the contract: a formatter can look faster by writing less.</summary>
    private void PrintSizeDiagnostics()
    {
        Span<byte> oldBytes = stackalloc byte[64];
        Span<byte> newBytes = stackalloc byte[64];

        foreach ((string name, float[] values) in new[]
                 {
                     ("GameLike", _floatGame), ("RandomFinite", _floatRandom), ("Adversarial", _floatAdversarial)
                 })
        {
            long oldTotal = 0, newTotal = 0, differences = 0;
            foreach (float v in values)
            {
                int oldLength = LegacyFormatter.Float(oldBytes, v);
                int newLength = LuminPackJsonFloatProbe.Write(newBytes, v);
                oldTotal += oldLength;
                newTotal += newLength;
                if (!oldBytes[..oldLength].SequenceEqual(newBytes[..newLength]))
                    differences++;
            }

            Console.WriteLine($"FLOAT {name,-12} legacy={oldTotal} new={newTotal} ({(double)newTotal / oldTotal:F5}x) diffs={differences}/{values.Length}");
        }

        foreach ((string name, double[] values) in new[]
                 {
                     ("GameLike", _doubleGame), ("RandomFinite", _doubleRandom), ("Adversarial", _doubleAdversarial)
                 })
        {
            long oldTotal = 0, newTotal = 0, differences = 0;
            foreach (double v in values)
            {
                int oldLength = LegacyFormatter.Double(oldBytes, v);
                int newLength = LuminPackJsonFloatProbe.Write(newBytes, v);
                oldTotal += oldLength;
                newTotal += newLength;
                if (!oldBytes[..oldLength].SequenceEqual(newBytes[..newLength]))
                    differences++;
            }

            Console.WriteLine($"DOUBLE {name,-12} legacy={oldTotal} new={newTotal} ({(double)newTotal / oldTotal:F5}x) diffs={differences}/{values.Length}");
        }
    }

    /// <summary>Measures floating cases for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    public static System.Collections.Generic.IEnumerable<object[]> FloatingCases()
    {
        yield return new object[] { FloatingDataset.GameLike };
        yield return new object[] { FloatingDataset.RandomFinite };
        yield return new object[] { FloatingDataset.Adversarial };
    }

    /// <summary>Measures float legacy for the configured benchmark input.</summary>
    /// <param name="d">The d used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("Float"), ArgumentsSource(nameof(FloatingCases))]
    public int Float_Legacy(FloatingDataset d) => WriteFloats(_buffer, GetFloats(d), 0, _state);

    /// <summary>Measures float xjb for the configured benchmark input.</summary>
    /// <param name="d">The d used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [Benchmark, BenchmarkCategory("Float"), ArgumentsSource(nameof(FloatingCases))]
    public int Float_Xjb(FloatingDataset d) => WriteFloats(_buffer, GetFloats(d), 1, _state);

    /// <summary>Measures float dot net for the configured benchmark input.</summary>
    /// <param name="d">The d used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [Benchmark, BenchmarkCategory("Float"), ArgumentsSource(nameof(FloatingCases))]
    public int Float_DotNet(FloatingDataset d) => WriteFloats(_buffer, GetFloats(d), 2, _state);

    /// <summary>Measures double legacy for the configured benchmark input.</summary>
    /// <param name="d">The d used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("Double"), ArgumentsSource(nameof(FloatingCases))]
    public int Double_Legacy(FloatingDataset d) => WriteDoubles(_buffer, GetDoubles(d), 0, _state);

    /// <summary>Measures double xjb for the configured benchmark input.</summary>
    /// <param name="d">The d used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [Benchmark, BenchmarkCategory("Double"), ArgumentsSource(nameof(FloatingCases))]
    public int Double_Xjb(FloatingDataset d) => WriteDoubles(_buffer, GetDoubles(d), 1, _state);

    /// <summary>Measures double dot net for the configured benchmark input.</summary>
    /// <param name="d">The d used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [Benchmark, BenchmarkCategory("Double"), ArgumentsSource(nameof(FloatingCases))]
    public int Double_DotNet(FloatingDataset d) => WriteDoubles(_buffer, GetDoubles(d), 2, _state);

    /// <summary>Obtains the floats for the supplied input.</summary>
    /// <param name="d">The d used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private float[] GetFloats(FloatingDataset d) =>
        d == FloatingDataset.GameLike ? _floatGame : d == FloatingDataset.RandomFinite ? _floatRandom : _floatAdversarial;

    /// <summary>Obtains the doubles for the supplied input.</summary>
    /// <param name="d">The d used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private double[] GetDoubles(FloatingDataset d) =>
        d == FloatingDataset.GameLike ? _doubleGame : d == FloatingDataset.RandomFinite ? _doubleRandom : _doubleAdversarial;

    /// <summary>Writes floats to the current output.</summary>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="values">The values used by this operation.</param>
    /// <param name="mode">The mode used by this operation.</param>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteFloats(Span<byte> destination, float[] values, int mode, LuminPackWriterOptionalState state)
    {
        if (mode == 2)
        {
            int position = 0;
            foreach (float value in values)
            {
                Utf8Formatter.TryFormat(value, destination[position..], out int written, s_g);
                position += written;
                destination[position++] = (byte)',';
            }

            return position;
        }

        if (mode == 1)
        {
            Span<byte> span = destination;
            var writer = new LuminPackJsonWriter(ref span, state);
            foreach (float value in values)
                writer.WriteFloat(value);
            return writer.CurrentIndex;
        }

        {
            int position = 0;
            foreach (float value in values)
            {
                position += LegacyFormatter.Float(destination[position..], value);
                destination[position++] = (byte)',';
            }

            return position;
        }
    }

    /// <summary>Writes doubles to the current output.</summary>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="values">The values used by this operation.</param>
    /// <param name="mode">The mode used by this operation.</param>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteDoubles(Span<byte> destination, double[] values, int mode, LuminPackWriterOptionalState state)
    {
        if (mode == 2)
        {
            int position = 0;
            foreach (double value in values)
            {
                Utf8Formatter.TryFormat(value, destination[position..], out int written, s_g);
                position += written;
                destination[position++] = (byte)',';
            }

            return position;
        }

        if (mode == 1)
        {
            Span<byte> span = destination;
            var writer = new LuminPackJsonWriter(ref span, state);
            foreach (double value in values)
                writer.WriteDouble(value);
            return writer.CurrentIndex;
        }

        {
            int position = 0;
            foreach (double value in values)
            {
                position += LegacyFormatter.Double(destination[position..], value);
                destination[position++] = (byte)',';
            }

            return position;
        }
    }

    private static readonly StandardFormat s_g = new('G');
}

/// <summary>Public-API bridge to the shipped formatter used by the size diagnostics.</summary>
internal static class LuminPackJsonFloatProbe
{
    /// <summary>Writes  to the current output.</summary>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int Write(Span<byte> destination, float value)
    {
        Span<byte> span = destination;
        var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
        writer.WriteFloat(value);
        return writer.CurrentIndex;
    }

    /// <summary>Writes  to the current output.</summary>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int Write(Span<byte> destination, double value)
    {
        Span<byte> span = destination;
        var writer = new LuminPackJsonWriter(ref span, new LuminPackWriterOptionalState());
        writer.WriteDouble(value);
        return writer.CurrentIndex;
    }
}

/// <summary>
/// The float/double formatting code that shipped before the xjb port, kept verbatim for A/B
/// measurements (guarded 1..9/17 fraction-digit loop with round-trip verification + Utf8Formatter).
/// </summary>
internal static class LegacyFormatter
{
    private static readonly StandardFormat G = new('G');
    private static readonly double[] FloatPow10D = { 1.0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9 };
    private static readonly double[] DoublePow10D = { 1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17 };

    /// <summary>Measures float for the configured benchmark input.</summary>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int Float(Span<byte> dest, float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        if ((bits & 0x7F800000u) == 0x7F800000u)
        {
            Utf8Formatter.TryFormat(value, dest, out int n, G);
            return n;
        }

        uint absBits = bits & 0x7FFF_FFFFu;
        uint exp = (absBits >> 23) & 0xFFu;
        if (absBits == 0 || (uint)(exp - 95u) <= 64u)
        {
            int fast = TryFloat(dest, value);
            if (fast > 0)
                return fast;
        }

        Utf8Formatter.TryFormat(value, dest, out int written, G);
        return written;
    }

    /// <summary>Measures double for the configured benchmark input.</summary>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int Double(Span<byte> dest, double value)
    {
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        if ((bits & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL)
        {
            Utf8Formatter.TryFormat(value, dest, out int n, G);
            return n;
        }

        ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;
        ulong exp = (absBits >> 52) & 0x7FFUL;
        if (absBits == 0 || (ulong)(exp - 991UL) <= 64UL)
        {
            int fast = TryDouble(dest, value);
            if (fast > 0)
                return fast;
        }

        Utf8Formatter.TryFormat(value, dest, out int written, G);
        return written;
    }

    /// <summary>Attempts to float and reports whether it succeeded.</summary>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static int TryFloat(Span<byte> dest, float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        int pos = 0;
        if ((bits & 0x80000000u) != 0)
        {
            dest[pos++] = (byte)'-';
            value = -value;
            bits &= 0x7FFFFFFFu;
        }

        if (bits == 0)
        {
            dest[pos++] = (byte)'0';
            return pos;
        }

        double d = value;
        if (d < 1e15 && value == MathF.Floor(value))
        {
            pos += WriteUInt64(dest[pos..], (ulong)d);
            return pos;
        }

        for (int dec = 1; dec <= 9; dec++)
        {
            double scale = FloatPow10D[dec];
            double scaledD = d * scale + 0.5;
            if (scaledD >= 9.2e18) continue;

            long scaled = (long)scaledD;
            if ((float)((double)scaled / scale) != value) continue;

            pos += EmitScaledFixed(dest[pos..], (ulong)scaled, dec);
            return pos;
        }

        return 0;
    }

    /// <summary>Attempts to double and reports whether it succeeded.</summary>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static int TryDouble(Span<byte> dest, double value)
    {
        ulong bits64 = BitConverter.DoubleToUInt64Bits(value);
        int pos = 0;
        if ((bits64 & 0x8000_0000_0000_0000UL) != 0)
        {
            dest[pos++] = (byte)'-';
            value = -value;
            bits64 &= 0x7FFF_FFFF_FFFF_FFFFUL;
        }

        if (bits64 == 0)
        {
            dest[pos++] = (byte)'0';
            return pos;
        }

        if (value < 1e15 && value == Math.Floor(value))
        {
            pos += WriteUInt64(dest[pos..], (ulong)value);
            return pos;
        }

        for (int dec = 1; dec <= 17; dec++)
        {
            double scale = DoublePow10D[dec];
            double scaledD = value * scale + 0.5;
            if (scaledD >= 9.2e18) continue;

            long scaled = (long)scaledD;
            if ((double)scaled / scale != value) continue;

            pos += EmitScaledFixed(dest[pos..], (ulong)scaled, dec);
            return pos;
        }

        return 0;
    }

    /// <summary>Writes u int64 to the current output.</summary>
    /// <param name="dest">The destination receiving the result.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static int WriteUInt64(Span<byte> dest, ulong value)
    {
        if (value < 10)
        {
            dest[0] = (byte)('0' + value);
            return 1;
        }

        int digits = 0;
        ulong tmp = value;
        while (tmp != 0)
        {
            tmp /= 10;
            digits++;
        }

        int pos = digits;
        while (value != 0)
        {
            dest[--pos] = (byte)('0' + value % 10);
            value /= 10;
        }

        return digits;
    }

    /// <summary>Measures emit scaled fixed for the configured benchmark input.</summary>
    /// <param name="destination">The destination receiving the result.</param>
    /// <param name="scaled">The scaled used by this operation.</param>
    /// <param name="decimals">The decimals used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static int EmitScaledFixed(Span<byte> destination, ulong scaled, int decimals)
    {
        while (decimals > 0 && scaled % 10 == 0)
        {
            scaled /= 10;
            decimals--;
        }

        if (scaled == 0)
        {
            destination[0] = (byte)'0';
            return 1;
        }

        int digits = 0;
        ulong tmp = scaled;
        while (tmp != 0)
        {
            tmp /= 10;
            digits++;
        }

        int length = decimals == 0 ? digits : (digits > decimals ? digits + 1 : decimals + 2);
        int dot = decimals == 0 ? length : length - decimals - 1;
        if (decimals > 0)
            destination[dot] = (byte)'.';

        int pos = length;
        ulong value = scaled;
        while (value != 0)
        {
            if (pos - 1 == dot) pos--;
            destination[--pos] = (byte)('0' + value % 10);
            value /= 10;
        }

        while (pos > 0)
        {
            if (pos - 1 == dot) pos--;
            destination[--pos] = (byte)'0';
        }

        return length;
    }
}
