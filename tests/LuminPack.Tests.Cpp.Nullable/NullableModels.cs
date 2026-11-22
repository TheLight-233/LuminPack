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
    public int? Value { get; set; }
    public long? Absent { get; set; }
    public CppMode? Mode { get; set; }
    public bool?[] Flags { get; set; } = [];
    public List<long?> Longs { get; set; } = [];
    public List<int?> Empty { get; set; } = [];
    public int?[]? NullArray { get; set; }
    public Dictionary<int, int?> ValuesById { get; set; } = new();
    public Dictionary<int, long?> WideById { get; set; } = new();

    /// <summary>Builds a fixed fixture containing nulls, boundary values and gaps between populated elements.</summary>
    /// <returns>The managed fixture.</returns>
    internal static CppNullablePacket Create() => new()
    {
        Value = int.MinValue,
        Mode = CppMode.High,
        Flags = [null, false, true, null],
        Longs = [null, long.MaxValue, long.MinValue, null],
        ValuesById = { [7] = null, [19] = int.MaxValue },
        WideById = { [13] = long.MinValue }
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
