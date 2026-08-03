using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using LuminPack.Code;
using LuminPack.Utility;

namespace LuminPackBenchmark;

#nullable disable

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
[DisassemblyDiagnoser(
    maxDepth: 3,
    printSource: true,
    printInstructionAddresses: true)]
public class UnionDispatchBenchmark
{
    private const int Count = 1000;

    private DispatchBase[] _values;
    private nint[] _methodTables;

    private LuminUnionMap<UnionEntry> _map;
    private ExperimentalUnionMap<UnionEntry, RawLowBitsHash> _rawMap;
    private ExperimentalUnionMap<UnionEntry, AlignmentShiftHash> _shiftMap;
    private ExperimentalUnionMap<UnionEntry, AlignmentShiftXorHash> _shiftXorMap;
    private ExperimentalUnionMap<UnionEntry, AlignmentShiftMultiplyXorHash> _multiplyXorMap;

    // Direct MT
    private nint _mt0;
    private nint _mt1;
    private nint _mt2;
    private nint _mt3;
    private nint _mt4;
    private nint _mt5;
    private nint _mt6;
    private nint _mt7;
    private nint _mt8;
    private nint _mt9;
    private nint _mt10;
    private nint _mt11;
    private nint _mt12;
    private nint _mt13;
    private nint _mt14;
    private nint _mt15;
    private nint _mt16;
    private nint _mt17;
    private nint _mt18;
    private nint _mt19;

    // PEXT
    private ulong _pextMask;
    private nint[] _pextKeys;
    private byte[] _pextTags;
    private bool _pextAvailable;

    public struct UnionEntry
    {
        public ushort Tag;
    }

    [GlobalSetup]
    public void SetUp()
    {
        DispatchBase[] prototypes =
        {
            new Dispatch00(),
            new Dispatch01(),
            new Dispatch02(),
            new Dispatch03(),
            new Dispatch04(),
            new Dispatch05(),
            new Dispatch06(),
            new Dispatch07(),
            new Dispatch08(),
            new Dispatch09(),
            new Dispatch10(),
            new Dispatch11(),
            new Dispatch12(),
            new Dispatch13(),
            new Dispatch14(),
            new Dispatch15(),
            new Dispatch16(),
            new Dispatch17(),
            new Dispatch18(),
            new Dispatch19(),
        };

        _methodTables = new nint[20];

        for (int i = 0; i < prototypes.Length; i++)
        {
            _methodTables[i] =
                LuminPackMarshal.GetMethodTable(prototypes[i]);
        }

        _mt0 = _methodTables[0];
        _mt1 = _methodTables[1];
        _mt2 = _methodTables[2];
        _mt3 = _methodTables[3];
        _mt4 = _methodTables[4];
        _mt5 = _methodTables[5];
        _mt6 = _methodTables[6];
        _mt7 = _methodTables[7];
        _mt8 = _methodTables[8];
        _mt9 = _methodTables[9];
        _mt10 = _methodTables[10];
        _mt11 = _methodTables[11];
        _mt12 = _methodTables[12];
        _mt13 = _methodTables[13];
        _mt14 = _methodTables[14];
        _mt15 = _methodTables[15];
        _mt16 = _methodTables[16];
        _mt17 = _methodTables[17];
        _mt18 = _methodTables[18];
        _mt19 = _methodTables[19];

        _values = new DispatchBase[Count];

        // 严格均匀的 20-type round-robin。
        // 注意你原 PolymorphismBenchmark 的 i % 20 + case 1..20
        // 会漏掉余数 0，并且 case 20 永远到不了。
        for (int i = 0; i < Count; i++)
        {
            _values[i] = prototypes[i % 20];
        }

        _map = CreateMap();
        _rawMap = CreateExperimentalMap<RawLowBitsHash>();
        _shiftMap = CreateExperimentalMap<AlignmentShiftHash>();
        _shiftXorMap = CreateExperimentalMap<AlignmentShiftXorHash>();
        _multiplyXorMap = CreateExperimentalMap<AlignmentShiftMultiplyXorHash>();

        Console.WriteLine($"UnionMap capacities: current={_map.Capacity}, raw={_rawMap.Capacity}, shift={_shiftMap.Capacity}, shift-xor={_shiftXorMap.Capacity}, multiply-xor={_multiplyXorMap.Capacity}");

        SetupPext();
    }

    private LuminUnionMap<UnionEntry> CreateMap()
    {
        var map = new LuminUnionMap<UnionEntry>(20);

        for (ushort i = 0; i < 20; i++)
        {
            if (!map.TryRegister(
                    _methodTables[i],
                    new UnionEntry { Tag = i }))
            {
                throw new InvalidOperationException(
                    $"UnionMap registration failed: {i}");
            }
        }

        return map;
    }

    private ExperimentalUnionMap<UnionEntry, THash> CreateExperimentalMap<THash>()
        where THash : IExperimentalUnionHash
    {
        var map = new ExperimentalUnionMap<UnionEntry, THash>(20);

        for (ushort i = 0; i < 20; i++)
        {
            if (!map.TryRegister(_methodTables[i], new UnionEntry { Tag = i }))
                throw new InvalidOperationException($"Experimental UnionMap registration failed: {typeof(THash).Name}, {i}");
        }

        return map;
    }

    // ------------------------------------------------------------
    // 当前 LuminPack：
    //
    // MethodTable
    // -> UnionMap
    // -> Tag
    // -> switch
    // ------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Dispatch")]
    public int UnionMapSwitch()
    {
        int result = 0;
        var values = _values;
        var map = _map;

        for (int i = 0; i < values.Length; i++)
        {
            nint mt = LuminPackMarshal.GetMethodTable(values[i]);

            ref UnionEntry entry =
                ref map.TryGetValueRef(mt);

            if (Unsafe.IsNullRef(ref entry))
                return -1;

            result += DispatchTag(entry.Tag);
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("HashLookup")]
    public int CurrentHashLookup()
    {
        int result = 0;
        var keys = _methodTables;
        var map = _map;

        for (int i = 0; i < Count; i++)
        {
            if (!map.TryGetValue(keys[i % 20], out var entry))
                return -1;
            result += entry.Tag;
        }

        return result;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("HashLookup")]
    public int RawLowBitsLookup() => LookupExperimental(_rawMap);

    [Benchmark]
    [BenchmarkCategory("HashLookup")]
    public int AlignmentShiftLookup() => LookupExperimental(_shiftMap);

    [Benchmark]
    [BenchmarkCategory("HashLookup")]
    public int AlignmentShiftXorLookup() => LookupExperimental(_shiftXorMap);

    [Benchmark]
    [BenchmarkCategory("HashLookup")]
    public int AlignmentShiftMultiplyXorLookup() => LookupExperimental(_multiplyXorMap);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LookupExperimental<THash>(ExperimentalUnionMap<UnionEntry, THash> map)
        where THash : IExperimentalUnionHash
    {
        int result = 0;
        var keys = _methodTables;

        for (int i = 0; i < Count; i++)
        {
            if (!map.TryGetValue(keys[i % 20], out var entry))
                return -1;
            result += entry.Tag;
        }

        return result;
    }

    // ------------------------------------------------------------
    // 新方案：
    //
    // object
    // -> CLR virtual dispatch
    // -> concrete implementation
    // ------------------------------------------------------------

    [Benchmark]
    [BenchmarkCategory("Dispatch")]
    public int VirtualDispatch()
    {
        int result = 0;
        var values = _values;

        for (int i = 0; i < values.Length; i++)
        {
            result += values[i].Dispatch();
        }

        return result;
    }

    // ------------------------------------------------------------
    // MethodTable 直接比较
    // 用来确定小型 Union 的理论表现
    // ------------------------------------------------------------

    [Benchmark]
    [BenchmarkCategory("Dispatch")]
    public int DirectMethodTable()
    {
        int result = 0;
        var values = _values;

        for (int i = 0; i < values.Length; i++)
        {
            nint mt = LuminPackMarshal.GetMethodTable(values[i]);

            result += DispatchMethodTable(mt);
        }

        return result;
    }

    // ------------------------------------------------------------
    // BMI2 PEXT 实验
    //
    // MT
    // -> PEXT
    // -> table
    // -> verify MT
    // -> tag switch
    // ------------------------------------------------------------

    [Benchmark]
    [BenchmarkCategory("Dispatch")]
    public int PextSwitch()
    {
        if (!_pextAvailable)
            return -1;

        int result = 0;

        var values = _values;
        var keys = _pextKeys;
        var tags = _pextTags;

        ulong mask = _pextMask;

        for (int i = 0; i < values.Length; i++)
        {
            nint mt = LuminPackMarshal.GetMethodTable(values[i]);

            nuint index =
                (nuint)Bmi2.X64.ParallelBitExtract(
                    (ulong)mt,
                    mask);

            if (keys[index] != mt)
                return -1;

            result += DispatchTag(tags[index]);
        }

        return result;
    }

    // ------------------------------------------------------------
    // 测 UnionMap 自身的实际 managed allocation。
    //
    // 这个 benchmark 不用于比较 dispatch 速度，
    // 而是让 MemoryDiagnoser 告诉我们：
    // 每一个 20-member UnionMap 到底常驻多少字节。
    // ------------------------------------------------------------

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public LuminUnionMap<UnionEntry> CreateUnionMap20()
    {
        var map = new LuminUnionMap<UnionEntry>(20);

        for (ushort i = 0; i < 20; i++)
        {
            map.TryRegister(
                _methodTables[i],
                new UnionEntry { Tag = i });
        }

        return map;
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public ExperimentalUnionMap<UnionEntry, RawLowBitsHash> CreateUnionMap20RawLowBits()
        => CreateExperimentalMap<RawLowBitsHash>();

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public ExperimentalUnionMap<UnionEntry, AlignmentShiftHash> CreateUnionMap20AlignmentShift()
        => CreateExperimentalMap<AlignmentShiftHash>();

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public ExperimentalUnionMap<UnionEntry, AlignmentShiftXorHash> CreateUnionMap20AlignmentShiftXor()
        => CreateExperimentalMap<AlignmentShiftXorHash>();

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public ExperimentalUnionMap<UnionEntry, AlignmentShiftMultiplyXorHash> CreateUnionMap20AlignmentShiftMultiplyXor()
        => CreateExperimentalMap<AlignmentShiftMultiplyXorHash>();

    // ------------------------------------------------------------
    // Tag dispatch
    //
    // 故意不返回 tag 本身。
    // 防止 JIT 把整个 switch 化简成 identity。
    // ------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DispatchTag(ushort tag)
    {
        return tag switch
        {
            0 => 101,
            1 => 307,
            2 => 509,
            3 => 701,
            4 => 907,

            5 => 1103,
            6 => 1301,
            7 => 1511,
            8 => 1709,
            9 => 1901,

            10 => 2111,
            11 => 2309,
            12 => 2503,
            13 => 2707,
            14 => 2903,

            15 => 3109,
            16 => 3301,
            17 => 3511,
            18 => 3701,
            19 => 3907,

            _ => -1
        };
    }

    // ------------------------------------------------------------
    // 直接 MT chain
    // ------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DispatchMethodTable(nint mt)
    {
        if (mt == _mt0) return 101;
        if (mt == _mt1) return 307;
        if (mt == _mt2) return 509;
        if (mt == _mt3) return 701;
        if (mt == _mt4) return 907;

        if (mt == _mt5) return 1103;
        if (mt == _mt6) return 1301;
        if (mt == _mt7) return 1511;
        if (mt == _mt8) return 1709;
        if (mt == _mt9) return 1901;

        if (mt == _mt10) return 2111;
        if (mt == _mt11) return 2309;
        if (mt == _mt12) return 2503;
        if (mt == _mt13) return 2707;
        if (mt == _mt14) return 2903;

        if (mt == _mt15) return 3109;
        if (mt == _mt16) return 3301;
        if (mt == _mt17) return 3511;
        if (mt == _mt18) return 3701;
        if (mt == _mt19) return 3907;

        return -1;
    }

    // ------------------------------------------------------------
    // PEXT mask 构建
    //
    // Cold path。
    // 使用 greedy 算法挑选最能区分 MethodTable 的 bit。
    // ------------------------------------------------------------

    private void SetupPext()
    {
        if (!Bmi2.X64.IsSupported)
        {
            _pextAvailable = false;
            return;
        }

        ulong varyingBits = 0;
        ulong first = (ulong)_methodTables[0];

        for (int i = 1; i < _methodTables.Length; i++)
        {
            varyingBits |=
                first ^ (ulong)_methodTables[i];
        }

        ulong selectedMask = 0;
        int currentUnique = 1;

        while (currentUnique < _methodTables.Length)
        {
            ulong bestBit = 0;
            int bestUnique = currentUnique;

            ulong remaining =
                varyingBits & ~selectedMask;

            while (remaining != 0)
            {
                int bitIndex =
                    BitOperations.TrailingZeroCount(remaining);

                ulong bit = 1UL << bitIndex;

                int unique = CountUniquePext(
                    selectedMask | bit);

                if (unique > bestUnique)
                {
                    bestUnique = unique;
                    bestBit = bit;
                }

                remaining &= remaining - 1;
            }

            if (bestBit == 0)
            {
                _pextAvailable = false;
                return;
            }

            selectedMask |= bestBit;
            currentUnique = bestUnique;
        }

        int selectedBits =
            BitOperations.PopCount(selectedMask);

        // 避免实验 table 失控。
        if (selectedBits > 12)
        {
            _pextAvailable = false;
            return;
        }

        int tableSize =
            1 << selectedBits;

        _pextKeys = new nint[tableSize];
        _pextTags = new byte[tableSize];

        for (byte i = 0; i < _methodTables.Length; i++)
        {
            nint mt = _methodTables[i];

            int index =
                (int)Bmi2.X64.ParallelBitExtract(
                    (ulong)mt,
                    selectedMask);

            if (_pextKeys[index] != 0)
            {
                _pextAvailable = false;
                return;
            }

            _pextKeys[index] = mt;
            _pextTags[index] = i;
        }

        _pextMask = selectedMask;
        _pextAvailable = true;

        Console.WriteLine(
            $"PEXT Mask = 0x{selectedMask:X16}");

        Console.WriteLine(
            $"PEXT Bits = {selectedBits}");

        Console.WriteLine(
            $"PEXT Table = {tableSize}");
    }

    private int CountUniquePext(ulong mask)
    {
        Span<ulong> values = stackalloc ulong[20];

        int uniqueCount = 0;

        for (int i = 0; i < _methodTables.Length; i++)
        {
            ulong value =
                Bmi2.X64.ParallelBitExtract(
                    (ulong)_methodTables[i],
                    mask);

            bool found = false;

            for (int j = 0; j < uniqueCount; j++)
            {
                if (values[j] == value)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                values[uniqueCount++] = value;
            }
        }

        return uniqueCount;
    }
}

public interface IExperimentalUnionHash
{
    static abstract nuint Hash(nint key);
}

public readonly struct RawLowBitsHash : IExperimentalUnionHash
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint Hash(nint key) => (nuint)key;
}

public readonly struct AlignmentShiftHash : IExperimentalUnionHash
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint Hash(nint key)
        => (nuint)key >> (IntPtr.Size == 8 ? 3 : 2);
}

public readonly struct AlignmentShiftXorHash : IExperimentalUnionHash
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint Hash(nint key)
    {
        nuint hash = (nuint)key >> (IntPtr.Size == 8 ? 3 : 2);
        return hash ^ (hash >> 13);
    }
}

public readonly struct AlignmentShiftMultiplyXorHash : IExperimentalUnionHash
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint Hash(nint key)
    {
        nuint hash = (nuint)key >> (IntPtr.Size == 8 ? 3 : 2);
        hash *= 0x9E3779B1u;
        return hash ^ (hash >> 16);
    }
}

public sealed class ExperimentalUnionMap<TValue, THash>
    where THash : IExperimentalUnionHash
{
    private const int MinCapacity = 8;

    private struct Entry
    {
        public nint Key;
        public TValue Value;
    }

    private Entry[] _table;
    private int _capacity;
    private int _count;
    private nuint _capacityMask;

    public int Capacity => _capacity;

    public ExperimentalUnionMap(int capacity)
    {
        _capacity = CalculateCapacity(capacity);
        InitializeTable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in nint key, out TValue value)
    {
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), HashIndex(key));
        if (entry.Key != key)
        {
            value = default;
            return false;
        }

        value = entry.Value;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRegister(in nint key, TValue value)
    {
        ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), HashIndex(key));
        if (entry.Key == 0)
        {
            entry.Key = key;
            entry.Value = value;
            _count++;
            return true;
        }

        if (entry.Key == key)
            return false;

        return CuckooInsert(key, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CuckooInsert(nint key, TValue value)
    {
        nint currentKey = key;
        TValue currentValue = value;

        for (int i = 0; i < _capacity; i++)
        {
            ref Entry entry = ref Unsafe.Add(ref LuminPackMarshal.GetNotNullArrayReference(_table), HashIndex(currentKey));
            if (entry.Key == 0)
            {
                entry.Key = currentKey;
                entry.Value = currentValue;
                _count++;
                return true;
            }

            (currentKey, entry.Key) = (entry.Key, currentKey);
            (currentValue, entry.Value) = (entry.Value, currentValue);
        }

        Resize();
        return TryRegister(currentKey, currentValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private nuint HashIndex(nint key)
    {
        return THash.Hash(key) & _capacityMask;
    }

    private void Resize()
    {
        Entry[] oldTable = _table;
        int oldCount = _count;
        _capacity *= 2;
        _count = 0;
        InitializeTable();

        for (int i = 0; i < oldTable.Length; i++)
        {
            if (oldTable[i].Key != 0 && !TryRegister(oldTable[i].Key, oldTable[i].Value))
                throw new InvalidOperationException("Failed to rehash experimental UnionMap");
        }

        if (_count != oldCount)
            throw new InvalidOperationException("Experimental UnionMap count changed during resize");
    }

    private void InitializeTable()
    {
        _capacityMask = (nuint)(_capacity - 1);
        _table = new Entry[_capacity];
    }

    private static int CalculateCapacity(int capacity)
    {
        if (capacity < MinCapacity)
            return MinCapacity;

        capacity--;
        capacity |= capacity >> 1;
        capacity |= capacity >> 2;
        capacity |= capacity >> 4;
        capacity |= capacity >> 8;
        capacity |= capacity >> 16;
        return capacity + 1;
    }
}

// ============================================================================
// 测试类型
// ============================================================================

public abstract class DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual int Dispatch() => -1;
}

public sealed class Dispatch00 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 101;
}

public sealed class Dispatch01 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 307;
}

public sealed class Dispatch02 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 509;
}

public sealed class Dispatch03 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 701;
}

public sealed class Dispatch04 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 907;
}

public sealed class Dispatch05 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 1103;
}

public sealed class Dispatch06 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 1301;
}

public sealed class Dispatch07 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 1511;
}

public sealed class Dispatch08 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 1709;
}

public sealed class Dispatch09 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 1901;
}

public sealed class Dispatch10 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 2111;
}

public sealed class Dispatch11 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 2309;
}

public sealed class Dispatch12 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 2503;
}

public sealed class Dispatch13 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 2707;
}

public sealed class Dispatch14 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 2903;
}

public sealed class Dispatch15 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 3109;
}

public sealed class Dispatch16 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 3301;
}

public sealed class Dispatch17 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 3511;
}

public sealed class Dispatch18 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 3701;
}

public sealed class Dispatch19 : DispatchBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Dispatch() => 3907;
}
