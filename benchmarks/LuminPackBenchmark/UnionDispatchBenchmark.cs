using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace LuminPackBenchmark;

/// <summary>
/// Compares runtime dispatch strategies for polymorphic LuminPack serialization:
/// 1. Ordinary virtual override dispatch.
/// 2. Sealed virtual override dispatch.
/// 3. An abstract TypeId property with a native function pointer table.
/// 4. A non-virtual TypeId field with a native function pointer table.
///
/// OperationsPerInvoke normalizes each batch to the cost of one polymorphic dispatch.
/// </summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 8, iterationCount: 15)]
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

    /// <summary>Gets or sets the pattern.</summary>
    [Params(
        DispatchPattern.Monomorphic,
        DispatchPattern.Bimorphic,
        DispatchPattern.RoundRobin20,
        DispatchPattern.Random20)]
    public DispatchPattern Pattern { get; set; }

    /// <summary>Initializes a new instance of <c>UnionDispatchBenchmark</c>.</summary>
    static UnionDispatchBenchmark()
    {
        // Allocate a 256-slot native function pointer table with TypeAssociatedMemory.
        // A byte TypeId avoids additional array range checks.
        // Its lifetime matches the owning type.
        // Avoid delegate allocations and delegate.Invoke.
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

    /// <summary>Updates the up used by the current operation.</summary>
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

            // Use different data per object so the JIT cannot reduce the loop to a constant.
            int a = unchecked(i * 31 + 17);
            int b = unchecked(i * 131 + typeId);

            _openVirtualItems[i] = CreateOpenVirtual(typeId, a, b);
            _sealedVirtualItems[i] = CreateSealedVirtual(typeId, a, b);
            _typeIdVirtualItems[i] = CreateTypeIdVirtual(typeId, a, b);
            _typeIdFieldItems[i] = CreateTypeIdField(typeId, a, b);
        }
    }

    /// <summary>Measures virtual override for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true, OperationsPerInvoke = ItemCount)]
    public int VirtualOverride()
    {
        var state = new DispatchState(17);
        var items = _openVirtualItems;

        for (int i = 0; i < items.Length; i++)
            items[i].Serialize(ref state);

        return state.Value;
    }

    /// <summary>Measures sealed override for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(OperationsPerInvoke = ItemCount)]
    public int SealedOverride()
    {
        var state = new DispatchState(17);
        var items = _sealedVirtualItems;

        for (int i = 0; i < items.Length; i++)
            items[i].Serialize(ref state);

        return state.Value;
    }

    /// <summary>Measures abstract type id function pointer for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(OperationsPerInvoke = ItemCount)]
    public int AbstractTypeId_FunctionPointer()
    {
        var state = new DispatchState(17);
        var items = _typeIdVirtualItems;

        for (int i = 0; i < items.Length; i++)
        {
            TypeIdVirtualBase value = items[i];

            // Model an abstract byte TypeId on the base type with a function pointer table.
            // Read TypeId through the virtual property, then load the function address and invoke calli.
            byte typeId = value.TypeId;
            var write = (delegate* managed<ref DispatchState, TypeIdVirtualBase, void>)
                s_typeIdVirtualTable[typeId];

            write(ref state, value);
        }

        return state.Value;
    }

    /// <summary>Measures type id field function pointer for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(OperationsPerInvoke = ItemCount)]
    public int TypeIdField_FunctionPointer()
    {
        var state = new DispatchState(17);
        var items = _typeIdFieldItems;

        for (int i = 0; i < items.Length; i++)
        {
            TypeIdFieldBase value = items[i];

            // This is the minimal-overhead function pointer table variant:
            // TypeId is a readonly field with no virtual getter overhead.
            byte typeId = value.TypeId;
            var write = (delegate* managed<ref DispatchState, TypeIdFieldBase, void>)
                s_typeIdFieldTable[typeId];

            write(ref state, value);
        }

        return state.Value;
    }

    /// <summary>Creates open virtual from the supplied input.</summary>
    /// <param name="typeId">The type id used by this operation.</param>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Creates sealed virtual from the supplied input.</summary>
    /// <param name="typeId">The type id used by this operation.</param>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Creates type id virtual from the supplied input.</summary>
    /// <param name="typeId">The type id used by this operation.</param>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Creates type id field from the supplied input.</summary>
    /// <param name="typeId">The type id used by this operation.</param>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Provides dispatch pattern functionality.</summary>
    public enum DispatchPattern
    {
        /// <summary>Uses one runtime type to give PGO the best opportunity to devirtualize.</summary>
        Monomorphic,

        /// <summary>Alternates between two runtime types.</summary>
        Bimorphic,

        /// <summary>Cycles through 20 types in a stable order to measure a highly polymorphic call site.</summary>
        RoundRobin20,

        /// <summary>Randomizes 20 runtime types to stress indirect branch prediction.</summary>
        Random20
    }

    /// <summary>Provides dispatch state functionality.</summary>
    private struct DispatchState
    {
        internal int Value;

        /// <summary>Initializes a new instance of <c>DispatchState</c>.</summary>
        /// <param name="seed">The seed used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DispatchState(int seed)
        {
            Value = seed;
        }

        /// <summary>Writes  to the current output.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <param name="typeId">The type id used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Write(int a, int b, int typeId)
        {
            // Keep the workload small enough to expose dispatch overhead while reading fields and updating state.
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    private class OpenVirtual0 : OpenVirtualBase
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
    private class OpenVirtual1 : OpenVirtualBase
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
    private class OpenVirtual2 : OpenVirtualBase
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
    private class OpenVirtual3 : OpenVirtualBase
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
    private class OpenVirtual4 : OpenVirtualBase
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
    private class OpenVirtual5 : OpenVirtualBase
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
    private class OpenVirtual6 : OpenVirtualBase
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
    private class OpenVirtual7 : OpenVirtualBase
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
    private class OpenVirtual8 : OpenVirtualBase
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
    private class OpenVirtual9 : OpenVirtualBase
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
    private class OpenVirtual10 : OpenVirtualBase
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
    private class OpenVirtual11 : OpenVirtualBase
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
    private class OpenVirtual12 : OpenVirtualBase
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
    private class OpenVirtual13 : OpenVirtualBase
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
    private class OpenVirtual14 : OpenVirtualBase
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
    private class OpenVirtual15 : OpenVirtualBase
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
    private class OpenVirtual16 : OpenVirtualBase
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
    private class OpenVirtual17 : OpenVirtualBase
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
    private class OpenVirtual18 : OpenVirtualBase
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
    private class OpenVirtual19 : OpenVirtualBase
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
    private class SealedVirtual0 : SealedVirtualBase
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
    private class SealedVirtual1 : SealedVirtualBase
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
    private class SealedVirtual2 : SealedVirtualBase
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
    private class SealedVirtual3 : SealedVirtualBase
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
    private class SealedVirtual4 : SealedVirtualBase
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
    private class SealedVirtual5 : SealedVirtualBase
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
    private class SealedVirtual6 : SealedVirtualBase
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
    private class SealedVirtual7 : SealedVirtualBase
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
    private class SealedVirtual8 : SealedVirtualBase
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
    private class SealedVirtual9 : SealedVirtualBase
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
    private class SealedVirtual10 : SealedVirtualBase
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
    private class SealedVirtual11 : SealedVirtualBase
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
    private class SealedVirtual12 : SealedVirtualBase
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
    private class SealedVirtual13 : SealedVirtualBase
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
    private class SealedVirtual14 : SealedVirtualBase
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
    private class SealedVirtual15 : SealedVirtualBase
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
    private class SealedVirtual16 : SealedVirtualBase
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
    private class SealedVirtual17 : SealedVirtualBase
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
    private class SealedVirtual18 : SealedVirtualBase
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
    private class SealedVirtual19 : SealedVirtualBase
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
    private class TypeIdVirtual0 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual0</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual0(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0;
        }
    }

    /// <summary>Provides type id virtual1 functionality.</summary>
    private class TypeIdVirtual1 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual1</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual1(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 1;
        }
    }

    /// <summary>Provides type id virtual2 functionality.</summary>
    private class TypeIdVirtual2 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual2</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual2(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 2;
        }
    }

    /// <summary>Provides type id virtual3 functionality.</summary>
    private class TypeIdVirtual3 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual3</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual3(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 3;
        }
    }

    /// <summary>Provides type id virtual4 functionality.</summary>
    private class TypeIdVirtual4 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual4</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual4(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 4;
        }
    }

    /// <summary>Provides type id virtual5 functionality.</summary>
    private class TypeIdVirtual5 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual5</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual5(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 5;
        }
    }

    /// <summary>Provides type id virtual6 functionality.</summary>
    private class TypeIdVirtual6 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual6</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual6(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 6;
        }
    }

    /// <summary>Provides type id virtual7 functionality.</summary>
    private class TypeIdVirtual7 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual7</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual7(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 7;
        }
    }

    /// <summary>Provides type id virtual8 functionality.</summary>
    private class TypeIdVirtual8 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual8</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual8(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 8;
        }
    }

    /// <summary>Provides type id virtual9 functionality.</summary>
    private class TypeIdVirtual9 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual9</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual9(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 9;
        }
    }

    /// <summary>Provides type id virtual10 functionality.</summary>
    private class TypeIdVirtual10 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual10</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual10(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 10;
        }
    }

    /// <summary>Provides type id virtual11 functionality.</summary>
    private class TypeIdVirtual11 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual11</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual11(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 11;
        }
    }

    /// <summary>Provides type id virtual12 functionality.</summary>
    private class TypeIdVirtual12 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual12</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual12(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 12;
        }
    }

    /// <summary>Provides type id virtual13 functionality.</summary>
    private class TypeIdVirtual13 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual13</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual13(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 13;
        }
    }

    /// <summary>Provides type id virtual14 functionality.</summary>
    private class TypeIdVirtual14 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual14</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual14(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 14;
        }
    }

    /// <summary>Provides type id virtual15 functionality.</summary>
    private class TypeIdVirtual15 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual15</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual15(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 15;
        }
    }

    /// <summary>Provides type id virtual16 functionality.</summary>
    private class TypeIdVirtual16 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual16</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual16(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 16;
        }
    }

    /// <summary>Provides type id virtual17 functionality.</summary>
    private class TypeIdVirtual17 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual17</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual17(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 17;
        }
    }

    /// <summary>Provides type id virtual18 functionality.</summary>
    private class TypeIdVirtual18 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual18</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual18(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 18;
        }
    }

    /// <summary>Provides type id virtual19 functionality.</summary>
    private class TypeIdVirtual19 : TypeIdVirtualBase
    {
        /// <summary>Initializes a new instance of <c>TypeIdVirtual19</c>.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        internal TypeIdVirtual19(int a, int b) : base(a, b) { }

        /// <summary>Gets the type id.</summary>
        internal sealed override byte TypeId
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



    /// <summary>Writes type id virtual0 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual0(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 0);

    /// <summary>Writes type id virtual1 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual1(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 1);

    /// <summary>Writes type id virtual2 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual2(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 2);

    /// <summary>Writes type id virtual3 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual3(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 3);

    /// <summary>Writes type id virtual4 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual4(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 4);

    /// <summary>Writes type id virtual5 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual5(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 5);

    /// <summary>Writes type id virtual6 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual6(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 6);

    /// <summary>Writes type id virtual7 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual7(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 7);

    /// <summary>Writes type id virtual8 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual8(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 8);

    /// <summary>Writes type id virtual9 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual9(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 9);

    /// <summary>Writes type id virtual10 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual10(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 10);

    /// <summary>Writes type id virtual11 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual11(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 11);

    /// <summary>Writes type id virtual12 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual12(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 12);

    /// <summary>Writes type id virtual13 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual13(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 13);

    /// <summary>Writes type id virtual14 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual14(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 14);

    /// <summary>Writes type id virtual15 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual15(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 15);

    /// <summary>Writes type id virtual16 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual16(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 16);

    /// <summary>Writes type id virtual17 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual17(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 17);

    /// <summary>Writes type id virtual18 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual18(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 18);

    /// <summary>Writes type id virtual19 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdVirtual19(
        ref DispatchState state,
        TypeIdVirtualBase value)
        => state.Write(value.A, value.B, 19);


    /// <summary>Writes type id field0 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField0(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 0);

    /// <summary>Writes type id field1 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField1(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 1);

    /// <summary>Writes type id field2 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField2(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 2);

    /// <summary>Writes type id field3 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField3(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 3);

    /// <summary>Writes type id field4 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField4(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 4);

    /// <summary>Writes type id field5 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField5(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 5);

    /// <summary>Writes type id field6 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField6(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 6);

    /// <summary>Writes type id field7 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField7(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 7);

    /// <summary>Writes type id field8 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField8(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 8);

    /// <summary>Writes type id field9 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField9(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 9);

    /// <summary>Writes type id field10 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField10(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 10);

    /// <summary>Writes type id field11 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField11(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 11);

    /// <summary>Writes type id field12 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField12(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 12);

    /// <summary>Writes type id field13 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField13(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 13);

    /// <summary>Writes type id field14 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField14(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 14);

    /// <summary>Writes type id field15 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField15(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 15);

    /// <summary>Writes type id field16 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField16(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 16);

    /// <summary>Writes type id field17 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField17(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 17);

    /// <summary>Writes type id field18 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField18(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 18);

    /// <summary>Writes type id field19 to the current output.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTypeIdField19(
        ref DispatchState state,
        TypeIdFieldBase value)
        => state.Write(value.A, value.B, 19);

}
