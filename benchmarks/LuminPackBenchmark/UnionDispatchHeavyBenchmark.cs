using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace LuminPackBenchmark;

/// <summary>Provides union dispatch heavy benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 6, iterationCount: 12)]
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

    /// <summary>Initializes a new instance of <c>UnionDispatchHeavyBenchmark</c>.</summary>
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

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _virtualItems = new OpenVirtualBase[ItemCount];
        _sealedItems = new SealedVirtualBase[ItemCount];
        _virtualTypeIdItems = new TypeIdVirtualBase[ItemCount];
        _fieldTypeIdItems = new TypeIdFieldBase[ItemCount];

        // Match the i % 20 distribution used by PolymorphismBenchmark.
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

    /// <summary>Measures virtual override for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Measures sealed override for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Measures abstract type id function pointer for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Measures type id field function pointer for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Measures type id field switch direct call for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Raises the exception for invalid type id.</summary>
    /// <param name="typeId">The type id used by this operation.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidTypeId(byte typeId)
        => throw new ArgumentOutOfRangeException(nameof(typeId), typeId, null);

    /// <summary>Creates virtual from the supplied input.</summary>
    /// <param name="typeId">The type id used by this operation.</param>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Creates sealed from the supplied input.</summary>
    /// <param name="typeId">The type id used by this operation.</param>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Creates abstract type id from the supplied input.</summary>
    /// <param name="typeId">The type id used by this operation.</param>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Creates field type id from the supplied input.</summary>
    /// <param name="typeId">The type id used by this operation.</param>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Provides dispatch state functionality.</summary>
    private struct DispatchState
    {
        internal int Value;

        /// <summary>Initializes a new instance of <c>DispatchState</c>.</summary>
        /// <param name="seed">The seed used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DispatchState(int seed) => Value = seed;

        /// <summary>Writes  to the current output.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <param name="typeId">The type id used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Write(int a, int b, int typeId)
        {
            int value = Value;
            value = unchecked((value * 16_777_619) ^ a);
            value = unchecked((value * 16_777_619) ^ b);
            Value = value ^ typeId;
        }
    }

    /// <summary>Provides open virtual base functionality.</summary>
    private abstract class OpenVirtualBase
    {
        internal readonly int A;
        internal readonly int B;

        /// <summary>Initializes a new instance of <c>OpenVirtualBase</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        protected OpenVirtualBase(int a, int b)
        {
            A = a;
            B = b;
        }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        internal abstract void Serialize(ref DispatchState state);
    }

    /// <summary>Provides sealed virtual base functionality.</summary>
    private abstract class SealedVirtualBase
    {
        internal readonly int A;
        internal readonly int B;

        /// <summary>Initializes a new instance of <c>SealedVirtualBase</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        protected SealedVirtualBase(int a, int b)
        {
            A = a;
            B = b;
        }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        internal abstract void Serialize(ref DispatchState state);
    }

    /// <summary>Provides type id virtual base functionality.</summary>
    private abstract class TypeIdVirtualBase
    {
        internal readonly int A;
        internal readonly int B;

        /// <summary>Initializes a new instance of <c>TypeIdVirtualBase</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        protected TypeIdVirtualBase(int a, int b)
        {
            A = a;
            B = b;
        }

        /// <summary>Gets the type id.</summary>
        internal abstract byte TypeId { get; }
    }

    /// <summary>Provides type id field base functionality.</summary>
    private abstract class TypeIdFieldBase
    {
        internal readonly byte TypeId;
        internal readonly int A;
        internal readonly int B;

        /// <summary>Initializes a new instance of <c>TypeIdFieldBase</c>.</summary>
        /// <param name="typeId">The type id used by this operation.</param>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        protected TypeIdFieldBase(byte typeId, int a, int b)
        {
            TypeId = typeId;
            A = a;
            B = b;
        }
    }


    /// <summary>Provides open virtual0 functionality.</summary>
    private sealed class OpenVirtual0 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual0</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual0(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 0);
    }

    /// <summary>Provides open virtual1 functionality.</summary>
    private sealed class OpenVirtual1 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual1</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual1(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 1);
    }

    /// <summary>Provides open virtual2 functionality.</summary>
    private sealed class OpenVirtual2 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual2</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual2(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 2);
    }

    /// <summary>Provides open virtual3 functionality.</summary>
    private sealed class OpenVirtual3 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual3</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual3(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 3);
    }

    /// <summary>Provides open virtual4 functionality.</summary>
    private sealed class OpenVirtual4 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual4</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual4(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 4);
    }

    /// <summary>Provides open virtual5 functionality.</summary>
    private sealed class OpenVirtual5 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual5</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual5(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 5);
    }

    /// <summary>Provides open virtual6 functionality.</summary>
    private sealed class OpenVirtual6 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual6</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual6(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 6);
    }

    /// <summary>Provides open virtual7 functionality.</summary>
    private sealed class OpenVirtual7 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual7</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual7(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 7);
    }

    /// <summary>Provides open virtual8 functionality.</summary>
    private sealed class OpenVirtual8 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual8</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual8(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 8);
    }

    /// <summary>Provides open virtual9 functionality.</summary>
    private sealed class OpenVirtual9 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual9</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual9(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 9);
    }

    /// <summary>Provides open virtual10 functionality.</summary>
    private sealed class OpenVirtual10 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual10</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual10(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 10);
    }

    /// <summary>Provides open virtual11 functionality.</summary>
    private sealed class OpenVirtual11 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual11</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual11(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 11);
    }

    /// <summary>Provides open virtual12 functionality.</summary>
    private sealed class OpenVirtual12 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual12</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual12(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 12);
    }

    /// <summary>Provides open virtual13 functionality.</summary>
    private sealed class OpenVirtual13 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual13</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual13(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 13);
    }

    /// <summary>Provides open virtual14 functionality.</summary>
    private sealed class OpenVirtual14 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual14</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual14(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 14);
    }

    /// <summary>Provides open virtual15 functionality.</summary>
    private sealed class OpenVirtual15 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual15</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual15(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 15);
    }

    /// <summary>Provides open virtual16 functionality.</summary>
    private sealed class OpenVirtual16 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual16</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual16(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 16);
    }

    /// <summary>Provides open virtual17 functionality.</summary>
    private sealed class OpenVirtual17 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual17</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual17(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 17);
    }

    /// <summary>Provides open virtual18 functionality.</summary>
    private sealed class OpenVirtual18 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual18</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual18(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 18);
    }

    /// <summary>Provides open virtual19 functionality.</summary>
    private sealed class OpenVirtual19 : OpenVirtualBase
    {
        /// <summary>Initializes a new instance of <c>OpenVirtual19</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal OpenVirtual19(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Serialize(ref DispatchState state)
            => state.Write(A, B, 19);
    }


    /// <summary>Provides sealed virtual0 functionality.</summary>
    private sealed class SealedVirtual0 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual0</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual0(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 0);
    }

    /// <summary>Provides sealed virtual1 functionality.</summary>
    private sealed class SealedVirtual1 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual1</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual1(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 1);
    }

    /// <summary>Provides sealed virtual2 functionality.</summary>
    private sealed class SealedVirtual2 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual2</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual2(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 2);
    }

    /// <summary>Provides sealed virtual3 functionality.</summary>
    private sealed class SealedVirtual3 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual3</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual3(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 3);
    }

    /// <summary>Provides sealed virtual4 functionality.</summary>
    private sealed class SealedVirtual4 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual4</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual4(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 4);
    }

    /// <summary>Provides sealed virtual5 functionality.</summary>
    private sealed class SealedVirtual5 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual5</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual5(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 5);
    }

    /// <summary>Provides sealed virtual6 functionality.</summary>
    private sealed class SealedVirtual6 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual6</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual6(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 6);
    }

    /// <summary>Provides sealed virtual7 functionality.</summary>
    private sealed class SealedVirtual7 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual7</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual7(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 7);
    }

    /// <summary>Provides sealed virtual8 functionality.</summary>
    private sealed class SealedVirtual8 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual8</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual8(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 8);
    }

    /// <summary>Provides sealed virtual9 functionality.</summary>
    private sealed class SealedVirtual9 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual9</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual9(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 9);
    }

    /// <summary>Provides sealed virtual10 functionality.</summary>
    private sealed class SealedVirtual10 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual10</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual10(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 10);
    }

    /// <summary>Provides sealed virtual11 functionality.</summary>
    private sealed class SealedVirtual11 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual11</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual11(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 11);
    }

    /// <summary>Provides sealed virtual12 functionality.</summary>
    private sealed class SealedVirtual12 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual12</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual12(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 12);
    }

    /// <summary>Provides sealed virtual13 functionality.</summary>
    private sealed class SealedVirtual13 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual13</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual13(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 13);
    }

    /// <summary>Provides sealed virtual14 functionality.</summary>
    private sealed class SealedVirtual14 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual14</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual14(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 14);
    }

    /// <summary>Provides sealed virtual15 functionality.</summary>
    private sealed class SealedVirtual15 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual15</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual15(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 15);
    }

    /// <summary>Provides sealed virtual16 functionality.</summary>
    private sealed class SealedVirtual16 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual16</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual16(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 16);
    }

    /// <summary>Provides sealed virtual17 functionality.</summary>
    private sealed class SealedVirtual17 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual17</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual17(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 17);
    }

    /// <summary>Provides sealed virtual18 functionality.</summary>
    private sealed class SealedVirtual18 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual18</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual18(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 18);
    }

    /// <summary>Provides sealed virtual19 functionality.</summary>
    private sealed class SealedVirtual19 : SealedVirtualBase
    {
        /// <summary>Initializes a new instance of <c>SealedVirtual19</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal SealedVirtual19(int a, int b) : base(a, b) { }

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal sealed override void Serialize(ref DispatchState state)
            => state.Write(A, B, 19);
    }


    /// <summary>Provides type id virtual0 functionality.</summary>
    private sealed class TypeIdVirtual0 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual0</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual0(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0;
        }
    }

    /// <summary>Provides type id virtual1 functionality.</summary>
    private sealed class TypeIdVirtual1 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual1</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual1(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 1;
        }
    }

    /// <summary>Provides type id virtual2 functionality.</summary>
    private sealed class TypeIdVirtual2 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual2</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual2(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 2;
        }
    }

    /// <summary>Provides type id virtual3 functionality.</summary>
    private sealed class TypeIdVirtual3 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual3</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual3(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 3;
        }
    }

    /// <summary>Provides type id virtual4 functionality.</summary>
    private sealed class TypeIdVirtual4 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual4</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual4(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 4;
        }
    }

    /// <summary>Provides type id virtual5 functionality.</summary>
    private sealed class TypeIdVirtual5 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual5</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual5(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 5;
        }
    }

    /// <summary>Provides type id virtual6 functionality.</summary>
    private sealed class TypeIdVirtual6 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual6</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual6(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 6;
        }
    }

    /// <summary>Provides type id virtual7 functionality.</summary>
    private sealed class TypeIdVirtual7 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual7</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual7(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 7;
        }
    }

    /// <summary>Provides type id virtual8 functionality.</summary>
    private sealed class TypeIdVirtual8 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual8</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual8(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 8;
        }
    }

    /// <summary>Provides type id virtual9 functionality.</summary>
    private sealed class TypeIdVirtual9 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual9</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual9(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 9;
        }
    }

    /// <summary>Provides type id virtual10 functionality.</summary>
    private sealed class TypeIdVirtual10 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual10</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual10(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 10;
        }
    }

    /// <summary>Provides type id virtual11 functionality.</summary>
    private sealed class TypeIdVirtual11 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual11</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual11(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 11;
        }
    }

    /// <summary>Provides type id virtual12 functionality.</summary>
    private sealed class TypeIdVirtual12 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual12</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual12(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 12;
        }
    }

    /// <summary>Provides type id virtual13 functionality.</summary>
    private sealed class TypeIdVirtual13 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual13</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual13(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 13;
        }
    }

    /// <summary>Provides type id virtual14 functionality.</summary>
    private sealed class TypeIdVirtual14 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual14</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual14(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 14;
        }
    }

    /// <summary>Provides type id virtual15 functionality.</summary>
    private sealed class TypeIdVirtual15 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual15</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual15(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 15;
        }
    }

    /// <summary>Provides type id virtual16 functionality.</summary>
    private sealed class TypeIdVirtual16 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual16</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual16(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 16;
        }
    }

    /// <summary>Provides type id virtual17 functionality.</summary>
    private sealed class TypeIdVirtual17 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual17</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual17(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 17;
        }
    }

    /// <summary>Provides type id virtual18 functionality.</summary>
    private sealed class TypeIdVirtual18 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual18</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual18(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 18;
        }
    }

    /// <summary>Provides type id virtual19 functionality.</summary>
    private sealed class TypeIdVirtual19 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual19</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual19(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 19;
        }
    }


    /// <summary>Provides type id field0 functionality.</summary>
    private sealed class TypeIdField0 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField0</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField0(int a, int b) : base(0, a, b) { }
    }

    /// <summary>Provides type id field1 functionality.</summary>
    private sealed class TypeIdField1 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField1</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField1(int a, int b) : base(1, a, b) { }
    }

    /// <summary>Provides type id field2 functionality.</summary>
    private sealed class TypeIdField2 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField2</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField2(int a, int b) : base(2, a, b) { }
    }

    /// <summary>Provides type id field3 functionality.</summary>
    private sealed class TypeIdField3 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField3</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField3(int a, int b) : base(3, a, b) { }
    }

    /// <summary>Provides type id field4 functionality.</summary>
    private sealed class TypeIdField4 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField4</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField4(int a, int b) : base(4, a, b) { }
    }

    /// <summary>Provides type id field5 functionality.</summary>
    private sealed class TypeIdField5 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField5</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField5(int a, int b) : base(5, a, b) { }
    }

    /// <summary>Provides type id field6 functionality.</summary>
    private sealed class TypeIdField6 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField6</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField6(int a, int b) : base(6, a, b) { }
    }

    /// <summary>Provides type id field7 functionality.</summary>
    private sealed class TypeIdField7 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField7</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField7(int a, int b) : base(7, a, b) { }
    }

    /// <summary>Provides type id field8 functionality.</summary>
    private sealed class TypeIdField8 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField8</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField8(int a, int b) : base(8, a, b) { }
    }

    /// <summary>Provides type id field9 functionality.</summary>
    private sealed class TypeIdField9 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField9</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField9(int a, int b) : base(9, a, b) { }
    }

    /// <summary>Provides type id field10 functionality.</summary>
    private sealed class TypeIdField10 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField10</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField10(int a, int b) : base(10, a, b) { }
    }

    /// <summary>Provides type id field11 functionality.</summary>
    private sealed class TypeIdField11 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField11</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField11(int a, int b) : base(11, a, b) { }
    }

    /// <summary>Provides type id field12 functionality.</summary>
    private sealed class TypeIdField12 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField12</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField12(int a, int b) : base(12, a, b) { }
    }

    /// <summary>Provides type id field13 functionality.</summary>
    private sealed class TypeIdField13 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField13</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField13(int a, int b) : base(13, a, b) { }
    }

    /// <summary>Provides type id field14 functionality.</summary>
    private sealed class TypeIdField14 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField14</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField14(int a, int b) : base(14, a, b) { }
    }

    /// <summary>Provides type id field15 functionality.</summary>
    private sealed class TypeIdField15 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField15</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField15(int a, int b) : base(15, a, b) { }
    }

    /// <summary>Provides type id field16 functionality.</summary>
    private sealed class TypeIdField16 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField16</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField16(int a, int b) : base(16, a, b) { }
    }

    /// <summary>Provides type id field17 functionality.</summary>
    private sealed class TypeIdField17 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField17</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField17(int a, int b) : base(17, a, b) { }
    }

    /// <summary>Provides type id field18 functionality.</summary>
    private sealed class TypeIdField18 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField18</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField18(int a, int b) : base(18, a, b) { }
    }

    /// <summary>Provides type id field19 functionality.</summary>
    private sealed class TypeIdField19 : TypeIdFieldBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdField19</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdField19(int a, int b) : base(19, a, b) { }
    }



    /// <summary>Writes abstract type id0 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId0(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 0);

    /// <summary>Writes abstract type id1 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId1(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 1);

    /// <summary>Writes abstract type id2 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId2(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 2);

    /// <summary>Writes abstract type id3 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId3(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 3);

    /// <summary>Writes abstract type id4 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId4(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 4);

    /// <summary>Writes abstract type id5 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId5(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 5);

    /// <summary>Writes abstract type id6 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId6(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 6);

    /// <summary>Writes abstract type id7 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId7(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 7);

    /// <summary>Writes abstract type id8 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId8(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 8);

    /// <summary>Writes abstract type id9 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId9(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 9);

    /// <summary>Writes abstract type id10 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId10(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 10);

    /// <summary>Writes abstract type id11 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId11(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 11);

    /// <summary>Writes abstract type id12 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId12(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 12);

    /// <summary>Writes abstract type id13 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId13(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 13);

    /// <summary>Writes abstract type id14 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId14(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 14);

    /// <summary>Writes abstract type id15 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId15(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 15);

    /// <summary>Writes abstract type id16 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId16(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 16);

    /// <summary>Writes abstract type id17 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId17(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 17);

    /// <summary>Writes abstract type id18 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId18(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 18);

    /// <summary>Writes abstract type id19 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteAbstractTypeId19(ref DispatchState state, TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 19);


    /// <summary>Writes field type id0 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId0(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 0);

    /// <summary>Writes field type id1 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId1(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 1);

    /// <summary>Writes field type id2 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId2(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 2);

    /// <summary>Writes field type id3 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId3(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 3);

    /// <summary>Writes field type id4 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId4(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 4);

    /// <summary>Writes field type id5 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId5(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 5);

    /// <summary>Writes field type id6 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId6(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 6);

    /// <summary>Writes field type id7 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId7(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 7);

    /// <summary>Writes field type id8 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId8(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 8);

    /// <summary>Writes field type id9 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId9(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 9);

    /// <summary>Writes field type id10 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId10(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 10);

    /// <summary>Writes field type id11 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId11(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 11);

    /// <summary>Writes field type id12 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId12(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 12);

    /// <summary>Writes field type id13 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId13(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 13);

    /// <summary>Writes field type id14 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId14(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 14);

    /// <summary>Writes field type id15 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId15(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 15);

    /// <summary>Writes field type id16 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId16(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 16);

    /// <summary>Writes field type id17 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId17(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 17);

    /// <summary>Writes field type id18 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId18(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 18);

    /// <summary>Writes field type id19 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFieldTypeId19(ref DispatchState state, TypeIdFieldBase value)
        => state.Write(value.A, value.B, 19);

}
