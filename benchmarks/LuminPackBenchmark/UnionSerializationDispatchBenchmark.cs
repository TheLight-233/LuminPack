using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 5, iterationCount: 10)]
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 8, printSource: true, printInstructionAddresses: true)]
public class UnionSerializationDispatchBenchmark
{
    private const int Count = 1000;

    private IFoo[] _values = null!;
    private IFoo[] _fooAValues = null!;
    private LuminBufferWriter _buffer = null!;
    private LuminPackWriterOptionalState _state = null!;
    private LuminUnionMap<LuminPackBenchmark_IFooParser.HashEntry> _legacyMap = null!;

    public static void RunJitProbe()
    {
        using var buffer = new LuminBufferWriter(useFirstBuffer: true);
        var state = new LuminPackWriterOptionalState();
        var writer = new LuminPackWriter(buffer, state);
        IFoo value = FooA.Create();

        DispatchGeneratedParser(ref writer, ref value);
        DispatchDirect(ref writer, value);
        DispatchLegacy(ref writer, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DispatchGeneratedParser(ref LuminPackWriter writer, ref IFoo value)
        => LuminPackParseProvider.Cache<IFoo>.Parser!.Serialize(ref writer, ref value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DispatchDirect(ref LuminPackWriter writer, IFoo value)
        => value.__LuminPackUnionSerialize_globalLuminPackBenchmarkIFoo_07473b24(ref writer);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DispatchLegacy(ref LuminPackWriter writer, IFoo value)
        => value.__BenchmarkLegacyUnionSerialize(ref writer);

    [GlobalSetup]
    public void SetUp()
    {
        IFoo[] prototypes =
        {
            FooA.Create(), FooB.Create(), FooC.Create(), FooD.Create(), FooE.Create(),
            FooF.Create(), FooG.Create(), FooH.Create(), FooI.Create(), FooJ.Create(),
            FooK.Create(), FooL.Create(), FooM.Create(), FooN.Create(), FooP.Create(),
            FooO.Create(), FooQ.Create(), FooR.Create(), FooS.Create(), FooZ.Create(),
        };

        _values = new IFoo[Count];
        _fooAValues = new IFoo[Count];
        _legacyMap = new LuminUnionMap<LuminPackBenchmark_IFooParser.HashEntry>(prototypes.Length);
        for (ushort i = 0; i < prototypes.Length; i++)
        {
            _legacyMap.Register(
                LuminPackMarshal.GetMethodTable(prototypes[i]),
                new LuminPackBenchmark_IFooParser.HashEntry { Tag = i });
        }

        for (int i = 0; i < Count; i++)
        {
            _values[i] = prototypes[i % prototypes.Length];
            _fooAValues[i] = prototypes[0];
        }

        _buffer = new LuminBufferWriter(useFirstBuffer: true);
        _state = new LuminPackWriterOptionalState();
    }

    [GlobalCleanup]
    public void CleanUp()
    {
        ((IDisposable)_state).Dispose();
        _buffer.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int UnionMapSwitchSerialize()
    {
        var writer = new LuminPackWriter(_buffer, _state);
        var values = _values;

        for (int i = 0; i < values.Length; i++)
        {
            ref IFoo value = ref values[i];
            nint methodTable = LuminPackMarshal.GetMethodTable(value);
            ref var entry = ref _legacyMap.TryGetValueRef(methodTable);
            if (Unsafe.IsNullRef(ref entry))
                return -1;

            WriteByTag(ref writer, ref value, entry.Tag);
        }

        return writer.CurrentIndex;
    }

    [Benchmark]
    public int VirtualDirectSerialize()
    {
        var writer = new LuminPackWriter(_buffer, _state);
        var values = _values;

        for (int i = 0; i < values.Length; i++)
            values[i].__LuminPackUnionSerialize_globalLuminPackBenchmarkIFoo_07473b24(ref writer);

        return writer.CurrentIndex;
    }

    [Benchmark]
    public int FooALegacyVirtualWrapper()
    {
        var writer = new LuminPackWriter(_buffer, _state);
        var values = _fooAValues;

        for (int i = 0; i < values.Length; i++)
            values[i].__BenchmarkLegacyUnionSerialize(ref writer);

        return writer.CurrentIndex;
    }

    [Benchmark]
    public int FooADirectVirtual()
    {
        var writer = new LuminPackWriter(_buffer, _state);
        var values = _fooAValues;

        for (int i = 0; i < values.Length; i++)
            values[i].__LuminPackUnionSerialize_globalLuminPackBenchmarkIFoo_07473b24(ref writer);

        return writer.CurrentIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteByTag(ref LuminPackWriter writer, ref IFoo value, ushort tag)
    {
        switch (tag)
        {
            case 0: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooA(ref writer, ref value); break;
            case 1: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooB(ref writer, ref value); break;
            case 2: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooC(ref writer, ref value); break;
            case 3: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooD(ref writer, ref value); break;
            case 4: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooE(ref writer, ref value); break;
            case 5: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooF(ref writer, ref value); break;
            case 6: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooG(ref writer, ref value); break;
            case 7: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooH(ref writer, ref value); break;
            case 8: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooI(ref writer, ref value); break;
            case 9: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooJ(ref writer, ref value); break;
            case 10: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooK(ref writer, ref value); break;
            case 11: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooL(ref writer, ref value); break;
            case 12: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooM(ref writer, ref value); break;
            case 13: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooN(ref writer, ref value); break;
            case 14: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooP(ref writer, ref value); break;
            case 15: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooO(ref writer, ref value); break;
            case 16: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooQ(ref writer, ref value); break;
            case 17: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooR(ref writer, ref value); break;
            case 18: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooS(ref writer, ref value); break;
            case 19: LuminPackBenchmark_IFooParser.WriteLuminPackBenchmark_FooZ(ref writer, ref value); break;
        }
    }
}
