using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

namespace LuminPack.JsonPerfBenchmarks;

/// <summary>
/// Final decision benchmark after the 225-case screening run.
/// No Main method.
///
/// Run:
/// BenchmarkRunner.Run<LuminPackJsonFinalDecisionBenchmark>();
///
/// This benchmark intentionally tests only unresolved decisions:
/// 1) type-specialized integer routing and LUT footprint (compact vs aggressive);
/// 2) float/double DirectCurrent guarded by IEEE754 exponent;
/// 3) float wide-integral + guarded DirectCurrent composition;
/// 4) decimal 64-bit fast path composed with the full 96-bit custom formatter;
/// 5) corrected adversarial floating datasets (all samples adversarial, not mostly random).
///
/// Already-settled token packed-store tests are not repeated.
/// </summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 5, iterationCount: 10)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
[DisassemblyDiagnoser(maxDepth: 8, printSource: true, printInstructionAddresses: true)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class LuminPackJsonFinalDecisionBenchmark
{
    private const int IntegerCount = 8192;
    private const int FloatCount = 4096;
    private const int DoubleCount = 4096;
    private const int DecimalCount = 4096;

    private byte[] _buffer = null!;

    private byte[] _byteSmall = null!, _byteMixed = null!, _byteBoundary = null!;
    private sbyte[] _sbyteSmall = null!, _sbyteMixed = null!, _sbyteBoundary = null!;
    private ushort[] _ushortSmall = null!, _ushortMixed = null!, _ushortBoundary = null!;
    private short[] _shortSmall = null!, _shortMixed = null!, _shortBoundary = null!;
    private uint[] _uintSmall = null!, _uintMixed = null!, _uintBoundary = null!;
    private int[] _intSmall = null!, _intMixed = null!, _intBoundary = null!;
    private ulong[] _ulongSmall = null!, _ulongMixed = null!, _ulongBoundary = null!;
    private long[] _longSmall = null!, _longMixed = null!, _longBoundary = null!;

    private float[] _floatGame = null!, _floatRandom = null!, _floatAdversarial = null!;
    private double[] _doubleGame = null!, _doubleRandom = null!, _doubleAdversarial = null!;
    private decimal[] _decimalGame = null!, _decimalRandom = null!, _decimalBoundary = null!;

    private static int s_validated;
    private static readonly object ValidationLock = new();

    [GlobalSetup]
    public void Setup()
    {
        bool firstValidatedProcess = EnsureValidated();

        var rng = new JsonBenchSplitMix64(0xA13F_7781_94D2_5519UL);

        _byteSmall = IntegerData.CreateBytes(IntegerDataset.Small, IntegerCount, ref rng);
        _byteMixed = IntegerData.CreateBytes(IntegerDataset.Mixed, IntegerCount, ref rng);
        _byteBoundary = IntegerData.CreateBytes(IntegerDataset.Boundary, IntegerCount, ref rng);

        _sbyteSmall = IntegerData.CreateSBytes(IntegerDataset.Small, IntegerCount, ref rng);
        _sbyteMixed = IntegerData.CreateSBytes(IntegerDataset.Mixed, IntegerCount, ref rng);
        _sbyteBoundary = IntegerData.CreateSBytes(IntegerDataset.Boundary, IntegerCount, ref rng);

        _ushortSmall = IntegerData.CreateUShorts(IntegerDataset.Small, IntegerCount, ref rng);
        _ushortMixed = IntegerData.CreateUShorts(IntegerDataset.Mixed, IntegerCount, ref rng);
        _ushortBoundary = IntegerData.CreateUShorts(IntegerDataset.Boundary, IntegerCount, ref rng);

        _shortSmall = IntegerData.CreateShorts(IntegerDataset.Small, IntegerCount, ref rng);
        _shortMixed = IntegerData.CreateShorts(IntegerDataset.Mixed, IntegerCount, ref rng);
        _shortBoundary = IntegerData.CreateShorts(IntegerDataset.Boundary, IntegerCount, ref rng);

        _uintSmall = IntegerData.CreateUInts(IntegerDataset.Small, IntegerCount, ref rng);
        _uintMixed = IntegerData.CreateUInts(IntegerDataset.Mixed, IntegerCount, ref rng);
        _uintBoundary = IntegerData.CreateUInts(IntegerDataset.Boundary, IntegerCount, ref rng);

        _intSmall = IntegerData.CreateInts(IntegerDataset.Small, IntegerCount, ref rng);
        _intMixed = IntegerData.CreateInts(IntegerDataset.Mixed, IntegerCount, ref rng);
        _intBoundary = IntegerData.CreateInts(IntegerDataset.Boundary, IntegerCount, ref rng);

        _ulongSmall = IntegerData.CreateULongs(IntegerDataset.Small, IntegerCount, ref rng);
        _ulongMixed = IntegerData.CreateULongs(IntegerDataset.Mixed, IntegerCount, ref rng);
        _ulongBoundary = IntegerData.CreateULongs(IntegerDataset.Boundary, IntegerCount, ref rng);

        _longSmall = IntegerData.CreateLongs(IntegerDataset.Small, IntegerCount, ref rng);
        _longMixed = IntegerData.CreateLongs(IntegerDataset.Mixed, IntegerCount, ref rng);
        _longBoundary = IntegerData.CreateLongs(IntegerDataset.Boundary, IntegerCount, ref rng);

        _floatGame = FloatingData.CreateFloats(FloatingDataset.GameLike, FloatCount);
        _floatRandom = FloatingData.CreateFloats(FloatingDataset.RandomFinite, FloatCount);
        _floatAdversarial = FinalCandidateFormatter.CreateTrueAdversarialFloats(FloatCount);

        _doubleGame = FloatingData.CreateDoubles(FloatingDataset.GameLike, DoubleCount);
        _doubleRandom = FloatingData.CreateDoubles(FloatingDataset.RandomFinite, DoubleCount);
        _doubleAdversarial = FinalCandidateFormatter.CreateTrueAdversarialDoubles(DoubleCount);

        _decimalGame = DecimalData.Create(DecimalDataset.GameLike, DecimalCount);
        _decimalRandom = DecimalData.Create(DecimalDataset.Random, DecimalCount);
        _decimalBoundary = DecimalData.Create(DecimalDataset.Boundary, DecimalCount);

        _buffer = new byte[Math.Max(IntegerCount * 32, Math.Max(DecimalCount * 40, Math.Max(FloatCount, DoubleCount) * 64))];

        // Print serialized-size diagnostics once for this compiled benchmark assembly.
        // Performance alone is not enough for float/double: a formatter can look faster
        // simply because it chooses a different (or longer) valid representation.
        if (firstValidatedProcess)
            PrintFloatingSizeDiagnostics();
    }

    private static bool EnsureValidated()
    {
        if (Volatile.Read(ref s_validated) != 0)
            return false;

        string stamp = Path.Combine(
            Path.GetTempPath(),
            $"LuminPackJsonFinalDecision-{typeof(LuminPackJsonFinalDecisionBenchmark).Assembly.ManifestModule.ModuleVersionId:N}.ok");

        if (File.Exists(stamp))
        {
            Volatile.Write(ref s_validated, 1);
            return false;
        }

        lock (ValidationLock)
        {
            if (s_validated != 0)
                return false;

            if (File.Exists(stamp))
            {
                Volatile.Write(ref s_validated, 1);
                return false;
            }

            FinalCandidateFormatter.Validate();

            try { File.WriteAllText(stamp, "ok"); }
            catch { /* Validation still succeeded; worst case another child process repeats it. */ }

            Volatile.Write(ref s_validated, 1);
            return true;
        }
    }

    private void PrintFloatingSizeDiagnostics()
    {
        PrintFloatSize("GameLike", _floatGame);
        PrintFloatSize("RandomFinite", _floatRandom);
        PrintFloatSize("TrueAdversarial", _floatAdversarial);

        PrintDoubleSize("GameLike", _doubleGame);
        PrintDoubleSize("RandomFinite", _doubleRandom);
        PrintDoubleSize("TrueAdversarial", _doubleAdversarial);
    }

    private static void PrintFloatSize(string name, float[] values)
    {
        Span<byte> tmp = stackalloc byte[128];
        long dotnet = 0, direct = 0, guard64 = 0, wideGuard64 = 0;

        foreach (float v in values)
        {
            dotnet += PrimitiveFormatter.FloatDotNet(tmp, v);
            direct += PrimitiveFormatter.FloatDirectCurrent(tmp, v);
            guard64 += FinalCandidateFormatter.FloatGuardedDirect(tmp, v, 64);
            wideGuard64 += FinalCandidateFormatter.FloatWideIntegralGuard64Direct(tmp, v);
        }

        Console.WriteLine(
            $"FLOAT_BYTES {name}: DotNet={dotnet}, Direct={direct} ({(double)direct / dotnet:F4}x), " +
            $"Guard64Direct={guard64} ({(double)guard64 / dotnet:F4}x), WideIntGuard64={wideGuard64} ({(double)wideGuard64 / dotnet:F4}x)");
    }

    private static void PrintDoubleSize(string name, double[] values)
    {
        Span<byte> tmp = stackalloc byte[128];
        long dotnet = 0, direct = 0, guard16 = 0, guard32 = 0, guard64 = 0;

        foreach (double v in values)
        {
            dotnet += PrimitiveFormatter.DoubleDotNet(tmp, v);
            direct += PrimitiveFormatter.DoubleDirectCurrent(tmp, v);
            guard16 += FinalCandidateFormatter.DoubleGuardedDirect(tmp, v, 16);
            guard32 += FinalCandidateFormatter.DoubleGuardedDirect(tmp, v, 32);
            guard64 += FinalCandidateFormatter.DoubleGuardedDirect(tmp, v, 64);
        }

        Console.WriteLine(
            $"DOUBLE_BYTES {name}: DotNet={dotnet}, Direct={direct} ({(double)direct / dotnet:F4}x), " +
            $"Guard16Direct={guard16} ({(double)guard16 / dotnet:F4}x), " +
            $"Guard32Direct={guard32} ({(double)guard32 / dotnet:F4}x), " +
            $"Guard64Direct={guard64} ({(double)guard64 / dotnet:F4}x)");
    }

    // ---------------------------------------------------------------------
    // Integers
    // ---------------------------------------------------------------------

    public static IEnumerable<object[]> IntegerCases()
    {
        foreach (IntegerType type in System.Enum.GetValues<IntegerType>())
        foreach (IntegerDataset data in System.Enum.GetValues<IntegerDataset>())
            yield return new object[] { new IntegerCase(type, data) };
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IntegerFinal")]
    [ArgumentsSource(nameof(IntegerCases))]
    public int Integer_DotNet(IntegerCase c) => c.Type switch
    {
        IntegerType.Byte => IntegerDotNet(Get(_byteSmall, _byteMixed, _byteBoundary, c.Data)),
        IntegerType.SByte => IntegerDotNet(Get(_sbyteSmall, _sbyteMixed, _sbyteBoundary, c.Data)),
        IntegerType.UInt16 => IntegerDotNet(Get(_ushortSmall, _ushortMixed, _ushortBoundary, c.Data)),
        IntegerType.Int16 => IntegerDotNet(Get(_shortSmall, _shortMixed, _shortBoundary, c.Data)),
        IntegerType.UInt32 => IntegerDotNet(Get(_uintSmall, _uintMixed, _uintBoundary, c.Data)),
        IntegerType.Int32 => IntegerDotNet(Get(_intSmall, _intMixed, _intBoundary, c.Data)),
        IntegerType.UInt64 => IntegerDotNet(Get(_ulongSmall, _ulongMixed, _ulongBoundary, c.Data)),
        _ => IntegerDotNet(Get(_longSmall, _longMixed, _longBoundary, c.Data))
    };

    [Benchmark]
    [BenchmarkCategory("IntegerFinal")]
    [ArgumentsSource(nameof(IntegerCases))]
    public int Integer_CompactAdaptive(IntegerCase c) => c.Type switch
    {
        IntegerType.Byte => IntegerCompact(Get(_byteSmall, _byteMixed, _byteBoundary, c.Data)),
        IntegerType.SByte => IntegerCompact(Get(_sbyteSmall, _sbyteMixed, _sbyteBoundary, c.Data)),
        IntegerType.UInt16 => IntegerCompact(Get(_ushortSmall, _ushortMixed, _ushortBoundary, c.Data)),
        IntegerType.Int16 => IntegerCompact(Get(_shortSmall, _shortMixed, _shortBoundary, c.Data)),
        IntegerType.UInt32 => IntegerCompact(Get(_uintSmall, _uintMixed, _uintBoundary, c.Data)),
        IntegerType.Int32 => IntegerCompact(Get(_intSmall, _intMixed, _intBoundary, c.Data)),
        IntegerType.UInt64 => IntegerCompact(Get(_ulongSmall, _ulongMixed, _ulongBoundary, c.Data)),
        _ => IntegerCompact(Get(_longSmall, _longMixed, _longBoundary, c.Data))
    };

    [Benchmark]
    [BenchmarkCategory("IntegerFinal")]
    [ArgumentsSource(nameof(IntegerCases))]
    public int Integer_AggressiveAdaptive(IntegerCase c) => c.Type switch
    {
        IntegerType.Byte => IntegerAggressive(Get(_byteSmall, _byteMixed, _byteBoundary, c.Data)),
        IntegerType.SByte => IntegerAggressive(Get(_sbyteSmall, _sbyteMixed, _sbyteBoundary, c.Data)),
        IntegerType.UInt16 => IntegerAggressive(Get(_ushortSmall, _ushortMixed, _ushortBoundary, c.Data)),
        IntegerType.Int16 => IntegerAggressive(Get(_shortSmall, _shortMixed, _shortBoundary, c.Data)),
        IntegerType.UInt32 => IntegerAggressive(Get(_uintSmall, _uintMixed, _uintBoundary, c.Data)),
        IntegerType.Int32 => IntegerAggressive(Get(_intSmall, _intMixed, _intBoundary, c.Data)),
        IntegerType.UInt64 => IntegerAggressive(Get(_ulongSmall, _ulongMixed, _ulongBoundary, c.Data)),
        _ => IntegerAggressive(Get(_longSmall, _longMixed, _longBoundary, c.Data))
    };

    private static T[] Get<T>(T[] small, T[] mixed, T[] boundary, IntegerDataset data) =>
        data == IntegerDataset.Small ? small : data == IntegerDataset.Mixed ? mixed : boundary;

    private int IntegerDotNet(byte[] values) { Span<byte> d = _buffer; int p = 0; foreach (byte v in values) { Utf8Formatter.TryFormat(v, d[p..], out int n); p += n; d[p++] = (byte)','; } return p; }
    private int IntegerDotNet(sbyte[] values) { Span<byte> d = _buffer; int p = 0; foreach (sbyte v in values) { Utf8Formatter.TryFormat(v, d[p..], out int n); p += n; d[p++] = (byte)','; } return p; }
    private int IntegerDotNet(ushort[] values) { Span<byte> d = _buffer; int p = 0; foreach (ushort v in values) { Utf8Formatter.TryFormat(v, d[p..], out int n); p += n; d[p++] = (byte)','; } return p; }
    private int IntegerDotNet(short[] values) { Span<byte> d = _buffer; int p = 0; foreach (short v in values) { Utf8Formatter.TryFormat(v, d[p..], out int n); p += n; d[p++] = (byte)','; } return p; }
    private int IntegerDotNet(uint[] values) { Span<byte> d = _buffer; int p = 0; foreach (uint v in values) { Utf8Formatter.TryFormat(v, d[p..], out int n); p += n; d[p++] = (byte)','; } return p; }
    private int IntegerDotNet(int[] values) { Span<byte> d = _buffer; int p = 0; foreach (int v in values) { Utf8Formatter.TryFormat(v, d[p..], out int n); p += n; d[p++] = (byte)','; } return p; }
    private int IntegerDotNet(ulong[] values) { Span<byte> d = _buffer; int p = 0; foreach (ulong v in values) { Utf8Formatter.TryFormat(v, d[p..], out int n); p += n; d[p++] = (byte)','; } return p; }
    private int IntegerDotNet(long[] values) { Span<byte> d = _buffer; int p = 0; foreach (long v in values) { Utf8Formatter.TryFormat(v, d[p..], out int n); p += n; d[p++] = (byte)','; } return p; }

    private int IntegerCompact(byte[] values) { Span<byte> d = _buffer; int p = 0; foreach (byte v in values) { p += FinalCandidateFormatter.WriteByteCompact(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerCompact(sbyte[] values) { Span<byte> d = _buffer; int p = 0; foreach (sbyte v in values) { p += FinalCandidateFormatter.WriteSByteCompact(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerCompact(ushort[] values) { Span<byte> d = _buffer; int p = 0; foreach (ushort v in values) { p += FinalCandidateFormatter.WriteUInt16Compact(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerCompact(short[] values) { Span<byte> d = _buffer; int p = 0; foreach (short v in values) { p += FinalCandidateFormatter.WriteInt16Compact(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerCompact(uint[] values) { Span<byte> d = _buffer; int p = 0; foreach (uint v in values) { p += FinalCandidateFormatter.WriteUInt32Compact(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerCompact(int[] values) { Span<byte> d = _buffer; int p = 0; foreach (int v in values) { p += FinalCandidateFormatter.WriteInt32Compact(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerCompact(ulong[] values) { Span<byte> d = _buffer; int p = 0; foreach (ulong v in values) { p += FinalCandidateFormatter.WriteUInt64Compact(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerCompact(long[] values) { Span<byte> d = _buffer; int p = 0; foreach (long v in values) { p += FinalCandidateFormatter.WriteInt64Compact(d[p..], v); d[p++] = (byte)','; } return p; }

    private int IntegerAggressive(byte[] values) { Span<byte> d = _buffer; int p = 0; foreach (byte v in values) { p += FinalCandidateFormatter.WriteByteAggressive(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerAggressive(sbyte[] values) { Span<byte> d = _buffer; int p = 0; foreach (sbyte v in values) { p += FinalCandidateFormatter.WriteSByteAggressive(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerAggressive(ushort[] values) { Span<byte> d = _buffer; int p = 0; foreach (ushort v in values) { p += FinalCandidateFormatter.WriteUInt16Aggressive(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerAggressive(short[] values) { Span<byte> d = _buffer; int p = 0; foreach (short v in values) { p += FinalCandidateFormatter.WriteInt16Aggressive(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerAggressive(uint[] values) { Span<byte> d = _buffer; int p = 0; foreach (uint v in values) { p += FinalCandidateFormatter.WriteUInt32Aggressive(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerAggressive(int[] values) { Span<byte> d = _buffer; int p = 0; foreach (int v in values) { p += FinalCandidateFormatter.WriteInt32Aggressive(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerAggressive(ulong[] values) { Span<byte> d = _buffer; int p = 0; foreach (ulong v in values) { p += FinalCandidateFormatter.WriteUInt64Aggressive(d[p..], v); d[p++] = (byte)','; } return p; }
    private int IntegerAggressive(long[] values) { Span<byte> d = _buffer; int p = 0; foreach (long v in values) { p += FinalCandidateFormatter.WriteInt64Aggressive(d[p..], v); d[p++] = (byte)','; } return p; }

    // ---------------------------------------------------------------------
    // Float
    // ---------------------------------------------------------------------

    public static IEnumerable<object[]> FloatingCases()
    {
        foreach (FloatingDataset d in System.Enum.GetValues<FloatingDataset>())
            yield return new object[] { d };
    }

    private float[] GetFloats(FloatingDataset d) => d == FloatingDataset.GameLike ? _floatGame : d == FloatingDataset.RandomFinite ? _floatRandom : _floatAdversarial;
    private double[] GetDoubles(FloatingDataset d) => d == FloatingDataset.GameLike ? _doubleGame : d == FloatingDataset.RandomFinite ? _doubleRandom : _doubleAdversarial;

    [Benchmark(Baseline = true), BenchmarkCategory("FloatFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Float_DotNet(FloatingDataset d) => FormatFloat(d, 0);

    [Benchmark, BenchmarkCategory("FloatFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Float_DirectCurrent(FloatingDataset d) => FormatFloat(d, 1);

    [Benchmark, BenchmarkCategory("FloatFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Float_Guard32Direct(FloatingDataset d) => FormatFloat(d, 2);

    [Benchmark, BenchmarkCategory("FloatFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Float_Guard64Direct(FloatingDataset d) => FormatFloat(d, 3);

    [Benchmark, BenchmarkCategory("FloatFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Float_WideIntegralGuard64Direct(FloatingDataset d) => FormatFloat(d, 4);

    private int FormatFloat(FloatingDataset d, int mode)
    {
        float[] values = GetFloats(d); Span<byte> dst = _buffer; int pos = 0;

        if (mode == 0) { foreach (float v in values) { pos += PrimitiveFormatter.FloatDotNet(dst[pos..], v); dst[pos++] = (byte)','; } return pos; }
        if (mode == 1) { foreach (float v in values) { pos += PrimitiveFormatter.FloatDirectCurrent(dst[pos..], v); dst[pos++] = (byte)','; } return pos; }
        if (mode == 2) { foreach (float v in values) { pos += FinalCandidateFormatter.FloatGuardedDirect(dst[pos..], v, 32); dst[pos++] = (byte)','; } return pos; }
        if (mode == 3) { foreach (float v in values) { pos += FinalCandidateFormatter.FloatGuardedDirect(dst[pos..], v, 64); dst[pos++] = (byte)','; } return pos; }
        foreach (float v in values) { pos += FinalCandidateFormatter.FloatWideIntegralGuard64Direct(dst[pos..], v); dst[pos++] = (byte)','; }
        return pos;
    }

    // ---------------------------------------------------------------------
    // Double
    // ---------------------------------------------------------------------

    [Benchmark(Baseline = true), BenchmarkCategory("DoubleFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Double_DotNet(FloatingDataset d) => FormatDouble(d, 0);

    [Benchmark, BenchmarkCategory("DoubleFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Double_DirectCurrent(FloatingDataset d) => FormatDouble(d, 1);

    [Benchmark, BenchmarkCategory("DoubleFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Double_Guard16Direct(FloatingDataset d) => FormatDouble(d, 2);

    [Benchmark, BenchmarkCategory("DoubleFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Double_Guard32Direct(FloatingDataset d) => FormatDouble(d, 3);

    [Benchmark, BenchmarkCategory("DoubleFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Double_Guard64Direct(FloatingDataset d) => FormatDouble(d, 4);

    [Benchmark, BenchmarkCategory("DoubleFinal"), ArgumentsSource(nameof(FloatingCases))]
    public int Double_Guard64Hybrid(FloatingDataset d) => FormatDouble(d, 5);

    private int FormatDouble(FloatingDataset d, int mode)
    {
        double[] values = GetDoubles(d); Span<byte> dst = _buffer; int pos = 0;

        if (mode == 0) { foreach (double v in values) { pos += PrimitiveFormatter.DoubleDotNet(dst[pos..], v); dst[pos++] = (byte)','; } return pos; }
        if (mode == 1) { foreach (double v in values) { pos += PrimitiveFormatter.DoubleDirectCurrent(dst[pos..], v); dst[pos++] = (byte)','; } return pos; }
        if (mode == 2) { foreach (double v in values) { pos += FinalCandidateFormatter.DoubleGuardedDirect(dst[pos..], v, 16); dst[pos++] = (byte)','; } return pos; }
        if (mode == 3) { foreach (double v in values) { pos += FinalCandidateFormatter.DoubleGuardedDirect(dst[pos..], v, 32); dst[pos++] = (byte)','; } return pos; }
        if (mode == 4) { foreach (double v in values) { pos += FinalCandidateFormatter.DoubleGuardedDirect(dst[pos..], v, 64); dst[pos++] = (byte)','; } return pos; }
        foreach (double v in values) { pos += PrimitiveFormatter.DoubleGuard64(dst[pos..], v); dst[pos++] = (byte)','; }
        return pos;
    }

    // ---------------------------------------------------------------------
    // Decimal
    // ---------------------------------------------------------------------

    public static IEnumerable<object[]> DecimalCases()
    {
        foreach (DecimalDataset d in System.Enum.GetValues<DecimalDataset>())
            yield return new object[] { d };
    }

    private decimal[] GetDecimals(DecimalDataset d) => d == DecimalDataset.GameLike ? _decimalGame : d == DecimalDataset.Random ? _decimalRandom : _decimalBoundary;

    [Benchmark(Baseline = true), BenchmarkCategory("DecimalFinal"), ArgumentsSource(nameof(DecimalCases))]
    public int Decimal_DotNet(DecimalDataset d) => FormatDecimal(d, 0);

    [Benchmark, BenchmarkCategory("DecimalFinal"), ArgumentsSource(nameof(DecimalCases))]
    public int Decimal_Custom96(DecimalDataset d) => FormatDecimal(d, 1);

    [Benchmark, BenchmarkCategory("DecimalFinal"), ArgumentsSource(nameof(DecimalCases))]
    public int Decimal_Combined64Then96(DecimalDataset d) => FormatDecimal(d, 2);

    private int FormatDecimal(DecimalDataset d, int mode)
    {
        decimal[] values = GetDecimals(d); Span<byte> dst = _buffer; int pos = 0;
        if (mode == 0) { foreach (decimal v in values) { pos += PrimitiveFormatter.DecimalDotNet(dst[pos..], v); dst[pos++] = (byte)','; } return pos; }
        if (mode == 1) { foreach (decimal v in values) { pos += PrimitiveFormatter.DecimalCustom96Quad(dst[pos..], v); dst[pos++] = (byte)','; } return pos; }
        foreach (decimal v in values) { pos += FinalCandidateFormatter.DecimalCombined64Then96(dst[pos..], v); dst[pos++] = (byte)','; }
        return pos;
    }
}

internal static class FinalCandidateFormatter
{
    private sealed class Packed4Lut
    {
        public readonly uint[] Packed;
        public readonly byte[] Lengths;

        public Packed4Lut(int count)
        {
            Packed = new uint[count];
            Lengths = new byte[count];
            Span<byte> tmp = stackalloc byte[16];

            for (int i = 0; i < count; i++)
            {
                Utf8Formatter.TryFormat(i, tmp, out int n);
                uint p = 0;
                for (int j = 0; j < n; j++)
                    p |= (uint)tmp[j] << (j * 8);

                Packed[i] = p;
                Lengths[i] = (byte)n;
            }
        }
    }

    private static readonly Packed4Lut Lut129 = new(129);
    private static readonly Packed4Lut Lut256 = new(256);
    private static readonly Packed4Lut Lut1024 = new(1024);
    private static readonly Packed4Lut Lut2048 = new(2048);
    private static readonly Packed4Lut Lut10000 = new(10000);

    private static readonly byte[] Pairs = BuildPairs();

    private static byte[] BuildPairs()
    {
        var p = new byte[200];
        for (int i = 0; i < 100; i++)
        {
            p[i * 2] = (byte)('0' + i / 10);
            p[i * 2 + 1] = (byte)('0' + i % 10);
        }
        return p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WritePacked4(Span<byte> dest, Packed4Lut lut, int index)
    {
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(dest), lut.Packed[index]);
        return lut.Lengths[index];
    }

    // -----------------------------------------------------------------
    // Type-specialized integer final routes
    // -----------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteByteCompact(Span<byte> d, byte v) => WritePacked4(d, Lut256, v);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteByteAggressive(Span<byte> d, byte v) => WritePacked4(d, Lut256, v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSByteCompact(Span<byte> d, sbyte v) => WriteSBytePacked(d, v);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSByteAggressive(Span<byte> d, sbyte v) => WriteSBytePacked(d, v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteSBytePacked(Span<byte> d, sbyte v)
    {
        if (v >= 0) return WritePacked4(d, Lut129, v);
        d[0] = (byte)'-';
        int magnitude = -v;
        return 1 + WritePacked4(d[1..], Lut129, magnitude);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt16Compact(Span<byte> d, ushort v)
    {
        if (v < 2048) return WritePacked4(d, Lut2048, v);
        Utf8Formatter.TryFormat(v, d, out int n);
        return n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt16Aggressive(Span<byte> d, ushort v)
    {
        if (v < 10000) return WritePacked4(d, Lut10000, v);
        Utf8Formatter.TryFormat(v, d, out int n);
        return n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteInt16Compact(Span<byte> d, short v) => WriteInt16Adaptive(d, v, false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteInt16Aggressive(Span<byte> d, short v) => WriteInt16Adaptive(d, v, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteInt16Adaptive(Span<byte> d, short v, bool aggressive)
    {
        bool neg = v < 0;
        uint mag = neg ? (uint)(-(int)v) : (uint)v;
        int limit = aggressive ? 10000 : 1024;

        if (mag < (uint)limit)
        {
            int pos = 0;
            if (neg) d[pos++] = (byte)'-';
            pos += WritePacked4(d[pos..], aggressive ? Lut10000 : Lut1024, (int)mag);
            return pos;
        }

        int p = 0;
        if (neg) d[p++] = (byte)'-';
        return p + WriteUInt32PairLinear(d[p..], mag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt32Compact(Span<byte> d, uint v)
    {
        if (v < 2048) return WritePacked4(d, Lut2048, (int)v);
        Utf8Formatter.TryFormat(v, d, out int n);
        return n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt32Aggressive(Span<byte> d, uint v)
    {
        if (v < 10000) return WritePacked4(d, Lut10000, (int)v);
        Utf8Formatter.TryFormat(v, d, out int n);
        return n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteInt32Compact(Span<byte> d, int v) => WriteInt32Adaptive(d, v, false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteInt32Aggressive(Span<byte> d, int v) => WriteInt32Adaptive(d, v, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteInt32Adaptive(Span<byte> d, int v, bool aggressive)
    {
        if (v >= 0)
        {
            if ((!aggressive && v < 1024))
                return WritePacked4(d, Lut1024, v);
            if (aggressive && v < 10000)
                return WritePacked4(d, Lut10000, v);

            Utf8Formatter.TryFormat(v, d, out int n);
            return n;
        }

        uint mag = 0u - (uint)v;
        if ((!aggressive && mag < 1024u) || (aggressive && mag < 10000u))
        {
            d[0] = (byte)'-';
            return 1 + WritePacked4(d[1..], aggressive ? Lut10000 : Lut1024, (int)mag);
        }

        Utf8Formatter.TryFormat(v, d, out int written);
        return written;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt64Compact(Span<byte> d, ulong v)
    {
        if (v < 2048UL) return WritePacked4(d, Lut2048, (int)v);
        return WriteUInt64QuadLog2(d, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt64Aggressive(Span<byte> d, ulong v)
    {
        if (v < 10000UL) return WritePacked4(d, Lut10000, (int)v);
        return WriteUInt64QuadLog2(d, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteInt64Compact(Span<byte> d, long v) => WriteInt64Adaptive(d, v, false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteInt64Aggressive(Span<byte> d, long v) => WriteInt64Adaptive(d, v, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteInt64Adaptive(Span<byte> d, long v, bool aggressive)
    {
        if (v >= 0)
        {
            ulong u = (ulong)v;
            if ((!aggressive && u < 1024UL) || (aggressive && u < 10000UL))
                return WritePacked4(d, aggressive ? Lut10000 : Lut1024, (int)u);
            return WriteUInt64QuadLog2(d, u);
        }

        ulong mag = 0UL - (ulong)v;
        d[0] = (byte)'-';
        if ((!aggressive && mag < 1024UL) || (aggressive && mag < 10000UL))
            return 1 + WritePacked4(d[1..], aggressive ? Lut10000 : Lut1024, (int)mag);

        return 1 + WriteUInt64QuadLog2(d[1..], mag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUInt32PairLinear(Span<byte> dest, uint value)
    {
        if (value < 10u) { dest[0] = (byte)('0' + value); return 1; }

        int digits =
            value < 100u ? 2 :
            value < 1_000u ? 3 :
            value < 10_000u ? 4 :
            value < 100_000u ? 5 :
            value < 1_000_000u ? 6 :
            value < 10_000_000u ? 7 :
            value < 100_000_000u ? 8 :
            value < 1_000_000_000u ? 9 : 10;

        int pos = digits;
        while (value >= 100u)
        {
            uint q = value / 100u;
            uint r = value - q * 100u;
            int pair = (int)r << 1;
            dest[--pos] = Pairs[pair + 1];
            dest[--pos] = Pairs[pair];
            value = q;
        }

        if (value < 10u) dest[--pos] = (byte)('0' + value);
        else
        {
            int pair = (int)value << 1;
            dest[--pos] = Pairs[pair + 1];
            dest[--pos] = Pairs[pair];
        }

        return digits;
    }

    private static readonly ulong[] Pow10U64 =
    {
        1UL,
        10UL,
        100UL,
        1_000UL,
        10_000UL,
        100_000UL,
        1_000_000UL,
        10_000_000UL,
        100_000_000UL,
        1_000_000_000UL,
        10_000_000_000UL,
        100_000_000_000UL,
        1_000_000_000_000UL,
        10_000_000_000_000UL,
        100_000_000_000_000UL,
        1_000_000_000_000_000UL,
        10_000_000_000_000_000UL,
        100_000_000_000_000_000UL,
        1_000_000_000_000_000_000UL,
        10_000_000_000_000_000_000UL
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigitsLog2(ulong value)
    {
        if (value == 0) return 1;
        int log2 = BitOperations.Log2(value);
        int estimate = ((log2 * 1233) >> 12) + 1;
        if (estimate < 20 && value >= Pow10U64[estimate])
            estimate++;
        return estimate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUInt64QuadLog2(Span<byte> dest, ulong value)
    {
        if (value < 10UL)
        {
            dest[0] = (byte)('0' + value);
            return 1;
        }

        int digits = CountDigitsLog2(value);
        int pos = digits;

        while (value >= 10_000UL)
        {
            ulong q = value / 10_000UL;
            uint r = (uint)(value - q * 10_000UL);
            pos -= 4;
            Write4Digits(dest[pos..], r);
            value = q;
        }

        uint head = (uint)value;

        if (head >= 1000u)
        {
            Write4Digits(dest, head);
        }
        else if (head >= 100u)
        {
            uint q = head / 100u;
            uint r = head - q * 100u;
            if (q >= 10u)
            {
                int qp = (int)q << 1;
                dest[0] = Pairs[qp];
                dest[1] = Pairs[qp + 1];
                int rp = (int)r << 1;
                dest[2] = Pairs[rp];
                dest[3] = Pairs[rp + 1];
            }
            else
            {
                dest[0] = (byte)('0' + q);
                int rp = (int)r << 1;
                dest[1] = Pairs[rp];
                dest[2] = Pairs[rp + 1];
            }
        }
        else if (head >= 10u)
        {
            int hp = (int)head << 1;
            dest[0] = Pairs[hp];
            dest[1] = Pairs[hp + 1];
        }
        else
        {
            dest[0] = (byte)('0' + head);
        }

        return digits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Write4Digits(Span<byte> dest, uint value)
    {
        uint q = value / 100u;
        uint r = value - q * 100u;
        int hp = (int)q << 1;
        int lp = (int)r << 1;
        dest[0] = Pairs[hp];
        dest[1] = Pairs[hp + 1];
        dest[2] = Pairs[lp];
        dest[3] = Pairs[lp + 1];
    }

    // -----------------------------------------------------------------
    // Floating candidates
    // -----------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloatGuardedDirect(Span<byte> dest, float value, int halfWidth)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        uint absBits = bits & 0x7FFF_FFFFu;

        if (absBits == 0)
            return PrimitiveFormatter.FloatDirectCurrent(dest, value);

        uint exp = (absBits >> 23) & 0xFFu;
        uint low = (uint)(127 - halfWidth);

        if ((uint)(exp - low) <= (uint)(halfWidth * 2))
            return PrimitiveFormatter.FloatDirectCurrent(dest, value);

        return PrimitiveFormatter.FloatDotNet(dest, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DoubleGuardedDirect(Span<byte> dest, double value, int halfWidth)
    {
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;

        if (absBits == 0)
            return PrimitiveFormatter.DoubleDirectCurrent(dest, value);

        ulong exp = (absBits >> 52) & 0x7FFUL;
        ulong low = (ulong)(1023 - halfWidth);

        if ((ulong)(exp - low) <= (ulong)(halfWidth * 2))
            return PrimitiveFormatter.DoubleDirectCurrent(dest, value);

        return PrimitiveFormatter.DoubleDotNet(dest, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloatWideIntegralGuard64Direct(Span<byte> dest, float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        uint absBits = bits & 0x7FFF_FFFFu;

        if ((absBits & 0x7F80_0000u) == 0x7F80_0000u)
            return PrimitiveFormatter.FloatDotNet(dest, value);

        if (absBits != 0 && IsIntegralFloat(absBits))
        {
            float abs = BitConverter.UInt32BitsToSingle(absBits);
            double d = abs;

            if (d < 18_446_744_073_709_551_616.0)
            {
                int p = 0;
                if ((bits & 0x8000_0000u) != 0)
                    dest[p++] = (byte)'-';

                return p + WriteUInt64QuadLog2(dest[p..], (ulong)d);
            }
        }

        return FloatGuardedDirect(dest, value, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntegralFloat(uint absBits)
    {
        int exponent = (int)((absBits >> 23) & 0xFFu) - 127;
        if (exponent < 0) return false;
        if (exponent >= 23) return true;

        uint mask = (1u << (23 - exponent)) - 1u;
        return (absBits & mask) == 0;
    }

    public static float[] CreateTrueAdversarialFloats(int count)
    {
        float[] fixedValues =
        {
            float.Epsilon, -float.Epsilon,
            float.MaxValue, float.MinValue,
            1e-45f, -1e-45f,
            1e-38f, -1e-38f,
            1e-37f, -1e-37f,
            1e-20f, -1e-20f,
            1e-10f, -1e-10f,
            1e10f, -1e10f,
            1e20f, -1e20f,
            1e37f, -1e37f,
            1.17549435E-38f, -1.17549435E-38f,
            3.4028235E+38f, -3.4028235E+38f,
            16_777_215f, 16_777_216f, 16_777_218f,
            0.99999994f, 1.0000001f,
            1677721.4f, MathF.PI, -MathF.PI,
            BitConverter.UInt32BitsToSingle(0x007F_FFFFu),
            BitConverter.UInt32BitsToSingle(0x0080_0000u),
            BitConverter.UInt32BitsToSingle(0x3F7F_FFFFu),
            BitConverter.UInt32BitsToSingle(0x3F80_0001u)
        };

        var result = new float[count];
        for (int i = 0; i < result.Length; i++)
            result[i] = fixedValues[i % fixedValues.Length];
        return result;
    }

    public static double[] CreateTrueAdversarialDoubles(int count)
    {
        double[] fixedValues =
        {
            double.Epsilon, -double.Epsilon,
            double.MaxValue, double.MinValue,
            1e-320, -1e-320,
            1e-308, -1e-308,
            1e-300, -1e-300,
            1e-200, -1e-200,
            1e-100, -1e-100,
            1e100, -1e100,
            1e200, -1e200,
            1e300, -1e300,
            2.2250738585072014E-308, -2.2250738585072014E-308,
            1.7976931348623157E+308, -1.7976931348623157E+308,
            9_007_199_254_740_991d,
            9_007_199_254_740_992d,
            9_007_199_254_740_994d,
            0.9999999999999999d,
            1.0000000000000002d,
            Math.PI, -Math.PI,
            BitConverter.UInt64BitsToDouble(0x000F_FFFF_FFFF_FFFFUL),
            BitConverter.UInt64BitsToDouble(0x0010_0000_0000_0000UL),
            BitConverter.UInt64BitsToDouble(0x3FEF_FFFF_FFFF_FFFFUL),
            BitConverter.UInt64BitsToDouble(0x3FF0_0000_0000_0001UL)
        };

        var result = new double[count];
        for (int i = 0; i < result.Length; i++)
            result[i] = fixedValues[i % fixedValues.Length];
        return result;
    }

    // -----------------------------------------------------------------
    // Decimal: compose the best 64-bit fast path with full custom96
    // -----------------------------------------------------------------

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecimalCombined64Then96(Span<byte> dest, decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);

        uint lo = (uint)bits[0];
        uint mid = (uint)bits[1];
        uint hi = (uint)bits[2];
        uint flags = (uint)bits[3];

        int scale = (int)((flags >> 16) & 0x7Fu);

        if (hi == 0 && scale <= 4)
        {
            ulong coefficient = ((ulong)mid << 32) | lo;
            bool negative = (flags & 0x8000_0000u) != 0;
            return WriteDecimalUInt64Fixed(dest, coefficient, scale, negative);
        }

        Span<byte> digits = stackalloc byte[29];
        int digitCount = WriteUInt96Digits(digits, hi, mid, lo);
        bool negative96 = (flags & 0x8000_0000u) != 0;
        return WriteDecimalDigits(dest, digits[..digitCount], scale, negative96);
    }

    [SkipLocalsInit]
    private static int WriteDecimalUInt64Fixed(Span<byte> dest, ulong coefficient, int scale, bool negative)
    {
        Span<byte> digits = stackalloc byte[20];
        int count = WriteUInt64QuadLog2(digits, coefficient);

        int pos = 0;
        if (negative)
            dest[pos++] = (byte)'-';

        if (scale == 0)
        {
            digits[..count].CopyTo(dest[pos..]);
            return pos + count;
        }

        if (count > scale)
        {
            int whole = count - scale;
            digits[..whole].CopyTo(dest[pos..]);
            pos += whole;
            dest[pos++] = (byte)'.';
            digits.Slice(whole, scale).CopyTo(dest[pos..]);
            return pos + scale;
        }

        dest[pos++] = (byte)'0';
        dest[pos++] = (byte)'.';
        int zeros = scale - count;
        dest.Slice(pos, zeros).Fill((byte)'0');
        pos += zeros;
        digits[..count].CopyTo(dest[pos..]);
        return pos + count;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteDecimalDigits(Span<byte> dest, ReadOnlySpan<byte> digits, int scale, bool negative)
    {
        int pos = 0;
        if (negative) dest[pos++] = (byte)'-';

        if (scale == 0)
        {
            digits.CopyTo(dest[pos..]);
            return pos + digits.Length;
        }

        if (digits.Length > scale)
        {
            int whole = digits.Length - scale;
            digits[..whole].CopyTo(dest[pos..]);
            pos += whole;
            dest[pos++] = (byte)'.';
            digits[whole..].CopyTo(dest[pos..]);
            return pos + scale;
        }

        dest[pos++] = (byte)'0';
        dest[pos++] = (byte)'.';
        int zeros = scale - digits.Length;
        dest.Slice(pos, zeros).Fill((byte)'0');
        pos += zeros;
        digits.CopyTo(dest[pos..]);
        return pos + digits.Length;
    }

    [SkipLocalsInit]
    private static int WriteUInt96Digits(Span<byte> dest, uint hi, uint mid, uint lo)
    {
        if ((hi | mid) == 0)
            return WriteUInt64QuadLog2(dest, lo);

        if (hi == 0)
            return WriteUInt64QuadLog2(dest, ((ulong)mid << 32) | lo);

        Span<uint> chunks = stackalloc uint[4];
        int count = 0;

        while ((hi | mid | lo) != 0)
            chunks[count++] = Div96By1Billion(ref hi, ref mid, ref lo);

        int pos = WriteUInt64QuadLog2(dest, chunks[count - 1]);

        for (int i = count - 2; i >= 0; i--)
        {
            WriteNineDigits(dest[pos..], chunks[i]);
            pos += 9;
        }

        return pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Div96By1Billion(ref uint hi, ref uint mid, ref uint lo)
    {
        const ulong Divisor = 1_000_000_000UL;

        ulong n = hi;
        uint qHi = (uint)(n / Divisor);
        ulong rem = n - (ulong)qHi * Divisor;

        n = (rem << 32) | mid;
        uint qMid = (uint)(n / Divisor);
        rem = n - (ulong)qMid * Divisor;

        n = (rem << 32) | lo;
        uint qLo = (uint)(n / Divisor);
        rem = n - (ulong)qLo * Divisor;

        hi = qHi;
        mid = qMid;
        lo = qLo;

        return (uint)rem;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNineDigits(Span<byte> dest, uint value)
    {
        for (int pos = 9; pos > 1; pos -= 2)
        {
            uint q = value / 100u;
            uint r = value - q * 100u;
            int pair = (int)r << 1;
            dest[pos - 2] = Pairs[pair];
            dest[pos - 1] = Pairs[pair + 1];
            value = q;
        }

        dest[0] = (byte)('0' + value);
    }

    // -----------------------------------------------------------------
    // Validation
    // -----------------------------------------------------------------

    public static void Validate()
    {
        Span<byte> expected = stackalloc byte[128];
        Span<byte> actual = stackalloc byte[128];

        var rng = new JsonBenchSplitMix64(0xEE91_17B2_7A01_51CDUL);

        for (int i = 0; i < 300_000; i++)
        {
            ulong u = rng.Next();
            ValidateU64(u, expected, actual);

            long s = unchecked((long)rng.Next());
            ValidateI64(s, expected, actual);

            uint u32 = (uint)rng.Next();
            ValidateU32(u32, expected, actual);

            int i32 = unchecked((int)rng.Next());
            ValidateI32(i32, expected, actual);
        }

        for (int i = 0; i <= ushort.MaxValue; i += 17)
        {
            ushort u = (ushort)i;
            Utf8Formatter.TryFormat(u, expected, out int en);
            int n1 = WriteUInt16Compact(actual, u);
            if (n1 != en || !actual[..n1].SequenceEqual(expected[..en])) throw new InvalidOperationException($"ushort compact mismatch: {u}");
            int n2 = WriteUInt16Aggressive(actual, u);
            if (n2 != en || !actual[..n2].SequenceEqual(expected[..en])) throw new InvalidOperationException($"ushort aggressive mismatch: {u}");
        }

        for (int i = sbyte.MinValue; i <= sbyte.MaxValue; i++)
        {
            sbyte v = (sbyte)i;
            Utf8Formatter.TryFormat(v, expected, out int en);
            int n = WriteSByteCompact(actual, v);
            if (n != en || !actual[..n].SequenceEqual(expected[..en])) throw new InvalidOperationException($"sbyte mismatch: {v}");
        }

        ValidateFloating();
        ValidateDecimal();

        Console.WriteLine("LuminPack JSON final-decision candidate validation: OK");
    }

    private static void ValidateU64(ulong v, Span<byte> e, Span<byte> a)
    {
        Utf8Formatter.TryFormat(v, e, out int en);

        int n = WriteUInt64Compact(a, v);
        if (n != en || !a[..n].SequenceEqual(e[..en])) throw new InvalidOperationException($"u64 compact mismatch: {v}");

        n = WriteUInt64Aggressive(a, v);
        if (n != en || !a[..n].SequenceEqual(e[..en])) throw new InvalidOperationException($"u64 aggressive mismatch: {v}");
    }

    private static void ValidateI64(long v, Span<byte> e, Span<byte> a)
    {
        Utf8Formatter.TryFormat(v, e, out int en);

        int n = WriteInt64Compact(a, v);
        if (n != en || !a[..n].SequenceEqual(e[..en])) throw new InvalidOperationException($"i64 compact mismatch: {v}");

        n = WriteInt64Aggressive(a, v);
        if (n != en || !a[..n].SequenceEqual(e[..en])) throw new InvalidOperationException($"i64 aggressive mismatch: {v}");
    }

    private static void ValidateU32(uint v, Span<byte> e, Span<byte> a)
    {
        Utf8Formatter.TryFormat(v, e, out int en);

        int n = WriteUInt32Compact(a, v);
        if (n != en || !a[..n].SequenceEqual(e[..en])) throw new InvalidOperationException($"u32 compact mismatch: {v}");

        n = WriteUInt32Aggressive(a, v);
        if (n != en || !a[..n].SequenceEqual(e[..en])) throw new InvalidOperationException($"u32 aggressive mismatch: {v}");
    }

    private static void ValidateI32(int v, Span<byte> e, Span<byte> a)
    {
        Utf8Formatter.TryFormat(v, e, out int en);

        int n = WriteInt32Compact(a, v);
        if (n != en || !a[..n].SequenceEqual(e[..en])) throw new InvalidOperationException($"i32 compact mismatch: {v}");

        n = WriteInt32Aggressive(a, v);
        if (n != en || !a[..n].SequenceEqual(e[..en])) throw new InvalidOperationException($"i32 aggressive mismatch: {v}");
    }

    private static void ValidateFloating()
    {
        Span<byte> b = stackalloc byte[128];
        var rng = new JsonBenchSplitMix64(0x9917_AA12_DD08_10C3UL);

        int fcount = 0;
        while (fcount < 250_000)
        {
            uint bits = (uint)rng.Next();
            if ((bits & 0x7F80_0000u) == 0x7F80_0000u) continue;

            float v = BitConverter.UInt32BitsToSingle(bits);
            ValidateFloat(v, b);
            fcount++;
        }

        foreach (float v in CreateTrueAdversarialFloats(4096))
            ValidateFloat(v, b);

        int dcount = 0;
        while (dcount < 250_000)
        {
            ulong bits = rng.Next();
            if ((bits & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL) continue;

            double v = BitConverter.UInt64BitsToDouble(bits);
            ValidateDouble(v, b);
            dcount++;
        }

        foreach (double v in CreateTrueAdversarialDoubles(4096))
            ValidateDouble(v, b);
    }

    private static void ValidateFloat(float v, Span<byte> b)
    {
        uint expectedBits = BitConverter.SingleToUInt32Bits(v);

        int n = FloatGuardedDirect(b, v, 32);
        ValidateFloatRoundTrip(b[..n], expectedBits, "Guard32Direct");

        n = FloatGuardedDirect(b, v, 64);
        ValidateFloatRoundTrip(b[..n], expectedBits, "Guard64Direct");

        n = FloatWideIntegralGuard64Direct(b, v);
        ValidateFloatRoundTrip(b[..n], expectedBits, "WideIntegralGuard64Direct");
    }

    private static void ValidateFloatRoundTrip(ReadOnlySpan<byte> text, uint expectedBits, string name)
    {
        if (!Utf8Parser.TryParse(text, out float parsed, out int consumed) ||
            consumed != text.Length ||
            BitConverter.SingleToUInt32Bits(parsed) != expectedBits)
            throw new InvalidOperationException($"float {name} mismatch 0x{expectedBits:X8}");
    }

    private static void ValidateDouble(double v, Span<byte> b)
    {
        ulong expectedBits = BitConverter.DoubleToUInt64Bits(v);

        int n = DoubleGuardedDirect(b, v, 16);
        ValidateDoubleRoundTrip(b[..n], expectedBits, "Guard16Direct");

        n = DoubleGuardedDirect(b, v, 32);
        ValidateDoubleRoundTrip(b[..n], expectedBits, "Guard32Direct");

        n = DoubleGuardedDirect(b, v, 64);
        ValidateDoubleRoundTrip(b[..n], expectedBits, "Guard64Direct");
    }

    private static void ValidateDoubleRoundTrip(ReadOnlySpan<byte> text, ulong expectedBits, string name)
    {
        if (!Utf8Parser.TryParse(text, out double parsed, out int consumed) ||
            consumed != text.Length ||
            BitConverter.DoubleToUInt64Bits(parsed) != expectedBits)
            throw new InvalidOperationException($"double {name} mismatch 0x{expectedBits:X16}");
    }

    private static void ValidateDecimal()
    {
        Span<byte> expected = stackalloc byte[128];
        Span<byte> actual = stackalloc byte[128];
        var rng = new JsonBenchSplitMix64(0x4D31_A77A_6110_0039UL);

        decimal[] corners =
        {
            0m, 1m, -1m, 0.1m, 1.2300m,
            decimal.MaxValue, decimal.MinValue,
            0.0000000000000000000000000001m,
            7922816251426433759354395.0335m
        };

        foreach (decimal v in corners)
            ValidateDecimalOne(v, expected, actual);

        for (int i = 0; i < 150_000; i++)
        {
            decimal v = new(
                unchecked((int)rng.Next()),
                unchecked((int)rng.Next()),
                unchecked((int)rng.Next()),
                (rng.Next() & 1) != 0,
                (byte)(rng.Next() % 29));

            ValidateDecimalOne(v, expected, actual);
        }
    }

    private static void ValidateDecimalOne(decimal v, Span<byte> expected, Span<byte> actual)
    {
        Utf8Formatter.TryFormat(v, expected, out int en);
        int n = DecimalCombined64Then96(actual, v);

        if (n != en || !actual[..n].SequenceEqual(expected[..en]))
            throw new InvalidOperationException($"decimal textual mismatch: {v}");

        if (!Utf8Parser.TryParse(actual[..n], out decimal parsed, out int consumed) ||
            consumed != n || parsed != v)
            throw new InvalidOperationException($"decimal roundtrip mismatch: {v}");
    }
}

public readonly struct IntegerCase
{
    public readonly IntegerType Type;
    public readonly IntegerDataset Data;

    public IntegerCase(IntegerType type, IntegerDataset data) { Type = type; Data = data; }
    public override string ToString() => $"{Type}_{Data}";
}

public enum IntegerType : byte { Byte, SByte, UInt16, Int16, UInt32, Int32, UInt64, Int64 }
public enum IntegerDataset : byte { Small, Mixed, Boundary }
public enum FloatingDataset : byte { GameLike, RandomFinite, Adversarial }
public enum DecimalDataset : byte { GameLike, Random, Boundary }
public enum TokenCase : byte { True, False, Null, MixedBool }

internal enum IntegerAlgorithm : byte { DigitPairLinear, DigitPairBinary, DigitQuadBinary, Lut1KThenQuad, Lut10KThenQuad }
internal enum FloatAlgorithm : byte { DotNet, CurrentLumin, DirectCurrent, IntegralOnlyQuad, HybridPair, HybridQuad, Guard16, Guard32, Guard64, CommonAdaptive32 }
internal enum DoubleAlgorithm : byte { DotNet, CurrentLumin, DirectCurrent, IntegralOnlyQuad, HybridPair, HybridQuad, Guard16, Guard32, Guard64, CommonAdaptive32 }
internal enum DecimalAlgorithm : byte { DotNet, Fast64Scale4, Custom96Quad }
internal enum TokenAlgorithm : byte { CurrentScalar, PackedSafe, Packed8Overstore }

internal static class PrimitiveFormatter
{
    private static readonly StandardFormat FloatG = new('G');
    private static readonly StandardFormat DoubleG = new('G');

    private static readonly double[] FloatPow10D = { 1.0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9 };
    private static readonly long[] FloatPow10L = { 1L, 10L, 100L, 1_000L, 10_000L, 100_000L, 1_000_000L, 10_000_000L, 100_000_000L, 1_000_000_000L };
    private static readonly double[] DoublePow10D = { 1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17 };
    private static readonly long[] DoublePow10L = { 1L, 10L, 100L, 1_000L, 10_000L, 100_000L, 1_000_000L, 10_000_000L, 100_000_000L, 1_000_000_000L, 10_000_000_000L, 100_000_000_000L, 1_000_000_000_000L, 10_000_000_000_000L, 100_000_000_000_000L, 1_000_000_000_000_000L, 10_000_000_000_000_000L, 100_000_000_000_000_000L };

    private static ReadOnlySpan<byte> DigitPairs =>
        "00010203040506070809101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899"u8;

    private static readonly SmallLut Small1K = BuildSmallPacked(1_000);
    private static readonly SmallLut Small10K = BuildSmallPacked(10_000);

    // ----------------------------- Integer routing -----------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnsigned(Span<byte> dest, ulong value, IntegerAlgorithm a) => a switch
    {
        IntegerAlgorithm.DigitPairLinear => WriteUInt64DigitPairLinear(dest, value),
        IntegerAlgorithm.DigitPairBinary => WriteUInt64DigitPairBinary(dest, value),
        IntegerAlgorithm.DigitQuadBinary => WriteUInt64DigitQuadBinary(dest, value),
        IntegerAlgorithm.Lut1KThenQuad => WriteUInt64Lut1K(dest, value),
        _ => WriteUInt64Lut10K(dest, value)
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSigned(Span<byte> dest, long value, IntegerAlgorithm a)
    {
        if (value >= 0)
            return WriteUnsigned(dest, (ulong)value, a);

        dest[0] = (byte)'-';
        ulong magnitude = (ulong)(-(value + 1)) + 1UL; // long.MinValue safe
        return 1 + WriteUnsigned(dest[1..], magnitude, a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnsignedPairLinear(Span<byte> dest, ulong value) => WriteUInt64DigitPairLinear(dest, value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnsignedPairBinary(Span<byte> dest, ulong value) => WriteUInt64DigitPairBinary(dest, value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnsignedQuadBinary(Span<byte> dest, ulong value) => WriteUInt64DigitQuadBinary(dest, value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnsignedLut1K(Span<byte> dest, ulong value) => WriteUInt64Lut1K(dest, value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnsignedLut10K(Span<byte> dest, ulong value) => WriteUInt64Lut10K(dest, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSignedPairLinear(Span<byte> dest, long value)
    {
        if (value >= 0) return WriteUInt64DigitPairLinear(dest, (ulong)value);
        dest[0] = (byte)'-';
        ulong magnitude = (ulong)(-(value + 1)) + 1UL;
        return 1 + WriteUInt64DigitPairLinear(dest[1..], magnitude);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSignedPairBinary(Span<byte> dest, long value)
    {
        if (value >= 0) return WriteUInt64DigitPairBinary(dest, (ulong)value);
        dest[0] = (byte)'-';
        ulong magnitude = (ulong)(-(value + 1)) + 1UL;
        return 1 + WriteUInt64DigitPairBinary(dest[1..], magnitude);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSignedQuadBinary(Span<byte> dest, long value)
    {
        if (value >= 0) return WriteUInt64DigitQuadBinary(dest, (ulong)value);
        dest[0] = (byte)'-';
        ulong magnitude = (ulong)(-(value + 1)) + 1UL;
        return 1 + WriteUInt64DigitQuadBinary(dest[1..], magnitude);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSignedLut1K(Span<byte> dest, long value)
    {
        if (value >= 0) return WriteUInt64Lut1K(dest, (ulong)value);
        dest[0] = (byte)'-';
        ulong magnitude = (ulong)(-(value + 1)) + 1UL;
        return 1 + WriteUInt64Lut1K(dest[1..], magnitude);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSignedLut10K(Span<byte> dest, long value)
    {
        if (value >= 0) return WriteUInt64Lut10K(dest, (ulong)value);
        dest[0] = (byte)'-';
        ulong magnitude = (ulong)(-(value + 1)) + 1UL;
        return 1 + WriteUInt64Lut10K(dest[1..], magnitude);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUInt64DigitPairLinear(Span<byte> dest, ulong value)
    {
        if (value < 10) { dest[0] = (byte)('0' + value); return 1; }
        int digits = CountDigitsLinear(value);
        return WritePairsBackwards(dest, value, digits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUInt64DigitPairBinary(Span<byte> dest, ulong value)
    {
        if (value < 10) { dest[0] = (byte)('0' + value); return 1; }
        int digits = CountDigitsBinary(value);
        return WritePairsBackwards(dest, value, digits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WritePairsBackwards(Span<byte> dest, ulong value, int digits)
    {
        int pos = digits;
        ReadOnlySpan<byte> pairs = DigitPairs;
        while (value >= 100)
        {
            ulong q = value / 100;
            uint r = (uint)(value - q * 100);
            int p = (int)r << 1;
            dest[--pos] = pairs[p + 1];
            dest[--pos] = pairs[p];
            value = q;
        }

        if (value < 10)
            dest[0] = (byte)('0' + value);
        else
        {
            int p = (int)value << 1;
            dest[0] = pairs[p];
            dest[1] = pairs[p + 1];
        }
        return digits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUInt64DigitQuadBinary(Span<byte> dest, ulong value)
    {
        if (value < 10) { dest[0] = (byte)('0' + value); return 1; }

        int digits = CountDigitsBinary(value);
        int pos = digits;
        ReadOnlySpan<byte> pairs = DigitPairs;

        while (value >= 10_000)
        {
            ulong q = value / 10_000;
            uint r = (uint)(value - q * 10_000);
            uint hi = r / 100;
            uint lo = r - hi * 100;
            int hp = (int)hi << 1;
            int lp = (int)lo << 1;
            pos -= 4;
            dest[pos] = pairs[hp];
            dest[pos + 1] = pairs[hp + 1];
            dest[pos + 2] = pairs[lp];
            dest[pos + 3] = pairs[lp + 1];
            value = q;
        }

        WriteLeadingUpTo4(dest, (uint)value);
        return digits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLeadingUpTo4(Span<byte> dest, uint value)
    {
        ReadOnlySpan<byte> pairs = DigitPairs;
        if (value < 10)
        {
            dest[0] = (byte)('0' + value);
        }
        else if (value < 100)
        {
            int p = (int)value << 1;
            dest[0] = pairs[p]; dest[1] = pairs[p + 1];
        }
        else if (value < 1_000)
        {
            uint q = value / 100;
            uint r = value - q * 100;
            int p = (int)r << 1;
            dest[0] = (byte)('0' + q); dest[1] = pairs[p]; dest[2] = pairs[p + 1];
        }
        else
        {
            uint q = value / 100;
            uint r = value - q * 100;
            int hp = (int)q << 1;
            int lp = (int)r << 1;
            dest[0] = pairs[hp]; dest[1] = pairs[hp + 1]; dest[2] = pairs[lp]; dest[3] = pairs[lp + 1];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUInt64Lut1K(Span<byte> dest, ulong value)
    {
        if (value < 1_000)
        {
            int i = (int)value;
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(dest), Small1K.Packed[i]);
            return Small1K.Lengths[i];
        }
        return WriteUInt64DigitQuadBinary(dest, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUInt64Lut10K(Span<byte> dest, ulong value)
    {
        if (value < 10_000)
        {
            int i = (int)value;
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(dest), Small10K.Packed[i]);
            return Small10K.Lengths[i];
        }
        return WriteUInt64DigitQuadBinary(dest, value);
    }

    private static SmallLut BuildSmallPacked(int count)
    {
        var packed = new ulong[count];
        var lengths = new byte[count];
        Span<byte> tmp = stackalloc byte[32];
        for (int i = 0; i < count; i++)
        {
            int n = WriteUInt64DigitQuadBinary(tmp, (uint)i);
            ulong p = 0;
            for (int j = 0; j < n; j++) p |= (ulong)tmp[j] << (j * 8);
            packed[i] = p;
            lengths[i] = (byte)n;
        }
        return new SmallLut(packed, lengths);
    }

    private readonly struct SmallLut
    {
        public readonly ulong[] Packed;
        public readonly byte[] Lengths;
        public SmallLut(ulong[] packed, byte[] lengths) { Packed = packed; Lengths = lengths; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigitsLinear(ulong value)
    {
        if (value < 10UL) return 1;
        if (value < 100UL) return 2;
        if (value < 1_000UL) return 3;
        if (value < 10_000UL) return 4;
        if (value < 100_000UL) return 5;
        if (value < 1_000_000UL) return 6;
        if (value < 10_000_000UL) return 7;
        if (value < 100_000_000UL) return 8;
        if (value < 1_000_000_000UL) return 9;
        if (value < 10_000_000_000UL) return 10;
        if (value < 100_000_000_000UL) return 11;
        if (value < 1_000_000_000_000UL) return 12;
        if (value < 10_000_000_000_000UL) return 13;
        if (value < 100_000_000_000_000UL) return 14;
        if (value < 1_000_000_000_000_000UL) return 15;
        if (value < 10_000_000_000_000_000UL) return 16;
        if (value < 100_000_000_000_000_000UL) return 17;
        if (value < 1_000_000_000_000_000_000UL) return 18;
        if (value < 10_000_000_000_000_000_000UL) return 19;
        return 20;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigitsBinary(ulong value)
    {
        if (value < 10_000_000_000UL)
        {
            if (value < 100_000UL)
            {
                if (value < 1_000UL)
                {
                    if (value < 100UL) return value < 10UL ? 1 : 2;
                    return 3;
                }
                return value < 10_000UL ? 4 : 5;
            }

            if (value < 10_000_000UL)
                return value < 1_000_000UL ? 6 : 7;
            return value < 1_000_000_000UL ? (value < 100_000_000UL ? 8 : 9) : 10;
        }

        if (value < 1_000_000_000_000_000UL)
        {
            if (value < 1_000_000_000_000UL)
                return value < 100_000_000_000UL ? 11 : 12;
            return value < 100_000_000_000_000UL ? (value < 10_000_000_000_000UL ? 13 : 14) : 15;
        }

        if (value < 100_000_000_000_000_000UL)
            return value < 10_000_000_000_000_000UL ? 16 : 17;
        if (value < 10_000_000_000_000_000_000UL)
            return value < 1_000_000_000_000_000_000UL ? 18 : 19;
        return 20;
    }

    // ----------------------------- Float / Double -----------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatDotNet(Span<byte> dest, float value) => DotNetFloat(value, dest);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatCurrentLumin(Span<byte> dest, float value) => CurrentLuminFloat(value, dest);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatDirectCurrent(Span<byte> dest, float value) => DirectCurrentFloat(value, dest);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatIntegralOnlyQuad(Span<byte> dest, float value) => IntegralOnlyFloat(value, dest);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatHybridPair(Span<byte> dest, float value) => HybridFloat(value, dest, false);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatHybridQuad(Span<byte> dest, float value) => HybridFloat(value, dest, true);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatGuard16(Span<byte> dest, float value) => GuardedFloat(value, dest, 16);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatGuard32(Span<byte> dest, float value) => GuardedFloat(value, dest, 32);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatGuard64(Span<byte> dest, float value) => GuardedFloat(value, dest, 64);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int FloatCommonAdaptive32(Span<byte> dest, float value) => CommonAdaptiveFloat(value, dest);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleDotNet(Span<byte> dest, double value) => DotNetDouble(value, dest);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleCurrentLumin(Span<byte> dest, double value) => CurrentLuminDouble(value, dest);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleDirectCurrent(Span<byte> dest, double value) => DirectCurrentDouble(value, dest);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleIntegralOnlyQuad(Span<byte> dest, double value) => IntegralOnlyDouble(value, dest);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleHybridPair(Span<byte> dest, double value) => HybridDouble(value, dest, false);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleHybridQuad(Span<byte> dest, double value) => HybridDouble(value, dest, true);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleGuard16(Span<byte> dest, double value) => GuardedDouble(value, dest, 16);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleGuard32(Span<byte> dest, double value) => GuardedDouble(value, dest, 32);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleGuard64(Span<byte> dest, double value) => GuardedDouble(value, dest, 64);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int DoubleCommonAdaptive32(Span<byte> dest, double value) => CommonAdaptiveDouble(value, dest);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteFloat(Span<byte> dest, float value, FloatAlgorithm a) => a switch
    {
        FloatAlgorithm.DotNet => DotNetFloat(value, dest),
        FloatAlgorithm.CurrentLumin => CurrentLuminFloat(value, dest),
        FloatAlgorithm.DirectCurrent => DirectCurrentFloat(value, dest),
        FloatAlgorithm.IntegralOnlyQuad => IntegralOnlyFloat(value, dest),
        FloatAlgorithm.HybridPair => HybridFloat(value, dest, false),
        FloatAlgorithm.HybridQuad => HybridFloat(value, dest, true),
        FloatAlgorithm.Guard16 => GuardedFloat(value, dest, 16),
        FloatAlgorithm.Guard32 => GuardedFloat(value, dest, 32),
        FloatAlgorithm.Guard64 => GuardedFloat(value, dest, 64),
        _ => CommonAdaptiveFloat(value, dest)
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteDouble(Span<byte> dest, double value, DoubleAlgorithm a) => a switch
    {
        DoubleAlgorithm.DotNet => DotNetDouble(value, dest),
        DoubleAlgorithm.CurrentLumin => CurrentLuminDouble(value, dest),
        DoubleAlgorithm.DirectCurrent => DirectCurrentDouble(value, dest),
        DoubleAlgorithm.IntegralOnlyQuad => IntegralOnlyDouble(value, dest),
        DoubleAlgorithm.HybridPair => HybridDouble(value, dest, false),
        DoubleAlgorithm.HybridQuad => HybridDouble(value, dest, true),
        DoubleAlgorithm.Guard16 => GuardedDouble(value, dest, 16),
        DoubleAlgorithm.Guard32 => GuardedDouble(value, dest, 32),
        DoubleAlgorithm.Guard64 => GuardedDouble(value, dest, 64),
        _ => CommonAdaptiveDouble(value, dest)
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DotNetFloat(float value, Span<byte> dest) { Utf8Formatter.TryFormat(value, dest, out int n, FloatG); return n; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DotNetDouble(double value, Span<byte> dest) { Utf8Formatter.TryFormat(value, dest, out int n, DoubleG); return n; }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CurrentLuminFloat(float value, Span<byte> dest)
    {
        Span<byte> tmp = stackalloc byte[24];
        int n = TryFloatCurrent(value, tmp);
        if (n > 0) { tmp[..n].CopyTo(dest); return n; }
        return DotNetFloat(value, dest);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CurrentLuminDouble(double value, Span<byte> dest)
    {
        Span<byte> tmp = stackalloc byte[32];
        int n = TryDoubleCurrent(value, tmp);
        if (n > 0) { tmp[..n].CopyTo(dest); return n; }
        return DotNetDouble(value, dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DirectCurrentFloat(float value, Span<byte> dest)
    {
        int n = TryFloatCurrent(value, dest);
        return n > 0 ? n : DotNetFloat(value, dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DirectCurrentDouble(double value, Span<byte> dest)
    {
        int n = TryDoubleCurrent(value, dest);
        return n > 0 ? n : DotNetDouble(value, dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IntegralOnlyFloat(float value, Span<byte> dest)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        uint absBits = bits & 0x7FFF_FFFFu;
        if ((absBits & 0x7F80_0000u) == 0x7F80_0000u) return DotNetFloat(value, dest);
        if (absBits == 0) return WriteCommonFloatBits(bits, dest);
        if (!IsIntegralFloat(absBits)) return DotNetFloat(value, dest);
        double d = BitConverter.UInt32BitsToSingle(absBits);
        if (d >= 18_446_744_073_709_551_616.0) return DotNetFloat(value, dest);
        int pos = 0; if ((bits & 0x8000_0000u) != 0) dest[pos++] = (byte)'-';
        return pos + WriteUInt64DigitQuadBinary(dest[pos..], (ulong)d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IntegralOnlyDouble(double value, Span<byte> dest)
    {
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;
        if ((absBits & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL) return DotNetDouble(value, dest);
        if (absBits == 0) return WriteCommonDoubleBits(bits, dest);
        if (!IsIntegralDouble(absBits)) return DotNetDouble(value, dest);
        double d = BitConverter.UInt64BitsToDouble(absBits);
        if (d >= 18_446_744_073_709_551_616.0) return DotNetDouble(value, dest);
        int pos = 0; if ((bits & 0x8000_0000_0000_0000UL) != 0) dest[pos++] = (byte)'-';
        return pos + WriteUInt64DigitQuadBinary(dest[pos..], (ulong)d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HybridFloat(float value, Span<byte> dest, bool quad)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        uint absBits = bits & 0x7FFF_FFFFu;
        if ((absBits & 0x7F80_0000u) == 0x7F80_0000u) return DotNetFloat(value, dest);

        int pos = 0;
        if ((bits & 0x8000_0000u) != 0) dest[pos++] = (byte)'-';
        if (absBits == 0) { dest[pos++] = (byte)'0'; return pos; }

        float abs = BitConverter.UInt32BitsToSingle(absBits);
        double d = abs;
        if (IsIntegralFloat(absBits) && d < 18_446_744_073_709_551_616.0)
        {
            pos += quad ? WriteUInt64DigitQuadBinary(dest[pos..], (ulong)d) : WriteUInt64DigitPairBinary(dest[pos..], (ulong)d);
            return pos;
        }

        int n = TryScaledFloat(abs, d, dest[pos..], 1, 10.0, 10UL, quad); if (n != 0) return pos + n;
        n = TryScaledFloat(abs, d, dest[pos..], 2, 100.0, 100UL, quad); if (n != 0) return pos + n;
        n = TryScaledFloat(abs, d, dest[pos..], 3, 1000.0, 1000UL, quad); if (n != 0) return pos + n;
        return DotNetFloat(value, dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HybridDouble(double value, Span<byte> dest, bool quad)
    {
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;
        if ((absBits & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL) return DotNetDouble(value, dest);

        int pos = 0;
        if ((bits & 0x8000_0000_0000_0000UL) != 0) dest[pos++] = (byte)'-';
        if (absBits == 0) { dest[pos++] = (byte)'0'; return pos; }

        double abs = BitConverter.UInt64BitsToDouble(absBits);
        if (IsIntegralDouble(absBits) && abs < 18_446_744_073_709_551_616.0)
        {
            pos += quad ? WriteUInt64DigitQuadBinary(dest[pos..], (ulong)abs) : WriteUInt64DigitPairBinary(dest[pos..], (ulong)abs);
            return pos;
        }

        int n = TryScaledDouble(abs, dest[pos..], 1, 10.0, 10UL, quad); if (n != 0) return pos + n;
        n = TryScaledDouble(abs, dest[pos..], 2, 100.0, 100UL, quad); if (n != 0) return pos + n;
        n = TryScaledDouble(abs, dest[pos..], 3, 1000.0, 1000UL, quad); if (n != 0) return pos + n;
        n = TryScaledDouble(abs, dest[pos..], 4, 10000.0, 10000UL, quad); if (n != 0) return pos + n;
        return DotNetDouble(value, dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GuardedFloat(float value, Span<byte> dest, int halfWidth)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        uint absBits = bits & 0x7FFF_FFFFu;
        if (absBits == 0) return HybridFloat(value, dest, true);
        uint exp = (absBits >> 23) & 0xFFu;
        uint low = (uint)(127 - halfWidth);
        if ((uint)(exp - low) <= (uint)(halfWidth * 2)) return HybridFloat(value, dest, true);
        return DotNetFloat(value, dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GuardedDouble(double value, Span<byte> dest, int halfWidth)
    {
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;
        if (absBits == 0) return HybridDouble(value, dest, true);
        ulong exp = (absBits >> 52) & 0x7FFUL;
        ulong low = (ulong)(1023 - halfWidth);
        if ((ulong)(exp - low) <= (ulong)(halfWidth * 2)) return HybridDouble(value, dest, true);
        return DotNetDouble(value, dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CommonAdaptiveFloat(float value, Span<byte> dest)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        int common = TryWriteCommonFloatBits(bits, dest);
        if (common != 0) return common;
        uint absBits = bits & 0x7FFF_FFFFu;
        uint exp = (absBits >> 23) & 0xFFu;
        if ((uint)(exp - 95u) <= 64u) return HybridFloat(value, dest, true);
        return DotNetFloat(value, dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CommonAdaptiveDouble(double value, Span<byte> dest)
    {
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        int common = TryWriteCommonDoubleBits(bits, dest);
        if (common != 0) return common;
        ulong absBits = bits & 0x7FFF_FFFF_FFFF_FFFFUL;
        ulong exp = (absBits >> 52) & 0x7FFUL;
        if ((ulong)(exp - 991UL) <= 64UL) return HybridDouble(value, dest, true);
        return DotNetDouble(value, dest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TryWriteCommonFloatBits(uint bits, Span<byte> dest)
    {
        switch (bits)
        {
            case 0x0000_0000u: dest[0] = (byte)'0'; return 1;
            case 0x8000_0000u: dest[0] = (byte)'-'; dest[1] = (byte)'0'; return 2;
            case 0x3F80_0000u: dest[0] = (byte)'1'; return 1;
            case 0xBF80_0000u: dest[0] = (byte)'-'; dest[1] = (byte)'1'; return 2;
            case 0x3F00_0000u: WriteAsciiPacked(dest, 0x0000000000352E30UL, 3); return 3; // 0.5
            case 0x3E80_0000u: WriteAsciiPacked(dest, 0x0000000035322E30UL, 4); return 4; // 0.25
            case 0x3F40_0000u: WriteAsciiPacked(dest, 0x0000000035372E30UL, 4); return 4; // 0.75
            default: return 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TryWriteCommonDoubleBits(ulong bits, Span<byte> dest)
    {
        switch (bits)
        {
            case 0x0000_0000_0000_0000UL: dest[0] = (byte)'0'; return 1;
            case 0x8000_0000_0000_0000UL: dest[0] = (byte)'-'; dest[1] = (byte)'0'; return 2;
            case 0x3FF0_0000_0000_0000UL: dest[0] = (byte)'1'; return 1;
            case 0xBFF0_0000_0000_0000UL: dest[0] = (byte)'-'; dest[1] = (byte)'1'; return 2;
            case 0x3FE0_0000_0000_0000UL: WriteAsciiPacked(dest, 0x0000000000352E30UL, 3); return 3;
            case 0x3FD0_0000_0000_0000UL: WriteAsciiPacked(dest, 0x0000000035322E30UL, 4); return 4;
            case 0x3FE8_0000_0000_0000UL: WriteAsciiPacked(dest, 0x0000000035372E30UL, 4); return 4;
            default: return 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteCommonFloatBits(uint bits, Span<byte> dest)
    {
        int n = TryWriteCommonFloatBits(bits, dest);
        if (n != 0) return n;
        dest[0] = (byte)'0'; return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteCommonDoubleBits(ulong bits, Span<byte> dest)
    {
        int n = TryWriteCommonDoubleBits(bits, dest);
        if (n != 0) return n;
        dest[0] = (byte)'0'; return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TryScaledFloat(float original, double value, Span<byte> dest, int decimals, double scale, ulong scaleInt, bool quad)
    {
        double scaledD = value * scale + 0.5;
        if (scaledD >= 9_223_372_036_854_775_807.0) return 0;
        ulong scaled = (ulong)scaledD;
        if ((float)((double)scaled / scale) != original) return 0;
        return WriteScaledUInt(dest, scaled, decimals, scaleInt, quad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TryScaledDouble(double value, Span<byte> dest, int decimals, double scale, ulong scaleInt, bool quad)
    {
        double scaledD = value * scale + 0.5;
        if (scaledD >= 9_223_372_036_854_775_807.0) return 0;
        ulong scaled = (ulong)scaledD;
        if ((double)scaled / scale != value) return 0;
        return WriteScaledUInt(dest, scaled, decimals, scaleInt, quad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteScaledUInt(Span<byte> dest, ulong scaled, int decimals, ulong scale, bool quad)
    {
        ulong intPart = scaled / scale;
        ulong frac = scaled - intPart * scale;
        int pos = quad ? WriteUInt64DigitQuadBinary(dest, intPart) : WriteUInt64DigitPairBinary(dest, intPart);
        if (frac == 0) return pos;

        dest[pos++] = (byte)'.';
        int start = pos;
        switch (decimals)
        {
            case 1: dest[pos++] = (byte)('0' + frac); break;
            case 2:
            {
                ulong q = frac / 10; dest[pos++] = (byte)('0' + q); dest[pos++] = (byte)('0' + (frac - q * 10)); break;
            }
            case 3:
            {
                ulong h = frac / 100; ulong rem = frac - h * 100; ulong t = rem / 10;
                dest[pos++] = (byte)('0' + h); dest[pos++] = (byte)('0' + t); dest[pos++] = (byte)('0' + (rem - t * 10)); break;
            }
            default:
            {
                uint v = (uint)frac; uint hi = v / 100; uint lo = v - hi * 100; ReadOnlySpan<byte> pairs = DigitPairs;
                int hp = (int)hi << 1; int lp = (int)lo << 1;
                dest[pos++] = pairs[hp]; dest[pos++] = pairs[hp + 1]; dest[pos++] = pairs[lp]; dest[pos++] = pairs[lp + 1]; break;
            }
        }
        while (pos > start && dest[pos - 1] == (byte)'0') pos--;
        return pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntegralFloat(uint absBits)
    {
        int exponent = (int)((absBits >> 23) & 0xFF) - 127;
        if (exponent < 0) return false;
        if (exponent >= 23) return true;
        uint mask = (1u << (23 - exponent)) - 1u;
        return (absBits & mask) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntegralDouble(ulong absBits)
    {
        int exponent = (int)((absBits >> 52) & 0x7FF) - 1023;
        if (exponent < 0) return false;
        if (exponent >= 52) return true;
        ulong mask = (1UL << (52 - exponent)) - 1UL;
        return (absBits & mask) == 0;
    }

    // Current Lumin equivalent used only for A/B.
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TryFloatCurrent(float value, Span<byte> dest)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        if ((bits & 0x7F800000u) == 0x7F800000u) return 0;
        int pos = 0;
        if ((bits & 0x80000000u) != 0) { dest[pos++] = (byte)'-'; value = -value; bits &= 0x7FFFFFFFu; }
        if (bits == 0) { dest[pos++] = (byte)'0'; return pos; }
        double d = value;
        if (d < 1e15 && value == MathF.Floor(value)) { pos += WriteUInt64Current(dest[pos..], (ulong)d); return pos; }
        for (int dec = 1; dec <= 9; dec++)
        {
            double scale = FloatPow10D[dec]; double scaledD = d * scale + 0.5; if (scaledD >= 9.2e18) continue;
            long scaled = (long)scaledD; long scaleL = FloatPow10L[dec]; if ((float)((double)scaled / scale) != value) continue;
            ulong intPart = (ulong)(scaled / scaleL); int fracRaw = (int)(scaled % scaleL); pos += WriteUInt64Current(dest[pos..], intPart);
            if (fracRaw > 0)
            {
                dest[pos++] = (byte)'.'; Span<byte> fracBuf = stackalloc byte[10]; int fr = fracRaw;
                for (int i = dec - 1; i >= 0; i--) { fracBuf[i] = (byte)('0' + fr % 10); fr /= 10; }
                int writeLen = dec; while (writeLen > 0 && fracBuf[writeLen - 1] == (byte)'0') writeLen--;
                fracBuf[..writeLen].CopyTo(dest[pos..]); pos += writeLen;
            }
            return pos;
        }
        return 0;
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TryDoubleCurrent(double value, Span<byte> dest)
    {
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        if ((bits & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL) return 0;
        int pos = 0;
        if ((bits & 0x8000_0000_0000_0000UL) != 0) { dest[pos++] = (byte)'-'; value = -value; bits &= 0x7FFF_FFFF_FFFF_FFFFUL; }
        if (bits == 0) { dest[pos++] = (byte)'0'; return pos; }
        if (value < 1e15 && value == Math.Floor(value)) { pos += WriteUInt64Current(dest[pos..], (ulong)value); return pos; }
        for (int dec = 1; dec <= 17; dec++)
        {
            double scale = DoublePow10D[dec]; double scaledD = value * scale + 0.5; if (scaledD >= 9.2e18) continue;
            long scaled = (long)scaledD; long scaleL = DoublePow10L[dec]; if ((double)scaled / scale != value) continue;
            ulong intPart = (ulong)(scaled / scaleL); long fracRaw = scaled % scaleL; pos += WriteUInt64Current(dest[pos..], intPart);
            if (fracRaw > 0)
            {
                dest[pos++] = (byte)'.'; Span<byte> fracBuf = stackalloc byte[18]; long fr = fracRaw;
                for (int i = dec - 1; i >= 0; i--) { fracBuf[i] = (byte)('0' + fr % 10); fr /= 10; }
                int writeLen = dec; while (writeLen > 0 && fracBuf[writeLen - 1] == (byte)'0') writeLen--;
                fracBuf[..writeLen].CopyTo(dest[pos..]); pos += writeLen;
            }
            return pos;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUInt64Current(Span<byte> dest, ulong value)
    {
        if (value < 10) { dest[0] = (byte)('0' + value); return 1; }
        int digits = 0; ulong tmp = value; while (tmp != 0) { tmp /= 10; digits++; }
        int pos = digits; while (value != 0) { dest[--pos] = (byte)('0' + value % 10); value /= 10; }
        return digits;
    }

    // ----------------------------- Decimal -----------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecimalDotNet(Span<byte> dest, decimal value) { Utf8Formatter.TryFormat(value, dest, out int n); return n; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecimalFast64Scale4(Span<byte> dest, decimal value) => Decimal64Scale4Fast(value, dest);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecimalCustom96Quad(Span<byte> dest, decimal value) => Decimal96Quad(value, dest);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteDecimal(Span<byte> dest, decimal value, DecimalAlgorithm a)
    {
        if (a == DecimalAlgorithm.DotNet)
        {
            Utf8Formatter.TryFormat(value, dest, out int n); return n;
        }
        if (a == DecimalAlgorithm.Fast64Scale4)
            return Decimal64Scale4Fast(value, dest);
        return Decimal96Quad(value, dest);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Decimal64Scale4Fast(decimal value, Span<byte> dest)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        uint lo = (uint)bits[0], mid = (uint)bits[1], hi = (uint)bits[2], flags = (uint)bits[3];
        int scale = (int)((flags >> 16) & 0x7Fu);
        if (hi == 0 && scale <= 4)
        {
            ulong coefficient = ((ulong)mid << 32) | lo;
            bool negative = (flags & 0x8000_0000u) != 0;
            return WriteDecimalUInt64Fixed(dest, coefficient, scale, negative);
        }
        Utf8Formatter.TryFormat(value, dest, out int n); return n;
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Decimal96Quad(decimal value, Span<byte> dest)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        uint lo = (uint)bits[0], mid = (uint)bits[1], hi = (uint)bits[2], flags = (uint)bits[3];
        int scale = (int)((flags >> 16) & 0x7Fu);
        bool negative = (flags & 0x8000_0000u) != 0;

        Span<byte> digits = stackalloc byte[29];
        int digitCount = WriteUInt96Digits(digits, hi, mid, lo);
        return WriteDecimalDigits(dest, digits[..digitCount], scale, negative);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteDecimalUInt64Fixed(Span<byte> dest, ulong coefficient, int scale, bool negative)
    {
        Span<byte> digits = stackalloc byte[20];
        int count = WriteUInt64DigitQuadBinary(digits, coefficient);
        return WriteDecimalDigits(dest, digits[..count], scale, negative);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteDecimalDigits(Span<byte> dest, ReadOnlySpan<byte> digits, int scale, bool negative)
    {
        int pos = 0;
        if (negative) dest[pos++] = (byte)'-';

        if (scale == 0)
        {
            digits.CopyTo(dest[pos..]); return pos + digits.Length;
        }

        if (digits.Length > scale)
        {
            int whole = digits.Length - scale;
            digits[..whole].CopyTo(dest[pos..]); pos += whole;
            dest[pos++] = (byte)'.';
            digits[whole..].CopyTo(dest[pos..]); return pos + scale;
        }

        dest[pos++] = (byte)'0';
        dest[pos++] = (byte)'.';
        int zeros = scale - digits.Length;
        dest.Slice(pos, zeros).Fill((byte)'0'); pos += zeros;
        digits.CopyTo(dest[pos..]); return pos + digits.Length;
    }

    [SkipLocalsInit]
    private static int WriteUInt96Digits(Span<byte> dest, uint hi, uint mid, uint lo)
    {
        if ((hi | mid) == 0)
            return WriteUInt64DigitQuadBinary(dest, lo);

        if (hi == 0)
            return WriteUInt64DigitQuadBinary(dest, ((ulong)mid << 32) | lo);

        Span<uint> chunks = stackalloc uint[4];
        int count = 0;
        while ((hi | mid | lo) != 0)
            chunks[count++] = Div96By1Billion(ref hi, ref mid, ref lo);

        int pos = WriteUInt64DigitQuadBinary(dest, chunks[count - 1]);
        for (int i = count - 2; i >= 0; i--)
        {
            WriteNineDigits(dest[pos..], chunks[i]);
            pos += 9;
        }
        return pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Div96By1Billion(ref uint hi, ref uint mid, ref uint lo)
    {
        const ulong Divisor = 1_000_000_000UL;
        ulong n = hi;
        uint qHi = (uint)(n / Divisor);
        ulong rem = n - (ulong)qHi * Divisor;

        n = (rem << 32) | mid;
        uint qMid = (uint)(n / Divisor);
        rem = n - (ulong)qMid * Divisor;

        n = (rem << 32) | lo;
        uint qLo = (uint)(n / Divisor);
        rem = n - (ulong)qLo * Divisor;

        hi = qHi; mid = qMid; lo = qLo;
        return (uint)rem;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNineDigits(Span<byte> dest, uint value)
    {
        ReadOnlySpan<byte> pairs = DigitPairs;
        for (int pos = 9; pos > 1; pos -= 2)
        {
            uint q = value / 100;
            uint r = value - q * 100;
            int p = (int)r << 1;
            dest[pos - 2] = pairs[p];
            dest[pos - 1] = pairs[p + 1];
            value = q;
        }
        dest[0] = (byte)('0' + value);
    }

    // ----------------------------- Fixed JSON tokens -----------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBoolTokenScalar(Span<byte> dest, ref int pos, bool value) { if (value) WriteTrueScalar(dest, ref pos); else WriteFalseScalar(dest, ref pos); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBoolTokenPackedSafe(Span<byte> dest, ref int pos, bool value) { if (value) WriteTruePackedSafe(dest, ref pos); else WriteFalsePackedSafe(dest, ref pos); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBoolTokenPacked8(Span<byte> dest, ref int pos, bool value) { if (value) WriteTruePacked8(dest, ref pos); else WriteFalsePacked8(dest, ref pos); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteFixedTokenScalar(Span<byte> dest, ref int pos, TokenCase token) { if (token == TokenCase.True) WriteTrueScalar(dest, ref pos); else if (token == TokenCase.False) WriteFalseScalar(dest, ref pos); else WriteNullScalar(dest, ref pos); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteFixedTokenPackedSafe(Span<byte> dest, ref int pos, TokenCase token) { if (token == TokenCase.True) WriteTruePackedSafe(dest, ref pos); else if (token == TokenCase.False) WriteFalsePackedSafe(dest, ref pos); else WriteNullPackedSafe(dest, ref pos); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteFixedTokenPacked8(Span<byte> dest, ref int pos, TokenCase token) { if (token == TokenCase.True) WriteTruePacked8(dest, ref pos); else if (token == TokenCase.False) WriteFalsePacked8(dest, ref pos); else WriteNullPacked8(dest, ref pos); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBoolToken(Span<byte> dest, ref int pos, bool value, TokenAlgorithm a)
    {
        if (a == TokenAlgorithm.CurrentScalar)
        {
            if (value) WriteTrueScalar(dest, ref pos); else WriteFalseScalar(dest, ref pos);
        }
        else if (a == TokenAlgorithm.PackedSafe)
        {
            if (value) WriteTruePackedSafe(dest, ref pos); else WriteFalsePackedSafe(dest, ref pos);
        }
        else
        {
            if (value) WriteTruePacked8(dest, ref pos); else WriteFalsePacked8(dest, ref pos);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteFixedToken(Span<byte> dest, ref int pos, TokenCase token, TokenAlgorithm a)
    {
        if (token == TokenCase.True) { WriteBoolToken(dest, ref pos, true, a); return; }
        if (token == TokenCase.False) { WriteBoolToken(dest, ref pos, false, a); return; }
        if (a == TokenAlgorithm.CurrentScalar) WriteNullScalar(dest, ref pos);
        else if (a == TokenAlgorithm.PackedSafe) WriteNullPackedSafe(dest, ref pos);
        else WriteNullPacked8(dest, ref pos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteByteRawSim(Span<byte> dest, ref int pos, byte value)
    {
        if ((uint)1 > (uint)(dest.Length - pos)) throw new InvalidOperationException();
        dest[pos++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureSim(Span<byte> dest, int pos, int count)
    {
        if ((uint)count > (uint)(dest.Length - pos)) throw new InvalidOperationException();
    }

    private static void WriteTrueScalar(Span<byte> d, ref int p) { WriteByteRawSim(d, ref p, (byte)'t'); WriteByteRawSim(d, ref p, (byte)'r'); WriteByteRawSim(d, ref p, (byte)'u'); WriteByteRawSim(d, ref p, (byte)'e'); }
    private static void WriteFalseScalar(Span<byte> d, ref int p) { WriteByteRawSim(d, ref p, (byte)'f'); WriteByteRawSim(d, ref p, (byte)'a'); WriteByteRawSim(d, ref p, (byte)'l'); WriteByteRawSim(d, ref p, (byte)'s'); WriteByteRawSim(d, ref p, (byte)'e'); }
    private static void WriteNullScalar(Span<byte> d, ref int p) { WriteByteRawSim(d, ref p, (byte)'n'); WriteByteRawSim(d, ref p, (byte)'u'); WriteByteRawSim(d, ref p, (byte)'l'); WriteByteRawSim(d, ref p, (byte)'l'); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTruePackedSafe(Span<byte> d, ref int p) { EnsureSim(d, p, 4); Unsafe.WriteUnaligned(ref d[p], 0x65757274u); p += 4; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNullPackedSafe(Span<byte> d, ref int p) { EnsureSim(d, p, 4); Unsafe.WriteUnaligned(ref d[p], 0x6C6C756Eu); p += 4; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFalsePackedSafe(Span<byte> d, ref int p) { EnsureSim(d, p, 5); Unsafe.WriteUnaligned(ref d[p], 0x736C6166u); d[p + 4] = (byte)'e'; p += 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTruePacked8(Span<byte> d, ref int p) { EnsureSim(d, p, 8); Unsafe.WriteUnaligned(ref d[p], 0x0000000065757274UL); p += 4; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteNullPacked8(Span<byte> d, ref int p) { EnsureSim(d, p, 8); Unsafe.WriteUnaligned(ref d[p], 0x000000006C6C756EUL); p += 4; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFalsePacked8(Span<byte> d, ref int p) { EnsureSim(d, p, 8); Unsafe.WriteUnaligned(ref d[p], 0x00000065736C6166UL); p += 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAsciiPacked(Span<byte> dest, ulong packed, int logicalLength)
    {
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(dest), packed);
    }

    // ----------------------------- Correctness gate -----------------------------

    public static void ValidateAllCandidates()
    {
        ValidateIntegerCandidates();
        ValidateFloatCandidates();
        ValidateDoubleCandidates();
        ValidateDecimalCandidates();
        ValidateTokenCandidates();
        Console.WriteLine("LuminPack JSON primitive candidate correctness validation: OK");
    }

    private static void ValidateIntegerCandidates()
    {
        Span<byte> expected = stackalloc byte[64];
        Span<byte> actual = stackalloc byte[64];
        ulong[] corners = { 0, 1, 9, 10, 11, 99, 100, 101, 999, 1_000, 9_999, 10_000, 99_999, 100_000, 999_999_999, 1_000_000_000, 9_999_999_999UL, 10_000_000_000UL, ulong.MaxValue };
        foreach (ulong v in corners) ValidateUnsigned(v, expected, actual);
        long[] signedCorners = { 0, 1, -1, 9, -9, 10, -10, 99, -99, 100, -100, 999, -999, 1000, -1000, int.MinValue, int.MaxValue, long.MinValue, long.MaxValue };
        foreach (long v in signedCorners) ValidateSigned(v, expected, actual);

        var rng = new JsonBenchSplitMix64(0xABCD_EF01_2345_6789UL);
        for (int i = 0; i < 500_000; i++)
        {
            ulong u = rng.Next();
            ValidateUnsigned(u, expected, actual);
            ValidateSigned(unchecked((long)rng.Next()), expected, actual);
        }
    }

    private static void ValidateUnsigned(ulong value, Span<byte> expected, Span<byte> actual)
    {
        Utf8Formatter.TryFormat(value, expected, out int en);
        foreach (IntegerAlgorithm a in System.Enum.GetValues<IntegerAlgorithm>())
        {
            actual.Clear(); int an = WriteUnsigned(actual, value, a);
            if (an != en || !actual[..an].SequenceEqual(expected[..en])) throw new InvalidOperationException($"Unsigned mismatch {a}: {value}");
        }
    }

    private static void ValidateSigned(long value, Span<byte> expected, Span<byte> actual)
    {
        Utf8Formatter.TryFormat(value, expected, out int en);
        foreach (IntegerAlgorithm a in System.Enum.GetValues<IntegerAlgorithm>())
        {
            actual.Clear(); int an = WriteSigned(actual, value, a);
            if (an != en || !actual[..an].SequenceEqual(expected[..en])) throw new InvalidOperationException($"Signed mismatch {a}: {value}");
        }
    }

    private static void ValidateFloatCandidates()
    {
        Span<byte> buf = stackalloc byte[64];
        float[] corners = { 0f, -0f, 1f, -1f, 0.5f, 0.25f, 0.75f, 0.1f, 0.01f, 0.001f, float.Epsilon, float.MaxValue, float.MinValue, MathF.PI, 16_777_216f, 16_777_218f, 123456.75f, 1e-20f, 1e20f };
        foreach (float v in corners) ValidateFloatValue(v, buf);
        var rng = new JsonBenchSplitMix64(0xF10A_7E55_1234_5678UL);
        int checkedCount = 0;
        while (checkedCount < 500_000)
        {
            uint bits = (uint)rng.Next();
            if ((bits & 0x7F80_0000u) == 0x7F80_0000u) continue;
            ValidateFloatValue(BitConverter.UInt32BitsToSingle(bits), buf); checkedCount++;
        }
    }

    private static void ValidateFloatValue(float value, Span<byte> buf)
    {
        FloatAlgorithm[] candidates = { FloatAlgorithm.DirectCurrent, FloatAlgorithm.IntegralOnlyQuad, FloatAlgorithm.HybridPair, FloatAlgorithm.HybridQuad, FloatAlgorithm.Guard16, FloatAlgorithm.Guard32, FloatAlgorithm.Guard64, FloatAlgorithm.CommonAdaptive32 };
        uint expected = BitConverter.SingleToUInt32Bits(value);
        foreach (FloatAlgorithm a in candidates)
        {
            int n = WriteFloat(buf, value, a);
            if (!Utf8Parser.TryParse(buf[..n], out float parsed, out int consumed) || consumed != n || BitConverter.SingleToUInt32Bits(parsed) != expected)
                throw new InvalidOperationException($"Float round-trip mismatch {a}: 0x{expected:X8}");
        }
    }

    private static void ValidateDoubleCandidates()
    {
        Span<byte> buf = stackalloc byte[64];
        double[] corners = { 0d, -0d, 1d, -1d, 0.5d, 0.25d, 0.75d, 0.1d, 0.01d, 0.001d, 0.0001d, double.Epsilon, double.MaxValue, double.MinValue, Math.PI, 9_007_199_254_740_992d, 123456789.125d, 1e-300, 1e300 };
        foreach (double v in corners) ValidateDoubleValue(v, buf);
        var rng = new JsonBenchSplitMix64(0xD0AB_1E55_9876_4321UL);
        int checkedCount = 0;
        while (checkedCount < 500_000)
        {
            ulong bits = rng.Next();
            if ((bits & 0x7FF0_0000_0000_0000UL) == 0x7FF0_0000_0000_0000UL) continue;
            ValidateDoubleValue(BitConverter.UInt64BitsToDouble(bits), buf); checkedCount++;
        }
    }

    private static void ValidateDoubleValue(double value, Span<byte> buf)
    {
        DoubleAlgorithm[] candidates = { DoubleAlgorithm.DirectCurrent, DoubleAlgorithm.IntegralOnlyQuad, DoubleAlgorithm.HybridPair, DoubleAlgorithm.HybridQuad, DoubleAlgorithm.Guard16, DoubleAlgorithm.Guard32, DoubleAlgorithm.Guard64, DoubleAlgorithm.CommonAdaptive32 };
        ulong expected = BitConverter.DoubleToUInt64Bits(value);
        foreach (DoubleAlgorithm a in candidates)
        {
            int n = WriteDouble(buf, value, a);
            if (!Utf8Parser.TryParse(buf[..n], out double parsed, out int consumed) || consumed != n || BitConverter.DoubleToUInt64Bits(parsed) != expected)
                throw new InvalidOperationException($"Double round-trip mismatch {a}: 0x{expected:X16}");
        }
    }

    private static void ValidateDecimalCandidates()
    {
        Span<byte> buf = stackalloc byte[64];
        decimal[] corners = { 0m, 1m, -1m, 0.1m, 1.2300m, decimal.MaxValue, decimal.MinValue, 0.0000000000000000000000000001m, 7922816251426433759354395.0335m };
        foreach (decimal v in corners) ValidateDecimalValue(v, buf);
        var rng = new JsonBenchSplitMix64(0xDEC1_A1C0_F00D_BAADUL);
        for (int i = 0; i < 250_000; i++)
        {
            decimal v = new(unchecked((int)rng.Next()), unchecked((int)rng.Next()), unchecked((int)rng.Next()), (rng.Next() & 1) != 0, (byte)(rng.Next() % 29));
            ValidateDecimalValue(v, buf);
        }
    }

    private static void ValidateDecimalValue(decimal value, Span<byte> buf)
    {
        Span<byte> expected = stackalloc byte[64];
        Utf8Formatter.TryFormat(value, expected, out int expectedLength);

        foreach (DecimalAlgorithm a in new[] { DecimalAlgorithm.Fast64Scale4, DecimalAlgorithm.Custom96Quad })
        {
            buf.Clear();
            int n = WriteDecimal(buf, value, a);
            if (n != expectedLength || !buf[..n].SequenceEqual(expected[..expectedLength]))
                throw new InvalidOperationException($"Decimal textual mismatch {a}: {value}");

            if (!Utf8Parser.TryParse(buf[..n], out decimal parsed, out int consumed) || consumed != n || parsed != value)
                throw new InvalidOperationException($"Decimal round-trip mismatch {a}: {value}");
        }
    }

    private static void ValidateTokenCandidates()
    {
        Span<byte> buf = stackalloc byte[32];
        foreach (TokenAlgorithm a in System.Enum.GetValues<TokenAlgorithm>())
        {
            foreach (TokenCase token in new[] { TokenCase.True, TokenCase.False, TokenCase.Null })
            {
                buf.Clear(); int pos = 0; WriteFixedToken(buf, ref pos, token, a);
                ReadOnlySpan<byte> expected = token == TokenCase.True ? "true"u8 : token == TokenCase.False ? "false"u8 : "null"u8;
                if (pos != expected.Length || !buf[..pos].SequenceEqual(expected)) throw new InvalidOperationException($"Token mismatch {a}/{token}");
            }
        }
    }
}

internal static class IntegerData
{
    public static byte[] CreateBytes(IntegerDataset d, int count, ref JsonBenchSplitMix64 rng)
    {
        var a = new byte[count];
        for (int i = 0; i < count; i++) a[i] = d switch { IntegerDataset.Small => (byte)(i % 256), IntegerDataset.Mixed => (byte)rng.Next(), _ => BoundaryByte(i) };
        return a;
    }

    public static sbyte[] CreateSBytes(IntegerDataset d, int count, ref JsonBenchSplitMix64 rng)
    {
        var a = new sbyte[count];
        for (int i = 0; i < count; i++) a[i] = d switch { IntegerDataset.Small => (sbyte)((i % 255) - 127), IntegerDataset.Mixed => unchecked((sbyte)rng.Next()), _ => BoundarySByte(i) };
        return a;
    }

    public static ushort[] CreateUShorts(IntegerDataset d, int count, ref JsonBenchSplitMix64 rng)
    {
        var a = new ushort[count];
        for (int i = 0; i < count; i++) a[i] = d switch { IntegerDataset.Small => (ushort)(i % 2_001), IntegerDataset.Mixed => (ushort)rng.Next(), _ => BoundaryUShort(i) };
        return a;
    }

    public static short[] CreateShorts(IntegerDataset d, int count, ref JsonBenchSplitMix64 rng)
    {
        var a = new short[count];
        for (int i = 0; i < count; i++) a[i] = d switch { IntegerDataset.Small => (short)((i % 2_001) - 1_000), IntegerDataset.Mixed => unchecked((short)rng.Next()), _ => BoundaryShort(i) };
        return a;
    }

    public static uint[] CreateUInts(IntegerDataset d, int count, ref JsonBenchSplitMix64 rng)
    {
        var a = new uint[count];
        for (int i = 0; i < count; i++) a[i] = d switch { IntegerDataset.Small => (uint)(i % 2_001), IntegerDataset.Mixed => (uint)rng.Next(), _ => BoundaryUInt(i) };
        return a;
    }

    public static int[] CreateInts(IntegerDataset d, int count, ref JsonBenchSplitMix64 rng)
    {
        var a = new int[count];
        for (int i = 0; i < count; i++) a[i] = d switch { IntegerDataset.Small => (i % 2_001) - 1_000, IntegerDataset.Mixed => unchecked((int)rng.Next()), _ => BoundaryInt(i) };
        return a;
    }

    public static ulong[] CreateULongs(IntegerDataset d, int count, ref JsonBenchSplitMix64 rng)
    {
        var a = new ulong[count];
        for (int i = 0; i < count; i++) a[i] = d switch { IntegerDataset.Small => (ulong)(i % 2_001), IntegerDataset.Mixed => rng.Next(), _ => BoundaryULong(i) };
        return a;
    }

    public static long[] CreateLongs(IntegerDataset d, int count, ref JsonBenchSplitMix64 rng)
    {
        var a = new long[count];
        for (int i = 0; i < count; i++) a[i] = d switch { IntegerDataset.Small => (i % 2_001) - 1_000L, IntegerDataset.Mixed => unchecked((long)rng.Next()), _ => BoundaryLong(i) };
        return a;
    }

    private static readonly byte[] ByteBounds = { 0, 1, 9, 10, 11, 99, 100, 101, 254, 255 };
    private static readonly sbyte[] SByteBounds = { sbyte.MinValue, -100, -99, -10, -9, -1, 0, 1, 9, 10, 99, 100, sbyte.MaxValue };
    private static readonly ushort[] UShortBounds = { 0, 1, 9, 10, 99, 100, 999, 1000, 9999, 10000, 65534, 65535 };
    private static readonly short[] ShortBounds = { short.MinValue, -10000, -9999, -1000, -999, -100, -99, -10, -9, -1, 0, 1, 9, 10, 99, 100, 999, 1000, 9999, 10000, short.MaxValue };
    private static readonly uint[] UIntBounds = { 0, 1, 9, 10, 99, 100, 999, 1000, 9999, 10000, 99999, 100000, 999999999, 1000000000, uint.MaxValue };
    private static readonly int[] IntBounds = { int.MinValue, -1000000000, -999999999, -10000, -9999, -1000, -999, -100, -99, -10, -9, -1, 0, 1, 9, 10, 99, 100, 999, 1000, 9999, 10000, 999999999, 1000000000, int.MaxValue };
    private static readonly ulong[] ULongBounds = { 0, 1, 9, 10, 99, 100, 999, 1000, 9999, 10000, 99999, 100000, 9999999999UL, 10000000000UL, 999999999999999999UL, 1000000000000000000UL, 9999999999999999999UL, 10000000000000000000UL, ulong.MaxValue };
    private static readonly long[] LongBounds = { long.MinValue, -1000000000000000000L, -999999999999999999L, -10000000000L, -9999999999L, -10000, -9999, -1000, -999, -100, -99, -10, -9, -1, 0, 1, 9, 10, 99, 100, 999, 1000, 9999, 10000, 9999999999L, 10000000000L, 999999999999999999L, 1000000000000000000L, long.MaxValue };

    private static byte BoundaryByte(int i) => ByteBounds[i % ByteBounds.Length];
    private static sbyte BoundarySByte(int i) => SByteBounds[i % SByteBounds.Length];
    private static ushort BoundaryUShort(int i) => UShortBounds[i % UShortBounds.Length];
    private static short BoundaryShort(int i) => ShortBounds[i % ShortBounds.Length];
    private static uint BoundaryUInt(int i) => UIntBounds[i % UIntBounds.Length];
    private static int BoundaryInt(int i) => IntBounds[i % IntBounds.Length];
    private static ulong BoundaryULong(int i) => ULongBounds[i % ULongBounds.Length];
    private static long BoundaryLong(int i) => LongBounds[i % LongBounds.Length];
}

internal static class FloatingData
{
    public static float[] CreateFloats(FloatingDataset kind, int count)
    {
        var data = new float[count]; var rng = new JsonBenchSplitMix64(0x1234_5678_9ABC_DEF0UL);
        for (int i = 0; i < count; i++) data[i] = kind switch { FloatingDataset.GameLike => GameFloat(i, ref rng), FloatingDataset.RandomFinite => RandomFiniteFloat(ref rng), _ => AdversarialFloat(i, ref rng) };
        return data;
    }

    public static double[] CreateDoubles(FloatingDataset kind, int count)
    {
        var data = new double[count]; var rng = new JsonBenchSplitMix64(0x0FED_CBA9_8765_4321UL);
        for (int i = 0; i < count; i++) data[i] = kind switch { FloatingDataset.GameLike => GameDouble(i, ref rng), FloatingDataset.RandomFinite => RandomFiniteDouble(ref rng), _ => AdversarialDouble(i, ref rng) };
        return data;
    }

    private static float GameFloat(int i, ref JsonBenchSplitMix64 rng)
    {
        int n = (i % 2001) - 1000;
        return (i & 15) switch { 0 => 0f, 1 => 1f, 2 => -1f, 3 => n, 4 => n / 10f, 5 => n / 100f, 6 => n / 1000f, 7 => 0.5f, 8 => 0.25f, 9 => 0.75f, 10 => 60f, 11 => 1920f, 12 => 0.016666668f, 13 => MathF.PI, 14 => 123456.75f, _ => ((int)(rng.Next() % 200_001) - 100_000) / 100f };
    }

    private static double GameDouble(int i, ref JsonBenchSplitMix64 rng)
    {
        int n = (i % 2001) - 1000;
        return (i & 15) switch { 0 => 0d, 1 => 1d, 2 => -1d, 3 => n, 4 => n / 10d, 5 => n / 100d, 6 => n / 1000d, 7 => 0.5d, 8 => 0.25d, 9 => 0.75d, 10 => 60d, 11 => 1920d, 12 => 1.0 / 60.0, 13 => Math.PI, 14 => 123456789.125d, _ => ((long)(rng.Next() % 20_000_001) - 10_000_000) / 1000d };
    }

    private static float RandomFiniteFloat(ref JsonBenchSplitMix64 rng) { while (true) { uint bits = (uint)rng.Next(); if ((bits & 0x7F80_0000u) != 0x7F80_0000u) return BitConverter.UInt32BitsToSingle(bits); } }
    private static double RandomFiniteDouble(ref JsonBenchSplitMix64 rng) { while (true) { ulong bits = rng.Next(); if ((bits & 0x7FF0_0000_0000_0000UL) != 0x7FF0_0000_0000_0000UL) return BitConverter.UInt64BitsToDouble(bits); } }

    private static float AdversarialFloat(int i, ref JsonBenchSplitMix64 rng)
    {
        ReadOnlySpan<float> fixedValues = [float.Epsilon, -float.Epsilon, float.MaxValue, float.MinValue, 1e-37f, 1e-20f, 1e-10f, 1e10f, 1e20f, 1e37f, 1.17549435E-38f, 3.4028235E+38f, 16_777_215f, 16_777_216f, 16_777_218f, 0.99999994f, 1.0000001f, 1677721.4f];
        return i < fixedValues.Length ? fixedValues[i] : RandomFiniteFloat(ref rng);
    }

    private static double AdversarialDouble(int i, ref JsonBenchSplitMix64 rng)
    {
        ReadOnlySpan<double> fixedValues = [double.Epsilon, -double.Epsilon, double.MaxValue, double.MinValue, 1e-300, 1e-200, 1e-100, 1e100, 1e200, 1e300, 2.2250738585072014E-308, 1.7976931348623157E+308, 9_007_199_254_740_991d, 9_007_199_254_740_992d, 9_007_199_254_740_994d, 0.9999999999999999d, 1.0000000000000002d];
        return i < fixedValues.Length ? fixedValues[i] : RandomFiniteDouble(ref rng);
    }
}

internal static class DecimalData
{
    public static decimal[] Create(DecimalDataset kind, int count)
    {
        var data = new decimal[count]; var rng = new JsonBenchSplitMix64(0x1357_9BDF_2468_ACE0UL);
        for (int i = 0; i < count; i++)
        {
            data[i] = kind switch
            {
                DecimalDataset.GameLike => Game(i, ref rng),
                DecimalDataset.Random => new decimal(unchecked((int)rng.Next()), unchecked((int)rng.Next()), unchecked((int)rng.Next()), (rng.Next() & 1) != 0, (byte)(rng.Next() % 29)),
                _ => Boundary(i)
            };
        }
        return data;
    }

    private static decimal Game(int i, ref JsonBenchSplitMix64 rng)
    {
        int n = (i % 2001) - 1000;
        return (i & 15) switch
        {
            0 => 0m, 1 => 1m, 2 => -1m, 3 => n, 4 => n / 10m, 5 => n / 100m, 6 => n / 1000m,
            7 => 0.5m, 8 => 0.25m, 9 => 0.75m, 10 => 60m, 11 => 1920m, 12 => 16.6667m,
            13 => 123456.75m, 14 => 1.2300m, _ => ((long)(rng.Next() % 20_000_001) - 10_000_000) / 1000m
        };
    }

    private static readonly decimal[] Bounds = { 0m, 1m, -1m, 0.1m, 1.2300m, 0.0000000000000000000000000001m, 79228162514264337593543950335m, -79228162514264337593543950335m, 7922816251426433759354395.0335m, -7922816251426433759354395.0335m };
    private static decimal Boundary(int i) => Bounds[i % Bounds.Length];
}

internal struct JsonBenchSplitMix64
{
    private ulong _state;
    public JsonBenchSplitMix64(ulong seed) => _state = seed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Next()
    {
        ulong z = (_state += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
