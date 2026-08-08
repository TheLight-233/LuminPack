using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace LuminPackBenchmark;

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 6, iterationCount: 12)]
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 4, printSource: true, printInstructionAddresses: true)]
public unsafe class UnionDispatchHeavyBenchmark
{
    private const int TypeCount = 20;
    private const int ItemCount = 4096;
    private const int RepeatCount = 8192;

    private OpenVirtualBase[] _virtualItems = null!;
    private SealedVirtualBase[] _sealedItems = null!;
    private TypeIdVirtualBase[] _virtualTypeIdItems = null!;
    private TypeIdFieldBase[] _fieldTypeIdItems = null!;

    private static readonly nint* s_AbstractTypeIdTable;
    private static readonly nint* s_FieldTypeIdTable;

    static UnionDispatchHeavyBenchmark()
    {
        s_AbstractTypeIdTable = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(UnionDispatchHeavyBenchmark), 256 * sizeof(nint));

        s_FieldTypeIdTable = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(UnionDispatchHeavyBenchmark), 256 * sizeof(nint));

        s_AbstractTypeIdTable[0] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId0;
        s_AbstractTypeIdTable[1] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId1;
        s_AbstractTypeIdTable[2] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId2;
        s_AbstractTypeIdTable[3] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId3;
        s_AbstractTypeIdTable[4] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId4;
        s_AbstractTypeIdTable[5] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId5;
        s_AbstractTypeIdTable[6] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId6;
        s_AbstractTypeIdTable[7] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId7;
        s_AbstractTypeIdTable[8] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId8;
        s_AbstractTypeIdTable[9] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId9;
        s_AbstractTypeIdTable[10] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId10;
        s_AbstractTypeIdTable[11] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId11;
        s_AbstractTypeIdTable[12] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId12;
        s_AbstractTypeIdTable[13] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId13;
        s_AbstractTypeIdTable[14] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId14;
        s_AbstractTypeIdTable[15] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId15;
        s_AbstractTypeIdTable[16] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId16;
        s_AbstractTypeIdTable[17] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId17;
        s_AbstractTypeIdTable[18] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId18;
        s_AbstractTypeIdTable[19] = (nint)(delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)&WriteAbstractTypeId19;
        s_FieldTypeIdTable[0] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId0;
        s_FieldTypeIdTable[1] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId1;
        s_FieldTypeIdTable[2] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId2;
        s_FieldTypeIdTable[3] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId3;
        s_FieldTypeIdTable[4] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId4;
        s_FieldTypeIdTable[5] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId5;
        s_FieldTypeIdTable[6] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId6;
        s_FieldTypeIdTable[7] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId7;
        s_FieldTypeIdTable[8] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId8;
        s_FieldTypeIdTable[9] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId9;
        s_FieldTypeIdTable[10] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId10;
        s_FieldTypeIdTable[11] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId11;
        s_FieldTypeIdTable[12] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId12;
        s_FieldTypeIdTable[13] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId13;
        s_FieldTypeIdTable[14] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId14;
        s_FieldTypeIdTable[15] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId15;
        s_FieldTypeIdTable[16] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId16;
        s_FieldTypeIdTable[17] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId17;
        s_FieldTypeIdTable[18] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId18;
        s_FieldTypeIdTable[19] = (nint)(delegate* managed<ref DispatchState, TypeIdFieldBase, void>)&WriteFieldTypeId19;
    }

    [GlobalSetup]
    public void Setup()
    {
        _virtualItems = new OpenVirtualBase[ItemCount];
        _sealedItems = new SealedVirtualBase[ItemCount];
        _virtualTypeIdItems = new TypeIdVirtualBase[ItemCount];
        _fieldTypeIdItems = new TypeIdFieldBase[ItemCount];

        // 模拟你 PolymorphismBenchmark 中 i % 20 的实际布局。
        for (int i = 0; i < ItemCount; i++)
        {
            int typeId = i % TypeCount;
            int a = unchecked(i * 31 + 17);
            int b = unchecked(i * 131 + typeId);

            _virtualItems[i] = CreateVirtual(typeId, a, b);
            _sealedItems[i] = CreateSealed(typeId, a, b);
            _virtualTypeIdItems[i] = CreateAbstractTypeId(typeId, a, b);
            _fieldTypeIdItems[i] = CreateFieldTypeId(typeId, a, b);
        }
    }

    [Benchmark(Baseline = true)]
    public int VirtualOverride()
    {
        var state = new DispatchState(17);
        var items = _virtualItems;

        for (int repeat = 0; repeat < RepeatCount; repeat++)
        {
            for (int i = 0; i < items.Length; i++)
                items[i].Serialize(ref state);
        }

        return state.Value;
    }

    [Benchmark]
    public int SealedOverride()
    {
        var state = new DispatchState(17);
        var items = _sealedItems;

        for (int repeat = 0; repeat < RepeatCount; repeat++)
        {
            for (int i = 0; i < items.Length; i++)
                items[i].Serialize(ref state);
        }

        return state.Value;
    }

    [Benchmark]
    public int AbstractTypeId_FunctionPointer()
    {
        var state = new DispatchState(17);
        var items = _virtualTypeIdItems;

        for (int repeat = 0; repeat < RepeatCount; repeat++)
        {
            for (int i = 0; i < items.Length; i++)
            {
                TypeIdVirtualBase value = items[i];
                byte typeId = value.TypeId;

                var fn =
                    (delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)
                    s_AbstractTypeIdTable[typeId];

                fn(ref state, value);
            }
        }

        return state.Value;
    }

    [Benchmark]
    public int TypeIdField_FunctionPointer()
    {
        var state = new DispatchState(17);
        var items = _fieldTypeIdItems;

        for (int repeat = 0; repeat < RepeatCount; repeat++)
        {
            for (int i = 0; i < items.Length; i++)
            {
                TypeIdFieldBase value = items[i];
                byte typeId = value.TypeId;

                var fn =
                    (delegate* managed<ref DispatchState, TypeIdFieldBase, void>)
                    s_FieldTypeIdTable[typeId];

                fn(ref state, value);
            }
        }

        return state.Value;
    }

    [Benchmark]
    public int TypeIdField_SwitchDirectCall()
    {
        var state = new DispatchState(17);
        var items = _fieldTypeIdItems;

        for (int repeat = 0; repeat < RepeatCount; repeat++)
        {
            for (int i = 0; i < items.Length; i++)
            {
                TypeIdFieldBase value = items[i];

                switch (value.TypeId)
                {
                    case 0:
                        WriteFieldTypeId0(ref state, value);
                        break;
                    case 1:
                        WriteFieldTypeId1(ref state, value);
                        break;
                    case 2:
                        WriteFieldTypeId2(ref state, value);
                        break;
                    case 3:
                        WriteFieldTypeId3(ref state, value);
                        break;
                    case 4:
                        WriteFieldTypeId4(ref state, value);
                        break;
                    case 5:
                        WriteFieldTypeId5(ref state, value);
                        break;
                    case 6:
                        WriteFieldTypeId6(ref state, value);
                        break;
                    case 7:
                        WriteFieldTypeId7(ref state, value);
                        break;
                    case 8:
                        WriteFieldTypeId8(ref state, value);
                        break;
                    case 9:
                        WriteFieldTypeId9(ref state, value);
                        break;
                    case 10:
                        WriteFieldTypeId10(ref state, value);
                        break;
                    case 11:
                        WriteFieldTypeId11(ref state, value);
                        break;
                    case 12:
                        WriteFieldTypeId12(ref state, value);
                        break;
                    case 13:
                        WriteFieldTypeId13(ref state, value);
                        break;
                    case 14:
                        WriteFieldTypeId14(ref state, value);
                        break;
                    case 15:
                        WriteFieldTypeId15(ref state, value);
                        break;
                    case 16:
                        WriteFieldTypeId16(ref state, value);
                        break;
                    case 17:
                        WriteFieldTypeId17(ref state, value);
                        break;
                    case 18:
                        WriteFieldTypeId18(ref state, value);
                        break;
                    case 19:
                        WriteFieldTypeId19(ref state, value);
                        break;
                    default:
                        ThrowInvalidTypeId(value.TypeId);
                        break;
                }
            }
        }

        return state.Value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidTypeId(byte typeId)
        => throw new ArgumentOutOfRangeException(nameof(typeId), typeId, null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OpenVirtualBase CreateVirtual(int typeId, int a, int b)
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
    private static SealedVirtualBase CreateSealed(int typeId, int a, int b)
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
    private static TypeIdVirtualBase CreateAbstractTypeId(int typeId, int a, int b)
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
    private static TypeIdFieldBase CreateFieldTypeId(int typeId, int a, int b)
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

    private struct DispatchState
    {
        internal int Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DispatchState(int seed) => Value = seed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Write(int a, int b, int typeId)
        {
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


    private sealed class OpenVirtual0 : OpenVirtualBase
    {
        internal OpenVirtual0(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 0);
    }

    private sealed class OpenVirtual1 : OpenVirtualBase
    {
        internal OpenVirtual1(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 1);
    }

    private sealed class OpenVirtual2 : OpenVirtualBase
    {
        internal OpenVirtual2(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 2);
    }

    private sealed class OpenVirtual3 : OpenVirtualBase
    {
        internal OpenVirtual3(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 3);
    }

    private sealed class OpenVirtual4 : OpenVirtualBase
    {
        internal OpenVirtual4(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 4);
    }

    private sealed class OpenVirtual5 : OpenVirtualBase
    {
        internal OpenVirtual5(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 5);
    }

    private sealed class OpenVirtual6 : OpenVirtualBase
    {
        internal OpenVirtual6(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 6);
    }

    private sealed class OpenVirtual7 : OpenVirtualBase
    {
        internal OpenVirtual7(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 7);
    }

    private sealed class OpenVirtual8 : OpenVirtualBase
    {
        internal OpenVirtual8(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 8);
    }

    private sealed class OpenVirtual9 : OpenVirtualBase
    {
        internal OpenVirtual9(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 9);
    }

    private sealed class OpenVirtual10 : OpenVirtualBase
    {
        internal OpenVirtual10(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 10);
    }

    private sealed class OpenVirtual11 : OpenVirtualBase
    {
        internal OpenVirtual11(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 11);
    }

    private sealed class OpenVirtual12 : OpenVirtualBase
    {
        internal OpenVirtual12(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 12);
    }

    private sealed class OpenVirtual13 : OpenVirtualBase
    {
        internal OpenVirtual13(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 13);
    }

    private sealed class OpenVirtual14 : OpenVirtualBase
    {
        internal OpenVirtual14(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 14);
    }

    private sealed class OpenVirtual15 : OpenVirtualBase
    {
        internal OpenVirtual15(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 15);
    }

    private sealed class OpenVirtual16 : OpenVirtualBase
    {
        internal OpenVirtual16(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 16);
    }

    private sealed class OpenVirtual17 : OpenVirtualBase
    {
        internal OpenVirtual17(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 17);
    }

    private sealed class OpenVirtual18 : OpenVirtualBase
    {
        internal OpenVirtual18(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 18);
    }

    private sealed class OpenVirtual19 : OpenVirtualBase
    {
        internal OpenVirtual19(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 19);
    }


    private sealed class SealedVirtual0 : SealedVirtualBase
    {
        internal SealedVirtual0(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 0);
    }

    private sealed class SealedVirtual1 : SealedVirtualBase
    {
        internal SealedVirtual1(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 1);
    }

    private sealed class SealedVirtual2 : SealedVirtualBase
    {
        internal SealedVirtual2(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 2);
    }

    private sealed class SealedVirtual3 : SealedVirtualBase
    {
        internal SealedVirtual3(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 3);
    }

    private sealed class SealedVirtual4 : SealedVirtualBase
    {
        internal SealedVirtual4(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 4);
    }

    private sealed class SealedVirtual5 : SealedVirtualBase
    {
        internal SealedVirtual5(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 5);
    }

    private sealed class SealedVirtual6 : SealedVirtualBase
    {
        internal SealedVirtual6(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 6);
    }

    private sealed class SealedVirtual7 : SealedVirtualBase
    {
        internal SealedVirtual7(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 7);
    }

    private sealed class SealedVirtual8 : SealedVirtualBase
    {
        internal SealedVirtual8(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 8);
    }

    private sealed class SealedVirtual9 : SealedVirtualBase
    {
        internal SealedVirtual9(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 9);
    }

    private sealed class SealedVirtual10 : SealedVirtualBase
    {
        internal SealedVirtual10(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 10);
    }

    private sealed class SealedVirtual11 : SealedVirtualBase
    {
        internal SealedVirtual11(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 11);
    }

    private sealed class SealedVirtual12 : SealedVirtualBase
    {
        internal SealedVirtual12(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 12);
    }

    private sealed class SealedVirtual13 : SealedVirtualBase
    {
        internal SealedVirtual13(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 13);
    }

    private sealed class SealedVirtual14 : SealedVirtualBase
    {
        internal SealedVirtual14(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 14);
    }

    private sealed class SealedVirtual15 : SealedVirtualBase
    {
        internal SealedVirtual15(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 15);
    }

    private sealed class SealedVirtual16 : SealedVirtualBase
    {
        internal SealedVirtual16(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 16);
    }

    private sealed class SealedVirtual17 : SealedVirtualBase
    {
        internal SealedVirtual17(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 17);
    }

    private sealed class SealedVirtual18 : SealedVirtualBase
    {
        internal SealedVirtual18(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 18);
    }

    private sealed class SealedVirtual19 : SealedVirtualBase
    {
        internal SealedVirtual19(int a, int b) : base(a, b) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 19);
    }


    private sealed class TypeIdVirtual0 : TypeIdVirtualBase
    {
        internal TypeIdVirtual0(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0;
        }
    }

    private sealed class TypeIdVirtual1 : TypeIdVirtualBase
    {
        internal TypeIdVirtual1(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 1;
        }
    }

    private sealed class TypeIdVirtual2 : TypeIdVirtualBase
    {
        internal TypeIdVirtual2(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 2;
        }
    }

    private sealed class TypeIdVirtual3 : TypeIdVirtualBase
    {
        internal TypeIdVirtual3(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 3;
        }
    }

    private sealed class TypeIdVirtual4 : TypeIdVirtualBase
    {
        internal TypeIdVirtual4(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 4;
        }
    }

    private sealed class TypeIdVirtual5 : TypeIdVirtualBase
    {
        internal TypeIdVirtual5(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 5;
        }
    }

    private sealed class TypeIdVirtual6 : TypeIdVirtualBase
    {
        internal TypeIdVirtual6(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 6;
        }
    }

    private sealed class TypeIdVirtual7 : TypeIdVirtualBase
    {
        internal TypeIdVirtual7(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 7;
        }
    }

    private sealed class TypeIdVirtual8 : TypeIdVirtualBase
    {
        internal TypeIdVirtual8(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 8;
        }
    }

    private sealed class TypeIdVirtual9 : TypeIdVirtualBase
    {
        internal TypeIdVirtual9(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 9;
        }
    }

    private sealed class TypeIdVirtual10 : TypeIdVirtualBase
    {
        internal TypeIdVirtual10(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 10;
        }
    }

    private sealed class TypeIdVirtual11 : TypeIdVirtualBase
    {
        internal TypeIdVirtual11(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 11;
        }
    }

    private sealed class TypeIdVirtual12 : TypeIdVirtualBase
    {
        internal TypeIdVirtual12(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 12;
        }
    }

    private sealed class TypeIdVirtual13 : TypeIdVirtualBase
    {
        internal TypeIdVirtual13(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 13;
        }
    }

    private sealed class TypeIdVirtual14 : TypeIdVirtualBase
    {
        internal TypeIdVirtual14(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 14;
        }
    }

    private sealed class TypeIdVirtual15 : TypeIdVirtualBase
    {
        internal TypeIdVirtual15(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 15;
        }
    }

    private sealed class TypeIdVirtual16 : TypeIdVirtualBase
    {
        internal TypeIdVirtual16(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 16;
        }
    }

    private sealed class TypeIdVirtual17 : TypeIdVirtualBase
    {
        internal TypeIdVirtual17(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 17;
        }
    }

    private sealed class TypeIdVirtual18 : TypeIdVirtualBase
    {
        internal TypeIdVirtual18(int a, int b) : base(a, b) { }

        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 18;
        }
    }

    private sealed class TypeIdVirtual19 : TypeIdVirtualBase
    {
        internal TypeIdVirtual19(int a, int b) : base(a, b) { }

        internal override byte TypeId
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
    private static void WriteAbstractTypeId0(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId1(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId2(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId3(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId4(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId5(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId6(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 6);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId7(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId8(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId9(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 9);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId10(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 10);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId11(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 11);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId12(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 12);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId13(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 13);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId14(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 14);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId15(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 15);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId16(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId17(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 17);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId18(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 18);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId19(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 19);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId0(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId1(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId2(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId3(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId4(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId5(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId6(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 6);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId7(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId8(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId9(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 9);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId10(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 10);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId11(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 11);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId12(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 12);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId13(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 13);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId14(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 14);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId15(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 15);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId16(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId17(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 17);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId18(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 18);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId19(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 19);

}