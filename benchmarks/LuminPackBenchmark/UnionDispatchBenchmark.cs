using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace LuminPackBenchmark;

/// <summary>
/// 对比 LuminPack 多态序列化的几种运行时分派方式：
/// 1. 普通 override 虚调用；
/// 2. sealed override 虚调用；
/// 3. abstract TypeId 属性 + 原生函数指针表；
/// 4. 非虚 TypeId 字段 + 原生函数指针表（函数指针方案的理论上限）。
/// 
/// 每个 Benchmark 报告的是单次多态分派成本，OperationsPerInvoke 会将整批循环折算为单次操作。
/// </summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 8, iterationCount: 15)]
[MemoryDiagnoser]
[DisassemblyDiagnoser(
    maxDepth: 6,
    printSource: true,
    printInstructionAddresses: true)]
public unsafe class UnionDispatchBenchmark
{
    private const int TypeCount = 20;
    private const int ItemCount = 4096;
    private const int PointerTableLength = 256;

    private OpenVirtualBase[] _openVirtualItems = null!;
    private SealedVirtualBase[] _sealedVirtualItems = null!;
    private TypeIdVirtualBase[] _typeIdVirtualItems = null!;
    private TypeIdFieldBase[] _typeIdFieldItems = null!;

    private static readonly nint* s_typeIdVirtualTable;
    private static readonly nint* s_typeIdFieldTable;

    [Params(
        DispatchPattern.Monomorphic,
        DispatchPattern.Bimorphic,
        DispatchPattern.RoundRobin20,
        DispatchPattern.Random20)]
    public DispatchPattern Pattern { get; set; }

    static UnionDispatchBenchmark()
    {
        // 使用 TypeAssociatedMemory 建立 256 槽的原生函数指针表：
        // - TypeId 是 byte，因此无需数组范围检查；
        // - 生命周期与当前类型一致；
        // - 避免 delegate 分配和 delegate.Invoke。
        s_typeIdVirtualTable = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(UnionDispatchBenchmark),
            PointerTableLength * sizeof(nint));

        s_typeIdFieldTable = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(UnionDispatchBenchmark),
            PointerTableLength * sizeof(nint));

        s_typeIdVirtualTable[0] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual0;
        s_typeIdVirtualTable[1] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual1;
        s_typeIdVirtualTable[2] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual2;
        s_typeIdVirtualTable[3] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual3;
        s_typeIdVirtualTable[4] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual4;
        s_typeIdVirtualTable[5] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual5;
        s_typeIdVirtualTable[6] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual6;
        s_typeIdVirtualTable[7] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual7;
        s_typeIdVirtualTable[8] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual8;
        s_typeIdVirtualTable[9] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual9;
        s_typeIdVirtualTable[10] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual10;
        s_typeIdVirtualTable[11] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual11;
        s_typeIdVirtualTable[12] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual12;
        s_typeIdVirtualTable[13] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual13;
        s_typeIdVirtualTable[14] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual14;
        s_typeIdVirtualTable[15] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual15;
        s_typeIdVirtualTable[16] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual16;
        s_typeIdVirtualTable[17] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual17;
        s_typeIdVirtualTable[18] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual18;
        s_typeIdVirtualTable[19] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteTypeIdVirtual19;

        s_typeIdFieldTable[0] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField0;
        s_typeIdFieldTable[1] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField1;
        s_typeIdFieldTable[2] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField2;
        s_typeIdFieldTable[3] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField3;
        s_typeIdFieldTable[4] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField4;
        s_typeIdFieldTable[5] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField5;
        s_typeIdFieldTable[6] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField6;
        s_typeIdFieldTable[7] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField7;
        s_typeIdFieldTable[8] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField8;
        s_typeIdFieldTable[9] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField9;
        s_typeIdFieldTable[10] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField10;
        s_typeIdFieldTable[11] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField11;
        s_typeIdFieldTable[12] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField12;
        s_typeIdFieldTable[13] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField13;
        s_typeIdFieldTable[14] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField14;
        s_typeIdFieldTable[15] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField15;
        s_typeIdFieldTable[16] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField16;
        s_typeIdFieldTable[17] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField17;
        s_typeIdFieldTable[18] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField18;
        s_typeIdFieldTable[19] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteTypeIdField19;
    }

    [GlobalSetup]
    public void Setup()
    {
        _openVirtualItems = new OpenVirtualBase[ItemCount];
        _sealedVirtualItems = new SealedVirtualBase[ItemCount];
        _typeIdVirtualItems = new TypeIdVirtualBase[ItemCount];
        _typeIdFieldItems = new TypeIdFieldBase[ItemCount];

        var random = new Random(0x51A7);

        for (int i = 0; i < ItemCount; i++)
        {
            int typeId = Pattern switch
            {
                DispatchPattern.Monomorphic => 0,
                DispatchPattern.Bimorphic => i & 1,
                DispatchPattern.RoundRobin20 => i % TypeCount,
                DispatchPattern.Random20 => random.Next(TypeCount),
                _ => throw new ArgumentOutOfRangeException()
            };

            // 每个对象的数据不同，避免 JIT 将整个循环化简为常量。
            int a = unchecked(i * 31 + 17);
            int b = unchecked(i * 131 + typeId);

            _openVirtualItems[i] = CreateOpenVirtual(typeId, a, b);
            _sealedVirtualItems[i] = CreateSealedVirtual(typeId, a, b);
            _typeIdVirtualItems[i] = CreateTypeIdVirtual(typeId, a, b);
            _typeIdFieldItems[i] = CreateTypeIdField(typeId, a, b);
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = ItemCount)]
    public int VirtualOverride()
    {
        var state = new DispatchState(17);
        var items = _openVirtualItems;

        for (int i = 0; i < items.Length; i++)
            items[i].Serialize(ref state);

        return state.Value;
    }

    [Benchmark(OperationsPerInvoke = ItemCount)]
    public int SealedOverride()
    {
        var state = new DispatchState(17);
        var items = _sealedVirtualItems;

        for (int i = 0; i < items.Length; i++)
            items[i].Serialize(ref state);

        return state.Value;
    }

    [Benchmark(OperationsPerInvoke = ItemCount)]
    public int AbstractTypeId_FunctionPointer()
    {
        var state = new DispatchState(17);
        var items = _typeIdVirtualItems;

        for (int i = 0; i < items.Length; i++)
        {
            TypeIdVirtualBase value = items[i];

            // 此处精确模拟“基类 abstract byte TypeId + 指针表”：
            // 先通过虚属性取得 TypeId，再从表中加载函数地址并 calli。
            byte typeId = value.TypeId;
            var write = (delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)
                s_typeIdVirtualTable[typeId];

            write(ref state, value);
        }

        return state.Value;
    }

    [Benchmark(OperationsPerInvoke = ItemCount)]
    public int TypeIdField_FunctionPointer()
    {
        var state = new DispatchState(17);
        var items = _typeIdFieldItems;

        for (int i = 0; i < items.Length; i++)
        {
            TypeIdFieldBase value = items[i];

            // 这是函数指针表方案的理论最优版本：
            // TypeId 为普通只读字段，没有获取 TypeId 的额外虚调用。
            byte typeId = value.TypeId;
            var write = (delegate* managed<ref DispatchState, TypeIdFieldBase, void>)
                s_typeIdFieldTable[typeId];

            write(ref state, value);
        }

        return state.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OpenVirtualBase CreateOpenVirtual(int typeId, int a, int b)
        => typeId switch
        {
            0 => new OpenVirtual0(a, b),
            1 => new OpenVirtual1(a, b),
            2 => new OpenVirtual2(a, b),
            3 => new OpenVirtual3(a, b),
            4 => new OpenVirtual4(a, b),
            5 => new OpenVirtual5(a, b),
            6 => new OpenVirtual6(a, b),
            7 => new OpenVirtual7(a, b),
            8 => new OpenVirtual8(a, b),
            9 => new OpenVirtual9(a, b),
            10 => new OpenVirtual10(a, b),
            11 => new OpenVirtual11(a, b),
            12 => new OpenVirtual12(a, b),
            13 => new OpenVirtual13(a, b),
            14 => new OpenVirtual14(a, b),
            15 => new OpenVirtual15(a, b),
            16 => new OpenVirtual16(a, b),
            17 => new OpenVirtual17(a, b),
            18 => new OpenVirtual18(a, b),
            19 => new OpenVirtual19(a, b),
            _ => throw new ArgumentOutOfRangeException(nameof(typeId))
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SealedVirtualBase CreateSealedVirtual(int typeId, int a, int b)
        => typeId switch
        {
            0 => new SealedVirtual0(a, b),
            1 => new SealedVirtual1(a, b),
            2 => new SealedVirtual2(a, b),
            3 => new SealedVirtual3(a, b),
            4 => new SealedVirtual4(a, b),
            5 => new SealedVirtual5(a, b),
            6 => new SealedVirtual6(a, b),
            7 => new SealedVirtual7(a, b),
            8 => new SealedVirtual8(a, b),
            9 => new SealedVirtual9(a, b),
            10 => new SealedVirtual10(a, b),
            11 => new SealedVirtual11(a, b),
            12 => new SealedVirtual12(a, b),
            13 => new SealedVirtual13(a, b),
            14 => new SealedVirtual14(a, b),
            15 => new SealedVirtual15(a, b),
            16 => new SealedVirtual16(a, b),
            17 => new SealedVirtual17(a, b),
            18 => new SealedVirtual18(a, b),
            19 => new SealedVirtual19(a, b),
            _ => throw new ArgumentOutOfRangeException(nameof(typeId))
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TypeIdVirtualBase CreateTypeIdVirtual(int typeId, int a, int b)
        => typeId switch
        {
            0 => new TypeIdVirtual0(a, b),
            1 => new TypeIdVirtual1(a, b),
            2 => new TypeIdVirtual2(a, b),
            3 => new TypeIdVirtual3(a, b),
            4 => new TypeIdVirtual4(a, b),
            5 => new TypeIdVirtual5(a, b),
            6 => new TypeIdVirtual6(a, b),
            7 => new TypeIdVirtual7(a, b),
            8 => new TypeIdVirtual8(a, b),
            9 => new TypeIdVirtual9(a, b),
            10 => new TypeIdVirtual10(a, b),
            11 => new TypeIdVirtual11(a, b),
            12 => new TypeIdVirtual12(a, b),
            13 => new TypeIdVirtual13(a, b),
            14 => new TypeIdVirtual14(a, b),
            15 => new TypeIdVirtual15(a, b),
            16 => new TypeIdVirtual16(a, b),
            17 => new TypeIdVirtual17(a, b),
            18 => new TypeIdVirtual18(a, b),
            19 => new TypeIdVirtual19(a, b),
            _ => throw new ArgumentOutOfRangeException(nameof(typeId))
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TypeIdFieldBase CreateTypeIdField(int typeId, int a, int b)
        => typeId switch
        {
            0 => new TypeIdField0(a, b),
            1 => new TypeIdField1(a, b),
            2 => new TypeIdField2(a, b),
            3 => new TypeIdField3(a, b),
            4 => new TypeIdField4(a, b),
            5 => new TypeIdField5(a, b),
            6 => new TypeIdField6(a, b),
            7 => new TypeIdField7(a, b),
            8 => new TypeIdField8(a, b),
            9 => new TypeIdField9(a, b),
            10 => new TypeIdField10(a, b),
            11 => new TypeIdField11(a, b),
            12 => new TypeIdField12(a, b),
            13 => new TypeIdField13(a, b),
            14 => new TypeIdField14(a, b),
            15 => new TypeIdField15(a, b),
            16 => new TypeIdField16(a, b),
            17 => new TypeIdField17(a, b),
            18 => new TypeIdField18(a, b),
            19 => new TypeIdField19(a, b),
            _ => throw new ArgumentOutOfRangeException(nameof(typeId))
        };

    public enum DispatchPattern
    {
        /// <summary>所有元素都是同一个运行时类型，最利于 PGO 去虚拟化。</summary>
        Monomorphic,

        /// <summary>两个运行时类型交替出现。</summary>
        Bimorphic,

        /// <summary>20 个类型固定轮转，分支规律稳定但调用点高度多态。</summary>
        RoundRobin20,

        /// <summary>20 个类型随机分布，最考验间接目标预测。</summary>
        Random20
    }

    private struct DispatchState
    {
        internal int Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DispatchState(int seed)
        {
            Value = seed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Write(int a, int b, int typeId)
        {
            // 足够小，仍能观察分派开销；同时模拟序列化中的字段读取和状态更新。
            int value = Value;
            value = unchecked((value * 16_777_619) ^ a);
            value = unchecked((value * 16_777_619) ^ b);
            Value = value ^ typeId;
        }
    }

    private abstract class OpenVirtualBase
    {
        internal readonly int A;
        internal readonly int B;

        protected OpenVirtualBase(int a, int b)
        {
            A = a;
            B = b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal abstract void Serialize(ref DispatchState state);
    }

    private abstract class SealedVirtualBase
    {
        internal readonly int A;
        internal readonly int B;

        protected SealedVirtualBase(int a, int b)
        {
            A = a;
            B = b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal abstract void Serialize(ref DispatchState state);
    }

    private abstract class TypeIdVirtualBase
    {
        internal readonly int A;
        internal readonly int B;

        protected TypeIdVirtualBase(int a, int b)
        {
            A = a;
            B = b;
        }

        internal abstract byte TypeId { get; }
    }

    private abstract class TypeIdFieldBase
    {
        internal readonly byte TypeId;
        internal readonly int A;
        internal readonly int B;

        protected TypeIdFieldBase(byte typeId, int a, int b)
        {
            TypeId = typeId;
            A = a;
            B = b;
        }
    }


    private class OpenVirtual0 : OpenVirtualBase
    {
        internal OpenVirtual0(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 0);
    }

    private class OpenVirtual1 : OpenVirtualBase
    {
        internal OpenVirtual1(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 1);
    }

    private class OpenVirtual2 : OpenVirtualBase
    {
        internal OpenVirtual2(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 2);
    }

    private class OpenVirtual3 : OpenVirtualBase
    {
        internal OpenVirtual3(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 3);
    }

    private class OpenVirtual4 : OpenVirtualBase
    {
        internal OpenVirtual4(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 4);
    }

    private class OpenVirtual5 : OpenVirtualBase
    {
        internal OpenVirtual5(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 5);
    }

    private class OpenVirtual6 : OpenVirtualBase
    {
        internal OpenVirtual6(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 6);
    }

    private class OpenVirtual7 : OpenVirtualBase
    {
        internal OpenVirtual7(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 7);
    }

    private class OpenVirtual8 : OpenVirtualBase
    {
        internal OpenVirtual8(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 8);
    }

    private class OpenVirtual9 : OpenVirtualBase
    {
        internal OpenVirtual9(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 9);
    }

    private class OpenVirtual10 : OpenVirtualBase
    {
        internal OpenVirtual10(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 10);
    }

    private class OpenVirtual11 : OpenVirtualBase
    {
        internal OpenVirtual11(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 11);
    }

    private class OpenVirtual12 : OpenVirtualBase
    {
        internal OpenVirtual12(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 12);
    }

    private class OpenVirtual13 : OpenVirtualBase
    {
        internal OpenVirtual13(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 13);
    }

    private class OpenVirtual14 : OpenVirtualBase
    {
        internal OpenVirtual14(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 14);
    }

    private class OpenVirtual15 : OpenVirtualBase
    {
        internal OpenVirtual15(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 15);
    }

    private class OpenVirtual16 : OpenVirtualBase
    {
        internal OpenVirtual16(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 16);
    }

    private class OpenVirtual17 : OpenVirtualBase
    {
        internal OpenVirtual17(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 17);
    }

    private class OpenVirtual18 : OpenVirtualBase
    {
        internal OpenVirtual18(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 18);
    }

    private class OpenVirtual19 : OpenVirtualBase
    {
        internal OpenVirtual19(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 19);
    }


    private class SealedVirtual0 : SealedVirtualBase
    {
        internal SealedVirtual0(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 0);
    }

    private class SealedVirtual1 : SealedVirtualBase
    {
        internal SealedVirtual1(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 1);
    }

    private class SealedVirtual2 : SealedVirtualBase
    {
        internal SealedVirtual2(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 2);
    }

    private class SealedVirtual3 : SealedVirtualBase
    {
        internal SealedVirtual3(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 3);
    }

    private class SealedVirtual4 : SealedVirtualBase
    {
        internal SealedVirtual4(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 4);
    }

    private class SealedVirtual5 : SealedVirtualBase
    {
        internal SealedVirtual5(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 5);
    }

    private class SealedVirtual6 : SealedVirtualBase
    {
        internal SealedVirtual6(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 6);
    }

    private class SealedVirtual7 : SealedVirtualBase
    {
        internal SealedVirtual7(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 7);
    }

    private class SealedVirtual8 : SealedVirtualBase
    {
        internal SealedVirtual8(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 8);
    }

    private class SealedVirtual9 : SealedVirtualBase
    {
        internal SealedVirtual9(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 9);
    }

    private class SealedVirtual10 : SealedVirtualBase
    {
        internal SealedVirtual10(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 10);
    }

    private class SealedVirtual11 : SealedVirtualBase
    {
        internal SealedVirtual11(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 11);
    }

    private class SealedVirtual12 : SealedVirtualBase
    {
        internal SealedVirtual12(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 12);
    }

    private class SealedVirtual13 : SealedVirtualBase
    {
        internal SealedVirtual13(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 13);
    }

    private class SealedVirtual14 : SealedVirtualBase
    {
        internal SealedVirtual14(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 14);
    }

    private class SealedVirtual15 : SealedVirtualBase
    {
        internal SealedVirtual15(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 15);
    }

    private class SealedVirtual16 : SealedVirtualBase
    {
        internal SealedVirtual16(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 16);
    }

    private class SealedVirtual17 : SealedVirtualBase
    {
        internal SealedVirtual17(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 17);
    }

    private class SealedVirtual18 : SealedVirtualBase
    {
        internal SealedVirtual18(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 18);
    }

    private class SealedVirtual19 : SealedVirtualBase
    {
        internal SealedVirtual19(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 19);
    }


    private class TypeIdVirtual0 : TypeIdVirtualBase
    {
        internal TypeIdVirtual0(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0;
        }
    }

    private class TypeIdVirtual1 : TypeIdVirtualBase
    {
        internal TypeIdVirtual1(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 1;
        }
    }

    private class TypeIdVirtual2 : TypeIdVirtualBase
    {
        internal TypeIdVirtual2(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 2;
        }
    }

    private class TypeIdVirtual3 : TypeIdVirtualBase
    {
        internal TypeIdVirtual3(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 3;
        }
    }

    private class TypeIdVirtual4 : TypeIdVirtualBase
    {
        internal TypeIdVirtual4(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 4;
        }
    }

    private class TypeIdVirtual5 : TypeIdVirtualBase
    {
        internal TypeIdVirtual5(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 5;
        }
    }

    private class TypeIdVirtual6 : TypeIdVirtualBase
    {
        internal TypeIdVirtual6(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 6;
        }
    }

    private class TypeIdVirtual7 : TypeIdVirtualBase
    {
        internal TypeIdVirtual7(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 7;
        }
    }

    private class TypeIdVirtual8 : TypeIdVirtualBase
    {
        internal TypeIdVirtual8(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 8;
        }
    }

    private class TypeIdVirtual9 : TypeIdVirtualBase
    {
        internal TypeIdVirtual9(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 9;
        }
    }

    private class TypeIdVirtual10 : TypeIdVirtualBase
    {
        internal TypeIdVirtual10(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 10;
        }
    }

    private class TypeIdVirtual11 : TypeIdVirtualBase
    {
        internal TypeIdVirtual11(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 11;
        }
    }

    private class TypeIdVirtual12 : TypeIdVirtualBase
    {
        internal TypeIdVirtual12(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 12;
        }
    }

    private class TypeIdVirtual13 : TypeIdVirtualBase
    {
        internal TypeIdVirtual13(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 13;
        }
    }

    private class TypeIdVirtual14 : TypeIdVirtualBase
    {
        internal TypeIdVirtual14(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 14;
        }
    }

    private class TypeIdVirtual15 : TypeIdVirtualBase
    {
        internal TypeIdVirtual15(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 15;
        }
    }

    private class TypeIdVirtual16 : TypeIdVirtualBase
    {
        internal TypeIdVirtual16(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 16;
        }
    }

    private class TypeIdVirtual17 : TypeIdVirtualBase
    {
        internal TypeIdVirtual17(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 17;
        }
    }

    private class TypeIdVirtual18 : TypeIdVirtualBase
    {
        internal TypeIdVirtual18(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 18;
        }
    }

    private class TypeIdVirtual19 : TypeIdVirtualBase
    {
        internal TypeIdVirtual19(int a, int b) : base(a, b) { }

        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 19;
        }
    }


    private sealed class TypeIdField0 : TypeIdFieldBase
    {
        internal TypeIdField0(int a, int b) : base(0, a, b) { }
    }

    private sealed class TypeIdField1 : TypeIdFieldBase
    {
        internal TypeIdField1(int a, int b) : base(1, a, b) { }
    }

    private sealed class TypeIdField2 : TypeIdFieldBase
    {
        internal TypeIdField2(int a, int b) : base(2, a, b) { }
    }

    private sealed class TypeIdField3 : TypeIdFieldBase
    {
        internal TypeIdField3(int a, int b) : base(3, a, b) { }
    }

    private sealed class TypeIdField4 : TypeIdFieldBase
    {
        internal TypeIdField4(int a, int b) : base(4, a, b) { }
    }

    private sealed class TypeIdField5 : TypeIdFieldBase
    {
        internal TypeIdField5(int a, int b) : base(5, a, b) { }
    }

    private sealed class TypeIdField6 : TypeIdFieldBase
    {
        internal TypeIdField6(int a, int b) : base(6, a, b) { }
    }

    private sealed class TypeIdField7 : TypeIdFieldBase
    {
        internal TypeIdField7(int a, int b) : base(7, a, b) { }
    }

    private sealed class TypeIdField8 : TypeIdFieldBase
    {
        internal TypeIdField8(int a, int b) : base(8, a, b) { }
    }

    private sealed class TypeIdField9 : TypeIdFieldBase
    {
        internal TypeIdField9(int a, int b) : base(9, a, b) { }
    }

    private sealed class TypeIdField10 : TypeIdFieldBase
    {
        internal TypeIdField10(int a, int b) : base(10, a, b) { }
    }

    private sealed class TypeIdField11 : TypeIdFieldBase
    {
        internal TypeIdField11(int a, int b) : base(11, a, b) { }
    }

    private sealed class TypeIdField12 : TypeIdFieldBase
    {
        internal TypeIdField12(int a, int b) : base(12, a, b) { }
    }

    private sealed class TypeIdField13 : TypeIdFieldBase
    {
        internal TypeIdField13(int a, int b) : base(13, a, b) { }
    }

    private sealed class TypeIdField14 : TypeIdFieldBase
    {
        internal TypeIdField14(int a, int b) : base(14, a, b) { }
    }

    private sealed class TypeIdField15 : TypeIdFieldBase
    {
        internal TypeIdField15(int a, int b) : base(15, a, b) { }
    }

    private sealed class TypeIdField16 : TypeIdFieldBase
    {
        internal TypeIdField16(int a, int b) : base(16, a, b) { }
    }

    private sealed class TypeIdField17 : TypeIdFieldBase
    {
        internal TypeIdField17(int a, int b) : base(17, a, b) { }
    }

    private sealed class TypeIdField18 : TypeIdFieldBase
    {
        internal TypeIdField18(int a, int b) : base(18, a, b) { }
    }

    private sealed class TypeIdField19 : TypeIdFieldBase
    {
        internal TypeIdField19(int a, int b) : base(19, a, b) { }
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual0(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual1(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual2(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual3(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual4(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual5(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual6(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 6);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual7(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual8(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual9(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 9);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual10(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 10);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual11(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 11);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual12(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 12);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual13(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 13);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual14(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 14);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual15(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 15);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual16(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual17(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 17);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual18(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 18);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual19(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 19);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField0(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField1(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField2(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField3(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField4(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField5(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField6(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 6);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField7(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField8(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField9(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 9);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField10(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 10);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField11(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 11);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField12(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 12);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField13(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 13);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField14(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 14);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField15(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 15);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField16(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField17(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 17);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField18(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 18);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField19(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 19);

}
