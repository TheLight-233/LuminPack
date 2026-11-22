using System.Buffers;
using System.Collections.Immutable;
using LuminPack.Attribute;

#if LUMINPACK_CPP_TEST_PACKED
[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
#elif LUMINPACK_CPP_TEST_NATIVE
[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
#endif

namespace LuminPackCppFixtures;

/// <summary>Carries primitive, Boolean and enum nullable representations through typical collections.</summary>
[LuminPackable]
public partial class CppNullablePacket
{
    public long?[] WideArray { get; set; } = [];
    public int? Value { get; set; }
    public long? Absent { get; set; }
    public CppMode? Mode { get; set; }
    public bool?[] Flags { get; set; } = [];
    public List<long?> Longs { get; set; } = [];
    public List<int?> Empty { get; set; } = [];
    public int?[]? NullArray { get; set; }
    public Dictionary<int, int?> ValuesById { get; set; } = new();
    public Dictionary<int, long?> WideById { get; set; } = new();
    public Memory<long?> WideMemory { get; set; }
    public ReadOnlyMemory<long?> WideReadOnlyMemory { get; set; }
    public ArraySegment<long?> WideSegment { get; set; }
    public ImmutableArray<long?> WideImmutable { get; set; }
    public ReadOnlySequence<long?> WideSequence { get; set; }

    /// <summary>Builds a fixed fixture containing nulls, boundary values and gaps between populated elements.</summary>
    /// <returns>The managed fixture.</returns>
    internal static CppNullablePacket Create() => new()
    {
        WideArray = [0x1122334455667788, null],
        Value = int.MinValue,
        Mode = CppMode.High,
        Flags = [null, false, true, null],
        Longs = [null, long.MaxValue, long.MinValue, null],
        ValuesById = { [7] = null, [19] = int.MaxValue },
        WideById = { [13] = long.MinValue },
        WideMemory = new long?[] { null, long.MaxValue },
        WideReadOnlyMemory = new long?[] { long.MinValue, null },
        WideSegment = new ArraySegment<long?>([0, null, 17, 0], 1, 2),
        WideImmutable = [null, -17],
        WideSequence = new ReadOnlySequence<long?>(new long?[] { 0x1122334455667788, null })
    };
}

/// <summary>Exercises nullable padding within an actual Version descriptor table.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public partial class CppNullableVersion
{
    public int? Present { get; set; }
    public short? Absent { get; set; }
    public CppMode? Mode { get; set; }

    /// <summary>Builds the current Version record containing both present and absent fields.</summary>
    /// <returns>The managed fixture.</returns>
    internal static CppNullableVersion Create() => new() { Present = -1234567, Mode = CppMode.High };
}

/// <summary>Verifies that a nullable enum retains the width of its unsigned underlying type.</summary>
public enum CppMode : ushort
{
    High = 0xabcd
}
