using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPack.Tests.InputLimits;

/// <summary>Exercises the binary grouping format of the opt-in assembly.</summary>
public static class FramedGroupingRegressionFixture
{
    /// <summary>Writes a root grouping with the assembly's default string profile.</summary>
    /// <param name="value">The grouping or null.</param>
    /// <returns>The framed binary record.</returns>
    public static byte[] WriteGrouping(IGrouping<string?, int>? value) => LuminPackSerializer.Serialize(value);

    /// <summary>Reads a root grouping with the assembly's default string profile.</summary>
    /// <param name="bytes">The framed binary record.</param>
    /// <returns>The decoded grouping or null.</returns>
    public static IGrouping<string?, int>? ReadGrouping(byte[] bytes) =>
        LuminPackSerializer.Deserialize<IGrouping<string?, int>>(bytes);

    /// <summary>Writes the groups in a lookup in the original enumeration order.</summary>
    /// <param name="value">The input lookup.</param>
    /// <returns>The framed binary record.</returns>
    public static byte[] WriteLookup(ILookup<string?, int> value) => LuminPackSerializer.Serialize(value);

    /// <summary>Reads the groups of a lookup in their wire order.</summary>
    /// <param name="bytes">The framed binary record.</param>
    /// <returns>The decoded lookup.</returns>
    public static ILookup<string?, int> ReadLookup(byte[] bytes) => LuminPackSerializer.Deserialize<ILookup<string?, int>>(bytes);

    /// <summary>Reads a nested group with its member-owned string profile and a following record field.</summary>
    /// <param name="group">The group assigned to the nested member.</param>
    /// <returns>The decoded group and following field.</returns>
    public static (IGrouping<string?, int>? Group, int Tail) RoundTripMember(IGrouping<string?, int>? group)
    {
        var value = new FramedGroupingEnvelope { Group = group, Tail = 71 };
        byte[] bytes = LuminPackSerializer.Serialize<FramedGroupingEnvelope, LuminPackUtf16VarIntOptions>(value);
        FramedGroupingEnvelope result = null!;
        LuminPackSerializer.Deserialize<FramedGroupingEnvelope, LuminPackUtf16VarIntOptions>(bytes, ref result);
        return (result.Group, result.Tail);
    }

    /// <summary>Round-trips a group whose raw key starts with the null marker byte.</summary>
    /// <param name="group">The group with the raw key.</param>
    /// <returns>The decoded grouping.</returns>
    public static IGrouping<byte, int> RoundTripRawKey(IGrouping<byte, int> group) =>
        LuminPackSerializer.Deserialize<IGrouping<byte, int>>(LuminPackSerializer.Serialize(group));
}

/// <summary>Checks grouping framing through generated member dispatch.</summary>
[LuminPackable]
public sealed partial class FramedGroupingEnvelope
{
    [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]
    public IGrouping<string?, int>? Group;

    public int Tail;
}
